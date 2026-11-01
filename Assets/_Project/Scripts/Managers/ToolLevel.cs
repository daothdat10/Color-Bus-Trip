#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using Newtonsoft.Json;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using UnityEngine.UI;

public class ToolLevel : MonoBehaviour
{
	private enum ColorPopupTarget
	{
		Bus,
		WaitingPoint
	}

	public enum RoadSurfaceMode
	{
		XY,
		XZ,
		SplineUp
	}

	[System.Serializable]
	private class GameplaySplineRestoreData
	{
		public List<GameplaySplineRestoreSegment> splines = new List<GameplaySplineRestoreSegment>();

		public int activeSplineIndex;

		public bool renderRoad;
	}

	[System.Serializable]
	private class GameplaySplineRestoreSegment
	{
		public List<Vector3> points = new List<Vector3>();
	}

	private static ToolLevel activeInstance;
	private Scene editorWorkspaceScene;


	[Header("UI")]
	public Transform btnIconRoot;

	public Button renderRoadButton;

	public Button gameplayDrawSplineButton;

	public Text roadMessageText;

	public TMP_Text roadMessageTmpText;

	[Header("Spline Road References")]
	public SplineContainer splineControl;

	private Mesh roadPiece;

	private Material groundMaterial;

	private Material roadMaterial;

	[SerializeField] private RoadTheme roadTheme = RoadTheme.Town;

	public Transform roadRoot;

	public Camera gameplayDrawCamera;

	private GameObject splinePointMarkerPrefab;

	private Material splinePointMarkerMaterial;

	public Transform splinePointMarkerRoot;

	[Header("Spline Road Settings")]
	public string generatedRoadName = "RoadGenerated_3D";[Min(0f)]
	public int splineIndex;

	public bool renderAllSplines = true;

	public RoadSurfaceMode roadSurfaceMode = RoadSurfaceMode.XZ;

	[Min(0.01f)]
	public float roadPieceLength = 0.35f;

	[Min(0.01f)]
	public float targetRoadWidth = 1.45f;

	[Tooltip("Vertical offset used to place the extruded road over the Tool background layout.")]
	public float roadVisualYOffset = 4.5f;



	public bool clearPreviousRoad = true;

	public int roadSortingOrder = 50;

	[Min(1f)]
	public int maxRoadPieces = 512;

	[Header("Gameplay Spline Draw")]
	public bool gameplaySplineDrawMode;

	public bool renderGameplaySplineWhileDrawing = true;

	public bool persistGameplaySplineAfterPlay = true;

	public bool showGameplaySplinePoints = true;

	[Min(0.01f)]
	public float minGameplayPointDistance = 0.15f;

	[Min(0.01f)]
	public float splinePointMarkerSize = 0.22f;

	[Min(0f)]
	public float splinePointMarkerOffset = 0.04f;

	public Color splinePointMarkerColor = new Color(1f, 0.12f, 0.12f, 1f);

	public Color activeSplinePointMarkerColor = new Color(1f, 0.86f, 0.12f, 1f);

	public string splinePointMarkerRootName = "GameplaySplinePoints";

	public int splinePointMarkerSortingOrder = 120;

	[Header("Generated")]
	public GameObject generatedRoadObject;

	public string lastRoadRenderMessage;

	public int gameplayActiveSplineIndex = -1;

	private bool gameplaySplineEditedInPlay;

	private readonly List<GameObject> gameplaySplinePointMarkers = new List<GameObject>();

	private Material runtimeSplinePointMarkerMaterial;

	private const string GameplaySplineRestoreKey = "ToolLevel.GameplaySplineRestore";
	private const string RoadPieceAssetPath = "Assets/_Project/Imported/ColorBusTripWinterRoad/testRoad15.001.asset";
	private const string GridPrefabAssetPath = "Assets/_Project/Prefabs/GRid.prefab";
	private const string BusPrefabAssetPath = "Assets/_Project/Prefabs/Car.prefab";
	private const string WaitingSignPrefabAssetPath = "Assets/_Project/Prefabs/bienBao.prefab";
	private const string StickManPrefabAssetPath = "Assets/_Project/Prefabs/Stickman.prefab";
	private const string ColorDataAssetPath = "Assets/_Project/Data/Color/Color.asset";


	[Header("Grid")]
	[SerializeField]
	private TMP_InputField widthField;

	[SerializeField]
	private TMP_InputField heightField;

	private GridEdit prefabGrid;

	[SerializeField]
	private Transform parentGrid;

	[SerializeField]
	private Camera gridRaycastCamera;

	[SerializeField]
	private LayerMask gridRaycastMask = Physics.DefaultRaycastLayers;

	[SerializeField]
	[Min(0.1f)]
	private float gridRaycastMaxDistance = 500f;

	[SerializeField]
	[Min(0f)]
	private float gridRaycastBoundsPadding = 0.05f;

	[Header("Bus")]
	private BusEdit prefabBus;

	[SerializeField]
	private Transform parentBus;

	[SerializeField]
	private RectTransform rectPopUpBus;

	public bool toggleSpawnBus;

	[SerializeField]
	private Image imgBtnColor;

	[SerializeField]
	private RectTransform rectPopupColor;

	[SerializeField]
	private RectTransform rectPopupWait;

	[SerializeField]
	private Image imgWaitBtnColor;

	[SerializeField]
	private Button waitColorButton;

	[SerializeField]
	private Button saveWaitingPointButton;

	private WaitingPointEdit selectedWaitingPoint;


	[SerializeField]
	private InputField tmpInputSlot;

	[SerializeField]
	[Min(0f)]
	private int draftBusColorIndex;

	[SerializeField]
	[Min(0f)]
	private int savedBusColorIndex;

	[SerializeField]
	[Min(0f)]
	private int draftWaitingPointColorIndex;

	[SerializeField]
	[Min(0f)]
	private int savedWaitingPointColorIndex;

	[SerializeField]
	private ColorPopupTarget colorPopupTarget;

	[SerializeField]
	[Min(0f)]
	private int savedSlotPerson;

	private GridEdit selectedBusGrid;

	private BusEdit busEditObject;

	[SerializeField]
	private Button btnDeleteStickMan;

	[SerializeField]
	private bool deleteStickManMode;

	private Color deleteStickManButtonDefaultColor = Color.white;
	private bool hasDeleteStickManButtonDefaultColor;

	[Header("Waiting Point Tool")]
	private GameObject waitingSignPrefab;

	[SerializeField]
	private Transform waitingPointRoot;

	[SerializeField]
	private Transform waitingCandidateRoot;

	private ColorData waitingPointColorData;

	private const string WaitingStickManNamePrefix = "WaitingStickman_";
	private const float WaitingStickManScale = 0.5f;

	[SerializeField]
	[Min(0.1f)]
	private float waitingPointSpacing = 1f;

	[SerializeField]
	[Min(0.01f)]
	private float waitingCandidateMarkerSize = 0.18f;

	[SerializeField]
	private Color waitingAreaMarkerColor = new Color(1f, 0.12f, 1f, 1f);

	[SerializeField]
	private Color waitingCandidateMarkerColor = new Color(0.1f, 0.95f, 1f, 1f);

	[SerializeField]
	private Button drawWaitingAreaButton;

	[SerializeField]
	private Button renderWaitingPointsButton;

	[SerializeField]
	private Button spawnWaitingPointButton;

	[SerializeField]
	private Button saveLevelButton;

	[SerializeField]
	private Button loadLevelButton;

	[SerializeField]
	private InputField levelInputField;

	[SerializeField]
	private TMP_InputField levelTmpInputField;

	[SerializeField]
	[Min(1)]
	private int currentLevelId = 1;

	[SerializeField]
	private bool waitingAreaDrawMode;

	[SerializeField]
	private bool waitingPointSpawnMode;

	[SerializeField, HideInInspector]
	private List<Vector3> waitingAreaPoints = new List<Vector3>();
	private readonly List<GameObject> waitingAreaMarkers = new List<GameObject>();
	private readonly List<GameObject> waitingCandidateMarkers = new List<GameObject>();

	private const string LevelResourceFolder = "Levels";
	private const string LevelAssetFolder = "Assets/_Project/Resources/Levels";
	private const string WaitingPointRootName = "WaitingPoints";
	private const string WaitingCandidateRootName = "WaitingPointCandidates";
	private const string WaitingAreaMarkerRootName = "WaitingAreaMarkers";
	// Grid is a parking board below the road, centered in the lower gameplay area.
	private const float GridSpawnY = 2f;
	private const float GridColumnSpacingX = 1f;
	// Start below the road exit; later parking rows may continue below the viewport.
	private const float FirstGridRowZ = -8.6f;
	// 2.32 world units projects to the same visible gap as one X column.
	private const float GridRowSpacingZ = 2.32f;



    public static ToolLevel ActiveInstance
	{
		get
		{
			if ((Object)(object)activeInstance == (Object)null)
			{
				activeInstance = Object.FindObjectOfType<ToolLevel>();
			}
			return activeInstance;
		}
	}

	[InitializeOnLoadMethod]
	private static void RegisterGameplaySplineRestoreCallbacks()
	{
		EditorApplication.playModeStateChanged -= HandleGameplaySplinePlayModeStateChanged;
		EditorApplication.playModeStateChanged += HandleGameplaySplinePlayModeStateChanged;
	}

	private static void HandleGameplaySplinePlayModeStateChanged(PlayModeStateChange state)
	{
		if ((int)state == 3)
		{
			CaptureGameplaySplinesBeforeLeavingPlayMode();
		}
		else if ((int)state == 0)
		{
			RestoreGameplaySplinesAfterPlayMode();
		}
	}

	private static void CaptureGameplaySplinesBeforeLeavingPlayMode()
	{
		ToolLevel toolLevel = Object.FindObjectOfType<ToolLevel>();
		if (!TryBuildGameplaySplineRestoreData(toolLevel, requireEdited: true, out var gameplaySplineRestoreData))
		{
			EditorPrefs.DeleteKey(GameplaySplineRestoreKey);
			return;
		}

		EditorPrefs.SetString(GameplaySplineRestoreKey, JsonUtility.ToJson((object)gameplaySplineRestoreData));
	}

	private static bool TryBuildGameplaySplineRestoreData(ToolLevel toolLevel, bool requireEdited, out GameplaySplineRestoreData gameplaySplineRestoreData)
	{
		gameplaySplineRestoreData = null;
		if ((Object)(object)toolLevel == (Object)null || !toolLevel.persistGameplaySplineAfterPlay || (requireEdited && !toolLevel.gameplaySplineEditedInPlay) || (Object)(object)toolLevel.splineControl == (Object)null || toolLevel.splineControl.Splines == null)
		{
			return false;
		}

		gameplaySplineRestoreData = new GameplaySplineRestoreData
		{
			activeSplineIndex = toolLevel.gameplayActiveSplineIndex,
			renderRoad = toolLevel.renderGameplaySplineWhileDrawing
		};
		for (int i = 0; i < toolLevel.splineControl.Splines.Count; i++)
		{
			Spline spline = toolLevel.splineControl.Splines[i];
			GameplaySplineRestoreSegment gameplaySplineRestoreSegment = new GameplaySplineRestoreSegment();
			for (int j = 0; j < spline.Count; j++)
			{
				gameplaySplineRestoreSegment.points.Add(ToVector3(spline[j].Position));
			}
			gameplaySplineRestoreData.splines.Add(gameplaySplineRestoreSegment);
		}
		if (gameplaySplineRestoreData.splines.Count == 0)
		{
			gameplaySplineRestoreData.splines.Add(new GameplaySplineRestoreSegment());
		}
		return true;
	}

	private static void RestoreGameplaySplinesAfterPlayMode()
	{
		if (!EditorPrefs.HasKey(GameplaySplineRestoreKey))
		{
			return;
		}
		string text = EditorPrefs.GetString(GameplaySplineRestoreKey);
		EditorPrefs.DeleteKey(GameplaySplineRestoreKey);
		GameplaySplineRestoreData gameplaySplineRestoreData = JsonUtility.FromJson<GameplaySplineRestoreData>(text);
		ToolLevel toolLevel = Object.FindObjectOfType<ToolLevel>(true);
		if (gameplaySplineRestoreData == null || gameplaySplineRestoreData.splines == null || (Object)(object)toolLevel == (Object)null || (Object)(object)toolLevel.splineControl == (Object)null)
		{
			return;
		}
		List<Spline> list = new List<Spline>();
		for (int i = 0; i < gameplaySplineRestoreData.splines.Count; i++)
		{
			GameplaySplineRestoreSegment gameplaySplineRestoreSegment = gameplaySplineRestoreData.splines[i];
			Spline spline = new Spline();
			if (gameplaySplineRestoreSegment != null && gameplaySplineRestoreSegment.points != null)
			{
				for (int j = 0; j < gameplaySplineRestoreSegment.points.Count; j++)
				{
					Vector3 val = gameplaySplineRestoreSegment.points[j];
					spline.Add(new BezierKnot(new float3(val.x, val.y, val.z)), TangentMode.AutoSmooth);
				}
			}
			ApplyAutoSmooth(spline);
			list.Add(spline);
		}
		if (list.Count == 0)
		{
			list.Add(new Spline());
		}
		toolLevel.splineControl.Splines = list;
		toolLevel.gameplayActiveSplineIndex = Mathf.Clamp(gameplaySplineRestoreData.activeSplineIndex, 0, list.Count - 1);
		toolLevel.gameplaySplineDrawMode = false;
		toolLevel.gameplaySplineEditedInPlay = false;
		toolLevel.renderAllSplines = true;
		toolLevel.MarkSplineDirty();
		if (gameplaySplineRestoreData.renderRoad)
		{
			toolLevel.RenderRoadMesh();
		}
		toolLevel.RefreshGameplaySplinePointMarkers();
	}

	private void Awake()
	{
		activeInstance = this;
	}

	private void OnDestroy()
	{
		if ((Object)(object)activeInstance == (Object)(object)this)
		{
			activeInstance = null;
		}
	}

private void Start()
	{
		InitializeEditorReferences();
		RefreshAllWaitingPointStickManVisuals();
		RefreshGameplaySplinePointMarkers();
		RefreshWaitingAreaMarkers();
	}

	private void Update()
	{
		if (Application.isPlaying)
		{
			if (waitingAreaDrawMode)
			{
				HandleWaitingAreaDrawInput();
			}
			else if (waitingPointSpawnMode && waitingCandidateMarkers.Count > 0 && HandleWaitingPointCandidateInput())
			{
			}
			else if (HandleWaitingPointEditInput())
			{
			}
			else if (toggleSpawnBus)
			{
				HandleGridRaycastInput();
			}
			else if (gameplaySplineDrawMode)
			{
				HandleGameplaySplineDrawInput();
			}
		}
	}

	private void OnValidate()
	{
		splineIndex = Mathf.Max(0, splineIndex);
		roadPieceLength = Mathf.Max(0.01f, roadPieceLength);
		targetRoadWidth = Mathf.Max(0.01f, targetRoadWidth);
		minGameplayPointDistance = Mathf.Max(0.01f, minGameplayPointDistance);
		splinePointMarkerSize = Mathf.Max(0.01f, splinePointMarkerSize);
		splinePointMarkerOffset = Mathf.Max(0f, splinePointMarkerOffset);
		maxRoadPieces = Mathf.Max(1, maxRoadPieces);
		draftBusColorIndex = Mathf.Max(0, draftBusColorIndex);
		savedBusColorIndex = Mathf.Max(0, savedBusColorIndex);
		draftWaitingPointColorIndex = Mathf.Max(-1, draftWaitingPointColorIndex);
		savedWaitingPointColorIndex = Mathf.Max(-1, savedWaitingPointColorIndex);
		savedSlotPerson = Mathf.Clamp(savedSlotPerson, 0, BusEdit.DefaultSlotBus);
		waitingPointSpacing = Mathf.Max(0.1f, waitingPointSpacing);
		waitingCandidateMarkerSize = Mathf.Max(0.01f, waitingCandidateMarkerSize);
		currentLevelId = Mathf.Max(1, currentLevelId);
		if (string.IsNullOrWhiteSpace(generatedRoadName))
		{
			generatedRoadName = "EnvironmentVisual_" + roadTheme;
		}
		if (string.IsNullOrWhiteSpace(splinePointMarkerRootName))
		{
			splinePointMarkerRootName = "GameplaySplinePoints";
		}
	}

	public void ToggleGameplaySplineDraw()
	{
		if (gameplaySplineDrawMode)
		{
			StopGameplaySplineDraw();
		}
		else
		{
			StartGameplaySplineDraw();
		}
	}

	public void StartGameplaySplineDraw()
	{
		SetWaitingAreaDrawMode(false);
		SetWaitingPointSpawnMode(false);
		if ((Object)(object)splineControl == (Object)null)
		{
			SetRoadMessage("Chua keo SplineControl vao ToolLevel");
			return;
		}
		if (splineControl.Splines == null || splineControl.Splines.Count == 0)
		{
			splineControl.Splines = new List<Spline>
			{
				new Spline()
			};
			gameplayActiveSplineIndex = 0;
			MarkGameplaySplineEdited();
		}
		else
		{
			gameplayActiveSplineIndex = Mathf.Clamp((gameplayActiveSplineIndex < 0) ? splineIndex : gameplayActiveSplineIndex, 0, splineControl.Splines.Count - 1);
			MarkSplineDirty();
		}
		gameplaySplineDrawMode = true;
		renderAllSplines = true;
		SetRoadMessage("Dang ve spline: click vao map de them diem");
		RefreshGameplaySplinePointMarkers();
	}

