using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Registers buses on the conveyor and handles their approach and entrance.</summary>
internal sealed class SplineBusEntryCoordinator
{
    private readonly SplineBusMotor motor;
    private readonly HashSet<BusController> approachingBuses = new HashSet<BusController>();
    private readonly BusTrafficRayProbe trafficProbe = new BusTrafficRayProbe();

    private float stopRayDistance;
    private float resumeRayDistance;
    private float laneHalfWidth;
    private float spacingPadding;
    private LayerMask rayLayer;
    private float moveSpeed;
    private float entryRotationDuration;
    private float scanGateZ;
    private float entryTurnRadius;
    private float conveyorCruiseSpeed;

    /// <summary>Nhận bộ quản lý xe trên spline để đăng ký xe mới khi qua cổng.</summary>
    public SplineBusEntryCoordinator(SplineBusMotor motor)
    {
        this.motor = motor;
    }

    /// <summary>Nhận các thông số tốc độ, khoảng cách và ray từ SplineControll.</summary>
    public void Configure(
        float stopDistance, float resumeDistance, float laneWidth,
        float padding, LayerMask layer, float busMoveSpeed,
        float rotationDuration, float gateZ, float turnRadius, float cruiseSpeed = 0f)
    {
        stopRayDistance = stopDistance;
        resumeRayDistance = resumeDistance;
        laneHalfWidth = laneWidth;
        spacingPadding = padding;
        rayLayer = layer;
        moveSpeed = busMoveSpeed;
        entryRotationDuration = rotationDuration;
        scanGateZ = gateZ;
        entryTurnRadius = turnRadius;
        conveyorCruiseSpeed = cruiseSpeed;
    }

    /// <summary>Xóa danh sách xe còn đang tiến tới cổng khi reset màn chơi.</summary>
    public void Clear() => approachingBuses.Clear();

    /// <summary>Bỏ theo dõi một xe đã rời luồng tiếp cận cổng.</summary>
    public void Untrack(BusController bus) => approachingBuses.Remove(bus);

    /// <summary>Kiểm tra ray phía trước; dùng ngưỡng xa hơn khi xe đang chờ chạy lại.</summary>
    public bool HasBusAhead(BusController bus, Vector3 direction, bool useResumeDistance)
    {
        float distance = useResumeDistance
            ? Mathf.Max(stopRayDistance, resumeRayDistance)
            : stopRayDistance;
        return trafficProbe.HasBusAhead(
            bus, direction, distance, laneHalfWidth, spacingPadding, rayLayer);
    }

    /// <summary>Mỗi frame, tạm dừng hoặc tiếp tục các xe đang đi tới cổng quét.</summary>
    public void TickApproaches()
    {
        foreach (BusController bus in approachingBuses)
        {
            if (bus == null || !bus.HasActiveSplineApproach)
                continue;

            // Dock approach only follows dock/entering buses. Loop occupancy is
            // handled once at StartReceiving, never by a second approach ray.
            bool blocked = HasEnteringBusAhead(bus);
            bus.SetSplineApproachPausedByTraffic(blocked);
        }
    }

    private bool HasEnteringBusAhead(BusController bus)
    {
        Vector3 direction = bus.GetSplineApproachDirection().normalized;
        for (int i = 0; i < motor.Buses.Count; i++)
        {
            SplineBusRuntime other = motor.Buses[i];
            if (other == null || other.Bus == null || other.Bus == bus || !other.IsEntering)
                continue;
            float extent = (bus.GetVisualLengthAlong(direction) +
                other.Bus.GetVisualLengthAlong(direction)) * 0.5f;
            if (BusTrafficMath.IsBusAhead(bus.transform.position, direction,
                other.Bus.transform.position, extent, laneHalfWidth, out _))
                return true;
        }
        return false;
    }

