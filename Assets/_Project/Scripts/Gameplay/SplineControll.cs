using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Scene-facing API and update order for gameplay spline movement.</summary>
public class SplineControll : MonoBehaviour
{
    [Space(10), Header("________Spline_________")]
    [SerializeField] private SplineContainer splineContainer;

    [Header("Bus Spline Approach")]
    [SerializeField] private float splineRayActivationZ = 0f;
    [SerializeField, Min(0.1f)] private float busMoveSpeed = 4f;
    [SerializeField, Min(0.01f)] private float splineCruiseSpeed = 0.15f;
    [SerializeField, Min(0.05f)] private float splineEntryRotationDuration = 0.35f;

    [Header("Spline Progress Traffic")]
    [SerializeField] private bool useSplineProgressSpacing = true;
    [SerializeField, Min(0f)] private float visualBusSpacingPadding = 0.2f;
    [SerializeField] private bool useBlockedProgressPoints = true;
    [SerializeField] private List<float> blockedProgressPoints = new List<float>();

    [Header("Bus Traffic Ray")]
    [SerializeField, Min(0.1f)] private float busStopRayDistance = 2.2f;
    [SerializeField, Min(0.1f)] private float busResumeRayDistance = 2.8f;
    [SerializeField, Min(0.05f)] private float busTrafficLaneHalfWidth = 0.55f;
    [SerializeField] private LayerMask busTrafficRayLayer = ~0;