	public void StopGameplaySplineDraw()
	{
		gameplaySplineDrawMode = false;
		SetRoadMessage("Da tat ve spline gameplay");
		MarkSplineDirty();
		RefreshGameplaySplinePointMarkers();
	}

public void RenderRoadMesh()
	{
		EnsureToolAssetReferences();
		if (groundMaterial == null || roadMaterial == null)
		{
			SetRoadMessage("Missing Mat_Ground/Mat_Road pair for " + roadTheme);
			return;
		}
		if (TryRenderMyGameSplineRoad())
		{
			return;
		}
		if (!ValidateRoadInputs(out List<int> safeSplineIndices, out List<float> splineLengths))
		{
			return;
		}

		if (clearPreviousRoad)
		{
			ClearGeneratedRoad();
		}

		Transform roadParent = roadRoot != null ? roadRoot : transform;
		GameObject roadObject = new GameObject(generatedRoadName);
		roadObject.transform.SetParent(roadParent, false);
		roadObject.transform.localPosition = Vector3.zero;
		roadObject.transform.localRotation = Quaternion.identity;
		roadObject.transform.localScale = Vector3.one;

		GameObject visualContainer = new GameObject("VisualContainer");
		visualContainer.transform.SetParent(roadObject.transform, false);

		GameObject groundObject = new GameObject("GroundBase_3D");
		groundObject.transform.SetParent(visualContainer.transform, false);
		Mesh groundMesh = BuildExtrudedRoadMesh(
			safeSplineIndices,
			splineLengths,
			groundObject.transform,
			Mathf.Max(targetRoadWidth + 0.35f, 1.8f),
			0.34f,
			-0.06f,
			0.08f);
		MeshFilter groundFilter = groundObject.AddComponent<MeshFilter>();
		groundFilter.sharedMesh = groundMesh;
		MeshRenderer groundRenderer = groundObject.AddComponent<MeshRenderer>();
		groundRenderer.sharedMaterial = groundMaterial;
		groundRenderer.shadowCastingMode = ShadowCastingMode.On;
		groundRenderer.receiveShadows = true;

		GameObject surfaceObject = new GameObject("RoadSurface_3D");
		surfaceObject.transform.SetParent(visualContainer.transform, false);
		Mesh surfaceMesh = BuildExtrudedRoadMesh(
			safeSplineIndices,
			splineLengths,
			surfaceObject.transform,
			Mathf.Max(targetRoadWidth, 1.45f),
			0.12f,
			0.01f,
			0.06f);
		MeshFilter surfaceFilter = surfaceObject.AddComponent<MeshFilter>();
		surfaceFilter.sharedMesh = surfaceMesh;
		MeshRenderer surfaceRenderer = surfaceObject.AddComponent<MeshRenderer>();
		surfaceRenderer.sharedMaterial = roadMaterial;
		surfaceRenderer.shadowCastingMode = ShadowCastingMode.On;
		surfaceRenderer.receiveShadows = true;
		surfaceRenderer.sortingOrder = roadSortingOrder;

		
		if (!Application.isPlaying && roadObject.scene != SceneManager.GetActiveScene())
		{
			roadObject.transform.SetParent(null, true);
			SceneManager.MoveGameObjectToScene(roadObject, SceneManager.GetActiveScene());
		}
		if (!Application.isPlaying)
		{
			SpriteRenderer[] backgroundSprites = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
			for (int i = 0; i < backgroundSprites.Length; i++)
			{
				SpriteRenderer candidate = backgroundSprites[i];
				if (candidate != null && candidate.sprite != null && candidate.sprite.name == "map1" && candidate.gameObject.scene == SceneManager.GetActiveScene() && candidate.gameObject.activeInHierarchy)
				{
					roadObject.transform.position = candidate.transform.position;
					roadObject.transform.rotation = candidate.transform.rotation;
					roadObject.transform.localScale = Vector3.one;
					break;
				}
			}
		} 
generatedRoadObject = roadObject;
		SetRoadMessage("Da render road extrusion 3D: ground " + groundMesh.vertexCount + " vertices, surface " + surfaceMesh.vertexCount + " vertices");
	}

private bool TryRenderMyGameSplineRoad()
{
    Scene targetScene = SceneManager.GetActiveScene();
    SplineControll gameplayPath = null;

    if (splineControl != null && splineControl.gameObject.scene == targetScene)
    {
        gameplayPath = splineControl.GetComponent<SplineControll>();
    }

    if (gameplayPath == null)
    {
        SplineControll[] candidates = Resources.FindObjectsOfTypeAll<SplineControll>();
        for (int i = 0; i < candidates.Length; i++)
        {
            SplineControll candidate = candidates[i];
            if (candidate != null &&
                candidate.gameObject.scene == targetScene &&
                candidate.gameObject.activeInHierarchy &&
                candidate.GetLevelPath() != null)
            {
                gameplayPath = candidate;
                break;
            }
        }
    }

    if (gameplayPath == null)
    {
        SetRoadMessage("Khong tim thay SplineControll.LevelPath trong scene dang mo");
        return true;
    }

    SplineContainer levelPath = gameplayPath.GetLevelPath();
    if (levelPath == null || levelPath.gameObject.scene != targetScene ||
        levelPath.Splines == null || levelPath.Splines.Count == 0 ||
        levelPath.Splines[0] == null || levelPath.Splines[0].Count < 2)
    {
        SetRoadMessage("LevelPath khong co main spline hop le tai index 0");
        return true;
    }

    Transform outputParent = roadRoot != null && roadRoot.gameObject.scene == targetScene
        ? roadRoot
        : gameplayPath.transform;

    if (clearPreviousRoad)
    {
        ClearGeneratedRoad();
    }

    // MyGame's conveyor uses LevelPath; SplineControll moves gameplay buses on
    // spline 0 and reserves spline 1 for the exit path.
    if (!DreamteckSplineRoadRenderer.TryBuild(
            levelPath,
            outputParent,
            generatedRoadName,
            false,
            0,
            roadTheme,
            out generatedRoadObject,
            out string message))
    {
        SetRoadMessage(message);
        return true;
    }

#if UNITY_EDITOR
    AssetDatabase.SaveAssets();
    EditorSceneManager.MarkSceneDirty(targetScene);
#endif
    SetRoadMessage(message);
    return true;
}

	public void ClearGeneratedRoad()
	{
		if ((Object)(object)generatedRoadObject != (Object)null)
		{
			DestroyRoadObject(generatedRoadObject);
			generatedRoadObject = null;
		}
		Transform val = (((Object)(object)roadRoot != (Object)null) ? roadRoot : ((Component)this).transform);
		for (int num = val.childCount - 1; num >= 0; num--)
		{
			Transform child = val.GetChild(num);
			if ((Object)(object)child != (Object)null && ((Object)child).name == generatedRoadName)
			{
				DestroyRoadObject(((Component)child).gameObject);
			}
		}
	}

	private bool ValidateRoadInputs(out List<int> safeSplineIndices, out List<float> splineLengths)
	{
		safeSplineIndices = new List<int>();
		splineLengths = new List<float>();
		if ((Object)(object)splineControl == (Object)null)
		{
			SetRoadMessage("Chua keo SplineControl vao ToolLevel");
			return false;
		}
		if ((Object)(object)roadPiece == (Object)null)
		{
			SetRoadMessage("Chua keo roadPiece.asset vao ToolLevel");
			return false;
		}
		if (roadPiece.vertexCount == 0)
		{
			SetRoadMessage("roadPiece khong co vertex");
			return false;
		}
		if (splineControl.Splines == null || splineControl.Splines.Count == 0)
		{
			SetRoadMessage("SplineControl khong co spline");
			return false;
		}
		if (renderAllSplines)
		{
			for (int i = 0; i < splineControl.Splines.Count; i++)
			{
				TryAddRenderableSpline(i, safeSplineIndices, splineLengths);
			}
		}
		else
		{
			TryAddRenderableSpline(Mathf.Clamp(splineIndex, 0, splineControl.Splines.Count - 1), safeSplineIndices, splineLengths);
		}
		if (safeSplineIndices.Count == 0)
		{
			SetRoadMessage("Khong co spline hop le de render");
			return false;
		}
		return true;
	}

	private bool TryAddRenderableSpline(int candidateSplineIndex, List<int> safeSplineIndices, List<float> splineLengths)
	{
		Spline spline = splineControl.Splines[candidateSplineIndex];
		if (spline == null || spline.Count < 2)
		{
			return false;
		}
		float num = splineControl.CalculateLength(candidateSplineIndex);
		if (num <= 0.001f)
		{
			return false;
		}
		safeSplineIndices.Add(candidateSplineIndex);
		splineLengths.Add(num);
		return true;
	}
	private void EvaluateRoadFrame(int safeSplineIndex, float distance, float splineLength, out Vector3 center, out Vector3 forward, out Vector3 side, out Vector3 surfaceNormal)
	{
		float t = Mathf.Clamp01(distance / splineLength);
		splineControl.Evaluate(safeSplineIndex, t, out var position, out var tangent, out var upVector);
		center = ToVector3(position);
		forward = ToVector3(tangent);
		surfaceNormal = GetSurfaceNormal(ToVector3(upVector));
		if (roadSurfaceMode == RoadSurfaceMode.XY)
		{
			forward.z = 0f;
		}
		else if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			forward.y = 0f;
		}
		if (forward.sqrMagnitude <= 0.0001f)
		{
			forward = EstimateForward(safeSplineIndex, t, surfaceNormal);
		}
		else
		{
			forward.Normalize();
		}
		side = Vector3.Cross(surfaceNormal, forward);
		if (side.sqrMagnitude <= 0.0001f)
		{
			side = Vector3.Cross(Vector3.forward, forward);
		}
		if (side.sqrMagnitude <= 0.0001f)
		{
			side = Vector3.right;
		}
		side.Normalize();
	}

	private Vector3 EstimateForward(int safeSplineIndex, float t, Vector3 surfaceNormal)
	{
		float t2 = Mathf.Clamp01(t + 0.002f);
		float t3 = Mathf.Clamp01(t - 0.002f);
		Vector3 val = ToVector3(splineControl.EvaluatePosition(safeSplineIndex, t2));
		Vector3 val2 = ToVector3(splineControl.EvaluatePosition(safeSplineIndex, t3));
		Vector3 val3 = val - val2;
		if (roadSurfaceMode == RoadSurfaceMode.XY)
		{
			val3.z = 0f;
		}
		else if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			val3.y = 0f;
		}
		if (val3.sqrMagnitude <= 0.0001f)
		{
			val3 = Vector3.Cross(Vector3.right, surfaceNormal);
		}
		if (!(val3.sqrMagnitude > 0.0001f))
		{
			return Vector3.forward;
		}
		return val3.normalized;
	}

	private Vector3 GetSurfaceNormal(Vector3 splineUp)
	{
		if (roadSurfaceMode == RoadSurfaceMode.XY)
		{
			return Vector3.forward;
		}
		if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			return Vector3.up;
		}
		if (!(splineUp.sqrMagnitude > 0.0001f))
		{
			return Vector3.up;
		}
		return splineUp.normalized;
	}
	private void BindRenderRoadButton()
	{
		if ((Object)(object)btnIconRoot == (Object)null)
		{
			GameObject val = GameObject.Find("BtnIcon");
			if ((Object)(object)val != (Object)null)
			{
				btnIconRoot = val.transform;
			}
		}
		if ((Object)(object)renderRoadButton == (Object)null && (Object)(object)btnIconRoot != (Object)null)
		{
			renderRoadButton = FindDeep<Button>(btnIconRoot, "RenderRoadButton");
		}
		if ((Object)(object)gameplayDrawSplineButton == (Object)null && (Object)(object)btnIconRoot != (Object)null)
		{
			gameplayDrawSplineButton = FindDeep<Button>(btnIconRoot, "GameplayDrawSplineButton");
		}
		if ((Object)(object)roadMessageText == (Object)null && (Object)(object)btnIconRoot != (Object)null)
		{
			roadMessageText = FindDeep<Text>(btnIconRoot, "RoadMessageText");
		}
		if ((Object)(object)roadMessageTmpText == (Object)null && (Object)(object)btnIconRoot != (Object)null)
		{
			roadMessageTmpText = FindDeep<TMP_Text>(btnIconRoot, "RoadMessageText");
		}
		if ((Object)(object)renderRoadButton != (Object)null)
		{
			AddClickListenerIfMissing(renderRoadButton, RenderRoadMesh, nameof(RenderRoadMesh));
		}
		if ((Object)(object)gameplayDrawSplineButton != (Object)null)
		{
			AddClickListenerIfMissing(gameplayDrawSplineButton, ToggleGameplaySplineDraw, nameof(ToggleGameplaySplineDraw));
		}
	}

	private void BindLevelToolUiReferences()
	{
		EnsureWaitingToolReferences();
		if ((Object)(object)rectPopupWait == (Object)null)
		{
			rectPopupWait = FindSceneComponentByName<RectTransform>("PopUpWait");
		}
		if ((Object)(object)rectPopupWait == (Object)null)
		{
			rectPopupWait = FindSceneComponentByName<RectTransform>("PopupWait");
		}
		if ((Object)(object)rectPopupWait == (Object)null)
		{
			rectPopupWait = FindSceneComponentByName<RectTransform>("Wait");
		}
		if ((Object)(object)btnIconRoot == (Object)null)
		{
			GameObject val = GameObject.Find("BtnIcon");
			if ((Object)(object)val != (Object)null)
			{
				btnIconRoot = val.transform;
			}
		}

		if ((Object)(object)btnIconRoot != (Object)null)
		{
			InputField btnIconLevelInput = FindDeep<InputField>(btnIconRoot, "LevelInputField");
			if ((Object)(object)btnIconLevelInput != (Object)null)
			{
				levelInputField = btnIconLevelInput;
			}
			TMP_InputField btnIconLevelTmpInput = FindDeep<TMP_InputField>(btnIconRoot, "LevelInputField");
			if ((Object)(object)btnIconLevelTmpInput != (Object)null)
			{
				levelTmpInputField = btnIconLevelTmpInput;
			}

			Button btnIconSpawnPoint = FindDeep<Button>(btnIconRoot, "SpawnPointButton");
			if ((Object)(object)btnIconSpawnPoint == (Object)null)
			{
				btnIconSpawnPoint = FindDeep<Button>(btnIconRoot, "DrawWaitingAreaButton");
			}
			if ((Object)(object)btnIconSpawnPoint != (Object)null)
			{
				drawWaitingAreaButton = btnIconSpawnPoint;
			}

			Button btnIconRenderWait = FindDeep<Button>(btnIconRoot, "RenderWaitingPointsButton");
			if ((Object)(object)btnIconRenderWait != (Object)null)
			{
				renderWaitingPointsButton = btnIconRenderWait;
			}

			Button btnIconSpawnWait = FindDeep<Button>(btnIconRoot, "SpawnWaitButton");
			if ((Object)(object)btnIconSpawnWait == (Object)null)
			{
				btnIconSpawnWait = FindDeep<Button>(btnIconRoot, "SpawnWait");
			}
			if ((Object)(object)btnIconSpawnWait != (Object)null)
			{
				spawnWaitingPointButton = btnIconSpawnWait;
			}

			Button btnIconSaveLevel = FindDeep<Button>(btnIconRoot, "SaveLevelButton");
			if ((Object)(object)btnIconSaveLevel != (Object)null)
			{
				saveLevelButton = btnIconSaveLevel;
			}

			Button btnIconLoadLevel = FindDeep<Button>(btnIconRoot, "LoadLevelButton");
			if ((Object)(object)btnIconLoadLevel != (Object)null)
			{
				loadLevelButton = btnIconLoadLevel;
			}
		}

		if ((Object)(object)rectPopupWait != (Object)null)
		{
			BindWaitPopupLevelControls();
		}

		if ((Object)(object)levelInputField != (Object)null)
		{
			if (string.IsNullOrWhiteSpace(levelInputField.text))
			{
				levelInputField.text = currentLevelId.ToString();
			}
			levelInputField.contentType = InputField.ContentType.IntegerNumber;
		}
		if ((Object)(object)levelTmpInputField != (Object)null)
		{
			if (string.IsNullOrWhiteSpace(levelTmpInputField.text))
			{
				levelTmpInputField.text = currentLevelId.ToString();
			}
			levelTmpInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
		}
		if ((Object)(object)drawWaitingAreaButton != (Object)null)
		{
			AddClickListenerIfMissing(drawWaitingAreaButton, ToggleWaitingAreaDrawMode, nameof(ToggleWaitingAreaDrawMode));
		}
		if ((Object)(object)renderWaitingPointsButton != (Object)null)
		{
			AddClickListenerIfMissing(renderWaitingPointsButton, RenderWaitingPointCandidates, nameof(RenderWaitingPointCandidates));
		}
		if ((Object)(object)spawnWaitingPointButton != (Object)null)
		{
			AddClickListenerIfMissing(spawnWaitingPointButton, ToggleWaitingPointSpawnMode, nameof(ToggleWaitingPointSpawnMode));
		}
		if ((Object)(object)saveLevelButton != (Object)null)
		{
			AddClickListenerIfMissing(saveLevelButton, SaveLevelJson, nameof(SaveLevelJson));
		}
		if ((Object)(object)loadLevelButton != (Object)null)
		{
			AddClickListenerIfMissing(loadLevelButton, LoadLevelJson, nameof(LoadLevelJson));
		}

		UpdateWaitingAreaDrawButtonVisual();
		UpdateWaitingPointSpawnButtonVisual();
	}

	private void EnsureToolAssetReferences()
	{
		roadPiece = AssetDatabase.LoadAssetAtPath<Mesh>(RoadPieceAssetPath);
		RoadThemeMaterials palette = Resources.Load<RoadThemeMaterials>("RoadThemeMaterials");
		if (palette == null || !palette.TryGet(roadTheme, out groundMaterial, out roadMaterial))
		{
			groundMaterial = null;
			roadMaterial = null;
		}
		prefabGrid = LoadPrefabComponent<GridEdit>(GridPrefabAssetPath);
		prefabBus = LoadPrefabComponent<BusEdit>(BusPrefabAssetPath);
		waitingSignPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaitingSignPrefabAssetPath);
		stickMan = LoadPrefabComponent<StickManEdit>(StickManPrefabAssetPath);
		waitingPointColorData = AssetDatabase.LoadAssetAtPath<ColorData>(ColorDataAssetPath);
	}

	private static T LoadPrefabComponent<T>(string assetPath) where T : Component
	{
		GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
		return prefab == null ? null : prefab.GetComponent<T>();
	}

public void InitializeEditorReferences()
	{
		if (!editorWorkspaceScene.IsValid() && gameObject.scene.IsValid())
		{
			editorWorkspaceScene = gameObject.scene;
		}

		EnsureToolAssetReferences();
		BindRenderRoadButton();
		BindBusUiReferences();
		BindLevelToolUiReferences();
		CacheGridRaycastCamera();
		EnsureWaitingToolReferences();
	}

public void InitializeStandaloneWorkspace(Scene workspaceScene)
	{
		editorWorkspaceScene = workspaceScene;
		EnsureToolAssetReferences();

		splineControl = GetOrCreateWorkspaceComponent<SplineContainer>("SplineControl");
		roadRoot = GetOrCreateWorkspaceRoot("Road");
		gameplayDrawCamera = GetOrCreateWorkspaceComponent<Camera>("Tool Camera");
		gameplayDrawCamera.enabled = false;
		gameplayDrawCamera.orthographic = true;
		gameplayDrawCamera.orthographicSize = 6.34f;
		gameplayDrawCamera.transform.SetPositionAndRotation(
			new Vector3(0f, 22f, -17f),
			Quaternion.Euler(50f, 0f, 0f));
		gameplayDrawCamera.clearFlags = CameraClearFlags.SolidColor;
		gameplayDrawCamera.backgroundColor = new Color(0.78f, 0.89f, 1f, 1f);
		splinePointMarkerRoot = GetOrCreateWorkspaceRoot(splinePointMarkerRootName);
		parentGrid = GetOrCreateWorkspaceRoot("Grid");
		parentBus = GetOrCreateWorkspaceRoot("Bus");

		if (splineControl.Splines == null || splineControl.Splines.Count == 0)
		{
			splineControl.Splines = new List<Spline> { new Spline() };
			gameplayActiveSplineIndex = 0;
		}

		EnsureWaitingToolReferences();
		CacheGridRaycastCamera();
	}

private Transform GetOrCreateWorkspaceRoot(string objectName)
	{
		Transform root = FindSceneTransform(objectName);
		if (root != null)
		{
			return root;
		}

		GameObject rootObject = new GameObject(objectName);
		if (editorWorkspaceScene.IsValid() && rootObject.scene != editorWorkspaceScene)
		{
			SceneManager.MoveGameObjectToScene(rootObject, editorWorkspaceScene);
		}
		return rootObject.transform;
	}

	private T GetOrCreateWorkspaceComponent<T>(string objectName) where T : Component
	{
		Transform root = GetOrCreateWorkspaceRoot(objectName);
		T component = root.GetComponent<T>();
		return component != null ? component : root.gameObject.AddComponent<T>();
	}