    /// <summary>
    /// BusDataNew Receiver_ConveyorBase: reserve receive points, check occupancy
    /// before StartReceiving, then run TransferStep continuously until EndTransfer.
    /// Approach and transfer share one timeline so the handoff consumes no extra frame.
    /// </summary>
    public float AdvanceEntrance(
        SplineBusRuntime runtime, IBusPath path, float deltaTime,
        float cruiseSpeed = 0f, bool useProgressSpacing = true)
    {
        if (runtime?.Bus == null || path == null || deltaTime <= 0f)
            return 0f;

        float start = runtime.EntryElapsed;
        float remaining = Mathf.Max(0f, runtime.EntryDuration - start);
        float requested = Mathf.Min(deltaTime, remaining);
        const float brakingTime = 0.25f;
        const float recoveryPerSecond = 4f;
        float targetSpeedFactor = 1f;
        if (!runtime.EntryTransferStarted)
        {
            if (CanInitiateTransfer(runtime) && CanStartTransfer(runtime))
            {
                runtime.EntryTransferReserved = true;
                runtime.EntryTransferStarted = true;
            }
            else
            {
                runtime.EntryTransferReserved = false;
                // Look far enough ahead to brake before the actual safe limit.
                // Dividing by brakingTime + dt approaches a busy gate without
                // reaching it at cruise speed and cutting motion to zero.
                float safeTime = ClampApproachStep(runtime, Mathf.Min(
                    brakingTime + deltaTime,
                    Mathf.Max(0f, runtime.EntryStraightDuration - start)));
                targetSpeedFactor = Mathf.Clamp01(safeTime / (brakingTime + deltaTime));
                requested = Mathf.Min(requested, safeTime);
            }
        }

        if (runtime.EntryTransferStarted) requested = ClampApproachStep(runtime, requested);

        runtime.EntrySpeedFactor = targetSpeedFactor < runtime.EntrySpeedFactor
            ? targetSpeedFactor
            : Mathf.MoveTowards(runtime.EntrySpeedFactor, targetSpeedFactor,
                recoveryPerSecond * deltaTime);
        requested = Mathf.Min(requested, deltaTime * runtime.EntrySpeedFactor);
        runtime.EntryWaitingForTraffic = requested <= 0.00001f;
        if (requested > 0f || start >= runtime.EntryDuration)
            SplineEntranceMovement.Advance(runtime, requested);
        runtime.Bus.SetTurnSwaySpeed(runtime.EntrySpeedFactor);
        float consumedTime = runtime.EntrySpeedFactor > Mathf.Epsilon
            ? (runtime.EntryElapsed - start) / runtime.EntrySpeedFactor : deltaTime;
        return runtime.IsEntering ? 0f : Mathf.Max(0f, deltaTime - consumedTime);
    }

    /// <summary>Sweep the unreserved straight before applying any movement.</summary>
    private float ClampApproachStep(SplineBusRuntime runtime, float requested)
    {
        int steps = Mathf.Max(1, Mathf.CeilToInt(requested * runtime.EntryMoveSpeed / 0.025f));
        float allowed = 0f;
        for (int step = 1; step <= steps; step++)
        {
            float time = requested * step / steps;
            SplineEntranceMovement.EvaluatePose(runtime, runtime.EntryElapsed + time,
                out Vector3 position, out Vector3 tangent);
            Quaternion rotation = tangent.sqrMagnitude > Mathf.Epsilon
                ? Quaternion.LookRotation(tangent.normalized, Vector3.up) : runtime.Bus.transform.rotation;
            for (int i = 0; i < motor.Buses.Count; i++)
            {
                SplineBusRuntime other = motor.Buses[i];
                if (other == null || other == runtime || !other.IsTrafficActive) continue;
                if (other.IsEntering)
                {
                    bool otherHasPriority = IndexOf(other) < IndexOf(runtime);
                    if (!otherHasPriority) continue;
                    if (SplineEntranceMovement.OverlapsTrafficCorridor(other, runtime, position, rotation))
                        return allowed;
                }
                // Only actual occupied space blocks movement, never another entrant's future route.
                if (BusTrafficMath.OverlapsBusFootprint(runtime.Bus, position, rotation, other.Bus, 0.04f))
                    return allowed;
            }
            allowed = time;
        }
        return allowed;
    }

    private int IndexOf(SplineBusRuntime runtime)
    {
        for (int i = 0; i < motor.Buses.Count; i++) if (motor.Buses[i] == runtime) return i;
        return int.MaxValue;
    }

