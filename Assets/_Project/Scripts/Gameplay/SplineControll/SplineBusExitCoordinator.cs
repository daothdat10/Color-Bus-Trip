using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Runtime state while a completed bus transfers to and travels on the exit path.</summary>
internal sealed class ExitBusRunTime
{
    public BusController bus;
    public GameExit gameExit;
    public float t;
    public float normalizedSpeed;
    public float worldSpeed;
    public float transferDistance;
    public float[] transferDistances;
    public bool isMovingToSpline;
    public float transferElapsed;
    public Vector3 transferStartPosition;
    public Vector3 transferTargetPosition;
    public Quaternion transferStartRotation;
    public Quaternion transferTargetRotation;
    public Vector3 transferStartScale;
    public Vector3 transferTargetScale;
    public Tween transferMotionTween;
    public Tween pathMotionTween;
    public IBusPath path;
    public float motionDeltaTime;
    public bool useXZBoard;
    public AnimationCurve movementX;
    public AnimationCurve movementY;
    public AnimationCurve rotationCurve;
    public AnimationCurve scaleCurve;
}

/// <summary>Owns exit connection, transfer animation, and exit-path movement.</summary>
internal sealed class SplineBusExitCoordinator
{
    private readonly SplineBusMotor motor;
    private readonly SplineBusEntryCoordinator entry;
    private readonly List<ExitBusRunTime> exitingBuses = new List<ExitBusRunTime>();
    private bool hasConnection;
    private bool useXZBoard;

    public void ConfigureSourceFrame(bool xzBoard)
    {
        useXZBoard = xzBoard;
    }
    private float connectionProgress;
    private Vector3 connectionPosition;

    public SplineBusExitCoordinator(SplineBusMotor motor, SplineBusEntryCoordinator entry)
    {
        this.motor = motor;
        this.entry = entry;
    }

    public void Clear()
    {
        for (int i = 0; i < exitingBuses.Count; i++)
            KillMotion(exitingBuses[i]);
        exitingBuses.Clear();
        InvalidateConnection();
    }

    public void InvalidateConnection() => hasConnection = false;

    public bool TryMoveDoneBus(
        BusController bus, IBusPath boardPath, IBusPath exitPath,
        float cruiseSpeed, float reachedProximity, LevelControl level)
    {
        if (bus == null || bus.SlotBusEmpty != 0 || !bus.isDone ||
            boardPath == null || exitPath == null ||
            !motor.TryGet(bus, out SplineBusRuntime runtime) ||
            FindExitBus(bus) != null ||
            !TryGetConnection(boardPath, exitPath,
                out float progress, out Vector3 position) ||
            !BusExitTransferMath.HasReachedConnection(
                bus.isDone, runtime.Progress, progress,
                bus.transform.position, position, reachedProximity))
            return false;

        GameExit gate = FindExitGate(position);
        gate?.PlayAnimExit();
        bus.MarkExitStarted();
        level.GetPanelSlot().UpSlot();
        DOVirtual.DelayedCall(1f, level.CheckWin);
        BeginTransfer(bus, runtime, gate, boardPath, exitPath, cruiseSpeed);
        return true;
    }