private void EnsureWaitingToolReferences()
	{
		if ((Object)(object)waitingSignPrefab == (Object)null)
		{
			waitingSignPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/bienBao.prefab");
		}
		if ((Object)(object)stickMan == (Object)null)
		{
			stickMan = AssetDatabase.LoadAssetAtPath<StickManEdit>("Assets/_Project/Prefabs/Stickman.prefab");
		}
		if ((Object)(object)waitingPointColorData == (Object)null)
		{
			waitingPointColorData = AssetDatabase.LoadAssetAtPath<ColorData>("Assets/_Project/Data/Color/Color.asset");
		}
		if ((Object)(object)waitingPointRoot == (Object)null)
		{
			waitingPointRoot = GetOrCreateSceneRoot(WaitingPointRootName);
		}
		if ((Object)(object)waitingCandidateRoot == (Object)null)
		{
			waitingCandidateRoot = GetOrCreateSceneRoot(WaitingCandidateRootName);
		}
	}

	private void BindWaitPopupLevelControls()
	{
		if ((Object)(object)rectPopupWait == (Object)null)
		{
			return;
		}

		Transform waitRoot = (Transform)(object)rectPopupWait;
		if ((Object)(object)levelInputField == (Object)null)
		{
			levelInputField = FindDeep<InputField>(waitRoot, "LevelInputField");
		}
		if ((Object)(object)levelTmpInputField == (Object)null)
		{
			levelTmpInputField = FindDeep<TMP_InputField>(waitRoot, "LevelInputField");
		}
		if ((Object)(object)levelInputField == (Object)null)
		{
			levelInputField = FindDeepContains<InputField>(waitRoot, "level");
		}
		if ((Object)(object)levelTmpInputField == (Object)null)
		{
			levelTmpInputField = FindDeepContains<TMP_InputField>(waitRoot, "level");
		}
		if ((Object)(object)drawWaitingAreaButton == (Object)null)
		{
			drawWaitingAreaButton = FindButtonByNameOrText(waitRoot, "spawnpoint", "spawn point", "drawwait", "draw wait");
		}
		if ((Object)(object)renderWaitingPointsButton == (Object)null)
		{
			renderWaitingPointsButton = FindButtonByNameOrText(waitRoot, "renderwait", "render wait");
		}
		if ((Object)(object)saveLevelButton == (Object)null)
		{
			saveLevelButton = FindButtonByNameOrText(waitRoot, "savelevel", "save level", "save");
		}
		if ((Object)(object)loadLevelButton == (Object)null)
		{
			loadLevelButton = FindButtonByNameOrText(waitRoot, "loadlevel", "load level", "load");
		}
		if ((Object)(object)waitColorButton == (Object)null)
		{
			waitColorButton = FindButtonByNameOrText(waitRoot, "color");
		}
		if ((Object)(object)waitColorButton != (Object)null)
		{
			imgWaitBtnColor = ((Component)waitColorButton).GetComponent<Image>();
			AddClickListenerIfMissing(waitColorButton, ChoseColorWait, nameof(ChoseColorWait));
		}
		if ((Object)(object)saveWaitingPointButton == (Object)null)
		{
			saveWaitingPointButton = FindButtonByNameOrText(waitRoot, "spawnslot", "save");
		}
		if ((Object)(object)saveWaitingPointButton != (Object)null)
		{
			AddClickListenerIfMissing(saveWaitingPointButton, SaveWaitingPointColor, nameof(SaveWaitingPointColor));
		}
		UpdateWaitingPointColorPreview(null);
	}

	private void HandleGameplaySplineDrawInput()
	{
		if (Input.GetMouseButtonDown(0))
		{
			if (ShouldBlockGameplaySplineInput((Vector2)Input.mousePosition))
			{
				return;
			}
			TryAddGameplaySplinePoint((Vector2)Input.mousePosition);
		}
		if (Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);
			if ((int)touch.phase == 0 && !ShouldBlockGameplaySplineInput(touch.position, touch.fingerId))
			{
				TryAddGameplaySplinePoint(touch.position);
			}
		}
	}

	private bool ShouldBlockGameplaySplineInput(Vector2 screenPosition, int pointerId = -1)
	{
		if ((Object)(object)EventSystem.current == (Object)null)
		{
			return false;
		}
		PointerEventData eventData = new PointerEventData(EventSystem.current)
		{
			position = screenPosition
		};
		List<RaycastResult> list = new List<RaycastResult>();
		EventSystem.current.RaycastAll(eventData, list);
		if (list.Count == 0)
		{
			return false;
		}
		for (int i = 0; i < list.Count; i++)
		{
			GameObject gameObject = list[i].gameObject;
			if (!((Object)(object)gameObject == (Object)null) && IsGameplayUiBlocker(gameObject))
			{
				return true;
			}
		}
		return false;
	}

	private bool IsGameplayUiBlocker(GameObject hitObject)
	{
		if ((Object)(object)hitObject == (Object)null)
		{
			return false;
		}
		if ((Object)(object)rectPopUpBus != (Object)null && hitObject.transform.IsChildOf((Transform)(object)rectPopUpBus))
		{
			return true;
		}
		if ((Object)(object)rectPopupColor != (Object)null && hitObject.transform.IsChildOf((Transform)(object)rectPopupColor))
		{
			return true;
		}
		if ((Object)(object)rectPopupWait != (Object)null && hitObject.transform.IsChildOf((Transform)(object)rectPopupWait))
		{
			return true;
		}
		if (HasBlockingUiControl(hitObject.transform))
		{
			return true;
		}
		return false;
	}

	private bool HasBlockingUiControl(Transform hitTransform)
	{
		while ((Object)(object)hitTransform != (Object)null)
		{
			if ((Object)(object)((Component)hitTransform).GetComponent<Button>() != (Object)null || (Object)(object)((Component)hitTransform).GetComponent<InputField>() != (Object)null || (Object)(object)((Component)hitTransform).GetComponent<TMP_InputField>() != (Object)null)
			{
				return true;
			}
			if ((Object)(object)((Component)hitTransform).GetComponent<Selectable>() != (Object)null || (Object)(object)((Component)hitTransform).GetComponent<ScrollRect>() != (Object)null)
			{
				return true;
			}
			hitTransform = hitTransform.parent;
		}
		return false;
	}

	private bool TryAddGameplaySplinePoint(Vector2 screenPosition)
	{
		if ((Object)(object)splineControl == (Object)null)
		{
			SetRoadMessage("Chua keo SplineControl vao ToolLevel");
			return false;
		}
		Spline gameplayActiveSpline = GetGameplayActiveSpline();
		if (gameplayActiveSpline == null)
		{
			splineControl.Splines = new List<Spline>
			{
				new Spline()
			};
			gameplayActiveSplineIndex = 0;
			MarkGameplaySplineEdited();
			gameplayActiveSpline = GetGameplayActiveSpline();
		}
		if (gameplayActiveSpline == null || !TryScreenToSplineLocalPoint(screenPosition, out var localPoint))
		{
			SetRoadMessage("Khong raycast duoc vao mat phang ve");
			return false;
		}
		if (gameplayActiveSpline.Count > 0 && Vector3.Distance(ToVector3(gameplayActiveSpline[gameplayActiveSpline.Count - 1].Position), localPoint) < minGameplayPointDistance)
		{
			SetRoadMessage("Diem moi qua gan diem cu");
			return false;
		}
		gameplayActiveSpline.Add(new BezierKnot(new float3(localPoint.x, localPoint.y, localPoint.z)), TangentMode.AutoSmooth);
		ApplyAutoSmooth(gameplayActiveSpline);
		MarkGameplaySplineEdited();
		RefreshGameplaySplinePointMarkers();
		if (renderGameplaySplineWhileDrawing && gameplayActiveSpline.Count >= 2)
		{
			RenderRoadMesh();
		}
		SetRoadMessage("Da them diem " + gameplayActiveSpline.Count + " vao spline " + gameplayActiveSplineIndex);
		return true;
	}

	private bool TryScreenToSplineLocalPoint(Vector2 screenPosition, out Vector3 localPoint)
	{
		localPoint = Vector3.zero;
		Camera val = (((Object)(object)gameplayDrawCamera != (Object)null) ? gameplayDrawCamera : Camera.main);
		if ((Object)(object)val == (Object)null)
		{
			val = Object.FindObjectOfType<Camera>();
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		Plane gameplayDrawPlane = GetGameplayDrawPlane();
		Ray val2 = val.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
		float num = default(float);
		if (!gameplayDrawPlane.Raycast(val2, out num))
		{
			return false;
		}
		Vector3 point = val2.GetPoint(num);
		localPoint = ((Component)splineControl).transform.InverseTransformPoint(point);
		if (roadSurfaceMode == RoadSurfaceMode.XY)
		{
			localPoint.z = 0f;
		}
		else if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			localPoint.y = 0f;
		}
		return true;
	}

	private void HandleWaitingAreaDrawInput()
	{
		if (Input.GetMouseButtonDown(0))
		{
			if (!ShouldBlockGameplaySplineInput((Vector2)Input.mousePosition))
			{
				TryAddWaitingAreaPoint((Vector2)Input.mousePosition);
			}
		}
		else if (Input.GetMouseButtonDown(1))
		{
			SetWaitingAreaDrawMode(false);
		}

		if (Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);
			if ((int)touch.phase == 0 && !ShouldBlockGameplaySplineInput(touch.position, touch.fingerId))
			{
				TryAddWaitingAreaPoint(touch.position);
			}
		}
	}

	private bool HandleWaitingPointCandidateInput()
	{
		bool hasPointerDown = Input.GetMouseButtonDown(0);
		Vector2 screenPosition = hasPointerDown ? (Vector2)Input.mousePosition : Vector2.zero;
		if (!hasPointerDown && Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);
			if ((int)touch.phase == 0)
			{
				hasPointerDown = true;
				screenPosition = touch.position;
			}
		}
		if (!hasPointerDown)
		{
			return false;
		}
		if (ShouldBlockGameplaySplineInput(screenPosition))
		{
			return true;
		}

		return TryPlaceWaitingPointFromCandidate(screenPosition);
	}

	public void ToggleWaitingAreaDrawMode()
	{
		SetWaitingAreaDrawMode(!waitingAreaDrawMode);
	}

	private void SetWaitingAreaDrawMode(bool active)
	{
		if (waitingAreaDrawMode == active)
		{
			UpdateWaitingAreaDrawButtonVisual();
			return;
		}

		waitingAreaDrawMode = active;
		if (active)
		{
			SetWaitingPointSpawnMode(false);
			toggleSpawnBus = false;
			gameplaySplineDrawMode = false;
			DisableDeleteStickManMode();
			ShowBusPopup(show: false);
			ShowColorPopup(show: false);
			ClearWaitingCandidateMarkers();
			waitingAreaPoints.Clear();
			RefreshWaitingAreaMarkers();
			SetRoadMessage("Dang ve vung wait: click de dat diem, right click de tat");
		}
		else
		{
			SetRoadMessage("Da tat ve vung wait");
		}

		UpdateWaitingAreaDrawButtonVisual();
	}

	public void ToggleWaitingPointSpawnMode()
	{
		SetWaitingPointSpawnMode(!waitingPointSpawnMode);
	}

	private void SetWaitingPointSpawnMode(bool active)
	{
		if (waitingPointSpawnMode == active)
		{
			UpdateWaitingPointSpawnButtonVisual();
			return;
		}

		waitingPointSpawnMode = active;
		if (active)
		{
			SetWaitingAreaDrawMode(false);
			toggleSpawnBus = false;
			gameplaySplineDrawMode = false;
			DisableDeleteStickManMode();
			ShowBusPopup(show: false);
			ShowColorPopup(show: false);
			if (waitingCandidateMarkers.Count == 0)
			{
				SetRoadMessage("Bam Render Wait truoc, hoac click bien wait da co de doi mau");
			}
			else
			{
				SetRoadMessage("SpawnWait dang bat: click diem wait de spawn bien bao");
			}
		}
		else
		{
			SetRoadMessage("Da tat SpawnWait");
		}

		UpdateWaitingPointSpawnButtonVisual();
	}

	private void UpdateWaitingAreaDrawButtonVisual()
	{
		if ((Object)(object)drawWaitingAreaButton != (Object)null)
		{
			SetButtonText(drawWaitingAreaButton, waitingAreaDrawMode ? "StopPoint" : "SpawnPoint");
			if ((Object)(object)((Selectable)drawWaitingAreaButton).targetGraphic != (Object)null)
			{
				((Selectable)drawWaitingAreaButton).targetGraphic.color = waitingAreaDrawMode ? new Color(1f, 0.55f, 0.18f, 1f) : new Color(0.04f, 0.62f, 0.28f, 1f);
			}
		}
	}

	private void UpdateWaitingPointSpawnButtonVisual()
	{
		if ((Object)(object)spawnWaitingPointButton != (Object)null)
		{
			SetButtonText(spawnWaitingPointButton, waitingPointSpawnMode ? "StopWait" : "SpawnWait");
			if ((Object)(object)((Selectable)spawnWaitingPointButton).targetGraphic != (Object)null)
			{
				((Selectable)spawnWaitingPointButton).targetGraphic.color = waitingPointSpawnMode ? new Color(1f, 0.55f, 0.18f, 1f) : new Color(0.04f, 0.62f, 0.28f, 1f);
			}
		}
	}


	private static void SetButtonText(Button button, string label)
	{
		if ((Object)(object)button == (Object)null)
		{
			return;
		}

		Text text = ((Component)button).GetComponentInChildren<Text>(true);
		if ((Object)(object)text != (Object)null)
		{
			text.text = label;
		}

		TMP_Text tmpText = ((Component)button).GetComponentInChildren<TMP_Text>(true);
		if ((Object)(object)tmpText != (Object)null)
		{
			tmpText.text = label;
		}
	}

	private bool TryAddWaitingAreaPoint(Vector2 screenPosition)
	{
		if (!TryScreenToWorldOnDrawPlane(screenPosition, out var worldPoint))
		{
			SetRoadMessage("Khong raycast duoc vao mat phang wait");
			return false;
		}
		if (waitingAreaPoints.Count > 0 && Vector3.Distance(waitingAreaPoints[waitingAreaPoints.Count - 1], worldPoint) < waitingPointSpacing * 0.5f)
		{
			SetRoadMessage("Diem wait qua gan diem truoc");
			return false;
		}

		waitingAreaPoints.Add(worldPoint);
		RefreshWaitingAreaMarkers();
		SetRoadMessage("Da them diem vung wait " + waitingAreaPoints.Count);
		return true;
	}

	private bool TryScreenToWorldOnDrawPlane(Vector2 screenPosition, out Vector3 worldPoint)
	{
		worldPoint = Vector3.zero;
		Camera val = (((Object)(object)gameplayDrawCamera != (Object)null) ? gameplayDrawCamera : Camera.main);
		if ((Object)(object)val == (Object)null)
		{
			val = Object.FindObjectOfType<Camera>();
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}

		Ray ray = val.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
		Plane plane = GetGameplayDrawPlane();
		float distance = default(float);
		if (!plane.Raycast(ray, out distance))
		{
			return false;
		}
		if (distance < 0f || distance > gridRaycastMaxDistance)
		{
			return false;
		}

		worldPoint = ray.GetPoint(distance);
		if (roadSurfaceMode == RoadSurfaceMode.XY)
		{
			worldPoint.z = GetWaitingPlaneFixedValue();
		}
		else if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			worldPoint.y = GetWaitingPlaneFixedValue();
		}
		return true;
	}

	private float GetWaitingPlaneFixedValue()
	{
		if (waitingAreaPoints.Count > 0)
		{
			return roadSurfaceMode == RoadSurfaceMode.XZ ? waitingAreaPoints[0].y : waitingAreaPoints[0].z;
		}

		if ((Object)(object)splineControl != (Object)null)
		{
			Vector3 position = ((Component)splineControl).transform.position;
			return roadSurfaceMode == RoadSurfaceMode.XZ ? position.y : position.z;
		}

		return 0f;
	}

	private void RefreshWaitingAreaMarkers()
	{
		ClearWaitingAreaMarkers();
		Transform root = GetOrCreateSceneRoot(WaitingAreaMarkerRootName);
		for (int i = 0; i < waitingAreaPoints.Count; i++)
		{
			GameObject marker = CreateWaitingMarker(root, "WaitingAreaPoint_" + i, waitingAreaPoints[i], waitingAreaMarkerColor, waitingCandidateMarkerSize * 0.8f, withCollider: false);
			if ((Object)(object)marker != (Object)null)
			{
				waitingAreaMarkers.Add(marker);
			}
		}
	}

	private void ClearWaitingAreaMarkers()
	{
		for (int i = waitingAreaMarkers.Count - 1; i >= 0; i--)
		{
			DestroyUnityObject((Object)(object)waitingAreaMarkers[i]);
		}
		waitingAreaMarkers.Clear();
		Transform root = FindSceneTransform(WaitingAreaMarkerRootName);
		if ((Object)(object)root == (Object)null)
		{
			return;
		}
		for (int i = root.childCount - 1; i >= 0; i--)
		{
			DestroyUnityObject((Object)(object)((Component)root.GetChild(i)).gameObject);
		}
	}

	public void RenderWaitingPointCandidates()
	{
		SetWaitingAreaDrawMode(false);
		if (waitingAreaPoints.Count < 3)
		{
			SetRoadMessage("Can it nhat 3 diem de tao vung wait");
			return;
		}

		ClearWaitingCandidateMarkers();
		EnsureWaitingToolReferences();
		List<Vector2> polygon = new List<Vector2>();
		for (int i = 0; i < waitingAreaPoints.Count; i++)
		{
			polygon.Add(ToWaitingPlane2D(waitingAreaPoints[i]));
		}

		GetPolygonBounds(polygon, out var min, out var max);
		float spacing = Mathf.Max(0.1f, waitingPointSpacing);
		float fixedValue = GetWaitingPlaneFixedValue();
		int index = 0;
		for (float x = min.x + spacing * 0.5f; x <= max.x; x += spacing)
		{
			for (float y = min.y + spacing * 0.5f; y <= max.y; y += spacing)
			{
				Vector2 candidate = new Vector2(x, y);
				if (!IsPointInPolygon(candidate, polygon) || !IsFarEnoughFromExistingCandidates(candidate, spacing))
				{
					continue;
				}

				Vector3 worldPoint = FromWaitingPlane2D(candidate, fixedValue);
				GameObject marker = CreateWaitingMarker(waitingCandidateRoot, "WaitingCandidate_" + index, worldPoint, waitingCandidateMarkerColor, waitingCandidateMarkerSize, withCollider: true);
				if ((Object)(object)marker != (Object)null)
				{
					waitingCandidateMarkers.Add(marker);
					index++;
				}
			}
		}

		SetRoadMessage("Da render " + waitingCandidateMarkers.Count + " diem wait");
	}

	private bool IsFarEnoughFromExistingCandidates(Vector2 candidate, float spacing)
	{
		float minDistanceSqr = spacing * spacing * 0.98f;
		for (int i = 0; i < waitingCandidateMarkers.Count; i++)
		{
			if ((Object)(object)waitingCandidateMarkers[i] == (Object)null)
			{
				continue;
			}

			Vector2 existing = ToWaitingPlane2D(waitingCandidateMarkers[i].transform.position);
			if ((existing - candidate).sqrMagnitude < minDistanceSqr)
			{
				return false;
			}
		}

		return true;
	}

	private bool TryPlaceWaitingPointFromCandidate(Vector2 screenPosition)
	{
		if ((Object)(object)waitingCandidateRoot == (Object)null || waitingCandidateMarkers.Count == 0)
		{
			return false;
		}

		Camera val = GetGridRaycastCamera();
		if ((Object)(object)val == (Object)null)
		{
			val = Camera.main;
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}

		Ray ray = val.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
		RaycastHit[] hits = Physics.RaycastAll(ray, gridRaycastMaxDistance, Physics.DefaultRaycastLayers, (QueryTriggerInteraction)2);
		System.Array.Sort(hits, (RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance));
		for (int i = 0; i < hits.Length; i++)
		{
			GameObject marker = FindWaitingCandidateMarker(((Component)hits[i].transform).gameObject);
			if ((Object)(object)marker == (Object)null)
			{
				continue;
			}

			WaitingPointEdit waitingPoint = PlaceWaitingPoint(marker.transform.position, Mathf.Max(-1, draftWaitingPointColorIndex));
			SelectWaitingPoint(waitingPoint, showPopup: true);
			waitingCandidateMarkers.Remove(marker);
			DestroyUnityObject((Object)(object)marker);
			SetRoadMessage("Da dat bien bao wait");
			return true;
		}

		return false;
	}

	private GameObject FindWaitingCandidateMarker(GameObject hitObject)
	{
		if ((Object)(object)hitObject == (Object)null)
		{
			return null;
		}

		Transform current = hitObject.transform;
		while ((Object)(object)current != (Object)null)
		{
			for (int i = 0; i < waitingCandidateMarkers.Count; i++)
			{
				if ((Object)(object)waitingCandidateMarkers[i] == (Object)(object)((Component)current).gameObject)
				{
					return waitingCandidateMarkers[i];
				}
			}

			if ((Object)(object)current == (Object)(object)waitingCandidateRoot)
			{
				break;
			}

			current = current.parent;
		}

		return null;
	}

	private WaitingPointEdit PlaceWaitingPoint(Vector3 worldPoint, int colorIndex)
	{
		EnsureWaitingToolReferences();
		GameObject pointObject;
		if ((Object)(object)waitingSignPrefab != (Object)null)
		{
			pointObject = Object.Instantiate<GameObject>(waitingSignPrefab, worldPoint, Quaternion.identity, waitingPointRoot);
		}
		else
		{
			pointObject = GameObject.CreatePrimitive((PrimitiveType)3);
			pointObject.transform.SetParent(waitingPointRoot, true);
			pointObject.transform.position = worldPoint;
			pointObject.transform.localScale = Vector3.one * waitingCandidateMarkerSize;
		}

		((Object)pointObject).name = "WaitingPoint_Color_" + colorIndex;
		pointObject.SetActive(true);
		WaitingPointEdit waitingPointEdit = pointObject.GetComponent<WaitingPointEdit>();
		if ((Object)(object)waitingPointEdit == (Object)null)
		{
			waitingPointEdit = pointObject.AddComponent<WaitingPointEdit>();
		}
		waitingPointEdit.SetColor(colorIndex, waitingPointColorData);
		EnsureWaitingPointClickable(waitingPointEdit);
		RefreshWaitingPointStickManVisuals(waitingPointEdit);
		return waitingPointEdit;
	}

	private void RefreshAllWaitingPointStickManVisuals()
	{
		if (Application.isPlaying)
		{
			return;
		}

		WaitingPointEdit[] waitingPoints = GetSceneWaitingPoints();
		for (int i = 0; i < waitingPoints.Length; i++)
		{
			RefreshWaitingPointStickManVisuals(waitingPoints[i]);
		}
	}

	public void EditorRefreshWaitingPointStickManVisuals()
	{
		EnsureWaitingToolReferences();
		RefreshAllWaitingPointStickManVisuals();
	}

	private void RefreshWaitingPointStickManVisuals(WaitingPointEdit waitingPoint)
	{
		if (Application.isPlaying || (Object)(object)waitingPoint == (Object)null)
		{
			return;
		}

		EnsureWaitingToolReferences();
		IReadOnlyList<int> colors = waitingPoint.StickManColorIndices;
		List<StickManEdit> visuals = new List<StickManEdit>();
		StickManEdit[] existing = waitingPoint.GetComponentsInChildren<StickManEdit>(true);
		for (int i = 0; i < existing.Length; i++)
		{
			StickManEdit candidate = existing[i];
			if (candidate != null && candidate.transform != waitingPoint.transform && candidate.name.StartsWith(WaitingStickManNamePrefix))
			{
				visuals.Add(candidate);
			}
		}

		for (int i = visuals.Count - 1; i >= colors.Count; i--)
		{
			if (visuals[i] != null)
			{
				Undo.DestroyObjectImmediate(visuals[i].gameObject);
			}
			visuals.RemoveAt(i);
		}

		for (int i = visuals.Count; i < colors.Count; i++)
		{
			StickManEdit created = EditorInstantiateComponent(stickMan, waitingPoint.transform);
			if (created == null)
			{
				continue;
			}

			Undo.RegisterCreatedObjectUndo(created.gameObject, "Create Waiting Stickman");
			visuals.Add(created);
		}

		int total = colors.Count;
		for (int i = 0; i < visuals.Count && i < total; i++)
		{
			StickManEdit visual = visuals[i];
			if (visual == null)
			{
				continue;
			}

			int colorIndex = Mathf.Max(0, colors[i]);
			visual.transform.SetParent(waitingPoint.transform, false);
			visual.transform.localPosition = GetEditorWaitingStickManPosition(i, total);
			visual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
			visual.transform.localScale = Vector3.one * WaitingStickManScale;
			visual.ClearSeat();
			visual.ChangeColor(colorIndex);
			visual.name = WaitingStickManNamePrefix + i.ToString("00") + "_Color_" + colorIndex;
		}

	}

	private static Vector3 GetEditorWaitingStickManPosition(int index, int total)
	{
		const float minX = -0.5f;
		const float maxX = 0.5f;
		const float minZ = -0.5f;
		const float maxZ = -0.24f;
		Vector2 center = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
		Vector2 halfSize = new Vector2((maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
		int safeTotal = Mathf.Max(1, total);
		float radius = Mathf.Sqrt((index + 0.5f) / safeTotal);
		float angle = index * 2.39996323f;
		float x = center.x + Mathf.Cos(angle) * halfSize.x * radius;
		float z = center.y + Mathf.Sin(angle) * halfSize.y * radius;
		return new Vector3(x, 0.05f, z);
	}

	private bool HandleWaitingPointEditInput()
	{
		bool hasPointerDown = Input.GetMouseButtonDown(0);
		Vector2 screenPosition = hasPointerDown ? (Vector2)Input.mousePosition : Vector2.zero;
		if (!hasPointerDown && Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);
			if ((int)touch.phase == 0)
			{
				hasPointerDown = true;
				screenPosition = touch.position;
			}
		}
		if (!hasPointerDown)
		{
			return false;
		}
		if (ShouldBlockGameplaySplineInput(screenPosition))
		{
			return true;
		}
		if (TryGetWaitingPointFromScreenPoint(screenPosition, out var waitingPoint))
		{
			SelectWaitingPoint(waitingPoint, showPopup: true);
			return true;
		}
		return false;
	}

	private bool TryGetWaitingPointFromScreenPoint(Vector2 screenPosition, out WaitingPointEdit waitingPoint)
	{
		waitingPoint = null;
		Camera val = GetGridRaycastCamera();
		if ((Object)(object)val == (Object)null)
		{
			val = Camera.main;
		}
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}

		Ray ray = val.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
		RaycastHit[] hits = Physics.RaycastAll(ray, gridRaycastMaxDistance, Physics.DefaultRaycastLayers, (QueryTriggerInteraction)2);
		System.Array.Sort(hits, (RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance));
		for (int i = 0; i < hits.Length; i++)
		{
			waitingPoint = ((Component)hits[i].transform).GetComponentInParent<WaitingPointEdit>();
			if ((Object)(object)waitingPoint != (Object)null)
			{
				return true;
			}
		}

		RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(ray, gridRaycastMaxDistance, Physics.DefaultRaycastLayers);
		System.Array.Sort(hits2D, (RaycastHit2D left, RaycastHit2D right) => left.distance.CompareTo(right.distance));
		for (int i = 0; i < hits2D.Length; i++)
		{
			if ((Object)(object)hits2D[i].collider == (Object)null)
			{
				continue;
			}

			waitingPoint = ((Component)hits2D[i].collider).GetComponentInParent<WaitingPointEdit>();
			if ((Object)(object)waitingPoint != (Object)null)
			{
				return true;
			}
		}

		return false;
	}

	private void SelectWaitingPoint(WaitingPointEdit waitingPoint, bool showPopup)
	{
		selectedWaitingPoint = waitingPoint;
		if ((Object)(object)selectedWaitingPoint != (Object)null)
		{
			savedWaitingPointColorIndex = Mathf.Max(-1, selectedWaitingPoint.ColorIndex);
			draftWaitingPointColorIndex = savedWaitingPointColorIndex;
			EnsureWaitingPointClickable(selectedWaitingPoint);
		}
		else
		{
			draftWaitingPointColorIndex = Mathf.Max(-1, savedWaitingPointColorIndex);
		}

		UpdateWaitingPointColorPreview(null);
		if (showPopup)
		{
			ShowWaitPopup(true);
			ShowColorPopup(show: false);
		}
	}

	private void EnsureWaitingPointClickable(WaitingPointEdit waitingPoint)
	{
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return;
		}
		GameObject pointObject = ((Component)waitingPoint).gameObject;
		if ((Object)(object)pointObject.GetComponentInChildren<Collider>(true) != (Object)null || (Object)(object)pointObject.GetComponentInChildren<Collider2D>(true) != (Object)null)
		{
			return;
		}

		BoxCollider collider = pointObject.AddComponent<BoxCollider>();
		Bounds bounds;
		if (TryGetRendererWorldBounds(pointObject, out bounds))
		{
			collider.center = pointObject.transform.InverseTransformPoint(bounds.center);
			Vector3 localSize = pointObject.transform.InverseTransformVector(bounds.size);
			collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
		}
		else
		{
			collider.size = Vector3.one * Mathf.Max(0.2f, waitingCandidateMarkerSize);
		}
	}

	private static bool TryGetRendererWorldBounds(GameObject target, out Bounds bounds)
	{
		bounds = default(Bounds);
		if ((Object)(object)target == (Object)null)
		{
			return false;
		}

		Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
		bool hasBounds = false;
		for (int i = 0; i < renderers.Length; i++)
		{
			if ((Object)(object)renderers[i] == (Object)null)
			{
				continue;
			}
			if (!hasBounds)
			{
				bounds = renderers[i].bounds;
				hasBounds = true;
			}
			else
			{
				bounds.Encapsulate(renderers[i].bounds);
			}
		}

		return hasBounds;
	}

	private GameObject CreateWaitingMarker(Transform root, string objectName, Vector3 worldPoint, Color color, float size, bool withCollider)
	{
		if ((Object)(object)root == (Object)null)
		{
			return null;
		}

		GameObject marker = GameObject.CreatePrimitive((PrimitiveType)0);
		((Object)marker).name = objectName;
		marker.transform.SetParent(root, true);
		marker.transform.position = worldPoint;
		marker.transform.rotation = GetSplinePointMarkerRotation();
		marker.transform.localScale = Vector3.one * size;
		if (!withCollider)
		{
			Collider collider = marker.GetComponent<Collider>();
			if ((Object)(object)collider != (Object)null)
			{
				DestroyUnityObject((Object)(object)collider);
			}
		}
		ApplySplinePointMarkerColor(marker, color);
		return marker;
	}

	private void ClearWaitingCandidateMarkers()
	{
		for (int i = waitingCandidateMarkers.Count - 1; i >= 0; i--)
		{
			DestroyUnityObject((Object)(object)waitingCandidateMarkers[i]);
		}
		waitingCandidateMarkers.Clear();
		if ((Object)(object)waitingCandidateRoot == (Object)null)
		{
			waitingCandidateRoot = FindSceneTransform(WaitingCandidateRootName);
		}
		if ((Object)(object)waitingCandidateRoot == (Object)null)
		{
			return;
		}
		for (int i = waitingCandidateRoot.childCount - 1; i >= 0; i--)
		{
			DestroyUnityObject((Object)(object)((Component)waitingCandidateRoot.GetChild(i)).gameObject);
		}
	}

	private Vector2 ToWaitingPlane2D(Vector3 worldPoint)
	{
		if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			return new Vector2(worldPoint.x, worldPoint.z);
		}

		return new Vector2(worldPoint.x, worldPoint.y);
	}

	private Vector3 FromWaitingPlane2D(Vector2 point, float fixedValue)
	{
		if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			return new Vector3(point.x, fixedValue, point.y);
		}

		return new Vector3(point.x, point.y, fixedValue);
	}

	private static void GetPolygonBounds(List<Vector2> polygon, out Vector2 min, out Vector2 max)
	{
		min = polygon[0];
		max = polygon[0];
		for (int i = 1; i < polygon.Count; i++)
		{
			min = Vector2.Min(min, polygon[i]);
			max = Vector2.Max(max, polygon[i]);
		}
	}

	private static bool IsPointInPolygon(Vector2 point, List<Vector2> polygon)
	{
		bool inside = false;
		for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
		{
			Vector2 pi = polygon[i];
			Vector2 pj = polygon[j];
			float denominator = pj.y - pi.y;
			if (Mathf.Abs(denominator) < 0.0001f)
			{
				denominator = denominator < 0f ? -0.0001f : 0.0001f;
			}
			bool intersects = ((pi.y > point.y) != (pj.y > point.y))
				&& (point.x < (pj.x - pi.x) * (point.y - pi.y) / denominator + pi.x);
			if (intersects)
			{
				inside = !inside;
			}
		}

		return inside;
	}

	private Plane GetGameplayDrawPlane()
	{
		Vector3 val = (((Object)(object)splineControl != (Object)null) ? ((Component)splineControl).transform.TransformPoint(Vector3.zero) : Vector3.zero);
		if (roadSurfaceMode == RoadSurfaceMode.XY)
		{
			return new Plane(Vector3.forward, val);
		}
		if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			return new Plane(Vector3.up, val);
		}
		Vector3 val2 = (((Object)(object)splineControl != (Object)null) ? ((Component)splineControl).transform.TransformDirection(Vector3.up) : Vector3.up);
		return new Plane((val2.sqrMagnitude > 0.0001f) ? val2.normalized : Vector3.up, val);
	}

	private Spline GetGameplayActiveSpline()
	{
		if ((Object)(object)splineControl == (Object)null || splineControl.Splines == null || splineControl.Splines.Count == 0)
		{
			return null;
		}
		if (gameplayActiveSplineIndex < 0 || gameplayActiveSplineIndex >= splineControl.Splines.Count)
		{
			gameplayActiveSplineIndex = Mathf.Clamp(splineIndex, 0, splineControl.Splines.Count - 1);
		}
		return splineControl.Splines[gameplayActiveSplineIndex];
	}

	private static void ApplyAutoSmooth(Spline spline)
	{
		if (spline != null)
		{
			for (int i = 0; i < spline.Count; i++)
			{
				spline.SetTangentMode(i, TangentMode.AutoSmooth);
			}
		}
	}
	public void RefreshGameplaySplinePointMarkers()
	{
		ClearGameplaySplinePointMarkers();
		if (!showGameplaySplinePoints || (Object)(object)splineControl == (Object)null || splineControl.Splines == null)
		{
			return;
		}
		Transform val = GetSplinePointMarkerRoot();
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		for (int i = 0; i < splineControl.Splines.Count; i++)
		{
			Spline spline = splineControl.Splines[i];
			if (spline == null)
			{
				continue;
			}
			bool isActiveSpline = i == gameplayActiveSplineIndex;
			for (int j = 0; j < spline.Count; j++)
			{
				Vector3 localPoint = ToVector3(spline[j].Position);
				GameObject val2 = CreateSplinePointMarker(val, i, j, localPoint, isActiveSpline);
				if ((Object)(object)val2 != (Object)null)
				{
					gameplaySplinePointMarkers.Add(val2);
				}
			}
		}
	}

	private void ClearGameplaySplinePointMarkers()
	{
		for (int num = gameplaySplinePointMarkers.Count - 1; num >= 0; num--)
		{
			DestroyUnityObject((Object)(object)gameplaySplinePointMarkers[num]);
		}
		gameplaySplinePointMarkers.Clear();
		Transform val = FindSplinePointMarkerRoot();
		if (!((Object)(object)val == (Object)null))
		{
			for (int num2 = val.childCount - 1; num2 >= 0; num2--)
			{
				DestroyUnityObject((Object)(object)((Component)val.GetChild(num2)).gameObject);
			}
		}
	}

	private Transform GetSplinePointMarkerRoot()
	{
		Transform val = FindSplinePointMarkerRoot();
		if ((Object)(object)val != (Object)null)
		{
			splinePointMarkerRoot = val;
			return val;
		}
		Transform val2 = (((Object)(object)roadRoot != (Object)null) ? roadRoot : ((Component)this).transform);
		GameObject val3 = new GameObject(splinePointMarkerRootName);
		((Object)val3).hideFlags = (HideFlags)16;
		val3.transform.SetParent(val2, false);
		val3.transform.localPosition = Vector3.zero;
		val3.transform.localRotation = Quaternion.identity;
		val3.transform.localScale = Vector3.one;
		splinePointMarkerRoot = val3.transform;
		return splinePointMarkerRoot;
	}

	private Transform FindSplinePointMarkerRoot()
	{
		if ((Object)(object)splinePointMarkerRoot != (Object)null)
		{
			return splinePointMarkerRoot;
		}
		Transform val = (((Object)(object)roadRoot != (Object)null) ? roadRoot : ((Component)this).transform);
		if ((Object)(object)val == (Object)null || string.IsNullOrEmpty(splinePointMarkerRootName))
		{
			return null;
		}
		return val.Find(splinePointMarkerRootName);
	}

	private GameObject CreateSplinePointMarker(Transform markerRoot, int markerSplineIndex, int pointIndex, Vector3 localPoint, bool isActiveSpline)
	{
		Vector3 splinePointMarkerWorldPosition = GetSplinePointMarkerWorldPosition(localPoint);
		GameObject val;
		if ((Object)(object)splinePointMarkerPrefab != (Object)null)
		{
			val = Object.Instantiate<GameObject>(splinePointMarkerPrefab, splinePointMarkerWorldPosition, GetSplinePointMarkerRotation(), markerRoot);
		}
		else
		{
			val = GameObject.CreatePrimitive((PrimitiveType)0);
			val.transform.SetParent(markerRoot, true);
			Collider component = val.GetComponent<Collider>();
			if ((Object)(object)component != (Object)null)
			{
				DestroyUnityObject((Object)(object)component);
			}
		}
		((Object)val).name = "SplinePoint_" + markerSplineIndex + "_" + pointIndex;
		((Object)val).hideFlags = (HideFlags)16;
		val.transform.position = splinePointMarkerWorldPosition;
		val.transform.rotation = GetSplinePointMarkerRotation();
		val.transform.localScale = Vector3.one * splinePointMarkerSize;
		Color markerColor = (isActiveSpline ? activeSplinePointMarkerColor : splinePointMarkerColor);
		ApplySplinePointMarkerColor(val, markerColor);
		return val;
	}

	private Vector3 GetSplinePointMarkerWorldPosition(Vector3 localPoint)
	{
		Vector3 val = ((Component)splineControl).transform.TransformPoint(localPoint);
		Camera val2 = (((Object)(object)gameplayDrawCamera != (Object)null) ? gameplayDrawCamera : Camera.main);
		Vector3 val3 = (((Object)(object)val2 != (Object)null) ? (-((Component)val2).transform.forward) : GetSurfaceNormal(Vector3.up));
		if (val3.sqrMagnitude <= 0.0001f)
		{
			val3 = GetSurfaceNormal(Vector3.up);
		}
		return val + val3.normalized * splinePointMarkerOffset;
	}

	private Quaternion GetSplinePointMarkerRotation()
	{
		Camera val = (((Object)(object)gameplayDrawCamera != (Object)null) ? gameplayDrawCamera : Camera.main);
		if ((Object)(object)val == (Object)null)
		{
			return Quaternion.identity;
		}
		return Quaternion.LookRotation(((Component)val).transform.forward, ((Component)val).transform.up);
	}

	private void ApplySplinePointMarkerColor(GameObject marker, Color markerColor)
	{
		SpriteRenderer[] componentsInChildren = marker.GetComponentsInChildren<SpriteRenderer>(true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			componentsInChildren[i].color = markerColor;
			((Renderer)componentsInChildren[i]).sortingOrder = splinePointMarkerSortingOrder;
		}
		Renderer[] componentsInChildren2 = marker.GetComponentsInChildren<Renderer>(true);
		Material val = GetSplinePointMarkerMaterial();
		for (int j = 0; j < componentsInChildren2.Length; j++)
		{
			if (!(componentsInChildren2[j] is SpriteRenderer))
			{
				if ((Object)(object)val != (Object)null)
				{
					componentsInChildren2[j].sharedMaterial = val;
				}
				componentsInChildren2[j].sortingOrder = splinePointMarkerSortingOrder;
				MaterialPropertyBlock val2 = new MaterialPropertyBlock();
				componentsInChildren2[j].GetPropertyBlock(val2);
				val2.SetColor("_BaseColor", markerColor);
				val2.SetColor("_Color", markerColor);
				componentsInChildren2[j].SetPropertyBlock(val2);
			}
		}
	}

	private Material GetSplinePointMarkerMaterial()
	{
		if ((Object)(object)splinePointMarkerMaterial != (Object)null)
		{
			return splinePointMarkerMaterial;
		}
		if ((Object)(object)runtimeSplinePointMarkerMaterial != (Object)null)
		{
			return runtimeSplinePointMarkerMaterial;
		}
		Shader val = Shader.Find("Universal Render Pipeline/Unlit");
		if ((Object)(object)val == (Object)null)
		{
			val = Shader.Find("Unlit/Color");
		}
		if ((Object)(object)val == (Object)null)
		{
			val = Shader.Find("Sprites/Default");
		}
		if ((Object)(object)val == (Object)null)
		{
			val = Shader.Find("Standard");
		}
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		runtimeSplinePointMarkerMaterial = new Material(val)
		{
			name = "GameplaySplinePointMarker_Mat",
			hideFlags = (HideFlags)61
		};
		SetMaterialColor(runtimeSplinePointMarkerMaterial, Color.white);
		return runtimeSplinePointMarkerMaterial;
	}

	private static void SetMaterialColor(Material material, Color color)
	{
		if (!((Object)(object)material == (Object)null))
		{
			if (material.HasProperty("_BaseColor"))
			{
				material.SetColor("_BaseColor", color);
			}
			if (material.HasProperty("_Color"))
			{
				material.SetColor("_Color", color);
			}
		}
	}

	private void MarkGameplaySplineEdited()
	{
		if (Application.isPlaying)
		{
			gameplaySplineEditedInPlay = true;
		}
		MarkSplineDirty();
	}

