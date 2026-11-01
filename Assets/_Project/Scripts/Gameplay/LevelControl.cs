using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Nomnom.RaycastVisualization.Shapes;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Initializes a level and coordinates its gameplay, queue, and win or loss flow.</summary>
public class LevelControl : MonoBehaviour
{
    public static LevelControl Instance { get; private set; }
   
    public ColorData colorData;
    
    public LevelData resource;

    [SerializeField] private SpawnController spawnController;
    [SerializeField] private ManagerSpline splineControlls;
    [SerializeField] private RoadTheme roadTheme = RoadTheme.Town;
    [SerializeField] private Sprite townEnvironmentSprite;
   
    [SerializeField, Min(0.1f)] private float queuedBusShiftSpeed = 4f;
    [SerializeField, Min(0.05f)] private float queuedBusMinimumShiftDuration = 0.18f;
    [SerializeField, Min(0.1f)] private float queuedBusMinimumClearDistance = 1.5f;
    [SerializeField, Min(0f)] private float queuedBusFollowStartDelay = 0.10f;
    [SerializeField, Min(0.05f), Tooltip("Full visible width of the rendered spline road, in world units.")]
    private float splineRoadWidth = 0.28f;
    [SerializeField, Min(0f), Tooltip("Extra width added to each side of the bus parking bay, in world units.")]
    private float busParkingBayExtraWidthPerSide = 0.12f;
    [SerializeField, Tooltip("Generate the lower bus parking apron connected to the U road.")]
    private bool renderBusParkingBay = true;
    [SerializeField, Tooltip("Visual/gameplay road plane scale. X widens the road layout; Y extends it vertically. Leaves bus model size and grid spacing unchanged.")]
    private Vector2 roadPlaneScale = Vector2.one;
    [SerializeField, Range(0f, 0.18f), Tooltip("Width of the grassy divider between the bus bay and exit lane, as a fraction of the lower apron width. Reduce this to narrow only the divider.")]
    private float busExitGrassDividerWidth = 0.09f;


    //public Action busDone;

    public Action isWin;

    public Action isLose;

    private bool winSequenceStarted;
    private bool loseSequenceStarted;
    private GameplayPathProjector pathProjector;
    private GameObject generatedRoad;
    private TownRoadSurfaceLayout townRoadLayout;

    public bool IsGameOver => winSequenceStarted || loseSequenceStarted;
    public bool UsesSourceTownLayout => townRoadLayout != null;

    public Action onAdd;

    public Action onBusFly;
    public Action onBusHammer;

    public PanelSlot GetPanelSlot()
    {
        return spawnController.PanelSlot;
    }

    public void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        InitLevel(() =>
        {
            spawnController.SetUpLevel(this, FitParkingBayToSpawnedBuses, resource);
        });
    }

    private void Update()
    {
        if (IsGameOver)
        {
            return;
        }

        if (spawnController == null || spawnController.listBuses == null)
        {
            return;
        }

        // A bus clicked while its predecessor is blocking remains pending. Recheck
        // it every frame so it starts without requiring a second click.
        for (int i = 0; i < spawnController.listBuses.Count; i++)
        {
            spawnController.listBuses[i]?.TryExecutePendingMove();
        }

        ResolveNearbyBusWaitTransfers();
        CheckLose();
    }

