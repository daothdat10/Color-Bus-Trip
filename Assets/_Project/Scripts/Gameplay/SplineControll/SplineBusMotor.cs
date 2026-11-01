using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Runtime state of a bus travelling on the main conveyor.</summary>
internal sealed class SplineBusRuntime
{
    public readonly BusController Bus;
    public readonly float ConveyorExtent;
    public readonly float TrafficRadius;
    public float[] EntryTrafficTimes;
    public Vector3[] EntryTrafficPositions;
    public Quaternion[] EntryTrafficRotations;
    public bool IsTrafficActive => Bus != null && Bus.gameObject.activeInHierarchy && !Bus.isExit;
    public Quaternion MotionRotation;
    public float Progress;
    public bool IsEntering;
    public Vector3 EntryStartPosition;
    public Quaternion EntryStartRotation;
    public Vector3 EntryTurnPosition;
    public Vector3 EntryCurveStart;
    public Vector3 EntryCurveEnd;
    public Vector3 EntryLeftPosition;
    public Quaternion EntryLeftRotation;
    public Vector3 EntryTargetPosition;
    public Quaternion EntryTargetRotation;
    public Vector3 EntryAlignControl1;
    public Vector3 EntryAlignControl2;
    public float[] EntryCurveDistances;
    public float[] EntryAlignDistances;
    public float EntryCurveLength;
    public float EntryAlignLength;
    public float EntryMoveSpeed;
    public float EntryEndSpeed;
    public float EntryElapsed;
    public float EntryDuration;
    public float EntryStraightDuration;
    public float EntryTurnDuration;
    public float EntryLeftDuration;
    public float EntryAlignDuration;
    public float EntrySpeedFactor = 1f;
    public bool EntryWaitingForTraffic;
    public bool EntryTransferReserved;
    public bool EntryTransferStarted;
    public float EntryReceiveStart;
    public float EntryNormalizedExtent;
    public bool EntryCurveSwayTriggered;
    public bool EntryAlignSwayTriggered;
    public Tween EntryMotionTween;
    public Tween PathMotionTween;

    public IBusPath MotionPath;
    public float MotionDeltaTime;
    public SplineBusRuntime(BusController bus, float progress)
    {
        Bus = bus;
        ConveyorExtent = bus == null ? 0f : bus.GetVisualLengthAlong(bus.transform.forward);
        TrafficRadius = BusTrafficMath.GetTrafficRadius(bus);
        Progress = Mathf.Repeat(progress, 1f);
    }
}

/// <summary>Owns conveyor occupancy and advances buses already on the main spline.</summary>
internal sealed class SplineBusMotor
{
    private const float MaximumTurnDegreesPerSecond = 360f;
    private readonly List<SplineBusRuntime> buses = new List<SplineBusRuntime>();
    private readonly Dictionary<BusController, float> previousProgress =
        new Dictionary<BusController, float>();

    public IReadOnlyList<SplineBusRuntime> Buses => buses;

    public void Clear()
    {
        for (int i = 0; i < buses.Count; i++)
            KillMotion(buses[i]);
        buses.Clear();
        previousProgress.Clear();
    }

    public void Add(SplineBusRuntime runtime) => buses.Add(runtime);
    public void Remove(SplineBusRuntime runtime)
    {
        KillMotion(runtime);
        buses.Remove(runtime);

    }

    public bool TryGet(BusController bus, out SplineBusRuntime runtime)
    {
        for (int i = 0; i < buses.Count; i++)
        {
            if (buses[i] != null && buses[i].Bus == bus)
            {
                runtime = buses[i];
                return true;
            }
        }

        runtime = null;
        return false;
    }