private void MarkSplineDirty()
	{
		if (!Application.isPlaying)
		{
			if ((Object)(object)splineControl != (Object)null)
			{
				EditorUtility.SetDirty((Object)(object)splineControl);
			}
			EditorUtility.SetDirty((Object)(object)this);
			Scene scene = ((Component)this).gameObject.scene;
			if (scene.IsValid() && !EditorSceneManager.IsPreviewScene(scene))
			{
				EditorSceneManager.MarkSceneDirty(scene);
			}
		}
	}

	private void SetRoadMessage(string message)
	{
		lastRoadRenderMessage = message;
		if ((Object)(object)roadMessageText != (Object)null)
		{
			roadMessageText.text = message;
		}
		if ((Object)(object)roadMessageTmpText != (Object)null)
		{
			roadMessageTmpText.text = message;
		}
	}

	private static Vector3 ToVector3(float3 value)
	{
		return new Vector3(value.x, value.y, value.z);
	}

	private static T FindDeep<T>(Transform root, string objectName) where T : Component
	{
		if ((Object)(object)root == (Object)null)
		{
			return default(T);
		}
		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			if (((Object)child).name == objectName)
			{
				T component = ((Component)child).GetComponent<T>();
				if ((Object)(object)component != (Object)null)
				{
					return component;
				}
			}
			T val = FindDeep<T>(child, objectName);
			if ((Object)(object)val != (Object)null)
			{
				return val;
			}
		}
		return default(T);
	}

	private static T FindDeepContains<T>(Transform root, string namePart) where T : Component
	{
		if ((Object)(object)root == (Object)null || string.IsNullOrWhiteSpace(namePart))
		{
			return default(T);
		}
		string text = namePart.ToLowerInvariant();
		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			if (((Object)child).name.ToLowerInvariant().Contains(text))
			{
				T component = ((Component)child).GetComponent<T>();
				if ((Object)(object)component != (Object)null)
				{
					return component;
				}
			}
			T val = FindDeepContains<T>(child, text);
			if ((Object)(object)val != (Object)null)
			{
				return val;
			}
		}
		return default(T);
	}

