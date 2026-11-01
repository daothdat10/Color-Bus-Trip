using DG.Tweening;
using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns a bus instance's gameplay state, visual setup, and movement commands.</summary>
public class BusController : MonoBehaviour
{
    private LevelControl levelControl;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private MeshRenderer meshCar;
    [SerializeField] private Transform trfParentStickMan;
    [SerializeField] private Vector3[] trfListPoint;
    private Material busStencilMaterial;
    private void OnDestroy() { if (busStencilMaterial != null) Destroy(busStencilMaterial); }
    private int colorBus;
    private int slotBus;
    private int slotEmpty;
    private int slotDefault = 16;
    protected BusData resource;
    private readonly BusPassengerRoster passengerRoster = new BusPassengerRoster();



    private bool isOnSplineConveyor;
    private Tween splineApproachTween;
    private Vector3 splineApproachDirection;
    private bool isSplineApproachPaused;

    private bool isRay;
    private bool isReceivingPassengers;
    private bool hasBeenClicked;
    private bool isWaitingForPredecessor;
    private bool isShiftingQueue;
    private Renderer[] visualRenderers;
    private Renderer[] trafficBodyRenderers;
    public bool busWait;

    /// <summary>Vehicle body and wheels only; passengers do not define traffic contact.</summary>
    internal IReadOnlyList<Renderer> TrafficBodyRenderers
    {
        get
        {
            if (trafficBodyRenderers == null)
            {
                if (meshRenderer != null)
                    trafficBodyRenderers = meshCar != null
                        ? new Renderer[] { meshRenderer, meshCar } : new Renderer[] { meshRenderer };
                else
                {
                    var bodies = new List<Renderer>();
                    foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
                        if (candidate.GetComponentInParent<StickManControl>() == null)
                            bodies.Add(candidate);
                    trafficBodyRenderers = bodies.ToArray();
                }
            }
            return trafficBodyRenderers;
        }
    }



    public bool IsRay { get => isRay; set => isRay = value; }
    public int ColorBus => colorBus;
    public int SlotBus => slotBus;
    public int SlotBusEmpty => slotEmpty;
    public IReadOnlyList<StickManControl> ListStickMans => passengerRoster.Passengers;
    public bool CanEnterSpline { get; private set; }
    public bool IsOnSplineConveyor => isOnSplineConveyor;
    public bool IsShiftingQueue => isShiftingQueue;
    public bool HasActiveSplineApproach =>
        splineApproachTween != null && splineApproachTween.IsActive();
    public bool IsReceivingPassengers => isReceivingPassengers;
    public bool IsSelectable =>
        !hasBeenClicked && !isMove && !isShiftingQueue &&
        !isExit && !isOnSplineConveyor;
    public int i, j;
    public bool isOut { get; private set; }
    public bool isMove { get; private set; }
    public bool isDone { get; private set; }
    public bool isExit { get; private set; }

    public bool isSpline;

    public Transform StickManParent => trfParentStickMan;


    /// <summary>
    /// Đo chiều dài phần hình ảnh của xe theo hướng di chuyển để giữ khoảng cách.
    /// Dùng localBounds và các trục của renderer: world bounds sẽ phình ra khi xe xoay.
    /// </summary>
    public float GetVisualLengthAlong(Vector3 direction)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
        {
            return 0f;
        }

        if (visualRenderers == null)
        {
            visualRenderers = GetComponentsInChildren<Renderer>(true);
        }

        direction.Normalize();
        float maximumLength = 0f;
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            Renderer renderer = visualRenderers[i];
            if (renderer == null || !renderer.enabled)
            {
                continue;
            }