    [Header("Completed Bus Exit")]
    [SerializeField] private AnimationCurve exitTransferMovementCurveX =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve exitTransferMovementCurveY =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve exitTransferRotationCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve exitTransferScaleCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);
    [SerializeField, Min(1)] private int exitSplineIndex = 1;

    [SerializeField, Min(0.05f)]
    private float splineEntryTurnRadius = 0.6f;

    private float exitReachedProximity = .4f;

    public void ConfigureImportedTownExit(float worldProximity)
    {
        EnsureCoordinators();
        exitReachedProximity = worldProximity;
        exit.ConfigureSourceFrame(true);
    }
    private readonly BusPathCache pathCache = new BusPathCache();
    private readonly SplineBusMotor motor = new SplineBusMotor();
    private SplineBusEntryCoordinator entry;
    private SplineBusExitCoordinator exit;
    private LevelControl levelControl;
    private int importedMainCurveCount;

    private void Awake()
    {
        EnsureCoordinators();
        pathCache.Clear();
        exit.InvalidateConnection();
    }

    private void OnDestroy()
    {
        motor.Clear();
        exit?.Clear();
    }

    private void EnsureCoordinators()
    {
        if (entry != null)
            return;

        entry = new SplineBusEntryCoordinator(motor);
        exit = new SplineBusExitCoordinator(motor, entry);
    }

    private void ConfigureEntry()
    {
        EnsureCoordinators();
        entry.Configure(
            busStopRayDistance, busResumeRayDistance, busTrafficLaneHalfWidth,
            visualBusSpacingPadding, busTrafficRayLayer, busMoveSpeed,
            splineEntryRotationDuration, splineRayActivationZ, splineEntryTurnRadius, splineCruiseSpeed);
    }

    private IBusPath GetPathSampler(int index) => pathCache.Get(splineContainer, index);

    public bool IsBoosterPaused { get; private set; }
    public void SetBoosterPaused(bool paused) => IsBoosterPaused = paused;

    private void Update()
    {
        if (IsBoosterPaused) return;
        ConfigureEntry();
        entry.TickApproaches();
        motor.Tick(
            GetPathSampler(0), entry, Time.deltaTime, splineCruiseSpeed,
            visualBusSpacingPadding, useSplineProgressSpacing,
            useBlockedProgressPoints, blockedProgressPoints);
        exit.Tick(
            GetPathSampler(exitSplineIndex), Time.deltaTime,
            exitTransferMovementCurveX,
            exitTransferMovementCurveY, exitTransferRotationCurve,
            exitTransferScaleCurve);
    }

    public void BindLevel(LevelControl level) => levelControl = level;

    public SplineContainer GetLevelPath() => splineContainer;

    public float ConvertImportedStopProgress(float sourceProgress)
    {
        var path = GetPathSampler(0) as UnitySplinePath;
        return path == null ? float.NaN : path.ConvertImportedProgress(sourceProgress, importedMainCurveCount);
    }

    public float ProjectStopProgress(Vector3 position)
    {
        IBusPath path = GetPathSampler(0);
        return path == null || path.Length <= Mathf.Epsilon ? float.NaN : path.GetClosestProgress(position);
    }

    public bool TryGetBusStopDistance(BusController bus, float stopProgress, out float distance)
    {
        distance = float.PositiveInfinity;
        if (bus == null || !bus.IsOnSplineConveyor || bus.isExit ||
            !motor.TryGet(bus, out SplineBusRuntime runtime) || runtime.IsEntering)
            return false;
        IBusPath path = GetPathSampler(0);
        if (path == null || splineContainer == null || splineContainer.Splines.Count == 0)
            return false;
        distance = BusStopProximity.GetDistance(runtime.Progress, stopProgress,
            path.Length, splineContainer.Splines[0].Closed);
        return !float.IsInfinity(distance);
    }

    public bool ApplyLevelPathSpline(Spline levelSpline)
    {
        if (splineContainer == null || levelSpline == null || levelSpline.Count < 2)
            return false;

        importedMainCurveCount = levelSpline.Closed ? levelSpline.Count : levelSpline.Count - 1;

        var splines = new List<Spline>(splineContainer.Splines);
        if (splines.Count == 0)
            splines.Add(levelSpline);
        else
            splines[0] = levelSpline;

        splineContainer.Splines = splines;
        pathCache.Clear();
        EnsureCoordinators();
        exit.InvalidateConnection();
        return true;
    }

    public void ApplyGameplaySplines(List<Spline> gameplaySplines)
    {
        splineContainer.Splines = gameplaySplines;
        pathCache.Clear();
        EnsureCoordinators();
        exit.InvalidateConnection();
    }

    // Retains the existing SpawnController API; buses are conveyor items now.
    public void Setup()
    {
        EnsureCoordinators();
        motor.Clear();
        entry.Clear();
        exit.Clear();
    }

    public bool TryMoveDoneBusToExitSpline(BusController bus)
    {
        EnsureCoordinators();
        return exit.TryMoveDoneBus(
            bus, GetPathSampler(0), GetPathSampler(exitSplineIndex),
            splineCruiseSpeed, exitReachedProximity, levelControl);
    }

    public bool TryInsertBusIntoSpline(BusController bus)
    {
        ConfigureEntry();
        return entry.TryInsert(bus, GetPathSampler(0));
    }

    public bool HasBusAhead(
        BusController sourceBus, Vector3 travelDirection, bool useResumeDistance)
    {
        ConfigureEntry();
        return entry.HasBusAhead(sourceBus, travelDirection, useResumeDistance);
    }

    public bool AddStickMan(BusController bus)
    {
        ConfigureEntry();
        return entry.AddStickMan(bus, GetPathSampler(0));
    }

    public bool TryFlyBus(BusController bus)
    {
        EnsureCoordinators();
        IBusPath path = GetPathSampler(0);
        if (bus == null || !bus.IsSelectable || path == null || path.Length <= 0f) return false;
        // Reserve a free landing position before changing the queue or inventory.
        for (int sample = 0; sample < 128; sample++)
        {
            float progress = sample / 128f;
            Vector3 position = path.EvaluatePosition(progress);
            float radius = BusTrafficMath.GetTrafficRadius(bus);
            bool occupied = false;
            foreach (var other in motor.Buses)
            {
                if (other.Bus != null && Vector3.Distance(position, other.Bus.transform.position) <
                    radius + other.TrafficRadius + visualBusSpacingPadding) { occupied = true; break; }
            }
            if (occupied) continue;
            Vector3 flightStart = bus.transform.position;
            bus.PrepareBoosterDeparture();
            motor.Add(new SplineBusRuntime(bus, progress));
            bus.transform.SetParent(null, true);
            bus.SetPositionAndRotationFromSpline(position, path.EvaluateRotation(progress));
            bus.SetOnSplineConveyor(true);
            bus.SetSplineMoving(true);
            bus.PlayBoosterFlight(flightStart);
            return true;
        }
        return false;
    }

}
