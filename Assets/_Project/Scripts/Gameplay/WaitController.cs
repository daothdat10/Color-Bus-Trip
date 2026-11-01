using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class WaitController : MonoBehaviour
{
    private LevelControl levelControl;
    private int colorId;
    private WaitingPassengerColumnData resource;
    public List<StickManControl> listStickMans = new List<StickManControl>();

    [Header("Waiting Stickmen")]
    [SerializeField] private Vector2 waitingAreaMin = new Vector2(-0.5f, -0.5f);
    [SerializeField] private Vector2 waitingAreaMax = new Vector2(0.5f, 0.5f);
    [Tooltip("Preset local positions for Stickmen inside this Wait. Extra passengers use the procedural fallback.")]
    [SerializeField] private List<Vector3> waitingStickManPositions = new List<Vector3>();
    [SerializeField, Min(0f)] private float jumpPower = 1f;
    [SerializeField, Min(0.01f)] private float stickManScale = 13f;
    [SerializeField] private float waitingGroundOffsetY = -1f;
    [SerializeField, Min(0f)] private float stickManCameraOffset = 0.05f;
    [Header("Passenger Transfer Proximity")]
    [Tooltip("Maximum distance along the main conveyor to the cached Stop marker, in world units.")]
    [SerializeField, Min(0.01f)] private float busInteractionDistance = 1.5f;

    [Tooltip("Slots used when no preset Stickman positions are authored.")]
    [SerializeField, Min(1)] private int fallbackCapacity = 16;

    [Header("Passenger Boarding")]
    [SerializeField, Min(0.01f)] private float passengerWalkSpeed = 3f;
    [SerializeField, Min(0f)] private float boardingJumpDistance = 0.25f;
    [SerializeField, Min(0f)] private float boardingStartInterval = 0.05f;

    private readonly Dictionary<StickManControl, int> slotByStickMan =
        new Dictionary<StickManControl, int>();
    [SerializeField, Min(0.01f)] private float waitingReflowSpeed = 1.5f;
    private int initialStickManCount;
    private bool isReceivingStickmen;
    private BusController pausedBus;
    private Sequence boardingSequence;
    private System.Action cancelBoarding;
    private Material defaultWaitMaterial;
    private float stopProgress = float.NaN;
    private float? importedExchangeDistance;
    private Vector3? importedPassengerOffset;

    public void ResetImportedLayout()
    {
        importedPassengerOffset = null;
        importedExchangeDistance = null;
        stopProgress = float.NaN;
        transform.rotation = Quaternion.identity;
    }

    public void SetImportedExchangeDistance(float distance)
    {
        importedExchangeDistance = distance > 0f && !float.IsInfinity(distance) && !float.IsNaN(distance)
            ? distance : (float?)null;
    }

    /// <summary>Fixed number of standing slots; a Wait never holds more passengers than this.</summary>
    public int Capacity => Mathf.Max(
        waitingStickManPositions != null && waitingStickManPositions.Count > 0
            ? waitingStickManPositions.Count
            : fallbackCapacity,
        initialStickManCount);

    public int FreeSlots => Mathf.Max(0, Capacity - listStickMans.Count);

    public float ExchangeDistance => importedExchangeDistance ?? busInteractionDistance;

    public void PlaceOnGround(float worldGroundY)
    {
        Vector3 position = transform.position;
        position.y = worldGroundY - transform.TransformVector(Vector3.up * waitingGroundOffsetY).y;
        transform.position = position;
    }

    public void SetImportedPassengerAnchor(Vector3 worldPosition)
    {
        importedPassengerOffset = transform.InverseTransformPoint(worldPosition);
    }

    // Cache after path conversion, before moving the visual sign into the grass.
    public void SetStopProgress(float progress) => stopProgress =
        BusStopProximity.IsValidProgress(progress) ? progress : float.NaN;

    public float StopProgress => stopProgress;

    public bool TryGetBusTransferDistance(BusController bus, out float distance)
    {
        distance = float.PositiveInfinity;
        SplineControll controller = levelControl == null ? null : levelControl.GetSplineControll();
        return controller != null && controller.TryGetBusStopDistance(bus, stopProgress, out distance);
    }
    

    public int GetColorId => colorId;
    public bool IsTransferringPassengers => isReceivingStickmen;
    [SerializeField] private MeshRenderer meshRenderer;

    private void Awake()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (meshRenderer != null)
        {
            defaultWaitMaterial = meshRenderer.sharedMaterial;
        }
    }


    public void Init(LevelControl levelControl, WaitingPassengerColumnData data)
    {
        this.levelControl = levelControl;
        resource = data;
        colorId = resource.colorId;
        InitWait();
    }

    public void InitWait()
    {
        initialStickManCount = resource.listStickmans.Count;
        if (resource.listStickmans.Count > 0)
        {
            for (int i = 0; i < resource.listStickmans.Count; i++)
            {
                StickManControl stickManControl = PoolingManager.Instance.SpawnStickMan(this.transform);
                stickManControl.SetStickManColor(levelControl.colorData.matColor[resource.listStickmans[i].colorId], resource.listStickmans[i].colorId);
                listStickMans.Add(stickManControl);
                PlaceStickMan(stickManControl, TakeSlot(stickManControl));
            }
        }
        ReleaseColorIfEmpty();
        ApplyWaitColor();
    }

    private void ApplyWaitColor()
    {
        if (meshRenderer == null)
        {
            return;
        }

        // -1 means: preserve the Wait prefab's original Unity material/color.
        if (colorId == -1)
        {
            if (defaultWaitMaterial != null)
            {
                SetWaitMaterial(defaultWaitMaterial);
            }

            return;
        }

        if (levelControl == null || levelControl.colorData == null ||
            levelControl.colorData.ColorWait == null ||
            levelControl.colorData.ColorWait.matColorWait == null ||
            colorId < 0 || colorId >= levelControl.colorData.ColorWait.matColorWait.Length)
        {
            return;
        }

        Material waitMaterial = levelControl.colorData.ColorWait.matColorWait[colorId];
        if (waitMaterial != null)
        {
            SetWaitMaterial(waitMaterial);
        }
    }

    private void SetWaitMaterial(Material material)
    {
        if (meshRenderer == null || material == null || meshRenderer.sharedMaterial == material)
        {
            return;
        }

        // This renderer only switches between shared color assets. Using
        // Renderer.material here creates a unique material instance every time.
        meshRenderer.sharedMaterial = material;
    }

    public void AddStickMan(BusController bus)
    {
        BusPassengerTransferPlan plan = BuildTransferPlan(bus);
        if (plan == null)
        {
            return;
        }

        if (plan.Direction == BusPassengerTransferDirection.BusToWait)
        {
            List<StickManControl> incomingStickmen = new List<StickManControl>();
            for (int i = 0; i < plan.Passengers.Count; i++)
            {
                StickManControl stickMan = plan.Passengers[i];
                if (stickMan == null || listStickMans.Contains(stickMan) ||
                    !bus.RemoveStick(stickMan))
                {
                    continue;
                }

                if (colorId == -1)
                {
                    colorId = stickMan.GetColorId;
                    ApplyWaitColor();
                }

                listStickMans.Add(stickMan);
                TakeSlot(stickMan);
                incomingStickmen.Add(stickMan);
            }

            if (incomingStickmen.Count > 0)
            {
                isReceivingStickmen = true;
                PauseBusForTransfer(bus);

                JumpStickMenIntoWait(incomingStickmen);
            }

            return;
        }

        JumpStickMenIntoBus(bus, plan.Passengers);
    }

    /// <summary>
    /// Mirrors MyGame's transfer finder without changing state. A plan is built
    /// first, then the animation consumes that fixed passenger snapshot.
    /// </summary>
    public bool CanInteractWith(BusController bus)
    {
        return BuildTransferPlan(bus) != null;
    }

    /// <summary>
    /// BusDataNew first limits candidates by distance along the conveyor, then
    /// validates the transfer plan. Sign and passenger positions are visual data.
    /// </summary>
    public bool CanInteractWithBusByDistance(BusController bus)
    {
        if (bus == null || isReceivingStickmen || bus.isExit || bus.isMove ||
            bus.IsReceivingPassengers ||
            !bus.IsOnSplineConveyor)
        {
            return false;
        }

        return TryGetBusTransferDistance(bus, out float distance) &&
               BusStopProximity.IsInRange(distance, ExchangeDistance) &&
               CanInteractWith(bus);
    }

    private BusPassengerTransferPlan BuildTransferPlan(BusController bus)
    {
        if (bus == null || isReceivingStickmen || bus.isExit)
        {
            return null;
        }

        // Keep the original direction priority: first unload a mismatched
        // passenger to this wait point, otherwise load this wait point's type.
        List<StickManControl> toWait = SelectBusToWaitPassengers(bus);
        if (toWait.Count > 0)
        {
            return new BusPassengerTransferPlan(
                BusPassengerTransferDirection.BusToWait,
                toWait
            );
        }

        if (!BusPassengerTransferRules.CanLoadFromWait(
                bus.ColorBus,
                colorId,
                bus.SlotBusEmpty,
                listStickMans.Count))
        {
            return null;
        }

        List<StickManControl> toBus = SelectWaitToBusPassengers(bus);
        return toBus.Count == 0
            ? null
            : new BusPassengerTransferPlan(
                BusPassengerTransferDirection.WaitToBus,
                toBus
            );
    }

    private List<StickManControl> SelectBusToWaitPassengers(BusController bus)
    {
        List<StickManControl> selected = new List<StickManControl>();
        int targetColor = colorId == -1 ? ChooseColorForEmptyWait(bus) : colorId;
        if (colorId == -1 && targetColor == -1)
        {
            return selected;
        }

        for (int i = bus.ListStickMans.Count - 1; i >= 0; i--)
        {
            StickManControl stickMan = bus.ListStickMans[i];
            if (stickMan == null || listStickMans.Contains(stickMan) ||
                !BusPassengerTransferRules.CanUnloadToWait(
                    bus.ColorBus,
                    targetColor,
                    stickMan.GetColorId
                ))
            {
                continue;
            }

            if (targetColor == -1)
            {
                targetColor = stickMan.GetColorId;
            }

            selected.Add(stickMan);
        }

        // Standing slots are fixed: never take more passengers than there is room for.
        int room = FreeSlots;
        if (selected.Count > room)
        {
            selected.RemoveRange(room, selected.Count - room);
        }

        return selected;
    }

    /// <summary>Returns the stickman's slot, giving it the lowest free one the first time.</summary>
    private int TakeSlot(StickManControl stickMan)
    {
        if (slotByStickMan.TryGetValue(stickMan, out int existing))
        {
            return existing;
        }

        int slot = 0;
        while (slotByStickMan.ContainsValue(slot))
        {
            slot++;
        }

        slotByStickMan[stickMan] = slot;
        return slot;
    }

    private int ChooseColorForEmptyWait(BusController bus)
    {
        List<int> candidateColors = new List<int>();
        for (int i = 0; i < bus.ListStickMans.Count; i++)
        {
            StickManControl stickMan = bus.ListStickMans[i];
            if (stickMan != null && !listStickMans.Contains(stickMan) &&
                BusPassengerTransferRules.CanUnloadToWait(
                    bus.ColorBus, -1, stickMan.GetColorId))
            {
                candidateColors.Add(stickMan.GetColorId);
            }
        }

        HashSet<int> boardBusColors = new HashSet<int>();
        List<BusController> buses = levelControl == null ? null : levelControl.GetListBus();
        if (buses != null)
        {
            for (int i = 0; i < buses.Count; i++)
            {
                BusController other = buses[i];
                if (other != null && other != bus && other.IsOnSplineConveyor && !other.isExit)
                {
                    boardBusColors.Add(other.ColorBus);
                }
            }
        }

        return BusPassengerTransferRules.ChooseColorForEmptyWait(candidateColors, boardBusColors);
    }

    private List<StickManControl> SelectWaitToBusPassengers(BusController bus)
    {
        List<StickManControl> selected = new List<StickManControl>();
        if (!BusPassengerTransferRules.CanLoadFromWait(
                bus.ColorBus,
                colorId,
                bus.SlotBusEmpty,
                listStickMans.Count))
        {
            return selected;
        }

        for (int i = 0; i < listStickMans.Count; i++)
        {
            StickManControl stickMan = listStickMans[i];
            if (stickMan != null && stickMan.GetColorId == colorId)
            {
                selected.Add(stickMan);
            }
        }

        // MyGame's SortByProximity rule chooses the closest valid passenger,
        // which also makes the visual boarding order deterministic.
        selected.Sort((first, second) =>
        {
            float firstDistance = first == null
                ? float.MaxValue
                : (first.transform.position - bus.transform.position).sqrMagnitude;
            float secondDistance = second == null
                ? float.MaxValue
                : (second.transform.position - bus.transform.position).sqrMagnitude;
            return firstDistance.CompareTo(secondDistance);
        });

        int transferCount = BusPassengerTransferRules.GetTransferCount(
            selected.Count,
            bus.SlotBusEmpty
        );
        if (selected.Count > transferCount)
        {
            selected.RemoveRange(transferCount, selected.Count - transferCount);
        }

        return selected;
    }

    private void JumpStickMenIntoBus(
        BusController bus,
        List<StickManControl> passengers)
    {
        Sequence transferSequence = DOTween.Sequence();
        int transferCount = 0;
        float lastBoardingEnd = 0f;
        var boardingSeats = new List<KeyValuePair<StickManControl, Vector3>>();

        for (int i = 0; i < passengers.Count; i++)
        {
            StickManControl stickMan = passengers[i];
            if (stickMan == null || !listStickMans.Contains(stickMan) ||
                !bus.TryReserveEmptySeat(stickMan, out Vector3 seatPosition) ||
                !listStickMans.Remove(stickMan))
            {
                continue;
            }

            // Logical seats are reserved first. Visual passengers stay at the
            // wait until their own walking phase starts; later boarders cannot
            // be reflowed into by the remaining crowd.
            if (transferCount == 0) bus.SetPassengerTransferInProgress(true);
            bus.AddStickMan(stickMan);
         
            boardingSeats.Add(new KeyValuePair<StickManControl, Vector3>(stickMan, seatPosition));
            float start = transferCount * Mathf.Max(0f, boardingStartInterval);
            Tween boarding = JumpInBus(bus, stickMan, seatPosition, Mathf.Max(0f, lastBoardingEnd - start));
            stickMan.SetLayer(10);
            transferSequence.Insert(start, boarding);
            lastBoardingEnd = start + boarding.Duration();
            transferCount++;
        }

        if (transferCount == 0)
        {
            transferSequence.Kill();
            return;
        }

        isReceivingStickmen = true;
        PauseBusForTransfer(bus);

        boardingSequence = transferSequence;
        bool finished = false;
        void FinishBoarding(bool cancelled)
        {
            if (finished) return;
            finished = true;
            boardingSequence = null;
            cancelBoarding = null;
            if (cancelled && bus != null && bus.StickManParent != null)
                foreach (var seat in boardingSeats)
                {
                    if (seat.Key == null) continue;
                    seat.Key.transform.SetParent(bus.StickManParent, false);
                    seat.Key.transform.localPosition = seat.Value;
                    seat.Key.transform.localRotation = Quaternion.identity;
                    seat.Key.transform.localScale = Vector3.one;
                    seat.Key.SetSeatedStencil(true);
                    SetLayerRecursively(seat.Key.transform, bus.gameObject.layer);
                }
            if (bus != null) bus.SetPassengerTransferInProgress(false);
            CompleteIncomingStickMen();
        }
        cancelBoarding = () => FinishBoarding(true);
        transferSequence
            .SetTarget(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(() => FinishBoarding(false))
            .OnKill(() => FinishBoarding(true));
    }

    private void OnDisable()
    {
        Sequence sequence = boardingSequence;
        sequence?.Kill();
        // Explicit cleanup also handles DOTween deferring a kill requested in
        // an update callback, and a wait being returned to its pool mid-walk.
        cancelBoarding?.Invoke();
    }

    private Vector3 GetBoardingApproachPosition(BusController bus, Vector3 start)
    {
        Vector3 outward = Vector3.ProjectOnPlane(start - bus.transform.position, Vector3.up);
        float distance = outward.magnitude;
        if (distance <= 0.00001f) return start;
        outward /= distance;
        float bodyEdge = 0f;
        foreach (Renderer body in bus.TrafficBodyRenderers)
        {
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) continue;
            Bounds bounds = body.localBounds;
            Matrix4x4 matrix = body.localToWorldMatrix;
            Vector3 e = bounds.extents;
            float radius = Mathf.Abs(Vector3.Dot(outward, matrix.MultiplyVector(Vector3.right * e.x))) +
                Mathf.Abs(Vector3.Dot(outward, matrix.MultiplyVector(Vector3.up * e.y))) +
                Mathf.Abs(Vector3.Dot(outward, matrix.MultiplyVector(Vector3.forward * e.z)));
            bodyEdge = Mathf.Max(bodyEdge, Vector3.Dot(outward,
                matrix.MultiplyPoint3x4(bounds.center) - bus.transform.position) + radius);
        }
        float threshold = Mathf.Max(boardingJumpDistance, bodyEdge + 0.08f);
        if (distance <= threshold) return start; // already close: never walk backwards
        Vector3 approach = bus.transform.position + outward * threshold;
        approach.y = start.y; // walking stays on the wait's ground plane
        return approach;
    }

    /// <summary>BusDataNew: move toward the bus, then jump into the reserved seat.</summary>
    private Tween JumpInBus(BusController bus, StickManControl stickMan, Vector3 seatPosition,
        float earliestJumpTime = 0f)
    {
        const float jumpDuration = 0.15f;
        stickMan.transform.DOKill();
        stickMan.gameObject.SetActive(true);
        Vector3 approach = GetBoardingApproachPosition(bus, stickMan.transform.position);
        float walkDuration = Vector3.Distance(stickMan.transform.position, approach) /
            Mathf.Max(0.01f, passengerWalkSpeed);
        Sequence boarding = DOTween.Sequence();
        boarding.AppendCallback(() =>
        {
            slotByStickMan.Remove(stickMan);
            ReflowWaitingPassengers();
        });
        if (walkDuration > 0.00001f)
        {
            Vector3 direction = approach - stickMan.transform.position;
            direction.y = 0f;
            boarding.Append(stickMan.transform.DOMove(approach, walkDuration).SetEase(Ease.Linear));
            if (direction.sqrMagnitude > Mathf.Epsilon)
                boarding.Join(stickMan.transform.DORotateQuaternion(
                    Quaternion.LookRotation(direction.normalized, Vector3.up), Mathf.Min(walkDuration, 0.12f))
                    .SetEase(Ease.Linear));
        }
        // Walks overlap; jumps use the same doorway one at a time.
        float delay = Mathf.Max(0f, earliestJumpTime - walkDuration);
        if (delay > 0f) boarding.AppendInterval(delay);
        bool jumping = false;
        Vector3 jumpStart = default, startScale = default;
        Quaternion startRotation = default;
        void BeginJump()
        {
            if (jumping || stickMan == null || bus == null) return;
            jumping = true;
            stickMan.SetSeatedStencil(true);
            stickMan.transform.position = approach;
            stickMan.transform.SetParent(bus.StickManParent, true);
            SetLayerRecursively(stickMan.transform, bus.gameObject.layer);
            jumpStart = stickMan.transform.localPosition;
            startScale = stickMan.transform.localScale;
            startRotation = stickMan.transform.localRotation;
        }
        boarding.AppendCallback(BeginJump);
        boarding.Append(DOTween.To(() => 0f, progress =>
        {
            BeginJump();
            if (stickMan == null) return;
            Vector3 point = Vector3.Lerp(jumpStart, seatPosition, progress);
            point.y += jumpPower * progress * (1f - progress);
            stickMan.transform.localPosition = point;
            stickMan.transform.localScale = Vector3.Lerp(startScale, Vector3.one, progress);
            stickMan.transform.localRotation = Quaternion.Slerp(startRotation, Quaternion.identity, progress);
        }, 1f, jumpDuration).SetEase(Ease.Linear));
        boarding.AppendCallback(() =>
        {
            stickMan.SetSeatedStencil(true);
            if (bus != null) bus.PlayPassengerBoardedAnimation();
            ReflowWaitingPassengers();
        });
        return boarding;
    }

    private void PlaceStickMan(StickManControl stickMan, int waitingIndex)
    {
        stickMan.transform.SetParent(transform, false);
        stickMan.transform.localScale = Vector3.one * stickManScale;
        stickMan.transform.localPosition = GetWaitingPositionInFrontOfWait(waitingIndex);
        SetLayerRecursively(stickMan.transform, gameObject.layer);
    }

    private void JumpStickMenIntoWait(List<StickManControl> incomingStickmen)
    {
        Sequence transferSequence = DOTween.Sequence();
        bool transferCompleted = false;

        for (int i = 0; i < incomingStickmen.Count; i++)
        {
            StickManControl stickMan = incomingStickmen[i];
            int waitingIndex = TakeSlot(stickMan);
            stickMan.SetLayer(0);
            stickMan.transform.DOKill();
            stickMan.gameObject.SetActive(true);
            Vector3 jumpStart = Vector3.zero;
            Vector3 startScale = Vector3.one;
            bool started = false;
            void BeginExit()
            {
                if (started || stickMan == null) return;
                started = true;
                // Keep waiting passengers under the swaying bus until their own turn.
                stickMan.transform.SetParent(transform, true);
                SetLayerRecursively(stickMan.transform, gameObject.layer);
                jumpStart = stickMan.transform.localPosition;
                startScale = stickMan.transform.localScale;
            }
            Vector3 target = GetWaitingPositionInFrontOfWait(waitingIndex);
            transferSequence.AppendCallback(BeginExit);
            transferSequence.Append(DOTween.To(() => 0f, progress =>
            {
                BeginExit();
                if (stickMan == null) return;
                Vector3 point = Vector3.Lerp(jumpStart, target, progress);
                point.y += 8f * progress * (1f - progress);
                stickMan.transform.localPosition = point;
                stickMan.transform.localScale = Vector3.Lerp(startScale, Vector3.one * stickManScale, progress);
            }, 1f, 0.15f).SetEase(Ease.OutQuad));
        }

        transferSequence
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() =>
            {
                transferCompleted = true;
                CompleteIncomingStickMen();
            })
            .OnKill(() =>
            {
                if (!transferCompleted)
                {
                    CompleteIncomingStickMen();
                }
            });
    }

    /// <summary>
    /// AtStop in BusDataNew pulls passengers toward the stop anchor. Repack the
    /// local authored crowd toward its centre as slots are vacated, preserving
    /// spacing and ground height rather than leaving permanent standing holes.
    /// </summary>
    private void ReflowWaitingPassengers()
    {
        listStickMans.RemoveAll(passenger => passenger == null);
        var remaining = new List<StickManControl>(listStickMans);
        if (remaining.Count == 0) return;

        var slots = new List<int>();
        Vector3 centre = Vector3.zero;
        for (int i = 0; i < Capacity; i++)
        {
            slots.Add(i);
            centre += GetWaitingPositionInFrontOfWait(i);
        }
        centre /= Capacity;
        slots.Sort((a, b) =>
        {
            Vector3 first = GetWaitingPositionInFrontOfWait(a) - centre;
            Vector3 second = GetWaitingPositionInFrontOfWait(b) - centre;
            int comparison = (first.x * first.x + first.z * first.z).CompareTo(
                second.x * second.x + second.z * second.z);
            return comparison != 0 ? comparison : a.CompareTo(b);
        });
        // Pending boarders keep their slots until their own jump completes.
        foreach (StickManControl passenger in remaining) slotByStickMan.Remove(passenger);
        foreach (int slot in slots)
        {
            if (remaining.Count == 0) break;
            if (slotByStickMan.ContainsValue(slot)) continue;
            Vector3 target = GetWaitingPositionInFrontOfWait(slot);
            Vector3 worldTarget = transform.TransformPoint(target);
            int closest = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < remaining.Count; i++)
            {
                float candidate = (remaining[i].transform.position - worldTarget).sqrMagnitude;
                if (candidate < distance) { distance = candidate; closest = i; }
            }
            StickManControl passenger = remaining[closest];
            remaining.RemoveAt(closest);
            slotByStickMan[passenger] = slot;
            passenger.transform.DOKill();
            if (distance <= 0.000001f) continue;
            float duration = Mathf.Clamp(Mathf.Sqrt(distance) / Mathf.Max(0.01f, waitingReflowSpeed), 0.08f, 0.35f);
            passenger.transform.DOLocalMove(target, duration)
                .SetEase(Ease.InOutSine)
                .SetLink(passenger.gameObject, LinkBehaviour.KillOnDisable);
        }
    }

    private Vector3 GetWaitingPosition(int waitingIndex)
    {
        if (waitingStickManPositions != null &&
            waitingIndex >= 0 &&
            waitingIndex < waitingStickManPositions.Count)
        {
            return waitingStickManPositions[waitingIndex];
        }

        Vector2 min = Vector2.Min(waitingAreaMin, waitingAreaMax);
        Vector2 max = Vector2.Max(waitingAreaMin, waitingAreaMax);
        Vector2 center = (min + max) * 0.5f;
        Vector2 halfSize = (max - min) * 0.5f;

        int totalStickmen = Mathf.Max(waitingIndex + 1, listStickMans.Count);
        float radius = Mathf.Sqrt((waitingIndex + 0.5f) / totalStickmen);
        float angle = waitingIndex * 2.39996323f;

        float x = center.x + Mathf.Cos(angle) * halfSize.x * radius;
        float z = center.y + Mathf.Sin(angle) * halfSize.y * radius;
        return new Vector3(x, 0f, z);
    }

    private Vector3 GetWaitingPositionInFrontOfWait(int waitingIndex)
    {
        Vector3 localPosition = GetWaitingPosition(waitingIndex);
        if (importedPassengerOffset.HasValue)
        {
            // Existing passenger slots are centred around local z=-0.62.
            // Move that centre to the authored PassengerStopPosition.
            localPosition.x += importedPassengerOffset.Value.x;
            localPosition.z += importedPassengerOffset.Value.z + 0.62f;
        }
        // Passengers stand at the post's base, below the sign's root pivot.
        localPosition.y += waitingGroundOffsetY;
        Camera renderCamera = Camera.main;
        if (renderCamera == null || stickManCameraOffset <= 0f)
        {
            return localPosition;
        }

        Vector3 worldPosition = transform.TransformPoint(localPosition);
        worldPosition -= renderCamera.transform.forward * stickManCameraOffset;
        return transform.InverseTransformPoint(worldPosition);
    }

    private static void SetLayerRecursively(Transform target, int layer)
    {
        if (target == null)
        {
            return;
        }

        target.gameObject.layer = layer;
        for (int i = 0; i < target.childCount; i++)
        {
            SetLayerRecursively(target.GetChild(i), layer);
        }
    }