            Bounds bounds = renderer.localBounds;
            Vector3 extents = bounds.extents;
            Matrix4x4 localToWorld = renderer.localToWorldMatrix;
            float projectedLength = 2f * (
                Mathf.Abs(Vector3.Dot(direction,
                    localToWorld.MultiplyVector(Vector3.right * extents.x))) +
                Mathf.Abs(Vector3.Dot(direction,
                    localToWorld.MultiplyVector(Vector3.up * extents.y))) +
                Mathf.Abs(Vector3.Dot(direction,
                    localToWorld.MultiplyVector(Vector3.forward * extents.z)))
            );
            maximumLength = Mathf.Max(maximumLength, projectedLength);
        }

        return maximumLength;
    }

    private void Update()
    {
        if (isDone)
        {
            levelControl?.GetSplineControll()
                ?.TryMoveDoneBusToExitSpline(this);
        }
    }

    public void Init(LevelControl levelControl, BusData data)
    {
        this.levelControl = levelControl;
        isOut = false;
        resource = data;
        IsRay = false;
        isExit = false;
        isDone = false;
        isReceivingPassengers = false;
        hasBeenClicked = false;
        isWaitingForPredecessor = false;
        isShiftingQueue = false;
        isMove = false;
        isOnSplineConveyor = false;
        CanEnterSpline = false;
        isSpline = false;
        busWait = true;
        if (resource == null)
        {
            Debug.LogError("Data truyen vao BusController khong phai BusData");
            return;
        }

        colorBus = resource.colorId;
        slotBus = slotDefault;

        SetVisual();
        ClearSpawnedStickMans();
        LoadPassengerData(resource.passengerData);
        RefreshSlotEmpty
            ();
        slotEmpty = slotDefault - passengerRoster.PassengerCount;
        RefreshDoneStatus();
        SetLayer(9);
    }

    public void SetLayer(int Layer)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = Layer;
    }

    /// <summary>Sets an absolute mesh scale, independent of level population or pooled root scale.</summary>
    public void ApplyTownModelScale(float worldModelScale, float maximumBodyWidth)
    {
        if (meshRenderer == null || worldModelScale <= 0f) return;
        MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null && maximumBodyWidth > 0f)
        {
            float sourceWidth = filter.sharedMesh.bounds.size.x;
            if (sourceWidth > Mathf.Epsilon)
                worldModelScale = Mathf.Min(worldModelScale, maximumBodyWidth / sourceWidth);
        }
        float current = Mathf.Abs(meshRenderer.transform.lossyScale.x);
        if (current > Mathf.Epsilon)
            transform.localScale *= worldModelScale / current;
    }