    private bool CanInitiateTransfer(SplineBusRuntime runtime)
    {
        if (SplineTrafficSpacing.IsReceivePointOccupied(
            runtime.EntryReceiveStart, runtime.EntryNormalizedExtent, runtime, motor.Buses))
            return false;
        return true; // CanStartTransfer performs the corridor test once.
    }

    private bool IsVisualCorridorClear(SplineBusRuntime runtime)
    {
        for (int i = 0; i < motor.Buses.Count; i++)
        {
            SplineBusRuntime other = motor.Buses[i];
            if (other == null || other == runtime || !other.IsTrafficActive || other.IsEntering)
                continue;
            if (SplineEntranceMovement.OverlapsTrafficCorridorWithGap(runtime, other,
                other.Bus.transform.position, other.Bus.transform.rotation,
                other.IsEntering ? 0f : 0.04f)) return false;
        }
        return true;
    }

    private bool CanStartTransfer(SplineBusRuntime runtime)
    {
        return IsVisualCorridorClear(runtime) && !SplineTrafficSpacing.IsReceivePointOccupied(
                   runtime.EntryReceiveStart, runtime.EntryNormalizedExtent, runtime, motor.Buses) &&
               !SplineTrafficSpacing.IsReceivePointOccupied(
                   runtime.Progress, runtime.EntryNormalizedExtent, runtime, motor.Buses);
    }

    /// <summary>Chỉ cho xe hợp lệ bắt đầu nhập spline khi vị trí đầu spline còn trống.</summary>
    public bool TryInsert(BusController bus, IBusPath path)
    {
        if (bus == null || !bus.CanEnterSpline ||
            motor.TryGet(bus, out _))
            return false;

        return TryRegister(bus, path);
    }

    /// <summary>
    /// Đăng ký xe với motor, tính các điểm đi thẳng, cung rẽ trái và đoạn căn vào spline.
    /// Thời lượng từng đoạn dựa trên chiều dài đường và tốc độ cấu hình.
    /// </summary>
    private bool TryRegister(BusController bus, IBusPath path)
    {
        if (bus == null || motor.TryGet(bus, out _) || path == null)
            return false;

        // Entry is always progress zero; nearest-point projection can jump ahead.
        const float entryProgress = 0f;
        SplineBusRuntime runtime = new SplineBusRuntime(bus, entryProgress);
        runtime.IsEntering = true;
        // This scene receives at progress zero (start == end). Store both points
        // explicitly, matching GetConveyorReceiveProgresses rather than a forecast.
        runtime.EntryReceiveStart = entryProgress;
        runtime.EntryNormalizedExtent = runtime.ConveyorExtent /
            Mathf.Max(path.Length, 0.0001f);

        Quaternion rotation = SplineBusMotor.EvaluatePathPose(
            path, entryProgress, out Vector3 position);
        runtime.EntryStartPosition = bus.transform.position;
        runtime.EntryStartRotation = bus.transform.rotation;
        runtime.EntryTargetPosition = position;
        runtime.EntryTargetRotation = rotation;
        float approachLength = bus.GetVisualLengthAlong(Vector3.forward);
        float turnZ = position.z - Mathf.Max(0.4f, approachLength * 0.5f);
        runtime.EntryTurnPosition = new Vector3(
            runtime.EntryStartPosition.x, position.y, turnZ);
        runtime.EntryLeftPosition = new Vector3(position.x, position.y, turnZ);
        Vector3 leftDirection = runtime.EntryLeftPosition - runtime.EntryTurnPosition;
        runtime.EntryLeftRotation = leftDirection.sqrMagnitude > Mathf.Epsilon
            ? Quaternion.LookRotation(leftDirection.normalized, Vector3.up)
            : rotation;
        runtime.EntryElapsed = 0f;

        float straightLength = Vector3.Distance(
            runtime.EntryStartPosition,
            runtime.EntryTurnPosition);

        float leftLength = Vector3.Distance(
            runtime.EntryTurnPosition,
            runtime.EntryLeftPosition);

        // Giới hạn cung để không vượt ra ngoài hai đoạn đường.
        float radius = Mathf.Min(
            entryTurnRadius,
            straightLength * 0.5f,
            leftLength * 0.5f);

        runtime.EntryCurveStart = Vector3.MoveTowards(
            runtime.EntryTurnPosition,
            runtime.EntryStartPosition,
            radius);

        runtime.EntryCurveEnd = Vector3.MoveTowards(
            runtime.EntryTurnPosition,
            runtime.EntryLeftPosition,
            radius);

        // Trim the horizontal segment and join the conveyor with a tangent-matched cubic.
        Vector3 corner = runtime.EntryLeftPosition;
        float alignDistance = Vector3.Distance(corner, runtime.EntryTargetPosition);
        float horizontalRoom = Vector3.Distance(runtime.EntryCurveEnd, corner);
        float trim = Mathf.Min(entryTurnRadius, horizontalRoom * 0.5f, alignDistance);
        runtime.EntryLeftPosition = Vector3.MoveTowards(corner, runtime.EntryCurveEnd, trim);
        Vector3 horizontalDirection = runtime.EntryLeftRotation * Vector3.forward;
        Vector3 finalDirection = runtime.EntryTargetRotation * Vector3.forward;
        const float quarterCircleHandle = 0.5522848f;
        runtime.EntryAlignControl1 = runtime.EntryLeftPosition +
            horizontalDirection * (trim * quarterCircleHandle);
        runtime.EntryAlignControl2 = runtime.EntryTargetPosition -
            finalDirection * (alignDistance * quarterCircleHandle);
        SplineEntranceMovement.Prepare(
            runtime, moveSpeed, entryRotationDuration, conveyorCruiseSpeed * path.Length);

        // Track every eligible click immediately. A second gate tween could
        // overtake a waiting entrant, reverse at the turn, or become untracked.
        // The receiver commits one transfer at a time in AdvanceEntrance.
        motor.Add(runtime);
        bus.KillSplineApproachTween();
        approachingBuses.Remove(bus);
        bus.DisableSplineEntryCheck();
        bus.IsRay = false;
        bus.SetSplineMoving(true);
        bus.SetOnSplineConveyor(true);
        bus.transform.SetParent(null, true);
        return true;
    }