private void CompleteIncomingStickMen()
    {
        isReceivingStickmen = false;
        // Also clean pending reservations if a boarding sequence was cancelled.
        var departed = new List<StickManControl>();
        foreach (var pair in slotByStickMan)
            if (pair.Key == null || !listStickMans.Contains(pair.Key)) departed.Add(pair.Key);
        foreach (StickManControl passenger in departed) slotByStickMan.Remove(passenger);
        ReflowWaitingPassengers();
        ReleaseColorIfEmpty();
        if (pausedBus != null)
        {
            pausedBus.SetPassengerTransferInProgress(false);
        }
        pausedBus = null;
    }

/// <summary>An empty Wait has no colour and accepts any colour, including the one it just lost.</summary>
    private void ReleaseColorIfEmpty()
    {
        if (listStickMans.Count > 0 || colorId == -1)
        {
            return;
        }

        colorId = -1;
        ApplyWaitColor();
    }

private void PauseBusForTransfer(BusController bus)
    {
        pausedBus = bus;
        if (pausedBus != null)
        {
            pausedBus.SetPassengerTransferInProgress(true);
        }
    }

    private void OnDrawGizmos()
    {
        Vector2 min = Vector2.Min(waitingAreaMin, waitingAreaMax);
        Vector2 max = Vector2.Max(waitingAreaMin, waitingAreaMax);
        Vector3 center = new Vector3((min.x + max.x) * 0.5f, 0.02f, (min.y + max.y) * 0.5f);
        Vector3 size = new Vector3(max.x - min.x, 0.02f, max.y - min.y);

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);
        Gizmos.color = new Color(1f, 1f, 0f, 0.12f);
        Gizmos.DrawCube(center, size);
        if (waitingStickManPositions != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waitingStickManPositions.Count; i++)
            {
                Gizmos.DrawWireSphere(transform.TransformPoint(waitingStickManPositions[i]), 0.04f);
            }
        }
        Gizmos.color = previousColor;
        Gizmos.matrix = previousMatrix;
    }

}

public enum BusPassengerTransferDirection
{
    BusToWait,
    WaitToBus
}

/// <summary>
/// Immutable-at-runtime snapshot of one bus/stop exchange. This mirrors
/// MyGame's PassengerTransferInfo: selection is completed before animation or
/// list mutation starts.
/// </summary>
public sealed class BusPassengerTransferPlan
{
    public BusPassengerTransferDirection Direction { get; }
    public List<StickManControl> Passengers { get; }

    public BusPassengerTransferPlan(
        BusPassengerTransferDirection direction,
        List<StickManControl> passengers)
    {
        Direction = direction;
        Passengers = passengers ?? new List<StickManControl>();
    }
}