    public void Tick(
        IBusPath path,
        SplineBusEntryCoordinator entry,
        float deltaTime,
        float cruiseSpeed,
        float spacingPadding,
        bool useProgressSpacing,
        bool useBlockedPoints,
        IList<float> blockedPoints)
    {
        if (path == null || path.Length <= Mathf.Epsilon || deltaTime <= 0f)
            return;
        previousProgress.Clear();
        for (int i = 0; i < buses.Count; i++)
        {
            SplineBusRuntime runtime = buses[i];
            if (runtime != null && runtime.Bus != null)
                previousProgress[runtime.Bus] = runtime.Progress;
        }

        for (int i = buses.Count - 1; i >= 0; i--)
        {
            SplineBusRuntime runtime = buses[i];
            BusController bus = runtime?.Bus;
            if (bus == null || !runtime.IsTrafficActive)
            {
                KillMotion(runtime);
                buses.RemoveAt(i);
                continue;
            }

            float movementDeltaTime = deltaTime;
            if (runtime.IsEntering)
            {
                movementDeltaTime = entry.AdvanceEntrance(
                    runtime, path, deltaTime, cruiseSpeed, useProgressSpacing);
                if (runtime.IsEntering || movementDeltaTime <= Mathf.Epsilon)
                    continue;
            }

            float delta = bus.IsReceivingPassengers ? 0f : movementDeltaTime * cruiseSpeed;
            float proposed = SplineProgressSolver.Advance(runtime.Progress, delta, true);
            float spacedProgress = SplineTrafficSpacing.ClampProgress(
                runtime, proposed, previousProgress, buses, path, spacingPadding,
                useProgressSpacing, useBlockedPoints, blockedPoints);
            float reservedProgress = spacedProgress; // Entrants yield to circulating traffic at actual contact.
            runtime.Progress = ClampAgainstVisualContact(runtime, reservedProgress, path, movementDeltaTime);
            runtime.MotionPath = path;
            runtime.MotionDeltaTime = movementDeltaTime;
            if (runtime.PathMotionTween == null || !runtime.PathMotionTween.IsActive())
            {
                runtime.PathMotionTween = DOTween.To(
                        () => 0f,
                        progress => ApplyPathPose(runtime, progress),
                        1f, 1f)
                    .SetEase(Ease.Linear)
                    .SetAutoKill(false)
                    .SetLink(bus.gameObject, LinkBehaviour.KillOnDestroy)
                    .Pause();
            }

            runtime.PathMotionTween.Goto(runtime.Progress, false);
        }
    }

    /// <summary>Resolve visual contact between buses already travelling on the loop.</summary>
    private float ClampAgainstVisualContact(
        SplineBusRuntime current, float proposed, IBusPath path, float deltaTime)
    {
        current.MotionRotation = current.Bus.transform.rotation;
        if (path == null) return proposed;
        float distance = SplineProgressSolver.ForwardDistance(current.Progress, proposed, true);
        if (distance <= SplineProgressSolver.Epsilon) return proposed;
        Quaternion requestedRotation = FollowPathRotation(current.Bus.transform.rotation,
            path.EvaluateRotation(proposed), deltaTime);
        // If a turn would sweep the tail into a follower, retry this frame
        // with translation only. The leader clears space instead of both buses
        // waiting for each other, and its body never applies an unchecked turn.
        for (int attempt = 0; attempt < 2; attempt++)
        {
            float angularTravel = Quaternion.Angle(current.Bus.transform.rotation, requestedRotation) *
                Mathf.Deg2Rad * current.TrafficRadius;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance * path.Length / 0.025f),
                Mathf.CeilToInt(angularTravel / 0.02f));
            float allowed = 0f;
            bool deferRotation = false;
            for (int i = 1; i <= steps; i++)
            {
                float fraction = i / (float)steps;
                if (Blocked(fraction))
                {
                    float low = allowed, high = fraction;
                    for (int iteration = 0; iteration < 10; iteration++)
                    {
                        float middle = (low + high) * 0.5f;
                        if (Blocked(middle)) high = middle;
                        else low = middle;
                    }
                    allowed = low;
                    break;
                }
                allowed = fraction;
            }
            if (deferRotation && attempt == 0)
            {
                requestedRotation = current.Bus.transform.rotation;
                continue;
            }
            current.MotionRotation = Quaternion.Slerp(current.Bus.transform.rotation, requestedRotation, allowed);
            return Mathf.Repeat(current.Progress + distance * allowed, 1f);