private void InitLevel(Action OnComPlate)
{
    int currentLevel = Mathf.Max(1, GameManager.Instance.CurrentLevel);
    TextAsset jsonFile = Resources.Load<TextAsset>("Levels/Level_" + currentLevel);
    if (jsonFile == null)
    {
        jsonFile = Resources.Load<TextAsset>("Levels/Level" + currentLevel);
    }

    if (jsonFile == null)
    {
        Debug.LogError("Khong tim thay Levels/Level_" + currentLevel);
        resource = new LevelData();
        return;
    }

    try
    {
        resource = LevelData.FromJson(jsonFile.text);
    }
    catch (JsonException ex)
    {
        Debug.LogError("Level_" + currentLevel + " sai format: " + ex.Message);
        resource = new LevelData();
        return;
    }

    if (resource == null)
    {
        Debug.LogError("Khong doc duoc Level_" + currentLevel);
        resource = new LevelData();
        return;
    }

    resource.levelId = currentLevel;
    splineControlls.SetUpSpline(0);
    GetSplineControll()?.BindLevel(this);
    spawnController = Instantiate(spawnController, Vector3.zero, Quaternion.identity, transform);
    if (!TryApplyMyGameLevelPath(resource))
    {
        return;
    }
    Debug.Log("Level_" + currentLevel + " loaded successfully.");
    townRoadLayout?.ConfigureStaticFill(spawnController.transform.Find("BackGround"));
    GetPanelSlot().Init();
    OnComPlate?.Invoke();
}
    
   

    public SplineControll GetSplineControll()
    {
        return splineControlls.GetSpline();
    }

    public bool CheckIsMove(BusController bus)
    {
        if (bus == null)
            return false;

        if (GetPanelSlot().GetSlot <= 0)
            return false;

        // ToolLevel maps column to the Z queue direction and row to the X lane.
        // Column 0 is the front position of each lane.
        if (bus.i == 0)
            return true;

        foreach (BusController otherBus in spawnController.listBuses)
        {
            if (otherBus == null || otherBus == bus)
                continue;

            // A following bus can leave only after the bus in front has actually
            // vacated its queue slot. Clicking alone must not release the next row.
            if (BusQueueMoveRules.IsBlockedByBusAhead(
                    bus.i,
                    bus.j,
                    otherBus.i,
                    otherBus.j,
                    otherBus.isOut))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Opens the queue gap when a bus leaves the grid. Each following bus takes
    /// both the visual position and logical column of the bus in front of it:
    /// (1, 0) -> (0, 0), (2, 0) -> (1, 0), and so on.
    /// </summary>
    public void ShiftQueuedBuses(BusController departingBus)
    {
        if (departingBus == null || spawnController == null ||
            spawnController.listBuses == null)
        {
            return;
        }
        var coordinator = new BusQueueShiftCoordinator(
            queuedBusShiftSpeed,
            queuedBusMinimumShiftDuration,
            queuedBusMinimumClearDistance,
            queuedBusFollowStartDelay);
        StartCoroutine(coordinator.ShiftAfterDeparture(
            departingBus,
            spawnController.listBuses));
    }

    public List<BusController> GetListBus()
    {
        return spawnController.listBuses;
    }

    public void CheckWin()
    {
        if (IsGameOver)
        return;

        List<BusController> buses = GetListBus();
        for (int i = 0; i < buses.Count; i++)
        {
            if (!buses[i].isExit)
                return;
        }
        
        winSequenceStarted = true;
        StartCoroutine(ShowWinPopupNextFrame());
    }

    private IEnumerator ShowWinPopupNextFrame()
    {
        // The win state is already confirmed on ExitBus contact. Defer only
        // the UI creation so it does not compete with that collision frame.
        yield return null;
        isWin?.Invoke();
        UIManager.Instance.ShowPopup<UIWin>(null);
    }

    /// <summary>
    /// Equivalent of MyGame's out-of-space gate for this project. The tray must
    /// be full or have no dock buses remaining, with no completion, movement,
    /// or board-wide passenger exchange still able to progress the level.
    /// </summary>
    public void CheckLose()
    {
        if (IsGameOver || spawnController == null || spawnController.PanelSlot == null ||
            spawnController.listBuses == null)
        {
            return;
        }

        int occupiedTrayBusCount = 0;
        bool hasBusResolving = false;
        bool hasCompletedBus = false;
        bool hasDockBuses = false;
        List<BusController> trayBuses = new List<BusController>();

        for (int i = 0; i < spawnController.listBuses.Count; i++)
        {
            BusController bus = spawnController.listBuses[i];
            if (bus == null || !bus.gameObject.activeInHierarchy || bus.isExit)
                continue;
            if (!bus.IsOnSplineConveyor)
            {
                hasDockBuses = true;
                continue;
            }

            occupiedTrayBusCount++;
            trayBuses.Add(bus);
            hasBusResolving |= bus.isMove || bus.HasActiveSplineApproach ||
                                bus.IsReceivingPassengers;
            hasCompletedBus |= bus.isDone;
        }

        bool hasPassengerTransferOpportunity = HasPassengerTransferOpportunity(trayBuses);
        if (!TrayLossRule.ShouldLose(
                PanelSlot.Capacity,
                occupiedTrayBusCount,
                winSequenceStarted,
                loseSequenceStarted,
                hasBusResolving,
                hasCompletedBus,
                hasPassengerTransferOpportunity,
                hasDockBuses))
        {
            return;
        }

        loseSequenceStarted = true;
        Debug.LogWarning("Level failed: no board entry or passenger exchange can progress the remaining buses.", this);
        isLose?.Invoke();
    }

    private bool HasPassengerTransferOpportunity(List<BusController> trayBuses)
    {
        List<WaitController> waits = spawnController.GetWaitControllers();
        if (waits == null)
        {
            return false;
        }

        for (int waitIndex = 0; waitIndex < waits.Count; waitIndex++)
        {
            WaitController wait = waits[waitIndex];
            if (wait == null || !wait.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (wait.IsTransferringPassengers)
            {
                return true;
            }

            for (int busIndex = 0; busIndex < trayBuses.Count; busIndex++)
            {
                BusController bus = trayBuses[busIndex];
                // GameUtilities.CheckAnyBoardBusToStopTransferPossible checks
                // every board-bus/stop pair, without the current proximity gate.
                // Driving around to a matching stop is still a valid next move.
                if (bus != null && wait.CanInteractWith(bus))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Resolves one transfer per stable bus using the nearest wait in MyGame's
    /// conveyor stop-proximity model. Keeping this in one pass prevents two Wait
    /// components from racing each other in Update and consuming the same bus.
    /// </summary>
    private void ResolveNearbyBusWaitTransfers()
    {
        if (spawnController == null || spawnController.listBuses == null)
        {
            return;
        }

        List<WaitController> waits = spawnController.GetWaitControllers();
        if (waits == null || waits.Count == 0)
        {
            return;
        }

        for (int busIndex = 0; busIndex < spawnController.listBuses.Count; busIndex++)
        {
            BusController bus = spawnController.listBuses[busIndex];
            if (bus == null || bus.isExit || bus.isMove ||
                bus.IsReceivingPassengers || !bus.IsOnSplineConveyor)
            {
                continue;
            }

            WaitController nearestWait = null;
            float nearestDistance = float.MaxValue;
            for (int waitIndex = 0; waitIndex < waits.Count; waitIndex++)
            {
                WaitController wait = waits[waitIndex];
                if (wait == null || !wait.CanInteractWithBusByDistance(bus))
                {
                    continue;
                }

                if (!wait.TryGetBusTransferDistance(bus, out float distance)) continue;
                if (distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearestWait = wait;
            }

            nearestWait?.AddStickMan(bus);
        }
    }



private bool TryApplyMyGameLevelPath(LevelData levelData)
{
    if (splineControlls == null || splineControlls.GetSpline() == null)
    {
        Debug.LogError("ManagerSpline has no active SplineControll for the current level.", this);
        return false;
    }

    if (!MyGameLevelPathPort.TryCreateSpline(
            levelData,
            out var mainSpline,
            out var knotSizes,
            out string error))
    {
        Debug.LogError("Cannot reconstruct MyGame Conveyor LevelPath: " + error, this);
        return false;
    }
        
        SplineControll splineController = splineControlls.GetSpline();
    if (!splineController.ApplyLevelPathSpline(mainSpline))
    {
        Debug.LogError("Failed to assign the MyGame Conveyor spline to SplineControl index 0.", this);
        return false;
    }

    UnityEngine.Splines.SplineContainer levelPath = splineController.GetLevelPath();
    if (levelPath == null)
    {
        Debug.LogError("SplineControll has no SplineContainer after applying the level path.", this);
        return false;
    }

    // Gameplay coordinates must not inherit camera movement during framing.
    if (roadTheme == RoadTheme.Town) levelPath.transform.SetParent(transform, true);
    float gameplayPlaneY = levelData.buses != null && levelData.buses.Count > 0
        ? levelData.buses[0].posY : 2f;
    if (roadTheme == RoadTheme.Town)
    {
        Transform fill = spawnController.transform.Find("BackGround/Road_InteriorFill");
        if (fill != null) gameplayPlaneY = fill.position.y;
    }
    string renderMessage;
    if (!DreamteckSplineRoadRenderer.TryBuild(
            levelPath,
            levelPath.transform,
            "RoadGenerated_MyGame",
            false,
            0,
            roadTheme,
            roadTheme == RoadTheme.Town ? Vector2.one : roadPlaneScale,
            splineRoadWidth,
            busParkingBayExtraWidthPerSide,
            busExitGrassDividerWidth,
            roadTheme != RoadTheme.Town && renderBusParkingBay,
            out generatedRoad,
            out renderMessage))
    {
        Debug.LogError("MyGame LevelPath was applied but its 3D road mesh failed: " + renderMessage, this);
        return false;
    }

    if (roadTheme == RoadTheme.Town)
    {
        townRoadLayout = LevelTownPresentation.Apply(levelPath, generatedRoad, townEnvironmentSprite,
            spawnController != null ? spawnController.transform.Find("BackGround") : null,
            knotSizes, gameplayPlaneY);
    }
    else
    {
        townRoadLayout = null;
        LevelTownPresentation.Hide(levelPath);
    }

    if (roadTheme == RoadTheme.Town)
    {
        if (townRoadLayout == null)
        {
            Debug.LogError("Town layout requires the authored BackGround and road profile.", this);
            return false;
        }
        Transform board = spawnController.transform.Find("BackGround");
        var paths = new List<Spline>(levelPath.Splines);
        paths[0] = townRoadLayout.Path.BuildMergedGameplaySpline(mainSpline, levelPath.transform);
        Spline exitPath = TownLevelPath.BuildFixedExitSpline(board, levelPath.transform, gameplayPlaneY);
        if (paths.Count < 2) paths.Add(exitPath);
        else paths[1] = exitPath;
        splineController.ApplyGameplaySplines(paths);
        splineController.ConfigureImportedTownExit(townRoadLayout.Path.ExitReachedDistance);
        pathProjector = null;
        Debug.Log("Loaded source Town coordinates and merged static conveyor for level " + levelData.levelId, this);
        return true;
    }

    // The generated road is a camera-facing visual. Its enlarged and offset
    // mesh is not in the same world coordinates as the buses. Project the
    // visual path onto the buses' Y plane before gameplay starts.
    Transform roadPlane = generatedRoad.transform.Find("MyGameSplineMesh_Road_0");
    if (roadPlane == null || Camera.main == null)
    {
        Debug.LogError("Cannot align gameplay path: road plane or camera is missing.", this);
        return false;
    }

    pathProjector = new GameplayPathProjector(
        levelPath, roadPlane, Camera.main, roadPlaneScale, gameplayPlaneY);
    if (!pathProjector.ProjectGameplaySplines(splineController))
    {
        Debug.LogError("Cannot align gameplay path with the visible road.", this);
        return false;
    }
        
        Debug.Log(
        "Rebuilt MyGame LevelPath for level " + levelData.levelId +
        " from " + mainSpline.Count + " knots (" + knotSizes.Count +
        " source sizes). " + renderMessage,
        this);
    return true;
}

public Vector3 ProjectImportedStopPosition(Vector3 sourcePosition, float splineProgress)
{
    if (townRoadLayout != null) return townRoadLayout.Path.ToGameplayStop(sourcePosition);
    return pathProjector == null
        ? sourcePosition
        : pathProjector.ProjectImportedStopPosition(sourcePosition);
}

private void FitParkingBayToSpawnedBuses()
{
    if (townRoadLayout != null && spawnController != null)
    {
        townRoadLayout.FitBusesToParkingBay(spawnController.listBuses);
        // Preserve the camera projection and framing configured in the scene.
        return;
    }
    if (!renderBusParkingBay || generatedRoad == null || spawnController == null)
    {
        return;
    }

    if (!DreamteckSplineRoadRenderer.FitParkingBayToBuses(
            generatedRoad, GetSplineControll().GetLevelPath(),
            spawnController.listBuses, Camera.main,
            busParkingBayExtraWidthPerSide, busExitGrassDividerWidth,
            out DreamteckSplineRoadRenderer.ParkingBayAlignment alignment,
            out string error))
    {
        Debug.LogWarning("Could not fit the lower road to the spawned buses: " + error, this);
        return;
    }

    if (alignment.hasExitLane)
    {
        SplineControll controller = GetSplineControll();
        SplineContainer path = controller.GetLevelPath();
        Vector3 localOffset = path.transform.InverseTransformVector(alignment.exitLaneOffset);
        var splines = new List<Spline>(path.Splines);
        for (int index = 1; index < splines.Count; index++)
        {
            var shifted = new Spline(splines[index]);
            for (int knotIndex = 0; knotIndex < shifted.Count; knotIndex++)
            {
                var knot = shifted[knotIndex];
                knot.Position += new Unity.Mathematics.float3(localOffset.x, localOffset.y, localOffset.z);
                shifted.SetKnotNoNotify(knotIndex, knot);
            }
            splines[index] = shifted;
        }
        controller.ApplyGameplaySplines(splines);
        GameExit gate = GetComponentInChildren<GameExit>();
        if (gate != null) gate.transform.position = alignment.gatePosition;
    }
}

public void AlignImportedWaitToGround(WaitController wait)
{
    if (townRoadLayout != null && wait != null)
    {
        wait.PlaceOnGround(townRoadLayout.GrassHeight);
        wait.SetImportedExchangeDistance(townRoadLayout.Path.ExchangeDistance);
    }
}

public Vector3 GetBusSpawnPosition(BusData busData)
{
    if (busData == null)
    {
        return Vector3.zero;
    }

    // Keep every bus at its authored grid coordinate. Individual road-clearance
    // pushes can skew adjacent default-position buses and make them overlap;
    // the whole grid is offset once through SpawnController's Bus parent.
    return new Vector3(busData.posX, townRoadLayout != null ? townRoadLayout.Path.GameplayY : busData.posY, busData.posZ);
}

}

/// <summary>
/// Pure queue gate used by both the runtime check and editor tests. A bus in a
/// later column is blocked only by a non-exited bus in the same lane and an
/// earlier column.
/// </summary>
public static class BusQueueMoveRules
{
    public static bool IsBlockedByBusAhead(
        int candidateColumn,
        int candidateRow,
        int otherColumn,
        int otherRow,
        bool otherBusIsOut)
    {
        return candidateColumn > 0 &&
               otherRow == candidateRow &&
               otherColumn < candidateColumn &&
               !otherBusIsOut;
    }
}

/// <summary>
/// Pure out-of-space rule. A full tray or exhausted dock is a loss only
/// when no bus is resolving and no waiting passenger can progress the board.
/// </summary>
public static class TrayLossRule
{
    public static bool ShouldLose(
        int trayCapacity,
        int occupiedTrayBusCount,
        bool winSequenceStarted,
        bool lossSequenceStarted,
        bool hasBusResolving,
        bool hasCompletedBus,
        bool hasPassengerTransferOpportunity,
        bool hasDockBuses = true
    )
    {
        // BusDataNew IsOutOfSpace: full board OR no remaining dock/extra entry,
        // then reject the loss while completion or an exchange is possible.
        return trayCapacity > 0 && occupiedTrayBusCount > 0 &&
               (occupiedTrayBusCount >= trayCapacity || !hasDockBuses) &&
               !winSequenceStarted &&
               !lossSequenceStarted &&
               !hasBusResolving &&
               !hasCompletedBus &&
               !hasPassengerTransferOpportunity;
    }
}