    public void Tick(
        IBusPath exitPath, float deltaTime,
        AnimationCurve movementX, AnimationCurve movementY,
        AnimationCurve rotationCurve, AnimationCurve scaleCurve)
    {
        for (int i = exitingBuses.Count - 1; i >= 0; i--)
        {
            ExitBusRunTime exitBus = exitingBuses[i];
            if (exitBus == null || exitBus.bus == null)
            {
                KillMotion(exitBus);
                exitingBuses.RemoveAt(i);
                continue;
            }

            float movementDeltaTime = Mathf.Max(0f, deltaTime);
            if (exitBus.isMovingToSpline)
            {
                if (exitBus.transferDistances == null)
                {
                    exitBus.movementX = movementX;
                    exitBus.movementY = movementY;
                    exitBus.rotationCurve = rotationCurve;
                    exitBus.scaleCurve = scaleCurve;
                    PrepareTransferDistance(exitBus);
                }
                movementDeltaTime = AdvanceTransfer(exitBus, movementDeltaTime);
                if (exitBus.isMovingToSpline) continue;
            }

            exitBus.path = exitPath;
            exitBus.motionDeltaTime = movementDeltaTime;
            if (exitBus.pathMotionTween == null || !exitBus.pathMotionTween.IsActive())
            {
                exitBus.pathMotionTween = DOTween.To(
                        () => 0f,
                        progress => ApplyPathPose(exitBus, progress),
                        1f, 1f)
                    .SetEase(Ease.Linear)
                    .SetAutoKill(false)
                    .SetLink(exitBus.bus.gameObject, LinkBehaviour.KillOnDestroy)
                    .Pause();
            }

            exitBus.t += movementDeltaTime * exitBus.normalizedSpeed;
            bool reachedEnd = exitBus.t >= 1f;
            exitBus.t = Mathf.Min(exitBus.t, 1f);
            exitBus.pathMotionTween.Goto(exitBus.t, false);

            if (reachedEnd)
            {
                exitBus.gameExit?.PlaySitExit();
                exitBus.bus.gameObject.SetActive(false);
                KillMotion(exitBus);
                exitingBuses.RemoveAt(i);
            }
        }
    }

    private static Vector3 EvaluateTransferPosition(ExitBusRunTime exitBus, float progress)
    {
        BusExitTransferMath.Evaluate(progress,
            exitBus.transferStartPosition, exitBus.transferTargetPosition,
            exitBus.transferStartRotation, exitBus.transferTargetRotation,
            exitBus.transferStartScale, exitBus.transferTargetScale,
            exitBus.movementX, exitBus.movementY, exitBus.rotationCurve, exitBus.scaleCurve,
            out Vector3 position, out _, out _, exitBus.useXZBoard);
        return position;
    }

    private static void PrepareTransferDistance(ExitBusRunTime exitBus)
    {
        const int samples = 64;
        exitBus.transferDistances = new float[samples + 1];
        Vector3 previous = exitBus.transferStartPosition;
        for (int i = 1; i <= samples; i++)
        {
            Vector3 current = EvaluateTransferPosition(exitBus, i / (float)samples);
            exitBus.transferDistances[i] = exitBus.transferDistances[i - 1] +
                Vector3.Distance(previous, current);
            previous = current;
        }
    }

    /// <summary>Move at board world speed, then hand the unused frame time to the exit path.</summary>
    private static float AdvanceTransfer(ExitBusRunTime exitBus, float deltaTime)
    {
        float length = exitBus.transferDistances[exitBus.transferDistances.Length - 1];
        if (length <= 0.00001f)
        {
            // A shared connection has no transfer distance. Keep the current
            // rotation for the path's normal turn-rate limit, and move this frame.
            exitBus.bus.transform.position = exitBus.transferTargetPosition;
            exitBus.bus.transform.localScale = exitBus.transferTargetScale;
            exitBus.isMovingToSpline = false;
            return deltaTime;
        }
        if (exitBus.worldSpeed <= Mathf.Epsilon) return 0f;
        float consumed = Mathf.Min(deltaTime,
            Mathf.Max(0f, length - exitBus.transferDistance) / exitBus.worldSpeed);
        exitBus.transferElapsed += consumed;
        exitBus.transferDistance = Mathf.Min(length,
            exitBus.transferDistance + consumed * exitBus.worldSpeed);
        float progress = BusExitTransferMath.DistanceToParameter(
            exitBus.transferDistances, exitBus.transferDistance);
        if (exitBus.transferMotionTween == null || !exitBus.transferMotionTween.IsActive())
        {
            exitBus.transferMotionTween = DOTween.To(
                    () => 0f, value => ApplyTransferPose(exitBus, value), 1f, 1f)
                .SetEase(Ease.Linear)
                .SetAutoKill(false)
                .SetLink(exitBus.bus.gameObject, LinkBehaviour.KillOnDestroy)
                .Pause();
        }
        exitBus.transferMotionTween.Goto(progress, false);
        if (exitBus.transferDistance >= length)
        {
            exitBus.isMovingToSpline = false;
            exitBus.transferMotionTween.Kill();
            exitBus.transferMotionTween = null;
        }
        return exitBus.isMovingToSpline ? 0f : Mathf.Max(0f, deltaTime - consumed);
    }