private T FindSceneComponentByName<T>(string objectName) where T : Component
	{
		if (string.IsNullOrWhiteSpace(objectName))
		{
			return default(T);
		}

		T[] components = Resources.FindObjectsOfTypeAll<T>();
		for (int i = 0; i < components.Length; i++)
		{
			T component = components[i];
			if (component == null || !component.gameObject.scene.IsValid())
			{
				continue;
			}
			if (editorWorkspaceScene.IsValid() && component.gameObject.scene != editorWorkspaceScene)
			{
				continue;
			}
			if (component.name == objectName)
			{
				return component;
			}
		}
		return default(T);
	}

	private static void DestroyRoadObject(GameObject target)
	{
		DestroyUnityObject((UnityEngine.Object)(object)target);
	}

	private static void DestroyUnityObject(UnityEngine.Object target)
	{
		if (!(target == (UnityEngine.Object)null))
		{
			if (Application.isPlaying)
			{
				UnityEngine.Object.Destroy(target);
			}
			else
			{
				UnityEngine.Object.DestroyImmediate(target);
			}
		}
	}

	public void SaveLevelJson()
	{
		int _i = Mathf.Max(1, int.Parse(widthField.text));
		int _j = Mathf.Max(1, int.Parse(heightField.text));
		int levelId = ReadCurrentLevelId();
		LevelData data = CaptureCurrentLevelData(levelId);
		data.i = _i;
		data.j = _j;
		data.level.id = "Level_" + levelId.ToString("D2");
		data.SyncDocumentFromRuntimeData();
		AppendSplineItems(data);
		Directory.CreateDirectory(LevelAssetFolder);
		string path = GetLevelAssetPath(levelId);
		File.WriteAllText(path, data.ToJson());
		AssetDatabase.ImportAsset(path);
		AssetDatabase.Refresh();
		SetRoadMessage("Da save " + path);
	}

	public void LoadLevelJson()
	{
		int levelId = ReadCurrentLevelId();
		string json = LoadLevelJsonText(levelId);
		if (string.IsNullOrWhiteSpace(json))
		{
			SetRoadMessage("Khong tim thay Level" + levelId + " trong Resources/Levels");
			return;
		}

		LevelData data;
		try
		{
			data = LevelData.FromJson(json);
		}
		catch (JsonException ex)
		{
			Debug.LogError(ex);
			SetRoadMessage("File Level" + levelId + " sai format");
			return;
		}

		if (data == null)
		{
			SetRoadMessage("File Level" + levelId + " khong doc duoc");
			return;
		}
		if ((Object)(object)widthField != (Object)null)
		{
			widthField.text = data.i.ToString();
		}
		if ((Object)(object)heightField != (Object)null)
		{
			heightField.text = data.j.ToString();
		}
		RebuildGridLayoutForLoadedLevel(data.i, data.j);
        ApplyLevelData(data);
		SetRoadMessage("Da load Level" + levelId);
	}

	private int ReadCurrentLevelId()
	{
		if ((Object)(object)levelInputField != (Object)null && int.TryParse(levelInputField.text, out var value))
		{
			currentLevelId = Mathf.Max(1, value);
		}
		else if ((Object)(object)levelTmpInputField != (Object)null && int.TryParse(levelTmpInputField.text, out value))
		{
			currentLevelId = Mathf.Max(1, value);
		}
		else
		{
			currentLevelId = Mathf.Max(1, currentLevelId);
		}

		if ((Object)(object)levelInputField != (Object)null)
		{
			levelInputField.text = currentLevelId.ToString();
		}
		if ((Object)(object)levelTmpInputField != (Object)null)
		{
			levelTmpInputField.text = currentLevelId.ToString();
		}

		return currentLevelId;
	}

	private string LoadLevelJsonText(int levelId)
	{
		string assetPath = GetLevelAssetPath(levelId);
		if (!File.Exists(assetPath))
		{
			assetPath = GetLegacyLevelAssetPath(levelId);
		}
		if (File.Exists(assetPath))
		{
			return File.ReadAllText(assetPath);
		}

		TextAsset textAsset = Resources.Load<TextAsset>(LevelResourceFolder + "/Level_" + Mathf.Max(1, levelId));
		if (textAsset == null)
		{
			textAsset = Resources.Load<TextAsset>(LevelResourceFolder + "/Level" + Mathf.Max(1, levelId));
		}
		return (Object)(object)textAsset != (Object)null ? textAsset.text : null;
	}

	private static string GetLevelAssetPath(int levelId)
	{
		return (LevelAssetFolder + "/Level_" + Mathf.Max(1, levelId) + ".json").Replace("\\", "/");
	}

	private static string GetLegacyLevelAssetPath(int levelId)
	{
		return (LevelAssetFolder + "/Level" + Mathf.Max(1, levelId) + ".json").Replace("\\", "/");
	}

	private void RebuildGridLayoutForLoadedLevel(int width, int height)
	{
		int safeWidth = Mathf.Max(1, width);
		int safeHeight = Mathf.Max(1, height);
		if (HasGridLayout(safeWidth, safeHeight))
		{
			return;
		}

		GridEdit[] currentGrids = GetSceneGrids();
		for (int index = currentGrids.Length - 1; index >= 0; index--)
		{
			GridEdit grid = currentGrids[index];
			if (grid == null)
			{
				continue;
			}

			grid.ClearBus();
			if (Application.isPlaying)
			{
				DestroyUnityObject(((Component)grid).gameObject);
			}
			else
			{
				Undo.DestroyObjectImmediate(((Component)grid).gameObject);
			}
		}

		EditorSpawnGrid(safeWidth, safeHeight);
	}

	private bool HasGridLayout(int width, int height)
	{
		GridEdit[] grids = GetSceneGrids();
		if (grids.Length != width * height)
		{
			return false;
		}

		for (int row = 0; row < height; row++)
		{
			for (int column = 0; column < width; column++)
			{
				if (FindGridByCoordinates(column, row) == null)
				{
					return false;
				}
			}
		}

		return true;
	}

	private LevelData CaptureCurrentLevelData(int levelId)
	{
		LevelData data = new LevelData
		{
			levelId = levelId
		};

		BusEdit[] buses = GetSceneBuses();
		for (int i = 0; i < buses.Length; i++)
		{
			BusEdit bus = buses[i];
			if ((Object)(object)bus == (Object)null)
			{
				continue;
			}

			Vector3 position = ((Component)bus).transform.position;
			BusData busData = new BusData
			{
				posX = position.x,
				posY = position.y,
				posZ = position.z,
				colorId = bus.ColorIndex,
				column = -1,
				row = -1,
				passengerData = new BusPassengerData()
			};
			TryGetGridCoordinates(bus.CurrentGrid, out busData.column, out busData.row);

			IReadOnlyList<BusEdit.StickManSeatState> seatStates = bus.SeatStates;
			for (int j = 0; j < seatStates.Count; j++)
			{
				if (seatStates[j] != null && seatStates[j].occupied && seatStates[j].stickMan != null)
				{
					busData.passengerData.passengers[j] = new StickManData
					{
						colorId = seatStates[j].colorIndex
					};
				}
			}

			data.buses.Add(busData);
		}

		WaitingPointEdit[] waitingPoints = GetSceneWaitingPoints();
		for (int i = 0; i < waitingPoints.Length; i++)
		{
			WaitingPointEdit waitingPoint = waitingPoints[i];
			if ((Object)(object)waitingPoint == (Object)null)
			{
				continue;
			}

			Vector3 position = ((Component)waitingPoint).transform.position;
			WaitingPassengerColumnData columnData = new WaitingPassengerColumnData
			{
				columnSlotId = i,
				posX = position.x,
				posY = position.y,
				posZ = position.z,
				colorId = waitingPoint.ColorIndex,
				Direction = waitingPoint.Direction,
				listStickmans = new List<StickManData>()
			};
			IReadOnlyList<int> stickManColors = waitingPoint.StickManColorIndices;
			for (int j = 0; j < stickManColors.Count; j++)
			{
				columnData.listStickmans.Add(new StickManData
				{
					colorId = stickManColors[j]
				});
			}
			data.waitingPassengerColumns.Add(columnData);
		}

		return data;
	}

private void AppendSplineItems(LevelData data)
{
    if (data == null || splineControl == null || splineControl.Splines == null ||
        splineControl.Splines.Count == 0)
    {
        return;
    }

    int mainSplineIndex = Mathf.Clamp(
        gameplayActiveSplineIndex,
        0,
        splineControl.Splines.Count - 1);
    Spline spline = splineControl.Splines[mainSplineIndex];
    if (spline == null || spline.Count < 2)
    {
        return;
    }

    var itemData = new LevelBoardItemData
    {
        positionsColl = new List<LevelVector3Data>(),
        tanInsColl = new List<LevelVector3Data>(),
        tanOutsColl = new List<LevelVector3Data>(),
        sizesColl = new List<float>()
    };

    for (int pointIndex = 0; pointIndex < spline.Count; pointIndex++)
    {
        BezierKnot knot = spline[pointIndex];
        float3 position = knot.Position;
        float3 tangentIn = knot.TangentIn;
        float3 tangentOut = knot.TangentOut;

        itemData.positionsColl.Add(new LevelVector3Data(position.x, position.y, position.z));
        itemData.tanInsColl.Add(new LevelVector3Data(tangentIn.x, tangentIn.y, tangentIn.z));
        itemData.tanOutsColl.Add(new LevelVector3Data(tangentOut.x, tangentOut.y, tangentOut.z));
        itemData.sizesColl.Add(1f);
    }

    data.AppendBoardItem(new LevelBoardItem
    {
        type = "Conveyor",
        typeId = 6,
        data = itemData
    });
}

private void ApplySplineItems(LevelData data)
{
    if (data == null || splineControl == null)
    {
        return;
    }

    if (!MyGameLevelPathPort.TryCreateSpline(
            data,
            out Spline levelSpline,
            out List<float> knotSizes,
            out string error))
    {
        SetRoadMessage("Khong the nap MyGame Conveyor LevelPath: " + error);
        return;
    }

    List<Spline> restoredSplines = new List<Spline>(splineControl.Splines);
    if (restoredSplines.Count == 0)
    {
        restoredSplines.Add(levelSpline);
    }
    else
    {
        restoredSplines[0] = levelSpline;
    }

    splineControl.Splines = restoredSplines;
    gameplayActiveSplineIndex = 0;
    MarkSplineDirty();
    RenderRoadMesh();
    RefreshGameplaySplinePointMarkers();

    if (knotSizes.Count != levelSpline.Count)
    {
        SetRoadMessage("LevelPath size data does not match its knot count.");
    }
}

	private void ApplyLevelData(LevelData data)
	{
		EnsureWaitingToolReferences();
		ClearLoadedLevelObjects();
		waitingAreaPoints.Clear();
		RefreshWaitingAreaMarkers();

		if (data.buses != null)
		{
			for (int i = 0; i < data.buses.Count; i++)
			{
				BusData busData = data.buses[i];
				if (busData == null)
				{
					continue;
				}

				GridEdit grid = FindGridByCoordinates(busData.column, busData.row);
				Vector3 busPosition = grid != null
					? grid.transform.position
					: new Vector3(busData.posX, busData.posY, busData.posZ);
				BusEdit bus = CreateBusAt(busPosition, Quaternion.identity);
				if ((Object)(object)bus == (Object)null)
				{
					continue;
				}

				bus.BusData(busData.colorId, 0);
				grid = grid != null ? grid : FindNearestGrid(((Component)bus).transform.position, 0.9f);
				if ((Object)(object)grid != (Object)null)
				{
					bus.SetGrid(grid);
					grid.SetBus(bus);
				}

				if (busData.passengerData != null && busData.passengerData.passengers != null)
				{
					foreach (KeyValuePair<int, StickManData> passenger in busData.passengerData.passengers)
					{
						StickManData passengerData = passenger.Value;
						if (passengerData != null)
						{
							if (!bus.SpawnStickManAtSeat(passenger.Key, passengerData.colorId))
							{
								bus.SpawnStickMan(1, passengerData.colorId);
							}
						}
					}
				}
			}
		}

		if (data.waitingPassengerColumns != null)
		{
			for (int i = 0; i < data.waitingPassengerColumns.Count; i++)
			{
				WaitingPassengerColumnData columnData = data.waitingPassengerColumns[i];
				if (columnData != null)
				{
					int colorIndex = columnData.colorId;
					WaitingPointEdit waitingPoint = PlaceWaitingPoint(new Vector3(columnData.posX, columnData.posY, columnData.posZ), colorIndex);
					if ((Object)(object)waitingPoint != (Object)null)
					{
						waitingPoint.SetDirection(columnData.Direction);
						if (columnData.listStickmans != null && columnData.listStickmans.Count > 0)
						{
							waitingPoint.SetStickManColors(columnData.listStickmans);
						}
					}
				}
			}
		}

		ApplySplineItems(data);
	}

	private void ClearLoadedLevelObjects()
	{
		ClearWaitingCandidateMarkers();
		ClearWaitingPoints();
		GridEdit[] grids = GetSceneGrids();
		for (int i = 0; i < grids.Length; i++)
		{
			if ((Object)(object)grids[i] != (Object)null)
			{
				grids[i].ClearBus();
			}
		}

		BusEdit[] buses = GetSceneBuses();
		for (int i = buses.Length - 1; i >= 0; i--)
		{
			if ((Object)(object)buses[i] != (Object)null)
			{
				DestroyUnityObject((Object)(object)((Component)buses[i]).gameObject);
			}
		}
	}

	private void ClearWaitingPoints()
	{
		if ((Object)(object)waitingPointRoot == (Object)null)
		{
			waitingPointRoot = FindSceneTransform(WaitingPointRootName);
		}
		if ((Object)(object)waitingPointRoot == (Object)null)
		{
			return;
		}

		for (int i = waitingPointRoot.childCount - 1; i >= 0; i--)
		{
			DestroyUnityObject((Object)(object)((Component)waitingPointRoot.GetChild(i)).gameObject);
		}
	}

	private BusEdit[] GetSceneBuses()
	{
		BusEdit[] buses = ((Object)(object)parentBus != (Object)null)
			? ((Component)parentBus).GetComponentsInChildren<BusEdit>(true)
			: Resources.FindObjectsOfTypeAll<BusEdit>();
		List<BusEdit> result = new List<BusEdit>();
		for (int i = 0; i < buses.Length; i++)
		{
			if ((Object)(object)buses[i] != (Object)null && ((Component)buses[i]).gameObject.scene.IsValid())
			{
				result.Add(buses[i]);
			}
		}
		return result.ToArray();
	}

	private GridEdit[] GetSceneGrids()
	{
		GridEdit[] grids = ((Object)(object)parentGrid != (Object)null)
			? ((Component)parentGrid).GetComponentsInChildren<GridEdit>(true)
			: Resources.FindObjectsOfTypeAll<GridEdit>();
		List<GridEdit> result = new List<GridEdit>();
		for (int i = 0; i < grids.Length; i++)
		{
			if ((Object)(object)grids[i] != (Object)null && ((Component)grids[i]).gameObject.scene.IsValid())
			{
				result.Add(grids[i]);
			}
		}
		return result.ToArray();
	}

	private WaitingPointEdit[] GetSceneWaitingPoints()
	{
		WaitingPointEdit[] waitingPoints = ((Object)(object)waitingPointRoot != (Object)null)
			? ((Component)waitingPointRoot).GetComponentsInChildren<WaitingPointEdit>(true)
			: Resources.FindObjectsOfTypeAll<WaitingPointEdit>();
		List<WaitingPointEdit> result = new List<WaitingPointEdit>();
		for (int i = 0; i < waitingPoints.Length; i++)
		{
			if ((Object)(object)waitingPoints[i] != (Object)null && ((Component)waitingPoints[i]).gameObject.scene.IsValid())
			{
				result.Add(waitingPoints[i]);
			}
		}
		return result.ToArray();
	}

	private GridEdit FindGridByCoordinates(int column, int row)
	{
		if (column < 0 || row < 0)
		{
			return null;
		}

		GridEdit[] grids = GetSceneGrids();
		for (int index = 0; index < grids.Length; index++)
		{
			if (grids[index] == null || !TryGetGridCoordinates(grids[index], out int gridColumn, out int gridRow))
			{
				continue;
			}
			if (gridColumn == column && gridRow == row)
			{
				return grids[index];
			}
		}
		return null;
	}

	private static bool TryGetGridCoordinates(GridEdit grid, out int column, out int row)
	{
		column = -1;
		row = -1;
		if (grid == null || grid.gameObject == null)
		{
			return false;
		}

		string[] parts = grid.gameObject.name.Split('_');
		return parts.Length >= 3
			&& int.TryParse(parts[1], out row)
			&& int.TryParse(parts[2], out column);
	}

	private GridEdit FindNearestGrid(Vector3 position, float maxDistance)
	{
		GridEdit[] grids = GetSceneGrids();
		GridEdit bestGrid = null;
		float bestDistance = maxDistance * maxDistance;
		for (int i = 0; i < grids.Length; i++)
		{
			if ((Object)(object)grids[i] == (Object)null || (Object)(object)grids[i].CurrentBus != (Object)null)
			{
				continue;
			}

			float distance = (((Component)grids[i]).transform.position - position).sqrMagnitude;
			if (distance <= bestDistance)
			{
				bestDistance = distance;
				bestGrid = grids[i];
			}
		}
		return bestGrid;
	}

private Transform GetOrCreateSceneRoot(string objectName)
	{
		Transform root = FindSceneTransform(objectName);
		if (root != null)
		{
			return root;
		}

		GameObject rootObject = new GameObject(objectName);
		if (editorWorkspaceScene.IsValid() && rootObject.scene != editorWorkspaceScene)
		{
			SceneManager.MoveGameObjectToScene(rootObject, editorWorkspaceScene);
		}
		rootObject.transform.position = Vector3.zero;
		rootObject.transform.rotation = Quaternion.identity;
		rootObject.transform.localScale = Vector3.one;
		return rootObject.transform;
	}