public void SetVisual()
    {
        if (meshRenderer == null || levelControl == null || levelControl.colorData == null || levelControl.colorData.matColor == null || levelControl.colorData.matColor.Length == 0)
        {
            return;
        }

        int safeColorIndex = Mathf.Clamp(colorBus, 0, levelControl.colorData.matColor.Length - 1);
        Material busMaterial = levelControl.colorData.matColor[safeColorIndex];
        if (busStencilMaterial != null) Destroy(busStencilMaterial);
        Shader stencilShader = Shader.Find("Bus/Toon Passenger Stencil");
        busStencilMaterial = new Material(busMaterial);
        if (stencilShader != null)
        {
            busStencilMaterial.shader = stencilShader;
            busStencilMaterial.renderQueue = 2005;
            busStencilMaterial.SetFloat("_BusStencilComp", 8); // Always
            busStencilMaterial.SetFloat("_BusStencilPass", 2); // Replace
        }
        meshRenderer.sharedMaterial = busStencilMaterial;

        if (meshCar == null)
        {
            return;
        }

        BusWheelVisual wheelVisual = meshCar.GetComponent<BusWheelVisual>();
        if (wheelVisual != null)
        {
            wheelVisual.SetRimMaterial(busMaterial);
        }
    }

    public void InitGrid(int column, int row)
    {
        i = column;
        j = row;
        isOut = false;
        hasBeenClicked = false;
        isWaitingForPredecessor = false;
        isShiftingQueue = false;
    }

    /// <summary>
    /// Marks the queue slot as physically vacated. This must happen after the
    /// bus has cleared the front grid cell, not when the player clicks it.
    /// </summary>
    public void MarkQueueSlotVacated()
    {
        isOut = true;
    }

    public void SpawnStickMan(int spawnSlot, int idxColor)
    {
        int requestedCount = Mathf.Max(0, spawnSlot);
        int spawnedCount = 0;

        for (int i = 0; i < GetSeatLimit() && spawnedCount < requestedCount; i++)
        {
            if (passengerRoster.IsSeatOccupied(i))
            {
                continue;
            }

            if (SpawnStickManAtSeat(i, idxColor))
            {
                spawnedCount++;
            }
        }

        RefreshSlotEmpty();
    }

    public bool SpawnStickManAtSeat(int seatIndex, int idxColor)
    {
        if (!IsValidSeatIndex(seatIndex) || passengerRoster.IsSeatOccupied(seatIndex))
        {
            return false;
        }

        Material stickManMaterial = GetColorMaterial(idxColor);
        if (stickManMaterial == null)
        {
            return false;
        }

        StickManControl stickMan = PoolingManager.Instance.SpawnStickMan(trfParentStickMan);
        stickMan.gameObject.SetActive(true);
        stickMan.SetStickManColor(stickManMaterial, idxColor);
        Vector3 pos = trfListPoint[seatIndex];

        stickMan.transform.localPosition = pos;
        stickMan.SetSeatedStencil(true);

        passengerRoster.AddSeatedPassenger(seatIndex, stickMan);
        RefreshSlotEmpty();
        RefreshDoneStatus();
        return true;
    }

    private void LoadPassengerData(BusPassengerData passengerData)
    {
        if (passengerData == null || passengerData.passengers == null)
        {
            return;
        }

        foreach (KeyValuePair<int, StickManData> passenger in passengerData.passengers)
        {
            StickManData stickManData = passenger.Value;
            if (stickManData == null)
            {
                continue;
            }

            SpawnStickManAtSeat(passenger.Key, stickManData.colorId);
        }
    }

    private void ClearSpawnedStickMans()
    {
        foreach (StickManControl stickMan in passengerRoster.SeatedPassengers)
        {
            if (stickMan == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(stickMan.gameObject);
            }
            else
            {
                DestroyImmediate(stickMan.gameObject);
            }
        }

        passengerRoster.Clear();
    }

    private void RefreshSlotEmpty()
    {
        slotEmpty = Mathf.Clamp(slotBus - passengerRoster.OccupiedSeatCount, 0, slotBus);
    }

    private bool IsValidSeatIndex(int seatIndex)
    {
        return seatIndex >= 0 && seatIndex < GetSeatLimit();
    }

    private int GetSeatLimit()
    {
        return Mathf.Min(slotDefault, trfListPoint.Length);
    }

    private Material GetColorMaterial(int idxColor)
    {

        int safeColorIndex = Mathf.Clamp(idxColor, 0, levelControl.colorData.matColor.Length - 1);
        return levelControl.colorData.matColor[safeColorIndex];
    }

    public bool MoveBus()
    {
        if (!IsSelectable || levelControl == null ||
            levelControl.GetPanelSlot() == null)
        {
            return false;
        }

        // A spawned bus can only be selected once, even after its movement ends.
        hasBeenClicked = true;
        if (!levelControl.CheckIsMove(this))
        {
            isWaitingForPredecessor = true;
            return false;
        }

        return BeginMove();
    }

    public bool TryExecutePendingMove()
    {
        if (!hasBeenClicked || !isWaitingForPredecessor ||
            isMove || isShiftingQueue || isExit || levelControl == null ||
            !levelControl.CheckIsMove(this))
        {
            return false;
        }

        isWaitingForPredecessor = false;
        return BeginMove();
    }

    /// <summary>
    /// Makes a bus temporarily non-selectable while its lane is waiting for the
    /// front bus to clear. LevelControl releases the reservation by starting the
    /// actual queue tween or by cancelling the shift.
    /// </summary>
    public void ReserveQueueShift()
    {
        if (!isMove && !isExit && !isOnSplineConveyor)
        {
            isShiftingQueue = true;
        }
    }

    public void CancelQueueShiftReservation()
    {
        if (!isMove && !isExit && !isOnSplineConveyor)
        {
            isShiftingQueue = false;
        }
    }

    public void ShiftQueuePosition(Vector3 targetPosition, float duration)
    {
        if (isMove || isExit || isOnSplineConveyor)
        {
            // A queued bus can be removed from the queue by another gameplay
            // transition before its turn arrives. Never leave its reservation
            // set in that case, otherwise it would remain permanently locked.
            isShiftingQueue = false;
            return;
        }

        transform.DOKill();
        isShiftingQueue = true;
        float safeDuration = Mathf.Max(0f, duration);
        if (safeDuration <= Mathf.Epsilon)
        {
            transform.position = targetPosition;
            isShiftingQueue = false;
            return;
        }

        transform.DOMove(targetPosition, safeDuration)
            // Queue followers should ease in and out like a continuous convoy;
            // OutCubic made their first frames look like a visible teleport.
            .SetEase(Ease.InOutSine)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .OnComplete(() => isShiftingQueue = false)
            .OnKill(() => isShiftingQueue = false);
    }

    private bool BeginMove()
    {
        if (isMove || isShiftingQueue || isExit || levelControl == null ||
            levelControl.GetPanelSlot() == null ||
            levelControl.GetPanelSlot().GetSlot <= 0)
        {
            if (levelControl != null &&
                levelControl.GetPanelSlot() != null &&
                levelControl.GetPanelSlot().GetSlot <= 0)
            {
                isWaitingForPredecessor = true;
            }
            return false;
        }
        SetLayer(10);
        busWait = false;
        isMove = true;
        levelControl.GetPanelSlot().ReSlot();
        CanEnterSpline = false;
        StartMoveAfterGateGranted();
        return true;
    }

    public void StartMoveAfterGateGranted()
    {
        if (!isMove)
        {
            return;
        }

        SplineControll splineControl = levelControl != null
            ? levelControl.GetSplineControll()
            : null;
        if (splineControl == null)
        {
            return;
        }

        // MyGame does not serialize all entrance traffic behind one global
        // gate. Every clicked bus enters its own Entrance state and the
        // approach controller pauses it when the bus ahead is too close.
        PlayMotionBusMove();
    }

    private void PlayMotionBusMove()
    {
        transform.DOKill();
        // Keep the bus under its current scene parent until spline registration.
        bool success = levelControl
            .GetSplineControll()
            .AddStickMan(this);

        if (success)
        {
            levelControl.ShiftQueuedBuses(this);
        }
        else
        {
            levelControl.GetPanelSlot().UpSlot();
            isMove = false;
            isOut = false;
            hasBeenClicked = false;
            isWaitingForPredecessor = false;
            CanEnterSpline = false;
           
        }


    }
    public bool RayBus()
    {
        if (!CanEnterSpline)
        {
            return false;
        }

        SplineControll splineControl = levelControl != null
            ? levelControl.GetSplineControll()
            : null;

        if (splineControl == null)
        {
            return false;
        }

        SetSplineApproachPaused(false);
        if (splineControl.TryInsertBusIntoSpline(this))
        {
           
            return true;

        }

        // The conveyor entry is occupied. Keep this bus at the gate and
        // re-evaluate receive-point occupancy next frame; it cannot jump over the bus
        // ahead to an arbitrary farther slot.
        return false;
    }

    public void SetOnSplineConveyor(bool value)
    {
        isOnSplineConveyor = value;
    }

    public void SetSplineMoving(bool moving)
    {
        isMove = moving;
       

    }

    public void MarkExitStarted()
    {
        isExit = true;
    }

    public void SetPositionAndRotationFromSpline(
        Vector3 position,
        Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

    }

    public void PlayTurnSway(float signedYawDegrees)
    {
        GetComponent<BusSwayController>()?.PlayTurnSway(signedYawDegrees);
    }

    public void SetTurnSwaySpeed(float speedFactor)
    {
        GetComponent<BusSwayController>()?.SetTurnSwaySpeed(speedFactor);
    }

    public void SetSplineApproachTween(Tween tween, Vector3 direction)
    {
        splineApproachTween = tween;
        splineApproachDirection = direction.sqrMagnitude > Mathf.Epsilon
            ? direction.normalized
            : transform.forward;
        isSplineApproachPaused = false;
    }

    public void ClearSplineApproachTween(Tween tween)
    {
        if (splineApproachTween != tween)
        {
            return;
        }

        splineApproachTween = null;
        splineApproachDirection = Vector3.zero;
        isSplineApproachPaused = false;
    }

    public void KillSplineApproachTween()
    {
        Tween tween = splineApproachTween;
        splineApproachTween = null;
        splineApproachDirection = Vector3.zero;
        isSplineApproachPaused = false;

        if (tween != null && tween.IsActive())
        {
            tween.Kill();
        }
    }

    public Vector3 GetSplineApproachDirection()
    {
        return splineApproachDirection.sqrMagnitude > Mathf.Epsilon
            ? splineApproachDirection
            : transform.forward;
    }

    private void SetSplineApproachPaused(bool paused)
    {
        if (splineApproachTween == null || !splineApproachTween.IsActive() ||
            isSplineApproachPaused == paused)
        {
            return;
        }

        isSplineApproachPaused = paused;
        if (paused)
        {
            splineApproachTween.Pause();
        }
        else
        {
            splineApproachTween.Play();
        }
    }

    public void SetSplineApproachPausedByTraffic(bool paused)
    {
        SetSplineApproachPaused(paused);
    }

    public void EnableSplineEntryCheck()
    {
        CanEnterSpline = true;
    }

    public void DisableSplineEntryCheck()
    {
        CanEnterSpline = false;
    }

    public void PlayPassengerBoardedAnimation()
    {
        GetComponent<BusSwayController>()?.PlayPassengerBoardedAnimation();
    }

    public void AddStickMan(StickManControl stickMan)
    {
        
        passengerRoster.AddPassenger(stickMan);
        RefreshDoneStatus();
    }

    public void SetPassengerTransferInProgress(bool inProgress)
    {
        isReceivingPassengers = inProgress;
        RefreshDoneStatus();
    }


    public bool RemoveStick(StickManControl stickMan)
    {
        if (!passengerRoster.RemovePassenger(stickMan))
        {
            return false;
        }
        RefreshSlotEmpty();
        RefreshDoneStatus();
        return true;
    }

    private void RefreshDoneStatus()
    {
        isDone = !isReceivingPassengers && SlotBusEmpty == 0 &&
                 passengerRoster.PassengerCount > 0 &&
                 passengerRoster.AllPassengersMatchColor(ColorBus);
    }

    public bool TryGetEmptySeat(out int seatIndex, out Vector3 localPosition)
    {
        if (passengerRoster.TryGetEmptySeat(trfListPoint.Length, out seatIndex))
        {
            localPosition = trfListPoint[seatIndex];
            return true;
        }
        localPosition = default;
        return false;
    }

    public bool TryReserveEmptySeat(StickManControl stickMan, out Vector3 localPosition)
    {
        if (!passengerRoster.TryReserveEmptySeat(stickMan, trfListPoint.Length,
                out int seatIndex))
        {
            localPosition = default;
            return false;
        }
        localPosition = trfListPoint[seatIndex];
        RefreshSlotEmpty();
        RefreshDoneStatus();
        return true;
    }

    


    public void PrepareBoosterDeparture()
    {
        hasBeenClicked = true;
        isWaitingForPredecessor = false;
        isMove = true;
        levelControl.GetPanelSlot().ReSlot();
        levelControl.ShiftQueuedBuses(this);
        MarkQueueSlotVacated();
    }

    public bool TryHammerComplete()
    {
        if (!isOnSplineConveyor || isExit || isDone || isReceivingPassengers ||
            levelControl == null || PoolingManager.Instance == null ||
            trfListPoint == null || trfListPoint.Length < slotBus) return false;
        ClearSpawnedStickMans();
        SpawnStickMan(slotBus, colorBus);
        RefreshDoneStatus();
        return isDone;
    }


    [Header("Booster Motion")]
    [SerializeField, Min(0.01f)] private float boosterFlightDuration = 1.1f;
    [SerializeField, Min(0f)] private float boosterFlightHeight = 3f;
    [SerializeField] private AnimationCurve boosterFlightTravel = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve boosterFlightArc = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f));
    [SerializeField, Min(0f)] private float hammerWindupDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float hammerHitDuration = 0.3f;
    [SerializeField] private Vector3 hammerHitScale = new Vector3(1.12f, 0.75f, 1.12f);
    [SerializeField] private AnimationCurve hammerImpact = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
    private Tween boosterMotion;

    public void PlayBoosterFlight(Vector3 start)
    {
        // Prepare: root reserves the landing slot while children carry the visuals.
        boosterMotion?.Kill();
        isReceivingPassengers = true;
        var children = new List<Transform>();
        var positions = new List<Vector3>();
        foreach (Transform child in transform) { children.Add(child); positions.Add(child.localPosition); }
        Vector3 offset = start - transform.position;
        System.Action<float> apply = progress =>
        {
            float travel = Mathf.Clamp01(boosterFlightTravel.Evaluate(progress));
            Vector3 displacement = offset * (1f - travel) + Vector3.up *
                (boosterFlightArc.Evaluate(progress) * boosterFlightHeight);
            for (int i = 0; i < children.Count; i++)
                if (children[i] != null) children[i].position = transform.TransformPoint(positions[i]) + displacement;
        };
        apply(0f);
        float value = 0f;
        // Perform: DOTween drives curve sampling; no competing coroutine writes transforms.
        boosterMotion = DOTween.To(() => value, x => { value = x; apply(x); }, 1f,
                Mathf.Max(0.01f, boosterFlightDuration))
            .SetEase(Ease.Linear).SetTarget(this)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnKill(() =>
            {
                // Resolve completion/cancellation: restore pose and release movement lock.
                for (int i = 0; i < children.Count; i++)
                    if (children[i] != null) children[i].localPosition = positions[i];
                isReceivingPassengers = false;
                RefreshDoneStatus();
                boosterMotion = null;
            });
    }

    public bool TryStartHammerBooster()
    {
        if (!isOnSplineConveyor || isExit || isDone || isReceivingPassengers ||
            levelControl == null || PoolingManager.Instance == null ||
            trfListPoint == null || trfListPoint.Length < slotBus) return false;
        // Prepare: hold the bus until the hit has resolved.
        boosterMotion?.Kill();
        isReceivingPassengers = true;
        Vector3 originalScale = transform.localScale;
        float value = 0f;
        Sequence sequence = DOTween.Sequence();
        sequence.AppendInterval(Mathf.Max(0f, hammerWindupDuration));
        sequence.Append(DOTween.To(() => value, x =>
        {
            value = x;
            transform.localScale = Vector3.Scale(originalScale,
                Vector3.LerpUnclamped(Vector3.one, hammerHitScale, hammerImpact.Evaluate(x)));
        }, 1f, Mathf.Max(0.01f, hammerHitDuration)).SetEase(Ease.Linear));
        sequence.OnComplete(() =>
        {
            transform.localScale = originalScale;
            isReceivingPassengers = false;
            TryHammerComplete();
        });
        sequence.OnKill(() =>
        {
            if (this != null) transform.localScale = originalScale;
            isReceivingPassengers = false;
            boosterMotion = null;
        });
        sequence.SetTarget(this).SetLink(gameObject, LinkBehaviour.KillOnDisable);
        boosterMotion = sequence;
        return true;
    }
}