    private static void ApplyTransferPose(ExitBusRunTime exitBus, float progress)
    {
        if (exitBus.bus == null)
            return;

        BusExitTransferMath.Evaluate(
            progress,
            exitBus.transferStartPosition, exitBus.transferTargetPosition,
            exitBus.transferStartRotation, exitBus.transferTargetRotation,
            exitBus.transferStartScale, exitBus.transferTargetScale,
            exitBus.movementX, exitBus.movementY,
            exitBus.rotationCurve, exitBus.scaleCurve,
            out Vector3 position, out Quaternion rotation, out Vector3 scale, exitBus.useXZBoard);
        exitBus.bus.transform.SetPositionAndRotation(position, rotation);
        exitBus.bus.transform.localScale = scale;
    }

    private static void ApplyPathPose(ExitBusRunTime exitBus, float progress)
    {
        if (exitBus.bus == null || exitBus.path == null)
            return;

        Quaternion targetRotation = SplineBusMotor.EvaluatePathPose(
            exitBus.path, progress, out Vector3 position);
        Quaternion rotation = SplineBusMotor.FollowPathRotation(
            exitBus.bus.transform.rotation, targetRotation, exitBus.motionDeltaTime);
        exitBus.bus.transform.SetPositionAndRotation(position, rotation);
    }

    private static void KillMotion(ExitBusRunTime exitBus)
    {
        if (exitBus == null)
            return;

        exitBus.transferMotionTween?.Kill();
        exitBus.pathMotionTween?.Kill();
        exitBus.transferMotionTween = null;
        exitBus.pathMotionTween = null;
    }

    private void BeginTransfer(
        BusController bus, SplineBusRuntime runtime, GameExit gate,
        IBusPath boardPath, IBusPath exitPath, float cruiseSpeed)
    {
        float worldSpeed = Mathf.Max(0f, cruiseSpeed) * boardPath.Length;
        float normalizedSpeed = BusExitTransferMath.GetExitNormalizedSpeed(
            cruiseSpeed, boardPath.Length, exitPath.Length);
        ExitBusRunTime exitBus = new ExitBusRunTime
        {
            bus = bus,
            gameExit = gate,
            t = 0f,
            normalizedSpeed = normalizedSpeed,
            worldSpeed = worldSpeed,
            useXZBoard = useXZBoard,
            isMovingToSpline = true
        };
        exitingBuses.Add(exitBus);

        Quaternion entryRotation = SplineBusMotor.EvaluatePathPose(
            exitPath, 0f, out Vector3 entryPosition);
        exitBus.transferStartPosition = bus.transform.position;
        exitBus.transferTargetPosition = entryPosition;
        exitBus.transferStartRotation = bus.transform.rotation;
        exitBus.transferTargetRotation = entryRotation;

        entry.Untrack(bus);
        motor.Remove(runtime);
        bus.KillSplineApproachTween();
        bus.DisableSplineEntryCheck();
        bus.IsRay = false;
        bus.SetSplineMoving(true);
        bus.SetOnSplineConveyor(false);
        bus.transform.DOKill();
        bus.transform.SetParent(null, true);
        exitBus.transferStartScale = bus.transform.localScale;
        exitBus.transferTargetScale = bus.transform.localScale;
    }

    private ExitBusRunTime FindExitBus(BusController bus)
    {
        for (int i = 0; i < exitingBuses.Count; i++)
            if (exitingBuses[i].bus == bus)
                return exitingBuses[i];
        return null;
    }

    private bool TryGetConnection(
        IBusPath boardPath, IBusPath exitPath,
        out float progress, out Vector3 position)
    {
        if (!hasConnection)
        {
            Vector3 exitStart = exitPath.EvaluatePosition(0f);
            connectionProgress = boardPath.GetClosestProgress(exitStart);
            connectionPosition = boardPath.EvaluatePosition(connectionProgress);
            hasConnection = true;
        }

        progress = connectionProgress;
        position = connectionPosition;
        return true;
    }

    private static GameExit FindExitGate(Vector3 position)
    {
        GameExit[] gates = Object.FindObjectsOfType<GameExit>();
        GameExit nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (GameExit gate in gates)
        {
            float distance = (gate.transform.position - position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearest = gate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }
}