    /// <summary>Thử nhập spline ngay; nếu đầu đường bận thì đưa xe tới cổng chờ.</summary>
    public bool AddStickMan(BusController bus, IBusPath path)
    {
        if (bus == null)
            return false;
        if (motor.TryGet(bus, out _))
            return true;

        bus.DisableSplineEntryCheck();
        bus.IsRay = false;
        if (TryRegister(bus, path))
            return true;

        MoveBusToScanGate(bus);
        return true;
    }

    /// <summary>Ngừng theo dõi và hủy trạng thái chờ của xe đang tiếp cận cổng.</summary>
    public void CancelApproach(BusController bus)
    {
        if (bus == null)
            return;

        approachingBuses.Remove(bus);
        bus.DisableSplineEntryCheck();
        bus.IsRay = false;
    }

    /// <summary>Cho xe chạy thẳng tới cổng bằng tween, rồi bật kiểm tra xe phía trước.</summary>
    private void MoveBusToScanGate(BusController bus)
    {
        // The gate uses local lane Z; the ray begins checking traffic there.
        approachingBuses.Add(bus);
        bus.KillSplineApproachTween();
        Vector3 start = bus.transform.localPosition;
        // Xe chỉ tiến về cổng; nếu đã qua cổng thì đứng yên chờ, không lùi lại.
        Vector3 target = new Vector3(start.x, start.y, Mathf.Max(start.z, scanGateZ));
        float duration = Mathf.Max(Vector3.Distance(start, target) / moveSpeed, 0.05f);
        Sequence sequence = DOTween.Sequence();
        sequence.Append(bus.transform.DOLocalMove(target, duration).SetEase(Ease.Linear));

        bool reachedGate = false;
        sequence
            .SetLink(bus.gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                reachedGate = true;
                approachingBuses.Remove(bus);
                bus.ClearSplineApproachTween(sequence);
                bus.EnableSplineEntryCheck();
                bus.IsRay = true;
            })
            .OnKill(() =>
            {
                approachingBuses.Remove(bus);
                bus.ClearSplineApproachTween(sequence);
                if (!reachedGate)
                    CancelApproach(bus);
            });

        Vector3 direction = bus.transform.parent != null
            ? bus.transform.parent.TransformDirection(target - start)
            : target - start;
        bus.SetSplineApproachTween(sequence, direction);
    }
}