private Transform FindSceneTransform(string objectName)
	{
		if (string.IsNullOrWhiteSpace(objectName))
		{
			return null;
		}

		Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
		for (int i = 0; i < transforms.Length; i++)
		{
			Transform candidate = transforms[i];
			if (candidate == null || !candidate.gameObject.scene.IsValid())
			{
				continue;
			}
			if (editorWorkspaceScene.IsValid() && candidate.gameObject.scene != editorWorkspaceScene)
			{
				continue;
			}
			if (candidate.name == objectName)
			{
				return candidate;
			}
		}
		return null;
	}

	private void HandleGridRaycastInput()
	{
		if (Input.GetMouseButtonDown(0))
		{
			if (deleteStickManMode)
			{
				TryDeleteStickManFromScreenPoint((Vector2)Input.mousePosition);
				return;
			}

			if (TryGetGridFromScreenPoint((Vector2)Input.mousePosition, out var grid))
			{
				HandleGridLeftClick(grid);
			}
		}
		else if (Input.GetMouseButtonDown(1))
		{
			if (TryGetGridFromScreenPoint((Vector2)Input.mousePosition, out var grid2))
			{
				HandleGridRightClick(grid2);
			}
		}
		else if (Input.touchCount > 0)
		{
			Touch touch = Input.GetTouch(0);
			if ((int)touch.phase == 0 && deleteStickManMode)
			{
				TryDeleteStickManFromScreenPoint(touch.position);
			}
			else if ((int)touch.phase == 0 && TryGetGridFromScreenPoint(touch.position, out var grid3))
			{
				HandleGridLeftClick(grid3);
			}
		}
	}

	private bool TryGetGridFromScreenPoint(Vector2 screenPosition, out GridEdit grid)
	{
		grid = null;
		if (ShouldBlockGameplaySplineInput(screenPosition))
		{
			return false;
		}
		Camera val = GetGridRaycastCamera();
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}
		Ray ray = val.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
		if (TryPickNearestProjectedGrid(screenPosition, val, out grid))
		{
			return true;
		}
		if (!TryRaycastGrid(ray, out grid))
		{
			return TryProjectRayToGridBounds(ray, val, out grid);
		}
		return true;
	}

	private Camera GetGridRaycastCamera()
	{
		if ((Object)(object)gridRaycastCamera == (Object)null && (Object)(object)gameplayDrawCamera != (Object)null)
		{
			gridRaycastCamera = gameplayDrawCamera;
		}
		return gridRaycastCamera;
	}

	private void CacheGridRaycastCamera()
	{
		if (!((Object)(object)gridRaycastCamera != (Object)null))
		{
			if ((Object)(object)gameplayDrawCamera != (Object)null)
			{
				gridRaycastCamera = gameplayDrawCamera;
			}
			else if ((Object)(object)Camera.main != (Object)null)
			{
				gridRaycastCamera = Camera.main;
			}
			else
			{
				gridRaycastCamera = Object.FindObjectOfType<Camera>();
			}
		}
	}

	private bool TryRaycastGrid(Ray ray, out GridEdit grid)
	{
		grid = null;
		bool queriesHitBackfaces = Physics.queriesHitBackfaces;
		Physics.queriesHitBackfaces = true;
		RaycastHit[] array;
		try
		{
			array = Physics.RaycastAll(ray, gridRaycastMaxDistance, gridRaycastMask, (QueryTriggerInteraction)2);
		}
		finally
		{
			Physics.queriesHitBackfaces = queriesHitBackfaces;
		}
		System.Array.Sort(array, (RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance));
		for (int num = 0; num < array.Length; num++)
		{
			if (TryResolveGridFromTransform(array[num].transform, out grid))
			{
				return true;
			}
		}
		RaycastHit2D[] rayIntersectionAll = Physics2D.GetRayIntersectionAll(ray, gridRaycastMaxDistance, gridRaycastMask);
		System.Array.Sort(rayIntersectionAll, (RaycastHit2D left, RaycastHit2D right) => left.distance.CompareTo(right.distance));
		for (int num2 = 0; num2 < rayIntersectionAll.Length; num2++)
		{
			if ((Object)(object)rayIntersectionAll[num2].collider != (Object)null && TryResolveGridFromTransform(((Component)rayIntersectionAll[num2].collider).transform, out grid))
			{
				return true;
			}
		}
		return false;
	}

	private bool TryDeleteStickManFromScreenPoint(Vector2 screenPosition)
	{
		if (ShouldBlockGameplaySplineInput(screenPosition))
		{
			return false;
		}

		Camera val = GetGridRaycastCamera();
		if ((Object)(object)val == (Object)null)
		{
			return false;
		}

		Ray ray = val.ScreenPointToRay(new Vector3(screenPosition.x, screenPosition.y, 0f));
		if (!TryRaycastStickMan(ray, out var stickMan))
		{
			return false;
		}

		return TryDeleteStickMan(stickMan);
	}

	private bool TryRaycastStickMan(Ray ray, out StickManEdit stickMan)
	{
		stickMan = null;
		RaycastHit[] array = Physics.RaycastAll(ray, gridRaycastMaxDistance, gridRaycastMask, (QueryTriggerInteraction)2);
		System.Array.Sort(array, (RaycastHit left, RaycastHit right) => left.distance.CompareTo(right.distance));
		for (int i = 0; i < array.Length; i++)
		{
			StickManEdit componentInParent = ((Component)array[i].transform).GetComponentInParent<StickManEdit>();
			if ((Object)(object)componentInParent != (Object)null)
			{
				stickMan = componentInParent;
				return true;
			}
		}

		RaycastHit2D[] rayIntersectionAll = Physics2D.GetRayIntersectionAll(ray, gridRaycastMaxDistance, gridRaycastMask);
		System.Array.Sort(rayIntersectionAll, (RaycastHit2D left, RaycastHit2D right) => left.distance.CompareTo(right.distance));
		for (int i = 0; i < rayIntersectionAll.Length; i++)
		{
			if ((Object)(object)rayIntersectionAll[i].collider == (Object)null)
			{
				continue;
			}

			StickManEdit componentInParent = ((Component)rayIntersectionAll[i].collider).GetComponentInParent<StickManEdit>();
			if ((Object)(object)componentInParent != (Object)null)
			{
				stickMan = componentInParent;
				return true;
			}
		}

		return false;
	}

	private bool TryProjectRayToGridBounds(Ray ray, Camera cameraToUse, out GridEdit grid)
	{
		grid = null;
		Plane gridRaycastPlane = GetGridRaycastPlane(cameraToUse);
		float num = default(float);
		if (!gridRaycastPlane.Raycast(ray, out num))
		{
			return false;
		}
		Vector3 point = ray.GetPoint(num);
		GridEdit[] array = (((Object)(object)parentGrid != (Object)null) ? ((Component)parentGrid).GetComponentsInChildren<GridEdit>(true) : Object.FindObjectsOfType<GridEdit>());
		float num2 = float.MaxValue;
		foreach (GridEdit gridEdit in array)
		{
			if (!((Object)(object)gridEdit == (Object)null) && ((Component)gridEdit).gameObject.activeInHierarchy && TryGetGridWorldBounds(gridEdit, out var bounds) && ContainsPointOnGridPlane(bounds, point))
			{
				Vector3 val = bounds.center - point;
				float sqrMagnitude = val.sqrMagnitude;
				if (sqrMagnitude < num2)
				{
					num2 = sqrMagnitude;
					grid = gridEdit;
				}
			}
		}
		return (Object)(object)grid != (Object)null;
	}

	private bool TryPickNearestProjectedGrid(Vector2 screenPosition, Camera cameraToUse, out GridEdit grid)
	{
		grid = null;
		if ((Object)(object)cameraToUse == (Object)null)
		{
			return false;
		}
		GridEdit[] array = (((Object)(object)parentGrid != (Object)null) ? ((Component)parentGrid).GetComponentsInChildren<GridEdit>(true) : Object.FindObjectsOfType<GridEdit>());
		float num = float.MaxValue;
		float num2 = Mathf.Max(4f, gridRaycastBoundsPadding * 64f);
		foreach (GridEdit gridEdit in array)
		{
			if ((Object)(object)gridEdit == (Object)null || !((Component)gridEdit).gameObject.activeInHierarchy)
			{
				continue;
			}
			if (!TryGetGridWorldBounds(gridEdit, out var bounds) || !TryGetScreenBounds(bounds, cameraToUse, out var screenBounds))
			{
				continue;
			}
			screenBounds.xMin -= num2;
			screenBounds.xMax += num2;
			screenBounds.yMin -= num2;
			screenBounds.yMax += num2;
			if (!screenBounds.Contains(screenPosition))
			{
				continue;
			}
			Vector3 vector = cameraToUse.WorldToScreenPoint(bounds.center);
			float sqrMagnitude = ((Vector2)vector - screenPosition).sqrMagnitude;
			if (sqrMagnitude < num)
			{
				num = sqrMagnitude;
				grid = gridEdit;
			}
		}
		return (Object)(object)grid != (Object)null;
	}

	private static bool TryGetScreenBounds(Bounds bounds, Camera cameraToUse, out Rect screenBounds)
	{
		screenBounds = default(Rect);
		if ((Object)(object)cameraToUse == (Object)null)
		{
			return false;
		}
		Vector3 min = bounds.min;
		Vector3 max = bounds.max;
		Vector3[] array =
		{
			new Vector3(min.x, min.y, min.z),
			new Vector3(min.x, min.y, max.z),
			new Vector3(min.x, max.y, min.z),
			new Vector3(min.x, max.y, max.z),
			new Vector3(max.x, min.y, min.z),
			new Vector3(max.x, min.y, max.z),
			new Vector3(max.x, max.y, min.z),
			new Vector3(max.x, max.y, max.z)
		};
		bool flag = false;
		float num = 0f;
		float num2 = 0f;
		float num3 = 0f;
		float num4 = 0f;
		for (int i = 0; i < array.Length; i++)
		{
			Vector3 vector = cameraToUse.WorldToScreenPoint(array[i]);
			if (vector.z <= 0f)
			{
				continue;
			}
			if (!flag)
			{
				num = vector.x;
				num2 = vector.x;
				num3 = vector.y;
				num4 = vector.y;
				flag = true;
			}
			else
			{
				num = Mathf.Min(num, vector.x);
				num2 = Mathf.Max(num2, vector.x);
				num3 = Mathf.Min(num3, vector.y);
				num4 = Mathf.Max(num4, vector.y);
			}
		}
		if (!flag)
		{
			return false;
		}
		screenBounds = Rect.MinMaxRect(num, num3, num2, num4);
		return true;
	}

	private Plane GetGridRaycastPlane(Camera cameraToUse)
	{
		Vector3 val = (((Object)(object)parentGrid != (Object)null) ? parentGrid.position : Vector3.zero);
		if ((Object)(object)parentGrid != (Object)null && parentGrid.childCount > 0)
		{
			val = parentGrid.GetChild(0).position;
		}
		if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			return new Plane(Vector3.up, val);
		}
		if (roadSurfaceMode == RoadSurfaceMode.SplineUp && (Object)(object)cameraToUse != (Object)null)
		{
			Vector3 val2 = -((Component)cameraToUse).transform.forward;
			return new Plane((val2.sqrMagnitude > 0.0001f) ? val2.normalized : Vector3.forward, val);
		}
		return new Plane(Vector3.forward, val);
	}

	private bool TryGetGridWorldBounds(GridEdit grid, out Bounds bounds)
	{
		Renderer componentInChildren = ((Component)grid).GetComponentInChildren<Renderer>();
		if ((Object)(object)componentInChildren != (Object)null)
		{
			bounds = componentInChildren.bounds;
			return true;
		}
		Collider componentInChildren2 = ((Component)grid).GetComponentInChildren<Collider>();
		if ((Object)(object)componentInChildren2 != (Object)null)
		{
			bounds = componentInChildren2.bounds;
			return true;
		}
		Collider2D componentInChildren3 = ((Component)grid).GetComponentInChildren<Collider2D>();
		if ((Object)(object)componentInChildren3 != (Object)null)
		{
			bounds = componentInChildren3.bounds;
			return true;
		}
		bounds = default(Bounds);
		return false;
	}

	private bool ContainsPointOnGridPlane(Bounds bounds, Vector3 worldPoint)
	{
		float num = gridRaycastBoundsPadding;
		if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			if (worldPoint.x >= bounds.min.x - num && worldPoint.x <= bounds.max.x + num && worldPoint.z >= bounds.min.z - num)
			{
				return worldPoint.z <= bounds.max.z + num;
			}
			return false;
		}
		if (worldPoint.x >= bounds.min.x - num && worldPoint.x <= bounds.max.x + num && worldPoint.y >= bounds.min.y - num)
		{
			return worldPoint.y <= bounds.max.y + num;
		}
		return false;
	}

	private static bool TryResolveGridFromTransform(Transform hitTransform, out GridEdit grid)
	{
		grid = null;
		if ((Object)(object)hitTransform == (Object)null)
		{
			return false;
		}
		BusEdit componentInParent = ((Component)hitTransform).GetComponentInParent<BusEdit>();
		if ((Object)(object)componentInParent != (Object)null && (Object)(object)componentInParent.CurrentGrid != (Object)null)
		{
			grid = componentInParent.CurrentGrid;
			return true;
		}
		grid = ((Component)hitTransform).GetComponentInParent<GridEdit>();
		return (Object)(object)grid != (Object)null;
	}

	private void BindBusUiReferences()
	{
		if ((Object)(object)rectPopUpBus == (Object)null)
		{
			rectPopUpBus = FindSceneComponentByName<RectTransform>("PopUpBus");
		}
		if ((Object)(object)rectPopupColor == (Object)null)
		{
			rectPopupColor = FindSceneComponentByName<RectTransform>("PopupColor");
		}
		
		if ((Object)(object)tmpInputSlot == (Object)null && (Object)(object)rectPopUpBus != (Object)null)
		{
			tmpInputSlot = FindDeepContains<InputField>((Transform)(object)rectPopUpBus, "slot");
		}
		
		BindBusPopupButtons();
		BindColorPopupButtons();
		ShowColorPopup(show: false);
	}

	private void BindBusPopupButtons()
	{
		if ((Object)(object)rectPopUpBus == (Object)null)
		{
			return;
		}

		Button[] componentsInChildren = ((Component)rectPopUpBus).GetComponentsInChildren<Button>(true);
		foreach (Button button in componentsInChildren)
		{
			if ((Object)(object)button == (Object)null)
			{
				continue;
			}

			bool isSaveButton = HasButtonNameOrText(button, "save");
			bool isSpawnPersonButton = !isSaveButton && HasButtonNameOrText(button, "spawn");
			bool isDeleteButton = HasButtonNameOrText(button, "delete") || HasButtonNameOrText(button, "remove");
			bool isColorButton = HasButtonNameOrText(button, "color");

			if (isSaveButton)
			{
				AddClickListenerIfMissing(button, SetBus, nameof(SetBus));
				continue;
			}

			if (isSpawnPersonButton)
			{
				btnSpawnStickMan = button;
				AddClickListenerIfMissing(button, SpawnStickMan, nameof(SpawnStickMan));
				continue;
			}

			if (isDeleteButton)
			{
				btnDeleteStickMan = button;
				AddClickListenerIfMissing(button, ToggleDeleteStickManMode, nameof(ToggleDeleteStickManMode));
				continue;
			}

			if (isColorButton)
			{
				Image buttonImage = ((Component)button).GetComponent<Image>();
				if ((Object)(object)buttonImage != (Object)null)
				{
					imgBtnColor = buttonImage;
				}

				AddClickListenerIfMissing(button, ChoseColorBus, nameof(ChoseColorBus));
			}
		}

		UpdateDeleteStickManButtonVisual();
	}

	private static void AddClickListenerIfMissing(Button button, UnityAction action, string methodName)
	{
		if ((Object)(object)button == (Object)null || action == null)
		{
			return;
		}

		((UnityEvent)button.onClick).RemoveListener(action);
		if (!HasPersistentListener(button, methodName))
		{
			((UnityEvent)button.onClick).AddListener(action);
		}
	}

	private static bool HasPersistentListener(Button button, string methodName)
	{
		if ((Object)(object)button == (Object)null || string.IsNullOrWhiteSpace(methodName))
		{
			return false;
		}

		for (int i = 0; i < ((UnityEvent)button.onClick).GetPersistentEventCount(); i++)
		{
			if (((UnityEvent)button.onClick).GetPersistentMethodName(i) == methodName)
			{
				return true;
			}
		}

		return false;
	}

	private void BindColorPopupButtons()
	{
		if ((Object)(object)rectPopupColor == (Object)null)
		{
			return;
		}
		Button[] componentsInChildren = ((Component)rectPopupColor).GetComponentsInChildren<Button>(true);
		foreach (Button button in componentsInChildren)
		{
			if (!((Object)(object)button == (Object)null))
			{
				AddClickListenerIfMissing(button, SetColorBusFromClickedButton, nameof(SetColorBusFromClickedButton));
			}
		}
	}

	public void HandleGridLeftClick(GridEdit grid)
	{
		if (!toggleSpawnBus || (Object)(object)grid == (Object)null)
		{
			return;
		}
		selectedBusGrid = grid;
		ShowBusPopup(show: true);
		if ((Object)(object)grid.CurrentBus != (Object)null)
		{
			busEditObject = grid.CurrentBus;
			LoadBusToPopup(grid.CurrentBus);
			return;
		}
        busEditObject = SpawnBus(grid);
		if ((Object)(object)busEditObject != (Object)null)
		{
			LoadBusToPopup(busEditObject);
		}
	}

	public void HandleGridRightClick(GridEdit grid)
	{
		if (toggleSpawnBus && !((Object)(object)grid == (Object)null) && !((Object)(object)grid.CurrentBus == (Object)null))
		{
			BusEdit currentBus = grid.CurrentBus;
			grid.ClearBus(currentBus);
			if ((Object)(object)selectedBusGrid == (Object)(object)grid)
			{
				selectedBusGrid = null;
			}
			DestroyUnityObject((Object)(object)((Component)currentBus).gameObject);
		}
	}

	public void ToggleSpawnBus()
	{
		SetWaitingAreaDrawMode(false);
		SetWaitingPointSpawnMode(false);
		DisableDeleteStickManMode();
		toggleSpawnBus = !toggleSpawnBus;
		ShowBusPopup(toggleSpawnBus);
		if (toggleSpawnBus)
		{
			WriteBusPopupFields(0);
			draftBusColorIndex = savedBusColorIndex;
		}
		else
		{
			selectedBusGrid = null;
			ShowColorPopup(show: false);
		}
	}

	public void ChoseColorBus()
	{
		DisableDeleteStickManMode();
		colorPopupTarget = ColorPopupTarget.Bus;
		ShowColorPopup(show: true);
	}

	public void ChoseColorWait()
	{
		DisableDeleteStickManMode();
		colorPopupTarget = ColorPopupTarget.WaitingPoint;
		ShowColorPopup(show: true);
	}

	public void SetColorBusFromClickedButton()
	{
		DisableDeleteStickManMode();
		GameObject val = (((Object)(object)EventSystem.current != (Object)null) ? EventSystem.current.currentSelectedGameObject : null);
		if (!((Object)(object)val == (Object)null) && TryGetColorNumber(val, out var colorNumber))
		{
			if (colorPopupTarget == ColorPopupTarget.WaitingPoint)
			{
				ApplyWaitingPointColorChoice(colorNumber - 1, val);
			}
			else
			{
				ApplyBusColorChoice(colorNumber - 1, val);
			}
		}
	}

	public void SaveWaitingPointColor()
	{
		DisableDeleteStickManMode();
		savedWaitingPointColorIndex = Mathf.Max(-1, draftWaitingPointColorIndex);
		if ((Object)(object)selectedWaitingPoint != (Object)null)
		{
			selectedWaitingPoint.SetColor(savedWaitingPointColorIndex, waitingPointColorData);
			((Object)selectedWaitingPoint).name = "WaitingPoint_Color_" + savedWaitingPointColorIndex;
		}
		ShowColorPopup(show: false);
		ShowWaitPopup(false);
		SaveLevelJson();
	}

	public void SetBus()
	{
		DisableDeleteStickManMode();
		savedBusColorIndex = Mathf.Max(0, draftBusColorIndex);
		BusEdit targetBus = GetCurrentSelectedBus();
		if ((Object)(object)targetBus != (Object)null)
		{
			targetBus.SaveSeatState(savedBusColorIndex);
			savedSlotPerson = targetBus.SlotBusPerson;
			busEditObject = targetBus;
		}
		ShowColorPopup(show: false);
	}

	private BusEdit SpawnBus(GridEdit grid)
	{
		if ((Object)(object)grid == (Object)null)
		{
			return null;
		}
		if ((Object)(object)grid.CurrentBus != (Object)null)
		{
			return grid.CurrentBus;
		}
		BusEdit busEdit = CreateBusAt(((Component)grid).transform.position, ((Component)grid).transform.rotation);
		if ((Object)(object)busEdit == (Object)null)
		{
			return null;
		}
		busEdit.BusData(savedBusColorIndex, 0);
		busEdit.SetGrid(grid);
		grid.SetBus(busEdit);
		return busEdit;
	}

	private BusEdit CreateBusAt(Vector3 position, Quaternion rotation)
	{
		if ((Object)(object)prefabBus == (Object)null)
		{
			return null;
		}
		BusEdit obj = (((Object)(object)parentBus != (Object)null) ? Object.Instantiate<BusEdit>(prefabBus, parentBus) : Object.Instantiate<BusEdit>(prefabBus));
		((Component)obj).transform.position = position;
		return obj;
	}

	private void LoadBusToPopup(BusEdit busEdit)
	{
		if (!((Object)(object)busEdit == (Object)null))
		{
			savedBusColorIndex = Mathf.Max(0, busEdit.ColorIndex);
			draftBusColorIndex = savedBusColorIndex;
			savedSlotPerson = Mathf.Clamp(busEdit.SlotBusPerson, 0, BusEdit.DefaultSlotBus);
			busEditObject = busEdit;
			WriteBusPopupFields(0);
			ShowBusPopup(show: true);
		}
	}

	private void ApplyBusColorChoice(int zeroBasedColorIndex, GameObject clickedButton)
	{
		DisableDeleteStickManMode();
		colorPopupTarget = ColorPopupTarget.Bus;
		draftBusColorIndex = Mathf.Max(0, zeroBasedColorIndex);
		UpdateBusColorPreview(clickedButton);
		ShowColorPopup(show: false);
	}

	private void ApplyWaitingPointColorChoice(int zeroBasedColorIndex, GameObject clickedButton)
	{
		DisableDeleteStickManMode();
		colorPopupTarget = ColorPopupTarget.WaitingPoint;
		draftWaitingPointColorIndex = Mathf.Max(-1, zeroBasedColorIndex);
		UpdateWaitingPointColorPreview(clickedButton);
		if ((Object)(object)selectedWaitingPoint != (Object)null)
		{
			selectedWaitingPoint.SetColor(draftWaitingPointColorIndex, waitingPointColorData);
			((Object)selectedWaitingPoint).name = "WaitingPoint_Color_" + draftWaitingPointColorIndex;
		}
		ShowColorPopup(show: false);
	}

	private void UpdateBusColorPreview(GameObject clickedButton)
	{
		if (!((Object)(object)imgBtnColor == (Object)null) && !((Object)(object)clickedButton == (Object)null))
		{
			Image component = clickedButton.GetComponent<Image>();
			if ((Object)(object)component != (Object)null)
			{
				imgBtnColor.color = component.color;
			}
		}
	}

	private void UpdateWaitingPointColorPreview(GameObject clickedButton)
	{
		if ((Object)(object)imgWaitBtnColor == (Object)null)
		{
			return;
		}
		if ((Object)(object)clickedButton != (Object)null)
		{
			Image component = clickedButton.GetComponent<Image>();
			if ((Object)(object)component != (Object)null)
			{
				imgWaitBtnColor.color = component.color;
				return;
			}
		}

		imgWaitBtnColor.color = GetColorPreview(draftWaitingPointColorIndex);
	}

	private Color GetColorPreview(int colorIndex)
	{
		if (colorIndex == -1)
		{
			return Color.white;
		}

		int safeIndex = Mathf.Max(0, colorIndex);
		if ((Object)(object)waitingPointColorData != (Object)null && waitingPointColorData.matColor != null && waitingPointColorData.matColor.Length > 0)
		{
			Material material = waitingPointColorData.matColor[Mathf.Clamp(safeIndex, 0, waitingPointColorData.matColor.Length - 1)];
			if ((Object)(object)material != (Object)null)
			{
				if (material.HasProperty("_BaseColor"))
				{
					return material.GetColor("_BaseColor");
				}
				if (material.HasProperty("_Color"))
				{
					return material.GetColor("_Color");
				}
			}
		}

		Color[] fallbackColors =
		{
			Color.red,
			Color.green,
			Color.blue,
			Color.yellow,
			Color.magenta,
			Color.cyan
		};
		return fallbackColors[Mathf.Abs(safeIndex) % fallbackColors.Length];
	}

	private void WriteBusPopupFields(int slotPerson)
	{
		string value = Mathf.Clamp(slotPerson, 0, BusEdit.DefaultSlotBus).ToString();
		WriteInput(tmpInputSlot, value);
	}

	private static void WriteInput(InputField tmpInputField, string value)
	{
		
		if ((Object)(object)tmpInputField != (Object)null)
		{
			tmpInputField.text = value;
		}
	}

	private static int ReadSeatInput( InputField tmpInputField, int fallback)
	{
		string s = null;
		
		if ((Object)(object)tmpInputField != (Object)null)
		{
			s = tmpInputField.text;
		}
		if (int.TryParse(s, out var result))
		{
			return result;
		}
		return fallback;
	}

	private void ShowBusPopup(bool show)
	{
		if ((Object)(object)rectPopUpBus != (Object)null)
		{
			((Component)rectPopUpBus).gameObject.SetActive(show);
		}
	}

	private void ShowColorPopup(bool show)
	{
		if ((Object)(object)rectPopupColor != (Object)null)
		{
			((Component)rectPopupColor).gameObject.SetActive(show);
		}
	}

	private void ShowWaitPopup(bool show)
	{
		if ((Object)(object)rectPopupWait != (Object)null)
		{
			((Component)rectPopupWait).gameObject.SetActive(show);
		}
	}

	private static bool TryGetColorNumber(GameObject clickedButton, out int colorNumber)
	{
		colorNumber = 0;
		if ((Object)(object)clickedButton == (Object)null)
		{
			return false;
		}
		TMP_Text componentInChildren = clickedButton.GetComponentInChildren<TMP_Text>();
		if ((Object)(object)componentInChildren != (Object)null && TryExtractInt(componentInChildren.text, out colorNumber))
		{
			return true;
		}
		Text componentInChildren2 = clickedButton.GetComponentInChildren<Text>();
		if ((Object)(object)componentInChildren2 != (Object)null && TryExtractInt(componentInChildren2.text, out colorNumber))
		{
			return true;
		}
		return TryExtractInt(((Object)clickedButton).name, out colorNumber);
	}

	private static Button FindButtonByNameOrText(Transform root, params string[] texts)
	{
		if ((Object)(object)root == (Object)null || texts == null || texts.Length == 0)
		{
			return null;
		}

		Button[] buttons = ((Component)root).GetComponentsInChildren<Button>(true);
		for (int i = 0; i < buttons.Length; i++)
		{
			if ((Object)(object)buttons[i] == (Object)null)
			{
				continue;
			}

			for (int j = 0; j < texts.Length; j++)
			{
				if (HasButtonNameOrText(buttons[i], texts[j]) || HasButtonNameOrTextNormalized(buttons[i], texts[j]))
				{
					return buttons[i];
				}
			}
		}

		return null;
	}

	private static bool HasButtonNameOrText(Button button, string text)
	{
		if ((Object)(object)button == (Object)null || string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string value = text.ToLowerInvariant();
		if (((Object)button).name.ToLowerInvariant().Contains(value))
		{
			return true;
		}
		TMP_Text componentInChildren = ((Component)button).GetComponentInChildren<TMP_Text>();
		if ((Object)(object)componentInChildren != (Object)null && componentInChildren.text.ToLowerInvariant().Contains(value))
		{
			return true;
		}
		Text componentInChildren2 = ((Component)button).GetComponentInChildren<Text>();
		if ((Object)(object)componentInChildren2 != (Object)null)
		{
			return componentInChildren2.text.ToLowerInvariant().Contains(value);
		}
		return false;
	}

	private static bool HasButtonNameOrTextNormalized(Button button, string text)
	{
		if ((Object)(object)button == (Object)null || string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		string value = NormalizeUiSearchText(text);
		if (string.IsNullOrEmpty(value))
		{
			return false;
		}
		if (NormalizeUiSearchText(((Object)button).name).Contains(value))
		{
			return true;
		}

		TMP_Text tmpText = ((Component)button).GetComponentInChildren<TMP_Text>(true);
		if ((Object)(object)tmpText != (Object)null && NormalizeUiSearchText(tmpText.text).Contains(value))
		{
			return true;
		}

		Text textComponent = ((Component)button).GetComponentInChildren<Text>(true);
		return (Object)(object)textComponent != (Object)null && NormalizeUiSearchText(textComponent.text).Contains(value);
	}

	private static string NormalizeUiSearchText(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		string result = string.Empty;
		for (int i = 0; i < value.Length; i++)
		{
			if (char.IsLetterOrDigit(value[i]))
			{
				result += char.ToLowerInvariant(value[i]);
			}
		}
		return result;
	}

	private static bool TryExtractInt(string text, out int value)
	{
		value = 0;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		string text2 = string.Empty;
		for (int i = 0; i < text.Length; i++)
		{
			if (char.IsDigit(text[i]))
			{
				text2 += text[i];
			}
			else if (text2.Length > 0)
			{
				break;
			}
		}
		if (text2.Length > 0)
		{
			return int.TryParse(text2, out value);
		}
		return false;
	}

    //StickMan
    [Header("StickMan")]
    [SerializeField] private Button btnSpawnStickMan;
    private StickManEdit stickMan;

	public void SpawnStickMan()
	{
		DisableDeleteStickManMode();
		BusEdit targetBus = GetCurrentSelectedBus();
		if ((Object)(object)targetBus == (Object)null)
		{
			Debug.LogWarning("Chua chon bus de spawn stickman");
			return;
		}

		int spawnCount = Mathf.Clamp(ReadSeatInput(tmpInputSlot, 0), 0, BusEdit.DefaultSlotBus);
		if (spawnCount <= 0)
		{
			Debug.LogWarning("Nhap so stickman can spawn lon hon 0");
			return;
		}

		busEditObject = targetBus;
		int spawnedCount = targetBus.SpawnStickMan(spawnCount, Mathf.Max(0, draftBusColorIndex));
		if (spawnedCount == 0 && targetBus.SlotEmpty == 0)
		{
			Debug.Log("Bus da day stickman");
		}

		savedSlotPerson = targetBus.SlotBusPerson;
    }

	public void ToggleDeleteStickManMode()
	{
		SetDeleteStickManMode(!deleteStickManMode);
	}

	public bool TryDeleteStickMan(StickManEdit stickMan)
	{
		if (!deleteStickManMode || (Object)(object)stickMan == (Object)null)
		{
			return false;
		}

		BusEdit targetBus = stickMan.CurrentBus;
		if ((Object)(object)targetBus == (Object)null)
		{
			targetBus = ((Component)stickMan).GetComponentInParent<BusEdit>();
		}

		if ((Object)(object)targetBus == (Object)null)
		{
			return false;
		}

		bool removed = targetBus.RemoveStickMan(stickMan);
		if (removed)
		{
			busEditObject = targetBus;
			savedSlotPerson = targetBus.SlotBusPerson;
		}

		return removed;
	}

	private BusEdit GetCurrentSelectedBus()
	{
		if ((Object)(object)busEditObject != (Object)null)
		{
			return busEditObject;
		}

		if ((Object)(object)selectedBusGrid != (Object)null)
		{
			return selectedBusGrid.CurrentBus;
		}

		return null;
	}

	private void DisableDeleteStickManMode()
	{
		if (deleteStickManMode)
		{
			SetDeleteStickManMode(false);
		}
	}

	private void SetDeleteStickManMode(bool active)
	{
		deleteStickManMode = active;
		ShowColorPopup(show: false);
		UpdateDeleteStickManButtonVisual();
	}

	private void UpdateDeleteStickManButtonVisual()
	{
		if ((Object)(object)btnDeleteStickMan == (Object)null || (Object)(object)((Selectable)btnDeleteStickMan).targetGraphic == (Object)null)
		{
			return;
		}

		Graphic targetGraphic = ((Selectable)btnDeleteStickMan).targetGraphic;
		if (!hasDeleteStickManButtonDefaultColor)
		{
			deleteStickManButtonDefaultColor = targetGraphic.color;
			hasDeleteStickManButtonDefaultColor = true;
		}

		targetGraphic.color = deleteStickManMode ? new Color(1f, 0.45f, 0.45f, 1f) : deleteStickManButtonDefaultColor;
	}

#if UNITY_EDITOR
	public bool EditorTryGetDrawPoint(Ray worldRay, out Vector3 worldPoint)
	{
		worldPoint = Vector3.zero;
		Plane drawPlane = GetGameplayDrawPlane();
		if (!drawPlane.Raycast(worldRay, out float distance) || distance < 0f || distance > gridRaycastMaxDistance)
		{
			return false;
		}

		worldPoint = worldRay.GetPoint(distance);
		return true;
	}

	public bool EditorAddGameplaySplinePoint(Ray worldRay)
	{
		if ((Object)(object)splineControl == (Object)null)
		{
			SetRoadMessage("Chua keo SplineControl vao ToolLevel");
			return false;
		}

		if (!EditorTryGetDrawPoint(worldRay, out Vector3 worldPoint))
		{
			SetRoadMessage("Khong raycast duoc vao mat phang ve");
			return false;
		}

		Undo.RegisterCompleteObjectUndo(splineControl, "Draw Level Spline");
		Undo.RegisterCompleteObjectUndo(this, "Draw Level Spline");
		if (splineControl.Splines == null || splineControl.Splines.Count == 0)
		{
			splineControl.Splines = new List<Spline> { new Spline() };
			gameplayActiveSplineIndex = 0;
		}

		Spline activeSpline = GetGameplayActiveSpline();
		if (activeSpline == null)
		{
			SetRoadMessage("Khong tao duoc spline dang ve");
			return false;
		}

		Vector3 localPoint = ((Component)splineControl).transform.InverseTransformPoint(worldPoint);
		if (roadSurfaceMode == RoadSurfaceMode.XY)
		{
			localPoint.z = 0f;
		}
		else if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			localPoint.y = 0f;
		}

		if (activeSpline.Count > 0 && Vector3.Distance(ToVector3(activeSpline[activeSpline.Count - 1].Position), localPoint) < minGameplayPointDistance)
		{
			SetRoadMessage("Diem moi qua gan diem cu");
			return false;
		}

		activeSpline.Add(new BezierKnot(new float3(localPoint.x, localPoint.y, localPoint.z)), TangentMode.AutoSmooth);
		ApplyAutoSmooth(activeSpline);
		gameplaySplineDrawMode = false;
		renderAllSplines = true;
		MarkGameplaySplineEdited();
		RefreshGameplaySplinePointMarkers();
		if (renderGameplaySplineWhileDrawing && activeSpline.Count >= 2)
		{
			RenderRoadMesh();
		}
		SetRoadMessage("Da them diem " + activeSpline.Count + " vao spline " + gameplayActiveSplineIndex + " (Edit Mode)");
		return true;
	}

	public bool EditorAddWaitingAreaPoint(Ray worldRay)
	{
		if (!EditorTryGetDrawPoint(worldRay, out Vector3 worldPoint))
		{
			SetRoadMessage("Khong raycast duoc vao mat phang wait");
			return false;
		}
		if (roadSurfaceMode == RoadSurfaceMode.XY)
		{
			worldPoint.z = GetWaitingPlaneFixedValue();
		}
		else if (roadSurfaceMode == RoadSurfaceMode.XZ)
		{
			worldPoint.y = GetWaitingPlaneFixedValue();
		}

		if (waitingAreaPoints == null)
		{
			waitingAreaPoints = new List<Vector3>();
		}
		if (waitingAreaPoints.Count > 0 && Vector3.Distance(waitingAreaPoints[waitingAreaPoints.Count - 1], worldPoint) < waitingPointSpacing * 0.5f)
		{
			SetRoadMessage("Diem wait qua gan diem truoc");
			return false;
		}

		Undo.RegisterCompleteObjectUndo(this, "Draw Waiting Area");
		waitingAreaPoints.Add(worldPoint);
		RefreshWaitingAreaMarkers();
		MarkSplineDirty();
		SetRoadMessage("Da them diem vung wait " + waitingAreaPoints.Count + " (Edit Mode)");
		return true;
	}

	public void EditorClearWaitingArea()
	{
		Undo.RegisterCompleteObjectUndo(this, "Clear Waiting Area");
		if (waitingAreaPoints == null)
		{
			waitingAreaPoints = new List<Vector3>();
		}
		waitingAreaPoints.Clear();
		ClearWaitingCandidateMarkers();
		RefreshWaitingAreaMarkers();
		MarkSplineDirty();
		SetRoadMessage("Da xoa vung wait");
	}

	public WaitingPointEdit EditorPlaceWaitingPoint(GameObject pickedObject, Ray worldRay, int colorIndex)
	{
		return EditorPlaceWaitingPoint(pickedObject, worldRay, colorIndex, 1);
	}

	public WaitingPointEdit EditorPlaceWaitingPoint(GameObject pickedObject, Ray worldRay, int colorIndex, int direction)
	{
		Plane waitingPlane = new Plane(Vector3.up, new Vector3(0f, 2f, 0f));
		if (!waitingPlane.Raycast(worldRay, out float enter))
		{
			SetRoadMessage("Khong tim thay vi tri dat wait");
			return null;
		}

		Vector3 worldPoint = worldRay.GetPoint(enter);
		worldPoint.y = 2f;

		int safeColorIndex = Mathf.Max(-1, colorIndex);
		WaitingPointEdit waitingPoint = PlaceWaitingPoint(worldPoint, safeColorIndex);
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return null;
		}

		Undo.RegisterCreatedObjectUndo(((Component)waitingPoint).gameObject, "Place Waiting Point");
		waitingPoint.SetDirection(direction);
		SelectWaitingPoint(waitingPoint, showPopup: false);
		MarkSplineDirty();
		SetRoadMessage("Da dat wait tai " + worldPoint + ", Y = 2, Direction = " + waitingPoint.Direction);
		return waitingPoint;
	}

	public bool EditorSetWaitingPointColor(WaitingPointEdit waitingPoint, int colorIndex)
	{
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return false;
		}

		Undo.RegisterCompleteObjectUndo(((Component)waitingPoint).gameObject, "Set Waiting Point Color");
		int safeColorIndex = Mathf.Max(-1, colorIndex);
		waitingPoint.SetColor(safeColorIndex, waitingPointColorData);
		((Object)waitingPoint).name = "WaitingPoint_Color_" + safeColorIndex;
		RefreshWaitingPointStickManVisuals(waitingPoint);
		selectedWaitingPoint = waitingPoint;
		draftWaitingPointColorIndex = safeColorIndex;
		savedWaitingPointColorIndex = safeColorIndex;
		MarkSplineDirty();
		return true;
	}

	public bool EditorSetWaitingPointDirection(WaitingPointEdit waitingPoint, int direction)
	{
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return false;
		}

		Undo.RegisterCompleteObjectUndo(((Component)waitingPoint).gameObject, "Set Waiting Point Direction");
		waitingPoint.SetDirection(direction);
		selectedWaitingPoint = waitingPoint;
		EditorUtility.SetDirty(waitingPoint);
		MarkSplineDirty();
		SetRoadMessage("Da gan Direction = " + waitingPoint.Direction + " cho " + ((Object)waitingPoint).name);
		return true;
	}

	public bool EditorSetWaitingPointStickManColor(WaitingPointEdit waitingPoint, int index, int colorIndex)
	{
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return false;
		}

		Undo.RegisterCompleteObjectUndo(((Component)waitingPoint).gameObject, "Set Waiting Passenger Color");
		if (!waitingPoint.SetStickManColor(index, colorIndex))
		{
			return false;
		}

		EditorUtility.SetDirty(waitingPoint);
		RefreshWaitingPointStickManVisuals(waitingPoint);
		MarkSplineDirty();
		SetRoadMessage("Da doi mau passenger wait #" + index + " thanh " + Mathf.Max(0, colorIndex));
		return true;
	}

	public bool EditorAddWaitingPointStickMan(WaitingPointEdit waitingPoint, int colorIndex)
	{
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return false;
		}

		Undo.RegisterCompleteObjectUndo(((Component)waitingPoint).gameObject, "Add Waiting Passenger");
		waitingPoint.AddStickManColor(colorIndex);
		RefreshWaitingPointStickManVisuals(waitingPoint);
		EditorUtility.SetDirty(waitingPoint);
		MarkSplineDirty();
		SetRoadMessage("Da them passenger wait mau " + Mathf.Max(0, colorIndex));
		return true;
	}

	public int EditorAddWaitingPointStickMen(WaitingPointEdit waitingPoint, int count, int colorIndex)
	{
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return 0;
		}

		int safeCount = Mathf.Clamp(count, 0, 100);
		if (safeCount <= 0)
		{
			SetRoadMessage("So luong stickman phai lon hon 0");
			return 0;
		}
		int safeColorIndex = Mathf.Max(-1, colorIndex);
		Undo.RegisterCompleteObjectUndo(((Component)waitingPoint).gameObject, "Spawn Waiting Passengers");
		for (int i = 0; i < safeCount; i++)
		{
			waitingPoint.AddStickManColor(safeColorIndex);
		}

		selectedWaitingPoint = waitingPoint;
		RefreshWaitingPointStickManVisuals(waitingPoint);
		EditorUtility.SetDirty(waitingPoint);
		MarkSplineDirty();
		SetRoadMessage("Da them " + safeCount + " passenger wait mau " + safeColorIndex);
		return safeCount;
	}

	public bool EditorRemoveWaitingPointStickMan(WaitingPointEdit waitingPoint, int index)
	{
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return false;
		}

		Undo.RegisterCompleteObjectUndo(((Component)waitingPoint).gameObject, "Remove Waiting Passenger");
		if (!waitingPoint.RemoveStickManColorAt(index))
		{
			return false;
		}

		RefreshWaitingPointStickManVisuals(waitingPoint);
		EditorUtility.SetDirty(waitingPoint);
		MarkSplineDirty();
		SetRoadMessage("Da xoa passenger wait #" + index);
		return true;
	}

	public bool EditorDeleteWaitingPoint(WaitingPointEdit waitingPoint)
	{
		if ((Object)(object)waitingPoint == (Object)null)
		{
			return false;
		}
		if ((Object)(object)selectedWaitingPoint == (Object)(object)waitingPoint)
		{
			selectedWaitingPoint = null;
		}
		Undo.DestroyObjectImmediate(((Component)waitingPoint).gameObject);
		MarkSplineDirty();
		SetRoadMessage("Da xoa waiting point");
		return true;
	}

	public int EditorSpawnGrid(int width, int height)
	{
		if ((Object)(object)prefabGrid == (Object)null)
		{
			EnsureToolAssetReferences();
		}
		if ((Object)(object)parentGrid == (Object)null)
		{
			parentGrid = GetOrCreateSceneRoot("Grid");
		}
		if ((Object)(object)prefabGrid == (Object)null || (Object)(object)parentGrid == (Object)null)
		{
			SetRoadMessage("Chua gan Prefab Grid hoac Parent Grid");
			return 0;
		}

		int safeWidth = Mathf.Max(1, width);
		int safeHeight = Mathf.Max(1, height);
		int createdCount = 0;
		parentGrid.gameObject.SetActive(true);
		for (int row = 0; row < safeHeight; row++)
		{
			for (int column = 0; column < safeWidth; column++)
			{
				GridEdit grid = EditorInstantiateComponent(prefabGrid, parentGrid);
				if ((Object)(object)grid == (Object)null)
				{
					continue;
				}
				((Component)grid).gameObject.SetActive(true);
				((Component)grid).gameObject.name = "Grid_" + row + "_" + column;
			((Component)grid).transform.localPosition = new Vector3(((float)row - ((float)safeHeight - 1f) * 0.5f) * GridColumnSpacingX, GridSpawnY, FirstGridRowZ - column * GridRowSpacingZ);
				((Component)grid).transform.localRotation = Quaternion.identity;
				Undo.RegisterCreatedObjectUndo(((Component)grid).gameObject, "Spawn Level Grid");
				createdCount++;
			}
		}

		if (createdCount > 0)
		{
			EditorUtility.SetDirty(parentGrid);
		}
		SetEditorLevelFields(currentLevelId, safeWidth, safeHeight);
		MarkSplineDirty();
		SetRoadMessage("Da tao " + createdCount + " grid (Edit Mode)");
		return createdCount;
	}

	public BusEdit EditorPlaceOrUpdateBus(GridEdit grid, int colorIndex, int passengerCount)
	{
		if ((Object)(object)grid == (Object)null || (Object)(object)prefabBus == (Object)null)
		{
			SetRoadMessage("Khong tim thay Grid hoac Prefab Bus");
			return null;
		}

		int safeColorIndex = Mathf.Max(-1, colorIndex);
		BusEdit bus = grid.CurrentBus;
		if ((Object)(object)bus == (Object)null)
		{
			bus = EditorInstantiateComponent(prefabBus, parentBus);
			if ((Object)(object)bus == (Object)null)
			{
				return null;
			}
			((Component)bus).transform.SetPositionAndRotation(((Component)grid).transform.position, ((Component)grid).transform.rotation);
			bus.BusData(safeColorIndex, 0);
			bus.SetGrid(grid);
			grid.SetBus(bus);
			Undo.RegisterCreatedObjectUndo(((Component)bus).gameObject, "Place Level Bus");
		}
		else
		{
			Undo.RegisterCompleteObjectUndo(((Component)bus).gameObject, "Edit Level Bus");
			Undo.RegisterCompleteObjectUndo(grid, "Edit Level Bus");
			bus.ChangeColor(safeColorIndex);
		}

		int missingPassengers = Mathf.Clamp(passengerCount, 0, BusEdit.DefaultSlotBus) - bus.SlotBusPerson;
		if (missingPassengers > 0)
		{
			bus.SpawnStickMan(missingPassengers, safeColorIndex);
		}
		selectedBusGrid = grid;
		busEditObject = bus;
		draftBusColorIndex = safeColorIndex;
		savedBusColorIndex = safeColorIndex;
		savedSlotPerson = bus.SlotBusPerson;
		EditorUtility.SetDirty(grid);
		EditorUtility.SetDirty(bus);
		MarkSplineDirty();
		SetRoadMessage("Da dat/cap nhat bus (Edit Mode)");
		return bus;
	}

	public bool EditorUpdateBus(BusEdit bus, int colorIndex, int passengerCount)
	{
		if ((Object)(object)bus == (Object)null)
		{
			return false;
		}

		int safeColorIndex = Mathf.Max(0, colorIndex);
		Undo.RegisterCompleteObjectUndo(((Component)bus).gameObject, "Edit Level Bus");
		bus.ChangeColor(safeColorIndex);

		int missingPassengers = Mathf.Clamp(passengerCount, 0, BusEdit.DefaultSlotBus) - bus.SlotBusPerson;
		if (missingPassengers > 0)
		{
			bus.SpawnStickMan(missingPassengers, safeColorIndex);
		}

		selectedBusGrid = bus.CurrentGrid;
		busEditObject = bus;
		draftBusColorIndex = safeColorIndex;
		savedBusColorIndex = safeColorIndex;
		savedSlotPerson = bus.SlotBusPerson;
		EditorUtility.SetDirty(bus);
		MarkSplineDirty();
		SetRoadMessage("Da cap nhat bus " + ((Object)bus).name + " (Edit Mode)");
		return true;
	}

	public bool EditorDeleteBus(GridEdit grid)
	{
		if ((Object)(object)grid == (Object)null || (Object)(object)grid.CurrentBus == (Object)null)
		{
			return false;
		}
		return EditorDeleteBus(grid.CurrentBus);
	}

	public bool EditorDeleteBus(BusEdit bus)
	{
		if ((Object)(object)bus == (Object)null)
		{
			return false;
		}

		GridEdit grid = bus.CurrentGrid;
		if ((Object)(object)grid != (Object)null)
		{
			Undo.RegisterCompleteObjectUndo(grid, "Delete Level Bus");
			grid.ClearBus(bus);
			EditorUtility.SetDirty(grid);
		}
		if ((Object)(object)busEditObject == (Object)(object)bus)
		{
			selectedBusGrid = null;
			busEditObject = null;
		}

		Undo.DestroyObjectImmediate(((Component)bus).gameObject);
		MarkSplineDirty();
		SetRoadMessage("Da xoa bus (Edit Mode)");
		return true;
	}

	public bool EditorDeleteStickMan(StickManEdit targetStickMan)
	{
		if ((Object)(object)targetStickMan == (Object)null)
		{
			return false;
		}

		BusEdit bus = targetStickMan.CurrentBus;
		if ((Object)(object)bus == (Object)null)
		{
			bus = ((Component)targetStickMan).GetComponentInParent<BusEdit>();
		}
		if ((Object)(object)bus == (Object)null)
		{
			return false;
		}

		Undo.RegisterCompleteObjectUndo(((Component)bus).gameObject, "Delete Bus Passenger");
		bool removed = bus.RemoveStickMan(targetStickMan);
		if (removed)
		{
			EditorUtility.SetDirty(bus);
			MarkSplineDirty();
			SetRoadMessage("Da xoa stickman (Edit Mode)");
		}
		return removed;
	}

	public bool EditorSaveLevelJson(int levelId, int width, int height)
	{
		int safeLevelId = Mathf.Max(1, levelId);
		int safeWidth = Mathf.Max(1, width);
		int safeHeight = Mathf.Max(1, height);
		try
		{
			LevelData data = CaptureCurrentLevelData(safeLevelId);
			data.i = safeWidth;
			data.j = safeHeight;
			data.level.id = "Level_" + safeLevelId.ToString("D2");
			data.SyncDocumentFromRuntimeData();
			AppendSplineItems(data);
			Directory.CreateDirectory(LevelAssetFolder);
			string path = GetLevelAssetPath(safeLevelId);
			File.WriteAllText(path, data.ToJson());
			AssetDatabase.ImportAsset(path);
			AssetDatabase.Refresh();
			SetEditorLevelFields(safeLevelId, safeWidth, safeHeight);
			SetRoadMessage("Da save " + path + " (Edit Mode)");
			return true;
		}
		catch (System.Exception exception)
		{
			Debug.LogException(exception);
			SetRoadMessage("Khong save duoc Level" + safeLevelId);
			return false;
		}
	}

	/// <summary>
	/// Validates the authored bus/wait combinations using the exact shared
	/// predicates consumed by WaitController at runtime.
	/// </summary>
	public bool EditorValidateTransferSetup()
	{
		BusEdit[] buses = GetSceneBuses();
		WaitingPointEdit[] waitingPoints = GetSceneWaitingPoints();
		int unloadPairs = 0;
		int loadPairs = 0;

		for (int busIndex = 0; busIndex < buses.Length; busIndex++)
		{
			BusEdit bus = buses[busIndex];
			if (bus == null)
			{
				continue;
			}

			for (int waitIndex = 0; waitIndex < waitingPoints.Length; waitIndex++)
			{
				WaitingPointEdit waitingPoint = waitingPoints[waitIndex];
				if (waitingPoint == null)
				{
					continue;
				}

				int busToWaitCount = CountTransferableBusPassengers(bus, waitingPoint);
				if (busToWaitCount > 0)
				{
					unloadPairs++;
					continue;
				}

				int waitToBusCount = CountTransferableWaitingPassengers(bus, waitingPoint);
				if (waitToBusCount > 0)
				{
					loadPairs++;
				}
			}
		}

		int totalPairs = unloadPairs + loadPairs;
		if (totalPairs == 0)
		{
			SetRoadMessage("Transfer check: chua co cap Bus/Wait nao hop le theo rule runtime");
			return false;
		}

		SetRoadMessage(
			"Transfer check OK: " + totalPairs + " cap hop le (Bus -> Wait: " +
			unloadPairs + ", Wait -> Bus: " + loadPairs + ")");
		return true;
	}

	private static int CountTransferableBusPassengers(BusEdit bus, WaitingPointEdit waitingPoint)
	{
		if (bus == null || waitingPoint == null || bus.SeatStates == null)
		{
			return 0;
		}

		int count = 0;
		for (int seatIndex = bus.SeatStates.Count - 1; seatIndex >= 0; seatIndex--)
		{
			BusEdit.StickManSeatState seat = bus.SeatStates[seatIndex];
			if (seat != null && seat.occupied && seat.stickMan != null &&
				BusPassengerTransferRules.CanUnloadToWait(
					bus.ColorIndex,
					waitingPoint.ColorIndex,
					seat.colorIndex))
			{
				count++;
			}
		}

		return count;
	}

	private static int CountTransferableWaitingPassengers(BusEdit bus, WaitingPointEdit waitingPoint)
	{
		if (bus == null || waitingPoint == null)
		{
			return 0;
		}

		IReadOnlyList<int> passengerColors = waitingPoint.StickManColorIndices;
		int matchingPassengers = BusPassengerTransferRules.CountPassengersOfColor(
			passengerColors,
			waitingPoint.ColorIndex);
		return BusPassengerTransferRules.CanLoadFromWait(
			bus.ColorIndex,
			waitingPoint.ColorIndex,
			bus.SlotEmpty,
			matchingPassengers)
			? BusPassengerTransferRules.GetTransferCount(matchingPassengers, bus.SlotEmpty)
			: 0;
	}

	public bool EditorLoadLevelJson(int levelId, out Vector2Int gridSize)
	{
		gridSize = Vector2Int.one;
		int safeLevelId = Mathf.Max(1, levelId);
		string json = LoadLevelJsonText(safeLevelId);
		if (string.IsNullOrWhiteSpace(json))
		{
			SetRoadMessage("Khong tim thay Level" + safeLevelId + " trong Resources/Levels");
			return false;
		}

		try
		{
			LevelData data = LevelData.FromJson(json);
			if (data == null)
			{
				return false;
			}
			gridSize = new Vector2Int(Mathf.Max(1, data.i), Mathf.Max(1, data.j));
			SetEditorLevelFields(safeLevelId, gridSize.x, gridSize.y);
			RebuildGridLayoutForLoadedLevel(gridSize.x, gridSize.y);
			ApplyLevelData(data);
			MarkSplineDirty();
			SetRoadMessage("Da load Level" + safeLevelId + " (Edit Mode)");
			return true;
		}
		catch (System.Exception exception)
		{
			Debug.LogException(exception);
			SetRoadMessage("File Level" + safeLevelId + " sai format");
			return false;
		}
	}

	public Color EditorGetColorPreview(int colorIndex)
	{
		return GetColorPreview(colorIndex);
	}

	private void SetEditorLevelFields(int levelId, int width, int height)
	{
		currentLevelId = Mathf.Max(1, levelId);
		if ((Object)(object)levelInputField != (Object)null)
		{
			levelInputField.text = currentLevelId.ToString();
		}
		if ((Object)(object)levelTmpInputField != (Object)null)
		{
			levelTmpInputField.text = currentLevelId.ToString();
		}
		if ((Object)(object)widthField != (Object)null)
		{
			widthField.text = Mathf.Max(1, width).ToString();
		}
		if ((Object)(object)heightField != (Object)null)
		{
			heightField.text = Mathf.Max(1, height).ToString();
		}
		EditorUtility.SetDirty(this);
	}

	private static T EditorInstantiateComponent<T>(T prefab, Transform parent) where T : Component
	{
		if ((Object)(object)prefab == (Object)null)
		{
			return null;
		}

		GameObject instanceObject;
		if (PrefabUtility.IsPartOfPrefabAsset(((Component)prefab).gameObject))
		{
			instanceObject = (GameObject)PrefabUtility.InstantiatePrefab(((Component)prefab).gameObject, parent);
		}
		else
		{
			instanceObject = Object.Instantiate(((Component)prefab).gameObject, parent);
		}
		return (Object)(object)instanceObject == (Object)null ? null : instanceObject.GetComponent<T>();
	}