            bool Blocked(float fraction)
            {
                bool blocked = LoopPoseBlocked(current, path, distance, fraction, requestedRotation,
                    out bool blockedByRearTurn);
                deferRotation |= blockedByRearTurn;
                return blocked;
            }
        }
        return current.Progress;
    }

    private bool LoopPoseBlocked(
        SplineBusRuntime current, IBusPath path, float distance, float fraction, Quaternion requestedRotation,
        out bool deferRotation)
    {
        deferRotation = false;
        float progress = Mathf.Repeat(current.Progress + distance * fraction, 1f);
        EvaluatePathPose(path, progress, out Vector3 position);
        Quaternion rotation = Quaternion.Slerp(current.Bus.transform.rotation, requestedRotation, fraction);
        for (int i = 0; i < buses.Count; i++)
        {
            SplineBusRuntime other = buses[i];
            if (other == null || other == current || !other.IsTrafficActive) continue;
            if (other.IsEntering)
            {
                if (BusTrafficMath.OverlapsBusFootprint(current.Bus, position, rotation, other.Bus, 0f) &&
                    (position - other.Bus.transform.position).sqrMagnitude <
                    (current.Bus.transform.position - other.Bus.transform.position).sqrMagnitude)
                    return true;
                continue;
            }
            float ahead = SplineProgressSolver.ForwardDistance(current.Progress, other.Progress, true);
            bool isBehind = ahead > 0.5f ||
                ahead <= SplineProgressSolver.Epsilon && i > buses.IndexOf(current);
            float gap = isBehind ? 0.002f : 0.015f;
            if (!BusTrafficMath.OverlapsBusFootprint(current.Bus, position, rotation, other.Bus, gap))
                continue;
            if (!isBehind) return true;

            // Recover an existing rear overlap by letting only the leader move
            // away. Do not grant permission to create a new visual overlap.
            if (BusTrafficMath.OverlapsBusFootprint(current.Bus, current.Bus.transform.position,
                    current.Bus.transform.rotation, other.Bus, 0f) &&
                (position - other.Bus.transform.position).sqrMagnitude >=
                (current.Bus.transform.position - other.Bus.transform.position).sqrMagnitude)
                continue;
            if (Quaternion.Angle(current.Bus.transform.rotation, rotation) > 0.001f &&
                !BusTrafficMath.OverlapsBusFootprint(current.Bus, position,
                    current.Bus.transform.rotation, other.Bus, 0.002f))
                deferRotation = true;
            return true;

        }
        return false;
    }

    private static void ApplyPathPose(SplineBusRuntime runtime, float progress)
    {
        if (runtime.Bus == null || runtime.MotionPath == null)
            return;

        EvaluatePathPose(runtime.MotionPath, progress, out Vector3 position);
        runtime.Bus.SetPositionAndRotationFromSpline(position, runtime.MotionRotation);
    }

    private static void KillMotion(SplineBusRuntime runtime)
    {
        if (runtime == null)
            return;

        runtime.EntryMotionTween?.Kill();
        runtime.PathMotionTween?.Kill();
        runtime.EntryMotionTween = null;
        runtime.PathMotionTween = null;
    }

    public static Quaternion EvaluatePathPose(
        IBusPath path, float progress, out Vector3 position)
    {
        position = path == null ? Vector3.zero : path.EvaluatePosition(progress);
        return path == null ? Quaternion.identity : path.EvaluateRotation(progress);
    }

    public static Quaternion FollowPathRotation(
        Quaternion current, Quaternion target, float deltaTime)
    {
        return Quaternion.RotateTowards(
            current, target, MaximumTurnDegreesPerSecond * Mathf.Max(0f, deltaTime));
    }
}