#endif




private Mesh BuildExtrudedRoadMesh(
		List<int> safeSplineIndices,
		List<float> splineLengths,
		Transform meshTransform,
		float width,
		float thickness,
		float topOffset,
		float bevel)
	{
		List<Vector3> vertices = new List<Vector3>();
		List<Vector3> normals = new List<Vector3>();
		List<Vector2> uvs = new List<Vector2>();
		List<int> triangles = new List<int>();

		float halfWidth = Mathf.Max(0.02f, width * 0.5f);
		float safeThickness = Mathf.Max(0.02f, thickness);
		float safeBevel = Mathf.Clamp(bevel, 0.005f, Mathf.Min(halfWidth * 0.45f, safeThickness * 0.45f));
		float sampleSpacing = Mathf.Max(0.12f, roadPieceLength * 0.5f);

		for (int pathIndex = 0; pathIndex < safeSplineIndices.Count; pathIndex++)
		{
			int splineIndex = safeSplineIndices[pathIndex];
			float splineLength = splineLengths[pathIndex];
			int sampleCount = Mathf.Clamp(Mathf.CeilToInt(splineLength / sampleSpacing) + 1, 2, Mathf.Max(2, maxRoadPieces * 4));
			int firstVertex = vertices.Count;

			for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
			{
				float progress = sampleCount <= 1 ? 0f : sampleIndex / (float)(sampleCount - 1);
				float distance = progress * splineLength;
				EvaluateRoadFrame(splineIndex, distance, splineLength, out Vector3 center, out Vector3 forward, out Vector3 side, out Vector3 surfaceNormal);
				center += Vector3.up * roadVisualYOffset;

				Vector3 leftBottom = center - side * halfWidth + surfaceNormal * (topOffset - safeThickness);
				Vector3 leftSide = center - side * halfWidth + surfaceNormal * (topOffset - safeBevel);
				Vector3 leftTop = center - side * (halfWidth - safeBevel) + surfaceNormal * topOffset;
				Vector3 rightTop = center + side * (halfWidth - safeBevel) + surfaceNormal * topOffset;
				Vector3 rightSide = center + side * halfWidth + surfaceNormal * (topOffset - safeBevel);
				Vector3 rightBottom = center + side * halfWidth + surfaceNormal * (topOffset - safeThickness);

				Vector3[] crossSection =
				{
					leftBottom,
					leftSide,
					leftTop,
					rightTop,
					rightSide,
					rightBottom
				};

				for (int sectionIndex = 0; sectionIndex < crossSection.Length; sectionIndex++)
				{
					vertices.Add(meshTransform.InverseTransformPoint(crossSection[sectionIndex]));
					normals.Add(meshTransform.InverseTransformDirection(surfaceNormal));
					uvs.Add(new Vector2(progress * Mathf.Max(1f, splineLength * 0.15f), sectionIndex / 5f));
				}
			}

			for (int sampleIndex = 0; sampleIndex < sampleCount - 1; sampleIndex++)
			{
				int current = firstVertex + sampleIndex * 6;
				int next = current + 6;

				for (int sectionIndex = 0; sectionIndex < 5; sectionIndex++)
				{
					int a = current + sectionIndex;
					int b = current + sectionIndex + 1;
					int c = next + sectionIndex + 1;
					int d = next + sectionIndex;
					triangles.Add(a);
					triangles.Add(c);
					triangles.Add(b);
					triangles.Add(a);
					triangles.Add(d);
					triangles.Add(c);
				}
			}

			int lastVertex = firstVertex + (sampleCount - 1) * 6;
			int startCap = vertices.Count;
			vertices.Add(meshTransform.InverseTransformPoint(EvaluateCapCenter(splineIndex, 0f, splineLength, topOffset - safeThickness * 0.5f)));
			normals.Add(Vector3.back);
			uvs.Add(Vector2.zero);
			for (int sectionIndex = 0; sectionIndex < 5; sectionIndex++)
			{
				triangles.Add(startCap);
				triangles.Add(firstVertex + sectionIndex);
				triangles.Add(firstVertex + sectionIndex + 1);
			}

			int endCap = vertices.Count;
			vertices.Add(meshTransform.InverseTransformPoint(EvaluateCapCenter(splineIndex, 1f, splineLength, topOffset - safeThickness * 0.5f)));
			normals.Add(Vector3.forward);
			uvs.Add(Vector2.one);
			for (int sectionIndex = 0; sectionIndex < 5; sectionIndex++)
			{
				triangles.Add(endCap);
				triangles.Add(lastVertex + sectionIndex + 1);
				triangles.Add(lastVertex + sectionIndex);
			}
		}

		Mesh mesh = new Mesh();
		mesh.name = generatedRoadName + "_ExtrudedMesh";
		if (vertices.Count > 65000)
		{
			mesh.indexFormat = IndexFormat.UInt32;
		}
		mesh.SetVertices(vertices);
		mesh.RecalculateNormals();
		mesh.SetUVs(0, uvs);
		mesh.SetTriangles(triangles, 0, true);
		mesh.RecalculateBounds();
		return mesh;
	}

	private Vector3 EvaluateCapCenter(int splineIndex, float progress, float splineLength, float offset)
	{
		EvaluateRoadFrame(splineIndex, progress * splineLength, splineLength, out Vector3 center, out Vector3 forward, out Vector3 side, out Vector3 surfaceNormal);
		return center + surfaceNormal * offset;
	}
}
#endif // UNITY_EDITOR
