using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ToolLevelEditorWindow : EditorWindow
{    private const float PortraitPreviewAspect = 1080f / 1920f;


    

    private enum SpiderButtonTone
    {
        Primary,
        Secondary,
        Danger,
        Active,
        Ghost
    }

    private enum LevelViewMode
    {
        SceneEditor,
        GameView
    }

    private struct PreviewViewState
    {
        public Vector3 pivot;
        public Quaternion rotation;
        public float distance;
        public float orthographicSize;
        public float aspect;
        public bool orthographic;
    }

    private enum SceneToolMode
    {
        None,
        PlaceWaitingPoint,
        PlaceBus,
        DeleteBus,
        DeleteWaitingPoint,
        DeleteStickMan
    }

    private ToolLevel toolLevel;
    private UnityEditor.Editor toolLevelInspector;
    private Vector2 scrollPosition;
    private SceneToolMode sceneToolMode;
    private bool showReferences;
    private int levelId = 1;
    private int gridWidth = 4;
    private int gridHeight = 4;
    private int busColorIndex;
    private int passengerCount;
    private int waitingColorIndex;
    private int waitingDirection = 1;
    private int waitingPassengerSpawnCount;
    private int waitingPassengerColorIndex;
    private WaitingPointEdit selectedWaitingPoint;
    private BusEdit selectedBus;
    private GameObject selectedEditObject;
    private GameObject previewCameraObject;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private Vector3 previewPivot;
    private Quaternion previewRotation = Quaternion.Euler(35f, 0f, 0f);
    private float previewDistance = 12f;
    private float previewOrthographicSize = 8f;
    private float previewAspect = PortraitPreviewAspect;
    private bool previewOrthographic;
    private bool previewInitialized;
    private int previewDragButton = -1;
    private LevelViewMode levelViewMode = LevelViewMode.GameView;
    private PreviewViewState gameViewState;
    private PreviewViewState sceneViewState;
    private bool gameViewStateInitialized;
    private bool sceneViewStateInitialized;
private Rect embeddedViewRect;
    private Rect previewImageRect;
    
    private string workspaceStatus;
    private Scene standaloneWorkspaceScene;


    
[MenuItem("Tools/Level Tool", false, 1)]
    public static void Open()
    {
        ToolLevelEditorWindow window = GetWindow<ToolLevelEditorWindow>();
        window.titleContent = new GUIContent("Level Tool");
        window.minSize = new Vector2(900f, 640f);
        Rect windowPosition = window.position;
        windowPosition.width = Mathf.Max(windowPosition.width, 1100f);
        windowPosition.height = Mathf.Max(windowPosition.height, 720f);
        window.position = windowPosition;
        window.Show();
        window.Focus();
    }

    private void OnEnable()
    {
        sceneToolMode = SceneToolMode.None;
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        EnsureToolWorkspaceLoaded();
        previewInitialized = false;
    }

private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
        ReleasePreviewResources();
        DisposeStandaloneWorkspace();
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }

    private void OnHierarchyChanged()
    {
        if (toolLevel == null)
        {
            EnsureToolWorkspaceLoaded();
        }
        Repaint();
    }

    private void OnUndoRedo()
    {
        if (toolLevel != null)
        {
            toolLevel.RefreshGameplaySplinePointMarkers();
            toolLevel.EditorRefreshWaitingPointStickManVisuals();
        }
        SceneView.RepaintAll();
        Repaint();
    }

    private void OnGUI()
    {
        if (toolLevel == null && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EnsureToolWorkspaceLoaded();
        }

        DrawSpiderCanvas();
        DrawHeader();
        float contentHeight = Mathf.Max(420f, position.height - 96f);
        float controlsWidth = Mathf.Clamp(position.width * 0.32f, 380f, 520f);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(SpiderStyles.Panel, GUILayout.ExpandWidth(true), GUILayout.Height(contentHeight)))
            {
                if (toolLevel == null)
                {
                    GUILayout.FlexibleSpace();
                    DrawSpiderInfoBox("WORKSPACE", string.IsNullOrEmpty(workspaceStatus)
                        ? "Dang nap Level Tool workspace..."
                        : workspaceStatus, SpiderStyles.Red);
                    GUILayout.FlexibleSpace();
                }
                else if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    GUILayout.FlexibleSpace();
                    DrawSpiderInfoBox("EDIT MODE ONLY", "Hay Stop Play Mode de dung Level View.", SpiderStyles.Blue);
                    GUILayout.FlexibleSpace();
                }
                else
                {
                    DrawEmbeddedLevelView(contentHeight - 8f);
                }
            }

            using (new EditorGUILayout.VerticalScope(SpiderStyles.Panel, GUILayout.Width(controlsWidth), GUILayout.Height(contentHeight)))
            {
                DrawTargetPicker();
                if (toolLevel == null)
                {
                    DrawSpiderInfoBox("AUTO WORKSPACE", "Level Tool tu dong nap workspace rieng va khong can chuyen scene thu cong.", SpiderStyles.Blue);
                    if (SpiderButton("Retry Load Level Workspace", SpiderButtonTone.Primary, GUILayout.Height(32f)))
                    {
                        EnsureToolWorkspaceLoaded();
                    }
                }
                else if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    DrawSpiderInfoBox("EDIT MODE ONLY", "Level Tool duoc thiet ke de dung o Edit Mode. Hay Stop Play Mode truoc.", SpiderStyles.Blue);
                }
                else
                {
                    scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
                    DrawStatus();
                    DrawLevelSection();
                    DrawWaitingSection();
                    DrawBusSection();
                    DrawReferencesSection();
                    EditorGUILayout.EndScrollView();
                }
            }
        }
    }

    private void DrawHeader()
    {
        Rect headerRect = EditorGUILayout.GetControlRect(false, 82f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(headerRect, SpiderStyles.Header);
        DrawWebPattern(headerRect, new Color(0.19f, 0.69f, 1f, 0.16f));

        Rect redStripe = new Rect(headerRect.x, headerRect.y, 7f, headerRect.height);
        EditorGUI.DrawRect(redStripe, SpiderStyles.Red);
        EditorGUI.DrawRect(new Rect(headerRect.x, headerRect.yMax - 3f, headerRect.width, 3f), SpiderStyles.Blue);
        GUI.Label(new Rect(headerRect.x + 20f, headerRect.y + 12f, headerRect.width - 40f, 29f), "SPIDER // LEVEL LAB", SpiderStyles.HeaderTitle);
        GUI.Label(new Rect(headerRect.x + 22f, headerRect.y + 43f, headerRect.width - 44f, 19f),
            "MULTIVERSE LEVEL EDITOR  •  EDIT MODE  •  AUTO WORKSPACE", SpiderStyles.HeaderSubtitle);
    }

    private void DrawSceneToolbar(Rect viewRect)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        const float toolbarHeight = 31f;
        const float buttonHeight = 24f;
        float toolbarWidth = Mathf.Max(260f, viewRect.width - 16f);
        Rect topToolbarRect = new Rect(viewRect.x + 8f, viewRect.y + 8f, toolbarWidth, toolbarHeight);
        EditorGUI.DrawRect(topToolbarRect, new Color(0.02f, 0.07f, 0.14f, 0.96f));
        EditorGUI.DrawRect(new Rect(topToolbarRect.x, topToolbarRect.y, topToolbarRect.width, 2f), SpiderStyles.Blue);

        float cursorX = topToolbarRect.x + 4f;
        float buttonY = topToolbarRect.y + 3f;
        string pivotLabel = Tools.pivotMode == PivotMode.Pivot ? "Pivot" : "Center";
        if (DrawToolbarButton(new Rect(cursorX, buttonY, 50f, buttonHeight), pivotLabel,
            Tools.pivotMode == PivotMode.Pivot, "Toggle Pivot / Center"))
        {
            Tools.pivotMode = Tools.pivotMode == PivotMode.Pivot ? PivotMode.Center : PivotMode.Pivot;
            Repaint();
        }
        cursorX += 54f;

        string rotationLabel = Tools.pivotRotation == PivotRotation.Local ? "Local" : "Global";
        if (DrawToolbarButton(new Rect(cursorX, buttonY, 52f, buttonHeight), rotationLabel,
            Tools.pivotRotation == PivotRotation.Local, "Toggle Local / Global rotation"))
        {
            Tools.pivotRotation = Tools.pivotRotation == PivotRotation.Local
                ? PivotRotation.Global
                : PivotRotation.Local;
            Repaint();
        }
        cursorX += 56f;

        bool showGrid = sceneView != null && sceneView.showGrid;
        if (DrawToolbarButton(new Rect(cursorX, buttonY, 42f, buttonHeight), "Grid", showGrid,
            "Show / hide SceneView grid") && sceneView != null)
        {
            sceneView.showGrid = !showGrid;
            SceneView.RepaintAll();
        }
        cursorX += 46f;

        bool snapEnabled = EditorSnapSettings.gridSnapEnabled;
        if (DrawToolbarButton(new Rect(cursorX, buttonY, 42f, buttonHeight), "Snap", snapEnabled,
            "Enable / disable grid snapping"))
        {
            EditorSnapSettings.gridSnapEnabled = !snapEnabled;
            SceneView.RepaintAll();
        }
        cursorX += 46f;

        bool in2D = sceneView != null && sceneView.in2DMode;
        if (DrawToolbarButton(new Rect(cursorX, buttonY, 32f, buttonHeight), "2D", in2D,
            "Toggle SceneView 2D mode") && sceneView != null)
        {
            sceneView.in2DMode = !in2D;
            SceneView.RepaintAll();
        }
        cursorX += 36f;

        bool lighting = sceneView != null && sceneView.sceneLighting;
        if (DrawToolbarButton(new Rect(cursorX, buttonY, 40f, buttonHeight), "Light", lighting,
            "Toggle SceneView lighting") && sceneView != null)
        {
            sceneView.sceneLighting = !lighting;
            SceneView.RepaintAll();
        }
        cursorX += 44f;

        bool audio = sceneView != null && sceneView.audioPlay;
        if (DrawToolbarButton(new Rect(cursorX, buttonY, 40f, buttonHeight), "Audio", audio,
            "Toggle SceneView audio") && sceneView != null)
        {
            sceneView.audioPlay = !audio;
            SceneView.RepaintAll();
        }
        cursorX += 44f;

        bool gizmos = sceneView != null && sceneView.drawGizmos;
        if (DrawToolbarButton(new Rect(cursorX, buttonY, 48f, buttonHeight), "Gizmos", gizmos,
            "Show / hide SceneView gizmos") && sceneView != null)
        {
            sceneView.drawGizmos = !gizmos;
            SceneView.RepaintAll();
        }

        Rect toolRailRect = new Rect(viewRect.x + 8f, viewRect.y + 45f, 34f, 178f);
        EditorGUI.DrawRect(toolRailRect, new Color(0.02f, 0.07f, 0.14f, 0.96f));
        EditorGUI.DrawRect(new Rect(toolRailRect.x, toolRailRect.y, 2f, toolRailRect.height), SpiderStyles.Blue);
        float toolX = toolRailRect.x + 4f;
        float toolY = toolRailRect.y + 4f;
        DrawSceneToolButton(new Rect(toolX, toolY, 26f, 24f), "Q", Tool.View, "View / hand tool");
        DrawSceneToolButton(new Rect(toolX, toolY + 28f, 26f, 24f), "W", Tool.Move, "Move tool");
        DrawSceneToolButton(new Rect(toolX, toolY + 56f, 26f, 24f), "E", Tool.Rotate, "Rotate tool");
        DrawSceneToolButton(new Rect(toolX, toolY + 84f, 26f, 24f), "R", Tool.Scale, "Scale tool");
        DrawSceneToolButton(new Rect(toolX, toolY + 112f, 26f, 24f), "T", Tool.Rect, "Rect tool");
        DrawSceneToolButton(new Rect(toolX, toolY + 140f, 26f, 24f), "Y", Tool.Transform, "Universal transform tool");
    }

    private static void DrawSceneToolButton(Rect rect, string label, Tool tool, string tooltip)
    {
        if (GUI.Button(rect, new GUIContent(label, tooltip),
            SpiderStyles.GetButton(Tools.current == tool ? SpiderButtonTone.Active : SpiderButtonTone.Ghost)))
        {
            Tools.current = tool;
        }
    }

    private static bool DrawToolbarButton(Rect rect, string label, bool active, string tooltip)
    {
        return GUI.Button(rect, new GUIContent(label, tooltip),
            SpiderStyles.GetButton(active ? SpiderButtonTone.Active : SpiderButtonTone.Ghost));
    }

    private void DrawSpiderCanvas()
    {
        Rect canvas = new Rect(0f, 0f, position.width, position.height);
        EditorGUI.DrawRect(canvas, SpiderStyles.Background);
        DrawWebPattern(new Rect(position.width - 260f, position.height - 220f, 280f, 240f), new Color(0.19f, 0.69f, 1f, 0.07f));
    }

    private static void DrawWebPattern(Rect rect, Color color)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        Handles.BeginGUI();
        Color previousColor = Handles.color;
        Handles.color = color;
        Vector3 center = new Vector3(rect.xMax, rect.yMax, 0f);
        float maxRadius = Mathf.Max(rect.width, rect.height);
        for (float radius = 28f; radius <= maxRadius; radius += 26f)
        {
            Handles.DrawWireArc(center, Vector3.forward, Vector3.left, 90f, radius);
        }

        for (int i = 0; i <= 8; i++)
        {
            float angle = Mathf.Lerp(180f, 270f, i / 8f) * Mathf.Deg2Rad;
            Vector3 edge = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * maxRadius;
            Handles.DrawLine(center, edge);
        }
        Handles.color = previousColor;
        Handles.EndGUI();
    }

    private static void DrawSpiderInfoBox(string title, string message, Color accent)
    {
        using (new EditorGUILayout.VerticalScope(SpiderStyles.InfoBox))
        {
            Rect titleRect = EditorGUILayout.GetControlRect(false, 16f);
            EditorGUI.DrawRect(new Rect(titleRect.x, titleRect.y, 3f, titleRect.height), accent);
            GUI.Label(new Rect(titleRect.x + 8f, titleRect.y, titleRect.width - 8f, titleRect.height), title, SpiderStyles.InfoHeading);
            GUILayout.Label(message, SpiderStyles.InfoBody);
        }
    }

    private static bool SpiderButton(string label, SpiderButtonTone tone, params GUILayoutOption[] options)
    {
        return GUILayout.Button(label, SpiderStyles.GetButton(tone), options);
    }

    private static int DrawSpiderIntField(string label, int value)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(label, SpiderStyles.FieldLabel, GUILayout.Width(140f));
            return EditorGUILayout.IntField(value, SpiderStyles.NumberField, GUILayout.Height(24f));
        }
    }

    private static void DrawSpiderViewportFrame(Rect rect)
    {
        const float border = 2f;
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, border), SpiderStyles.Blue);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMax - border, rect.width, border), SpiderStyles.Blue);
        EditorGUI.DrawRect(new Rect(rect.xMin, rect.yMin, border, rect.height), SpiderStyles.Blue);
        EditorGUI.DrawRect(new Rect(rect.xMax - border, rect.yMin, border, rect.height), SpiderStyles.Blue);
    }

    private static class SpiderStyles
    {
        public static readonly Color Background = new Color(0.015f, 0.035f, 0.08f, 1f);
        public static readonly Color Header = new Color(0.025f, 0.095f, 0.19f, 1f);
        public static readonly Color SectionSurface = new Color(0.045f, 0.12f, 0.23f, 1f);
        public static readonly Color Viewport = new Color(0.01f, 0.025f, 0.055f, 1f);
        public static readonly Color Red = new Color(0.89f, 0.08f, 0.13f, 1f);
        public static readonly Color Blue = new Color(0.19f, 0.69f, 1f, 1f);

        private static GUIStyle panel;
        private static GUIStyle headerTitle;
        private static GUIStyle headerSubtitle;
        private static GUIStyle sectionTitle;
        private static GUIStyle fieldLabel;
        private static GUIStyle workspaceValue;
        private static GUIStyle infoBox;
        private static GUIStyle infoHeading;
        private static GUIStyle infoBody;
        private static GUIStyle overlayTitle;
        private static GUIStyle overlayBody;
        private static GUIStyle numberField;
        private static GUIStyle primaryButton;
        private static GUIStyle secondaryButton;
        private static GUIStyle dangerButton;
        private static GUIStyle activeButton;
        private static GUIStyle ghostButton;
public static GUIStyle Panel => panel ?? (panel = CreatePanelStyle());
        public static GUIStyle HeaderTitle => headerTitle ?? (headerTitle = CreateLabelStyle(23, FontStyle.Bold, new Color(0.95f, 0.98f, 1f, 1f), TextAnchor.MiddleLeft));
        public static GUIStyle HeaderSubtitle => headerSubtitle ?? (headerSubtitle = CreateLabelStyle(10, FontStyle.Bold, new Color(0.56f, 0.78f, 0.96f, 1f), TextAnchor.MiddleLeft));
        public static GUIStyle SectionTitle => sectionTitle ?? (sectionTitle = CreateLabelStyle(12, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft));
        public static GUIStyle FieldLabel => fieldLabel ?? (fieldLabel = CreateLabelStyle(11, FontStyle.Bold, new Color(0.45f, 0.75f, 1f, 1f), TextAnchor.MiddleLeft));
        public static GUIStyle WorkspaceValue => workspaceValue ?? (workspaceValue = CreateLabelStyle(11, FontStyle.Bold, new Color(0.91f, 0.96f, 1f, 1f), TextAnchor.MiddleLeft));
        public static GUIStyle InfoBox => infoBox ?? (infoBox = CreateInfoBoxStyle());
        public static GUIStyle InfoHeading => infoHeading ?? (infoHeading = CreateLabelStyle(10, FontStyle.Bold, new Color(0.44f, 0.74f, 1f, 1f), TextAnchor.MiddleLeft));
        public static GUIStyle InfoBody => infoBody ?? (infoBody = CreateInfoBodyStyle());
        public static GUIStyle OverlayTitle => overlayTitle ?? (overlayTitle = CreateLabelStyle(11, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft));
        public static GUIStyle OverlayBody => overlayBody ?? (overlayBody = CreateLabelStyle(10, FontStyle.Normal, new Color(0.82f, 0.9f, 1f, 1f), TextAnchor.MiddleLeft));
        public static GUIStyle NumberField => numberField ?? (numberField = CreateNumberFieldStyle());
public static GUIStyle GetButton(SpiderButtonTone tone)
        {
            switch (tone)
            {
                case SpiderButtonTone.Primary:
                    return primaryButton ?? (primaryButton = CreateButtonStyle(Red, new Color(1f, 0.18f, 0.23f, 1f), new Color(0.61f, 0.035f, 0.06f, 1f), Color.white));
                case SpiderButtonTone.Secondary:
                    return secondaryButton ?? (secondaryButton = CreateButtonStyle(new Color(0.04f, 0.28f, 0.53f, 1f), new Color(0.08f, 0.4f, 0.72f, 1f), new Color(0.025f, 0.17f, 0.34f, 1f), Color.white));
                case SpiderButtonTone.Danger:
                    return dangerButton ?? (dangerButton = CreateButtonStyle(new Color(0.38f, 0.045f, 0.075f, 1f), new Color(0.68f, 0.06f, 0.1f, 1f), new Color(0.21f, 0.025f, 0.04f, 1f), new Color(1f, 0.91f, 0.92f, 1f)));
                case SpiderButtonTone.Active:
                    return activeButton ?? (activeButton = CreateButtonStyle(Blue, new Color(0.38f, 0.8f, 1f, 1f), new Color(0.06f, 0.42f, 0.72f, 1f), new Color(0.01f, 0.04f, 0.09f, 1f)));
                default:
                    return ghostButton ?? (ghostButton = CreateButtonStyle(new Color(0.055f, 0.13f, 0.24f, 1f), new Color(0.09f, 0.22f, 0.4f, 1f), new Color(0.025f, 0.075f, 0.14f, 1f), new Color(0.86f, 0.93f, 1f, 1f)));
            }
        }

        private static GUIStyle CreatePanelStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(8, 8, 8, 8),
                padding = new RectOffset(12, 12, 10, 12),
                border = new RectOffset(1, 1, 1, 1)
            };
            style.normal.background = CreateTexture(new Color(0.025f, 0.075f, 0.14f, 0.98f));
            return style;
        }

        private static GUIStyle CreateInfoBoxStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(0, 0, 5, 5),
                padding = new RectOffset(10, 10, 7, 7),
                wordWrap = true
            };
            style.normal.background = CreateTexture(new Color(0.02f, 0.065f, 0.12f, 1f));
            return style;
        }

        private static GUIStyle CreateInfoBodyStyle()
        {
            GUIStyle style = CreateLabelStyle(11, FontStyle.Normal, new Color(0.84f, 0.91f, 1f, 1f), TextAnchor.UpperLeft);
            style.wordWrap = true;
            style.padding = new RectOffset(0, 0, 2, 0);
            return style;
        }



        private static GUIStyle CreateNumberFieldStyle()
        {
            GUIStyle style = new GUIStyle(EditorStyles.numberField)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 3, 3)
            };
            style.normal.background = CreateTexture(new Color(0.015f, 0.045f, 0.09f, 1f));
            style.hover.background = CreateTexture(new Color(0.03f, 0.1f, 0.19f, 1f));
            style.focused.background = CreateTexture(new Color(0.055f, 0.16f, 0.28f, 1f));
            style.normal.textColor = new Color(0.94f, 0.98f, 1f, 1f);
            style.hover.textColor = style.normal.textColor;
            style.focused.textColor = style.normal.textColor;
            return style;
        }

        private static GUIStyle CreateLabelStyle(int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(EditorStyles.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment,
                wordWrap = false
            };
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.focused.textColor = color;
            return style;
        }

        private static GUIStyle CreateButtonStyle(Color normal, Color hover, Color active, Color text)
        {
            GUIStyle style = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 11,
                border = new RectOffset(1, 1, 1, 1),
                padding = new RectOffset(8, 8, 4, 4),
                margin = new RectOffset(2, 2, 2, 2),
                clipping = TextClipping.Clip,
                wordWrap = false
            };
            style.normal.background = CreateTexture(normal);
            style.hover.background = CreateTexture(hover);
            style.active.background = CreateTexture(active);
            style.focused.background = CreateTexture(new Color(0.1f, 0.72f, 1f, 1f));
            style.onNormal.background = style.normal.background;
            style.onHover.background = style.hover.background;
            style.onActive.background = style.active.background;
            style.onFocused.background = style.focused.background;
            style.normal.textColor = text;
            style.hover.textColor = text;
            style.active.textColor = text;
            style.focused.textColor = new Color(0.01f, 0.04f, 0.09f, 1f);
            style.onNormal.textColor = text;
            style.onHover.textColor = text;
            style.onActive.textColor = text;
            style.onFocused.textColor = new Color(0.01f, 0.04f, 0.09f, 1f);
            return style;
        }

        private static Texture2D CreateTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }

    private void DrawTargetPicker()
    {
        DrawSectionTitle("WORKSPACE LINK");
        string workspaceName = toolLevel == null ? "Not loaded" : "Standalone Preview (memory)";
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField("Level Workspace", SpiderStyles.FieldLabel, GUILayout.Width(126f));
            EditorGUILayout.LabelField(workspaceName, SpiderStyles.WorkspaceValue);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (SpiderButton("Reconnect", SpiderButtonTone.Secondary, GUILayout.Height(30f)))
            {
                EnsureToolWorkspaceLoaded();
            }
            using (new EditorGUI.DisabledScope(toolLevel == null))
            {
                if (SpiderButton("Frame Level", SpiderButtonTone.Ghost, GUILayout.Height(30f)))
                {
                    FrameLevelInPreview();
                }
            }
        }
    }

    private void DrawStatus()
    {
        string modeText = GetModeInstruction();
        DrawSpiderInfoBox(sceneToolMode == SceneToolMode.None ? "READY" : "ACTIVE MODE", modeText,
            sceneToolMode == SceneToolMode.None ? SpiderStyles.Blue : SpiderStyles.Red);
        if (!string.IsNullOrWhiteSpace(toolLevel.lastRoadRenderMessage))
        {
            DrawSpiderInfoBox("STATUS", toolLevel.lastRoadRenderMessage, SpiderStyles.Blue);
        }
    }

    private void DrawLevelSection()
    {
        DrawSectionTitle("LEVEL DATA");
        levelId = Mathf.Max(1, DrawSpiderIntField("Level ID", levelId));
        gridWidth = Mathf.Max(1, DrawSpiderIntField("Grid Width", gridWidth));
        gridHeight = Mathf.Max(1, DrawSpiderIntField("Grid Height", gridHeight));

        using (new EditorGUILayout.HorizontalScope())
        {
            if (SpiderButton("Spawn Grid", SpiderButtonTone.Secondary, GUILayout.Height(30f)))
            {
                int createdGridCount = toolLevel.EditorSpawnGrid(gridWidth, gridHeight);
                if (createdGridCount > 0)
                {
                    FrameLevelInPreview();
                }
                FinishAction();
            }
            if (SpiderButton("Load Level", SpiderButtonTone.Ghost, GUILayout.Height(30f)))
            {
                RegisterSceneUndo("Load Level");
                if (toolLevel.EditorLoadLevelJson(levelId, out Vector2Int loadedSize))
                {
                    gridWidth = loadedSize.x;
                    gridHeight = loadedSize.y;
                    FrameLevelInPreview();
                }
                FinishAction();
            }
        }
        if (SpiderButton("Save Level JSON", SpiderButtonTone.Primary, GUILayout.Height(32f)))
        {
            toolLevel.EditorSaveLevelJson(levelId, gridWidth, gridHeight);
            FinishAction();
        }
        if (SpiderButton("Check Bus Transfer", SpiderButtonTone.Secondary, GUILayout.Height(28f)))
        {
            toolLevel.EditorValidateTransferSetup();
            FinishAction();
        }
        EditorGUILayout.LabelField(
            "Kiem tra dung rule runtime: mau, capacity, passenger va cap Bus/Wait.",
            EditorStyles.miniLabel);
    }

    private void DrawWaitingSection()
    {
        DrawSectionTitle("WAITING POINTS");
        EditorGUI.BeginChangeCheck();
        waitingColorIndex = Mathf.Max(-1, DrawSpiderIntField("Wait Color Index (-1 = Default)", waitingColorIndex));
        if (EditorGUI.EndChangeCheck() && selectedWaitingPoint != null)
        {
            toolLevel.EditorSetWaitingPointColor(selectedWaitingPoint, waitingColorIndex);
            FinishAction();
        }
        Rect colorRect = EditorGUILayout.GetControlRect(false, 18f);
        EditorGUI.DrawRect(colorRect, toolLevel.EditorGetColorPreview(waitingColorIndex));

        EditorGUILayout.LabelField("Direction", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawDirectionButton("Left (-2)", -2);
            DrawDirectionButton("Top (1)", 1);
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            DrawDirectionButton("Bottom (-1)", -1);
            DrawDirectionButton("Right (2)", 2);
        }

        if (selectedWaitingPoint != null)
        {
            DrawSpiderInfoBox("SELECTED WAIT", selectedWaitingPoint.name + "  •  Direction " + selectedWaitingPoint.Direction, SpiderStyles.Blue);
            DrawWaitingPassengerSpawner();
            DrawWaitingPassengerColors();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawModeButton(SceneToolMode.PlaceWaitingPoint, "Place Wait");
            DrawModeButton(SceneToolMode.DeleteWaitingPoint, "Delete Wait");
        }
        EditorGUILayout.LabelField("Place Wait: click vi tri bat ky, object luon co Y = 2. Click Wait da co de chinh sua.", EditorStyles.miniLabel);
    }

    private void DrawWaitingPassengerSpawner()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("SPAWN STICKMAN FOR SELECTED WAIT", EditorStyles.boldLabel);
        waitingPassengerSpawnCount = Mathf.Clamp(
            DrawSpiderIntField("Quantity", waitingPassengerSpawnCount), 0, 100);
        waitingPassengerColorIndex = Mathf.Max(
            0, DrawSpiderIntField("Stickman Color Index", waitingPassengerColorIndex));

        Rect colorPreviewRect = EditorGUILayout.GetControlRect(false, 18f);
        EditorGUI.DrawRect(colorPreviewRect, toolLevel.EditorGetColorPreview(waitingPassengerColorIndex));

        if (SpiderButton("Spawn Stickman", SpiderButtonTone.Primary, GUILayout.Height(32f)))
        {
            int spawnedCount = toolLevel.EditorAddWaitingPointStickMen(
                selectedWaitingPoint, waitingPassengerSpawnCount, waitingPassengerColorIndex);
            if (spawnedCount > 0)
            {
                FinishAction();
            }
        }
    }

    private void DrawWaitingPassengerColors()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Waiting Passenger Colors", EditorStyles.boldLabel);
        var colors = selectedWaitingPoint.StickManColorIndices;
        for (int i = 0; i < colors.Count; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                int newColorIndex = Mathf.Max(0, EditorGUILayout.IntField("Passenger " + (i + 1), colors[i], SpiderStyles.NumberField));
                if (EditorGUI.EndChangeCheck())
                {
                    toolLevel.EditorSetWaitingPointStickManColor(selectedWaitingPoint, i, newColorIndex);
                    FinishAction();
                }

                Rect previewRect = GUILayoutUtility.GetRect(22f, 18f, GUILayout.Width(22f));
                EditorGUI.DrawRect(previewRect, toolLevel.EditorGetColorPreview(newColorIndex));
                if (SpiderButton("X", SpiderButtonTone.Danger, GUILayout.Width(30f), GUILayout.Height(24f)))
                {
                    toolLevel.EditorRemoveWaitingPointStickMan(selectedWaitingPoint, i);
                    FinishAction();
                    return;
                }
            }
        }

        if (SpiderButton("Add Passenger Color", SpiderButtonTone.Secondary, GUILayout.Height(28f)))
        {
            toolLevel.EditorAddWaitingPointStickMan(selectedWaitingPoint, waitingColorIndex);
            FinishAction();
        }
    }

    private void DrawDirectionButton(string label, int value)
    {
        SpiderButtonTone tone = waitingDirection == value ? SpiderButtonTone.Active : SpiderButtonTone.Ghost;
        if (SpiderButton(label, tone, GUILayout.Height(28f)))
        {
            waitingDirection = value;
            if (selectedWaitingPoint != null)
            {
                toolLevel.EditorSetWaitingPointDirection(selectedWaitingPoint, value);
                FinishAction();
            }
        }
    }

    private void DrawBusSection()
    {
        DrawSectionTitle("BUS & PASSENGERS");
        EditorGUI.BeginChangeCheck();
        busColorIndex = Mathf.Max(0, DrawSpiderIntField("Bus Color Index", busColorIndex));
        passengerCount = Mathf.Clamp(DrawSpiderIntField("Spawn Passengers", passengerCount), 0, BusEdit.DefaultSlotBus);
        if (EditorGUI.EndChangeCheck() && selectedBus != null)
        {
            toolLevel.EditorUpdateBus(selectedBus, busColorIndex, passengerCount);
            FinishAction();
        }

        if (selectedBus != null)
        {
            DrawSpiderInfoBox("SELECTED BUS", selectedBus.name, SpiderStyles.Blue);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawSpawnBusButton();
            DrawModeButton(SceneToolMode.DeleteBus, "Delete Bus");
            DrawModeButton(SceneToolMode.DeleteStickMan, "Delete Stickman");
        }
        EditorGUILayout.LabelField("Spawn Bus: bat che do, click grid trong de tao Bus. Bam Huy Spawn Bus de dung.", EditorStyles.miniLabel);
    }

    private void DrawSpawnBusButton()
    {
        bool isSpawningBus = sceneToolMode == SceneToolMode.PlaceBus;
        string label = isSpawningBus ? "Huy Spawn Bus" : "Spawn Bus";
        SpiderButtonTone tone = isSpawningBus ? SpiderButtonTone.Active : SpiderButtonTone.Secondary;
        if (SpiderButton(label, tone, GUILayout.Height(30f)))
        {
            SetSceneToolMode(isSpawningBus ? SceneToolMode.None : SceneToolMode.PlaceBus);
        }
    }

    private void DrawReferencesSection()
    {
        EditorGUILayout.Space(8f);
        showReferences = EditorGUILayout.Foldout(showReferences, "ToolLevel References & Settings", true);
        if (!showReferences)
        {
            return;
        }

        EnsureInspector();
        if (toolLevelInspector != null)
        {
            EditorGUI.indentLevel++;
            toolLevelInspector.OnInspectorGUI();
            EditorGUI.indentLevel--;
        }
    }

    private static void DrawSectionTitle(string title)
    {
        EditorGUILayout.Space(10f);
        Rect rect = EditorGUILayout.GetControlRect(false, 29f);
        EditorGUI.DrawRect(rect, SpiderStyles.SectionSurface);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 5f, rect.height), SpiderStyles.Red);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), SpiderStyles.Blue);
        GUI.Label(new Rect(rect.x + 13f, rect.y + 4f, rect.width - 20f, rect.height - 6f), title, SpiderStyles.SectionTitle);
    }

    private void DrawModeButton(SceneToolMode mode, string label)
    {
        bool isActive = sceneToolMode == mode;
        bool isDangerous = mode == SceneToolMode.DeleteBus || mode == SceneToolMode.DeleteWaitingPoint || mode == SceneToolMode.DeleteStickMan;
        SpiderButtonTone tone = isActive ? SpiderButtonTone.Active : isDangerous ? SpiderButtonTone.Danger : SpiderButtonTone.Secondary;
        string buttonLabel = isActive ? "Stop " + label : label;
        if (SpiderButton(buttonLabel, tone, GUILayout.Height(30f)))
        {
            SetSceneToolMode(isActive ? SceneToolMode.None : mode);
        }
    }

    private void SetSceneToolMode(SceneToolMode mode)
    {
        sceneToolMode = mode;
        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawViewModeButton(LevelViewMode mode, string label)
    {
        SpiderButtonTone tone = levelViewMode == mode ? SpiderButtonTone.Active : SpiderButtonTone.Ghost;
        if (SpiderButton(label, tone, GUILayout.Height(28f)))
        {
            SwitchLevelViewMode(mode);
        }
    }

    private void SwitchLevelViewMode(LevelViewMode targetMode)
    {
        if (levelViewMode == targetMode)
        {
            return;
        }

        if (levelViewMode == LevelViewMode.GameView)
        {
            SaveActivePreviewView(ref gameViewState);
            gameViewStateInitialized = true;
            if (!sceneViewStateInitialized)
            {
                sceneViewState = gameViewState;
                sceneViewState.aspect = 9f / 16f;
                sceneViewStateInitialized = true;
            }
            RestorePreviewView(sceneViewState);
        }
        else
        {
            SaveActivePreviewView(ref sceneViewState);
            sceneViewStateInitialized = true;
            if (!gameViewStateInitialized)
            {
                gameViewState = sceneViewState;
                gameViewState.aspect = 9f / 16f;
                gameViewStateInitialized = true;
            }
            RestorePreviewView(gameViewState);
        }

        levelViewMode = targetMode;
        Repaint();
    }

    private void SaveActivePreviewView(ref PreviewViewState state)
    {
        state.pivot = previewPivot;
        state.rotation = previewRotation;
        state.distance = previewDistance;
        state.orthographicSize = previewOrthographicSize;
        state.aspect = previewAspect > 0.01f ? previewAspect : PortraitPreviewAspect;
        state.orthographic = previewOrthographic;
    }

    private void RestorePreviewView(PreviewViewState state)
    {
        previewPivot = state.pivot;
        previewRotation = state.rotation;
        previewDistance = state.distance;
        previewOrthographicSize = state.orthographicSize;
        previewAspect = state.aspect > 0.01f ? state.aspect : PortraitPreviewAspect;
        previewOrthographic = state.orthographic;
    }

    private void DrawEmbeddedLevelView(float availableHeight)
    {
        DrawSectionTitle("LEVEL VIEW");

        using (new EditorGUILayout.HorizontalScope())
        {
            DrawViewModeButton(LevelViewMode.SceneEditor, "Scene Editor");
            DrawViewModeButton(LevelViewMode.GameView, "Game View");
            GUILayout.FlexibleSpace();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (SpiderButton("Frame", SpiderButtonTone.Ghost, GUILayout.Height(28f)))
            {
                FrameLevelInPreview();
            }
            if (SpiderButton("Game Cam", SpiderButtonTone.Ghost, GUILayout.Height(28f)))
            {
                CopyGameCameraToPreview();
            }
            if (SpiderButton("Scene Cam", SpiderButtonTone.Ghost, GUILayout.Height(28f)))
            {
                CopySceneCameraToPreview();
            }
            if (SpiderButton(previewOrthographic ? "Orthographic" : "Perspective", SpiderButtonTone.Secondary, GUILayout.Height(28f)))
            {
                previewOrthographic = !previewOrthographic;
                Repaint();
            }
        }

        float viewHeight = Mathf.Max(220f, availableHeight - 92f);
        embeddedViewRect = GUILayoutUtility.GetRect(100f, viewHeight, GUILayout.ExpandWidth(true));
        EnsurePreviewTexture(embeddedViewRect);
        if (Event.current.type == EventType.Repaint)
        {
            RenderEmbeddedLevel();
        }

        EditorGUI.DrawRect(embeddedViewRect, SpiderStyles.Viewport);
        previewImageRect = GetAspectFitRect(embeddedViewRect,
            previewTexture == null ? previewAspect : (float)previewTexture.width / previewTexture.height);
        if (previewTexture != null)
        {
            GUI.DrawTexture(previewImageRect, previewTexture, ScaleMode.StretchToFill, false);
        }
        DrawSpiderViewportFrame(previewImageRect);
        DrawGridOverlays();
        DrawSelectedObjectHighlight();
        DrawSceneToolbar(embeddedViewRect);

        Rect modeRect = new Rect(embeddedViewRect.x + 50f, embeddedViewRect.y + 48f,
            Mathf.Min(430f, embeddedViewRect.width - 58f), 42f);
        EditorGUI.DrawRect(modeRect, new Color(0.02f, 0.07f, 0.14f, 0.93f));
        EditorGUI.DrawRect(new Rect(modeRect.x, modeRect.y, 3f, modeRect.height),
            sceneToolMode == SceneToolMode.None ? SpiderStyles.Blue : SpiderStyles.Red);
        string viewModeLabel = levelViewMode == LevelViewMode.SceneEditor ? "SCENE EDITOR" : "GAME VIEW";
        GUI.Label(new Rect(modeRect.x + 9f, modeRect.y + 3f, modeRect.width - 16f, 18f),
            viewModeLabel + "  •  MODE: " + sceneToolMode, SpiderStyles.OverlayTitle);
        GUI.Label(new Rect(modeRect.x + 9f, modeRect.y + 21f, modeRect.width - 16f, 17f),
            GetModeInstruction(), SpiderStyles.OverlayBody);

        Rect navigationRect = new Rect(embeddedViewRect.x + 8f, embeddedViewRect.yMax - 26f,
            embeddedViewRect.width - 16f, 20f);
        EditorGUI.DrawRect(navigationRect, new Color(0.02f, 0.07f, 0.14f, 0.93f));
        string navigationText = levelViewMode == LevelViewMode.SceneEditor
            ? "  SCENE: Alt + drag = orbit  |  middle drag = pan  |  wheel = zoom"
            : "  GAME: Alt + drag = orbit  |  middle drag = pan  |  wheel = zoom  |  click = select / edit";
        GUI.Label(navigationRect, navigationText, SpiderStyles.OverlayBody);
        HandleEmbeddedViewInput(embeddedViewRect);
    }

    private void DrawSelectedObjectHighlight()
    {
        if (selectedEditObject == null || previewCamera == null || !TryGetObjectBounds(selectedEditObject, out Bounds bounds))
        {
            return;
        }

        ApplyPreviewCameraTransform();
        Rect imageRect = previewImageRect.width > 0f && previewImageRect.height > 0f
            ? previewImageRect
            : embeddedViewRect;
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
        };

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        bool hasVisibleCorner = false;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 viewport = previewCamera.WorldToViewportPoint(corners[i]);
            if (viewport.z <= 0f)
            {
                continue;
            }

            hasVisibleCorner = true;
            float guiX = Mathf.Lerp(imageRect.xMin, imageRect.xMax, viewport.x);
            float guiY = Mathf.Lerp(imageRect.yMax, imageRect.yMin, viewport.y);
            minX = Mathf.Min(minX, guiX);
            minY = Mathf.Min(minY, guiY);
            maxX = Mathf.Max(maxX, guiX);
            maxY = Mathf.Max(maxY, guiY);
        }

        if (!hasVisibleCorner)
        {
            return;
        }

        const float padding = 4f;
        minX = Mathf.Clamp(minX - padding, imageRect.xMin, imageRect.xMax);
        minY = Mathf.Clamp(minY - padding, imageRect.yMin, imageRect.yMax);
        maxX = Mathf.Clamp(maxX + padding, imageRect.xMin, imageRect.xMax);
        maxY = Mathf.Clamp(maxY + padding, imageRect.yMin, imageRect.yMax);
        if (maxX <= minX || maxY <= minY)
        {
            return;
        }

        const float thickness = 3f;
        Color yellow = new Color(1f, 0.82f, 0f, 1f);
        EditorGUI.DrawRect(new Rect(minX, minY, maxX - minX, thickness), yellow);
        EditorGUI.DrawRect(new Rect(minX, maxY - thickness, maxX - minX, thickness), yellow);
        EditorGUI.DrawRect(new Rect(minX, minY, thickness, maxY - minY), yellow);
        EditorGUI.DrawRect(new Rect(maxX - thickness, minY, thickness, maxY - minY), yellow);
        GUI.Label(new Rect(minX + 4f, Mathf.Max(imageRect.yMin, minY - 19f), Mathf.Max(80f, maxX - minX), 18f), selectedEditObject.name, EditorStyles.whiteMiniLabel);
    }

    private void DrawGridOverlays()
    {
        if (toolLevel == null || previewCamera == null || previewImageRect.width <= 0f || previewImageRect.height <= 0f)
        {
            return;
        }

        GridEdit[] grids = Resources.FindObjectsOfTypeAll<GridEdit>();
        for (int i = 0; i < grids.Length; i++)
        {
            GridEdit grid = grids[i];
            if (grid == null || grid.gameObject.scene != toolLevel.gameObject.scene || !grid.gameObject.activeInHierarchy
                || !TryGetObjectBounds(grid.gameObject, out Bounds bounds) || !TryGetViewportRect(bounds, out Rect viewportRect, out _))
            {
                continue;
            }

            Rect guiRect = ViewportRectToGuiRect(viewportRect);
            if (guiRect.width < 2f || guiRect.height < 2f)
            {
                continue;
            }

            Color color = grid.HasBus
                ? new Color(1f, 0.65f, 0.1f, 0.8f)
                : new Color(0.1f, 0.75f, 1f, 0.9f);
            EditorGUI.DrawRect(guiRect, new Color(color.r, color.g, color.b, 0.16f));
            EditorGUI.DrawRect(new Rect(guiRect.xMin, guiRect.yMin, guiRect.width, 2f), color);
            EditorGUI.DrawRect(new Rect(guiRect.xMin, guiRect.yMax - 2f, guiRect.width, 2f), color);
            EditorGUI.DrawRect(new Rect(guiRect.xMin, guiRect.yMin, 2f, guiRect.height), color);
            EditorGUI.DrawRect(new Rect(guiRect.xMax - 2f, guiRect.yMin, 2f, guiRect.height), color);
        }
    }

    private Rect ViewportRectToGuiRect(Rect viewportRect)
    {
        float xMin = Mathf.Lerp(previewImageRect.xMin, previewImageRect.xMax, viewportRect.xMin);
        float xMax = Mathf.Lerp(previewImageRect.xMin, previewImageRect.xMax, viewportRect.xMax);
        float yMin = Mathf.Lerp(previewImageRect.yMax, previewImageRect.yMin, viewportRect.yMax);
        float yMax = Mathf.Lerp(previewImageRect.yMax, previewImageRect.yMin, viewportRect.yMin);
        return Rect.MinMaxRect(
            Mathf.Max(previewImageRect.xMin, xMin),
            Mathf.Max(previewImageRect.yMin, yMin),
            Mathf.Min(previewImageRect.xMax, xMax),
            Mathf.Min(previewImageRect.yMax, yMax));
    }

    private static bool TryGetObjectBounds(GameObject target, out Bounds bounds)
    {
        bounds = new Bounds(target.transform.position, Vector3.one * 0.25f);
        bool hasBounds = false;
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
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

        if (hasBounds)
        {
            return true;
        }

        Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = colliders[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
        }
        return true;
    }

    private void EnsurePreviewCamera()
    {
        if (previewCamera != null)
        {
            return;
        }

        previewCameraObject = new GameObject("LevelToolPreviewCamera")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        previewCamera = previewCameraObject.AddComponent<Camera>();
        previewCamera.hideFlags = HideFlags.HideAndDontSave;
        previewCamera.enabled = false;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = new Color(0.13f, 0.17f, 0.2f, 1f);
        previewCamera.cullingMask = ~0;
        previewCamera.fieldOfView = 60f;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 5000f;
        previewCamera.allowHDR = true;
        previewCamera.allowMSAA = true;
    }

    private void EnsurePreviewTexture(Rect viewRect)
    {
        int height = Mathf.Clamp(Mathf.RoundToInt(viewRect.height * EditorGUIUtility.pixelsPerPoint), 128, 1536);
        float aspect = Mathf.Clamp(previewAspect, 0.25f, 3f);
        int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 128, 2048);
        height = Mathf.Clamp(Mathf.RoundToInt(width / aspect), 128, 1536);
        if (previewTexture != null && previewTexture.width == width && previewTexture.height == height)
        {
            return;
        }

        ReleasePreviewTexture();
        previewTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            name = "Level Tool View",
            hideFlags = HideFlags.HideAndDontSave,
            antiAliasing = 2
        };
        previewTexture.Create();
    }

    private void RenderEmbeddedLevel()
    {
        if (toolLevel == null || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsurePreviewCamera();
        if (previewTexture == null)
        {
            return;
        }

        if (!previewInitialized)
        {
            if (levelViewMode == LevelViewMode.GameView)
            {
                CopyGameCameraToPreview();
            }
            else
            {
                CopySceneCameraToPreview();
                FrameLevelInPreview();
            }
        }

        ApplyPreviewCameraTransform();
        previewCamera.aspect = (float)previewTexture.width / previewTexture.height;
        previewCamera.orthographic = previewOrthographic;
        previewCamera.orthographicSize = Mathf.Max(0.01f, previewOrthographicSize);
        previewCamera.targetTexture = previewTexture;
        previewCamera.Render();
        previewCamera.targetTexture = null;
    }

    private void ApplyPreviewCameraTransform()
    {
        if (previewCamera == null)
        {
            return;
        }
        previewCamera.transform.rotation = previewRotation;
        previewCamera.transform.position = previewPivot - previewRotation * Vector3.forward * Mathf.Max(0.05f, previewDistance);
    }

    private void CopyGameCameraToPreview()
    {
        Camera source = null;
        if (toolLevel != null && toolLevel.gameplayDrawCamera != null)
        {
            source = toolLevel.gameplayDrawCamera;
        }
        if (source == null && Camera.main != null && Camera.main != previewCamera)
        {
            source = Camera.main;
        }
        if (source == null)
        {
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null && cameras[i] != previewCamera && cameras[i].gameObject.scene.IsValid())
                {
                    source = cameras[i];
                    break;
                }
            }
        }
        CopyCameraToPreview(source);
    }

    private void CopySceneCameraToPreview()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        CopyCameraToPreview(sceneView == null ? null : sceneView.camera);
    }

    private void CopyCameraToPreview(Camera source)
    {
        if (source == null)
        {
            ShowNotification(new GUIContent("Khong tim thay camera de copy view."));
            return;
        }

        EnsurePreviewCamera();
        previewRotation = source.transform.rotation;
        previewOrthographic = source.orthographic;
        previewOrthographicSize = Mathf.Max(0.01f, source.orthographicSize);
        previewCamera.fieldOfView = source.fieldOfView;
        previewCamera.clearFlags = source.clearFlags;
        previewCamera.backgroundColor = source.backgroundColor;
        previewCamera.nearClipPlane = Mathf.Max(0.01f, source.nearClipPlane);
        previewCamera.farClipPlane = Mathf.Max(previewCamera.nearClipPlane + 1f, source.farClipPlane);
        previewCamera.cullingMask = source.cullingMask;
        previewAspect = PortraitPreviewAspect;
        
        // Keep the copied camera at its real gameplay position. Framing around the
        // generated grid changes the visual meaning of local (0, 0) coordinates.
        previewDistance = 10f;
        previewPivot = source.transform.position + source.transform.forward * previewDistance;
        previewInitialized = true;
        Repaint();
    }

    private static Rect GetAspectFitRect(Rect container, float aspect)
    {
        aspect = Mathf.Max(0.01f, aspect);
        float containerAspect = container.width / Mathf.Max(1f, container.height);
        if (containerAspect > aspect)
        {
            float width = container.height * aspect;
            return new Rect(container.center.x - width * 0.5f, container.y, width, container.height);
        }

        float height = container.width / aspect;
        return new Rect(container.x, container.center.y - height * 0.5f, container.width, height);
    }

    private void FrameLevelInPreview()
    {
        EnsurePreviewCamera();
        if (levelViewMode == LevelViewMode.GameView)
        {
            // Game View must keep the gameplay camera so Grid_0_0 stays at its
            // real map position and successive negative-Z rows go downward.
            CopyGameCameraToPreview();
            return;
        }
        if (!TryGetLevelBounds(out Bounds bounds))
        {
            previewPivot = toolLevel == null ? Vector3.zero : toolLevel.transform.position;
            previewDistance = 12f;
            previewOrthographicSize = 8f;
            previewInitialized = true;
            Repaint();
            return;
        }

        previewPivot = bounds.center;
        float aspect = Mathf.Max(0.1f, previewAspect);
        Quaternion inverseRotation = Quaternion.Inverse(previewRotation);
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
        };
        float halfWidth = 0.5f;
        float halfHeight = 0.5f;
        float halfDepth = 0.5f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = inverseRotation * (corners[i] - previewPivot);
            halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
            halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
            halfDepth = Mathf.Max(halfDepth, Mathf.Abs(local.z));
        }

        previewOrthographicSize = Mathf.Max(halfHeight, halfWidth / aspect) * 1.08f;
        float verticalTangent = Mathf.Tan(Mathf.Max(5f, previewCamera.fieldOfView * 0.5f) * Mathf.Deg2Rad);
        float horizontalTangent = verticalTangent * aspect;
        float verticalDistance = halfHeight / Mathf.Max(0.01f, verticalTangent);
        float horizontalDistance = halfWidth / Mathf.Max(0.01f, horizontalTangent);
        previewDistance = Mathf.Max(1f, Mathf.Max(verticalDistance, horizontalDistance) + halfDepth * 1.1f);
        previewInitialized = true;
        Repaint();
    }

    private bool TryGetLevelBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || toolLevel == null || renderer.gameObject.scene != toolLevel.gameObject.scene || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }
            if ((renderer.hideFlags & HideFlags.HideAndDontSave) != 0)
            {
                continue;
            }
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return hasBounds;
    }

    private bool HandlePreviewNavigation(Rect viewRect, Event currentEvent)
    {
        if (currentEvent.type == EventType.MouseDown)
        {
            if (!viewRect.Contains(currentEvent.mousePosition))
            {
                return false;
            }

            bool orbitClick = currentEvent.alt && currentEvent.button == 0;
            bool panClick = currentEvent.button == 2;
            if (!orbitClick && !panClick)
            {
                return false;
            }

            previewDragButton = currentEvent.button;
            GUI.FocusControl(null);
            currentEvent.Use();
            return true;
        }

        if (currentEvent.type == EventType.MouseUp && previewDragButton >= 0)
        {
            previewDragButton = -1;
            currentEvent.Use();
            return true;
        }

        if (currentEvent.type != EventType.MouseDrag || previewDragButton < 0)
        {
            return false;
        }

        if (previewDragButton == 2)
        {
            float panScale = previewOrthographic
                ? previewOrthographicSize * 2f / Mathf.Max(1f, viewRect.height)
                : previewDistance * 0.0025f;
            Vector3 right = previewRotation * Vector3.right;
            Vector3 up = previewRotation * Vector3.up;
            previewPivot += (-right * currentEvent.delta.x + up * currentEvent.delta.y) * panScale;
        }
        else
        {
            Vector3 euler = previewRotation.eulerAngles;
            euler.x = Mathf.Repeat(euler.x - currentEvent.delta.y * 0.35f, 360f);
            euler.y = Mathf.Repeat(euler.y + currentEvent.delta.x * 0.35f, 360f);
            previewRotation = Quaternion.Euler(euler.x, euler.y, 0f);
        }

        currentEvent.Use();
        Repaint();
        return true;
    }

    private void HandleEmbeddedViewInput(Rect viewRect)
    {
        Event currentEvent = Event.current;
        if (HandlePreviewNavigation(viewRect, currentEvent))
        {
            return;
        }

        if (!viewRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (currentEvent.type == EventType.MouseDown && previewImageRect.width > 0f
            && previewImageRect.height > 0f && !previewImageRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (currentEvent.type == EventType.ScrollWheel)
        {
            float zoomFactor = Mathf.Pow(1.12f, currentEvent.delta.y);
            if (previewOrthographic)
            {
                previewOrthographicSize = Mathf.Clamp(previewOrthographicSize * zoomFactor, 0.05f, 5000f);
            }
            else
            {
                previewDistance = Mathf.Clamp(previewDistance * zoomFactor, 0.05f, 10000f);
            }
            currentEvent.Use();
            Repaint();
            return;
        }

        if (currentEvent.type != EventType.MouseDown)
        {
            return;
        }

        GUI.FocusControl(null);
        if (currentEvent.button == 0 && sceneToolMode == SceneToolMode.None)
        {
            Ray selectionRay = GetEmbeddedViewRay(viewRect, currentEvent.mousePosition);
            Vector2 selectionViewport = GetEmbeddedViewportPoint(viewRect, currentEvent.mousePosition);
            GameObject editableObject = PickEmbeddedEditableObject(selectionRay, selectionViewport);
            if (editableObject != null)
            {
                SelectEditableObject(editableObject);
                RenderEmbeddedLevel();
                Repaint();
                currentEvent.Use();
                return;
            }

            ClearEditedObject();
            currentEvent.Use();
            return;
        }

        if (currentEvent.button == 1)
        {
            HandleEmbeddedRightClick(viewRect, currentEvent.mousePosition);
            currentEvent.Use();
            return;
        }

        if (currentEvent.button == 0 && sceneToolMode != SceneToolMode.None)
        {
            Ray worldRay = GetEmbeddedViewRay(viewRect, currentEvent.mousePosition);
            Vector2 viewportPoint = GetEmbeddedViewportPoint(viewRect, currentEvent.mousePosition);
            GameObject picked = PickEmbeddedObject(worldRay, viewportPoint);
            ClearEditedObject();
            if (HandleSceneClick(picked, worldRay))
            {
                FinishAction();
            }
            else
            {
                RenderEmbeddedLevel();
                Repaint();
            }
            currentEvent.Use();
        }
    }

    private void HandleEmbeddedRightClick(Rect viewRect, Vector2 mousePosition)
    {
        if (sceneToolMode == SceneToolMode.PlaceBus)
        {
            Ray ray = GetEmbeddedViewRay(viewRect, mousePosition);
            GameObject picked = PickEmbeddedObject(ray, GetEmbeddedViewportPoint(viewRect, mousePosition));
            BusEdit bus = picked == null ? null : picked.GetComponentInParent<BusEdit>();
            bool deleted = bus != null ? toolLevel.EditorDeleteBus(bus) : toolLevel.EditorDeleteBus(ResolveGrid(picked));
            if (deleted)
            {
                ClearEditedObject();
                FinishAction();
            }
            return;
        }
        SetSceneToolMode(SceneToolMode.None);
    }

    private Ray GetEmbeddedViewRay(Rect viewRect, Vector2 mousePosition)
    {
        EnsurePreviewCamera();
        ApplyPreviewCameraTransform();
        Vector2 viewportPoint = GetEmbeddedViewportPoint(viewRect, mousePosition);
        return previewCamera.ViewportPointToRay(new Vector3(viewportPoint.x, viewportPoint.y, 0f));
    }

    private Vector2 GetEmbeddedViewportPoint(Rect viewRect, Vector2 mousePosition)
    {
        Rect imageRect = previewImageRect.width > 0f && previewImageRect.height > 0f
            ? previewImageRect
            : GetAspectFitRect(viewRect, previewAspect);
        float x = Mathf.InverseLerp(imageRect.xMin, imageRect.xMax, mousePosition.x);
        float y = 1f - Mathf.InverseLerp(imageRect.yMin, imageRect.yMax, mousePosition.y);
        return new Vector2(x, y);
    }

    private GameObject PickEmbeddedObject(Ray worldRay, Vector2 viewportPoint)
    {
        GameObject editableObject = PickEmbeddedEditableObject(worldRay, viewportPoint);
        if (editableObject != null)
        {
            bool isBusMode = sceneToolMode == SceneToolMode.PlaceBus || sceneToolMode == SceneToolMode.DeleteBus;
            bool isWaitMode = sceneToolMode == SceneToolMode.PlaceWaitingPoint || sceneToolMode == SceneToolMode.DeleteWaitingPoint;
            if ((isBusMode && editableObject.GetComponent<BusEdit>() != null)
                || (isWaitMode && editableObject.GetComponent<WaitingPointEdit>() != null))
            {
                return editableObject;
            }
        }

        RaycastHit[] hits3D = Physics.RaycastAll(worldRay, 10000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits3D, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits3D.Length; i++)
        {
            GameObject candidate = hits3D[i].transform.gameObject;
            if (IsCandidateForCurrentMode(candidate))
            {
                return candidate;
            }
        }

        RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(worldRay, 10000f, Physics.DefaultRaycastLayers);
        System.Array.Sort(hits2D, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits2D.Length; i++)
        {
            if (hits2D[i].collider != null && IsCandidateForCurrentMode(hits2D[i].collider.gameObject))
            {
                return hits2D[i].collider.gameObject;
            }
        }
        return PickEmbeddedObjectByRenderer(viewportPoint);
    }

    private GameObject PickEmbeddedEditableObject(Ray worldRay, Vector2 viewportPoint)
    {
        // The embedded view is a rendered image, so visible renderer bounds must win over
        // broad/overlapping colliders. Otherwise clicking Bus B can keep selecting Bus A.
        EnsurePreviewCamera();
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        GameObject bestObject = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || toolLevel == null || renderer.gameObject.scene != toolLevel.gameObject.scene || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            GameObject editable = ResolveEditableObject(renderer.gameObject);
            if (editable == null || !TryGetViewportRect(renderer.bounds, out Rect projectedRect, out Vector3 viewportCenter)
                || !projectedRect.Contains(viewportPoint))
            {
                continue;
            }

            float score = ((Vector2)viewportCenter - viewportPoint).sqrMagnitude + viewportCenter.z * 0.000001f;
            if (score < bestScore)
            {
                bestScore = score;
                bestObject = editable;
            }
        }
        if (bestObject != null)
        {
            return bestObject;
        }

        RaycastHit[] hits3D = Physics.RaycastAll(worldRay, 10000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits3D, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits3D.Length; i++)
        {
            GameObject editable = ResolveEditableObject(hits3D[i].transform.gameObject);
            if (editable != null)
            {
                return editable;
            }
        }

        RaycastHit2D[] hits2D = Physics2D.GetRayIntersectionAll(worldRay, 10000f, Physics.DefaultRaycastLayers);
        System.Array.Sort(hits2D, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits2D.Length; i++)
        {
            if (hits2D[i].collider == null)
            {
                continue;
            }

            GameObject editable = ResolveEditableObject(hits2D[i].collider.gameObject);
            if (editable != null)
            {
                return editable;
            }
        }
        return null;
    }

    private bool TryGetViewportRect(Bounds bounds, out Rect projectedRect, out Vector3 viewportCenter)
    {
        viewportCenter = previewCamera.WorldToViewportPoint(bounds.center);
        projectedRect = default;
        if (viewportCenter.z <= 0f)
        {
            return false;
        }

        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
        };

        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 viewport = previewCamera.WorldToViewportPoint(corners[i]);
            if (viewport.z <= 0f)
            {
                continue;
            }
            minX = Mathf.Min(minX, viewport.x);
            minY = Mathf.Min(minY, viewport.y);
            maxX = Mathf.Max(maxX, viewport.x);
            maxY = Mathf.Max(maxY, viewport.y);
        }

        if (float.IsInfinity(minX))
        {
            return false;
        }
        projectedRect = Rect.MinMaxRect(minX - 0.015f, minY - 0.015f, maxX + 0.015f, maxY + 0.015f);
        return true;
    }

    private static GameObject ResolveEditableObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        WaitingPointEdit waitingPoint = candidate.GetComponentInParent<WaitingPointEdit>();
        if (waitingPoint != null)
        {
            return waitingPoint.gameObject;
        }

        BusEdit bus = candidate.GetComponentInParent<BusEdit>();
        return bus == null ? null : bus.gameObject;
    }

    private GameObject PickEmbeddedObjectByRenderer(Vector2 viewportPoint)
    {
        EnsurePreviewCamera();
        Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
        GameObject bestObject = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || toolLevel == null || renderer.gameObject.scene != toolLevel.gameObject.scene || !renderer.gameObject.activeInHierarchy || !IsCandidateForCurrentMode(renderer.gameObject))
            {
                continue;
            }

            Vector3 viewportCenter = previewCamera.WorldToViewportPoint(renderer.bounds.center);
            if (viewportCenter.z <= 0f)
            {
                continue;
            }
            Vector3 viewportMin = previewCamera.WorldToViewportPoint(renderer.bounds.min);
            Vector3 viewportMax = previewCamera.WorldToViewportPoint(renderer.bounds.max);
            Rect projectedRect = Rect.MinMaxRect(
                Mathf.Min(viewportMin.x, viewportMax.x) - 0.015f,
                Mathf.Min(viewportMin.y, viewportMax.y) - 0.015f,
                Mathf.Max(viewportMin.x, viewportMax.x) + 0.015f,
                Mathf.Max(viewportMin.y, viewportMax.y) + 0.015f);
            if (!projectedRect.Contains(viewportPoint))
            {
                continue;
            }

            float score = ((Vector2)viewportCenter - viewportPoint).sqrMagnitude + viewportCenter.z * 0.000001f;
            if (score < bestScore)
            {
                bestScore = score;
                bestObject = renderer.gameObject;
            }
        }
        return bestObject;
    }

    private bool IsCandidateForCurrentMode(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }
        switch (sceneToolMode)
        {
            case SceneToolMode.PlaceBus:
            case SceneToolMode.DeleteBus:
                return candidate.GetComponentInParent<BusEdit>() != null || ResolveGrid(candidate) != null;
            case SceneToolMode.PlaceWaitingPoint:
                return candidate.GetComponentInParent<WaitingPointEdit>() != null;
            case SceneToolMode.DeleteWaitingPoint:
                return candidate.GetComponentInParent<WaitingPointEdit>() != null;
            case SceneToolMode.DeleteStickMan:
                return candidate.GetComponentInParent<StickManEdit>() != null;
            default:
                return false;
        }
    }

    private void ReleasePreviewResources()
    {
        ReleasePreviewTexture();
        if (previewCameraObject != null)
        {
            DestroyImmediate(previewCameraObject);
        }
        previewCameraObject = null;
        previewCamera = null;
    }

    private void ReleasePreviewTexture()
    {
        if (previewTexture == null)
        {
            return;
        }
        previewTexture.Release();
        DestroyImmediate(previewTexture);
        previewTexture = null;
        previewImageRect = default;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (sceneToolMode == SceneToolMode.None || toolLevel == null || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        DrawSceneOverlay();
        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
        {
            SetSceneToolMode(SceneToolMode.None);
            currentEvent.Use();
            return;
        }
        if (currentEvent.alt || currentEvent.type != EventType.MouseDown)
        {
            return;
        }
        if (currentEvent.button == 1)
        {
            if (sceneToolMode == SceneToolMode.PlaceBus)
            {
                GameObject pickedForDelete = HandleUtility.PickGameObject(currentEvent.mousePosition, false);
                BusEdit busForDelete = pickedForDelete == null ? null : pickedForDelete.GetComponentInParent<BusEdit>();
                GridEdit gridForDelete = ResolveGrid(pickedForDelete);
                bool deleted = busForDelete != null
                    ? toolLevel.EditorDeleteBus(busForDelete)
                    : toolLevel.EditorDeleteBus(gridForDelete);
                if (deleted)
                {
                    ClearEditedObject();
                    FinishAction();
                }
            }
            else
            {
                SetSceneToolMode(SceneToolMode.None);
            }
            currentEvent.Use();
            return;
        }
        if (currentEvent.button != 0)
        {
            return;
        }

        Ray worldRay = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
        GameObject picked = HandleUtility.PickGameObject(currentEvent.mousePosition, false);
        ClearEditedObject();
        bool handled = HandleSceneClick(picked, worldRay);
        if (handled)
        {
            FinishAction();
            currentEvent.Use();
        }
    }

    private bool HandleSceneClick(GameObject picked, Ray worldRay)
    {
        switch (sceneToolMode)
        {
            case SceneToolMode.PlaceWaitingPoint:
            {
                WaitingPointEdit existingWaitingPoint = picked == null ? null : picked.GetComponentInParent<WaitingPointEdit>();
                if (existingWaitingPoint != null)
                {
                    SelectWaitingPointForEditing(existingWaitingPoint);
                    return true;
                }

                WaitingPointEdit waitingPoint = toolLevel.EditorPlaceWaitingPoint(picked, worldRay, waitingColorIndex, waitingDirection);
                if (waitingPoint != null)
                {
                    SelectWaitingPointForEditing(waitingPoint);
                    return true;
                }
                return false;
            }
            case SceneToolMode.PlaceBus:
            {
                BusEdit clickedBus = picked == null ? null : picked.GetComponentInParent<BusEdit>();
                if (clickedBus != null)
                {
                    SelectBusForEditing(clickedBus);
                    return true;
                }

                GridEdit grid = ResolveGrid(picked);
                BusEdit bus = toolLevel.EditorPlaceOrUpdateBus(grid, busColorIndex, passengerCount);
                if (bus != null)
                {
                    SelectBusForEditing(bus);
                    return true;
                }
                return false;
            }
            case SceneToolMode.DeleteBus:
            {
                BusEdit bus = picked == null ? null : picked.GetComponentInParent<BusEdit>();
                GridEdit grid = ResolveGrid(picked);
                if (bus == null && grid != null)
                {
                    bus = grid.CurrentBus;
                }
                bool deleted = bus != null ? toolLevel.EditorDeleteBus(bus) : false;
                if (deleted)
                {
                    ClearEditedObject();
                }
                return deleted;
            }
            case SceneToolMode.DeleteWaitingPoint:
            {
                WaitingPointEdit waitingPoint = picked == null ? null : picked.GetComponentInParent<WaitingPointEdit>();
                bool deleted = toolLevel.EditorDeleteWaitingPoint(waitingPoint);
                if (deleted && selectedWaitingPoint == waitingPoint)
                {
                    ClearEditedObject();
                }
                return deleted;
            }
            case SceneToolMode.DeleteStickMan:
                return toolLevel.EditorDeleteStickMan(picked == null ? null : picked.GetComponentInParent<StickManEdit>());
            default:
                return false;
        }
    }

    private void SelectWaitingPointForEditing(WaitingPointEdit waitingPoint)
    {
        selectedWaitingPoint = waitingPoint;
        selectedBus = null;
        selectedEditObject = waitingPoint == null ? null : waitingPoint.gameObject;
        if (waitingPoint != null)
        {
            waitingColorIndex = waitingPoint.ColorIndex;
            waitingDirection = waitingPoint.Direction;
            waitingPassengerColorIndex = waitingPoint.ColorIndex;
        }
        Repaint();
    }

    private void SelectBusForEditing(BusEdit bus)
    {
        selectedWaitingPoint = null;
        selectedBus = bus;
        selectedEditObject = bus == null ? null : bus.gameObject;
        if (bus != null)
        {
            busColorIndex = bus.ColorIndex;
            passengerCount = bus.SlotBusPerson;
        }
        Repaint();
    }

    private void SelectEditableObject(GameObject editableObject)
    {
        WaitingPointEdit waitingPoint = editableObject == null ? null : editableObject.GetComponent<WaitingPointEdit>();
        if (waitingPoint != null)
        {
            SelectWaitingPointForEditing(waitingPoint);
            return;
        }

        BusEdit bus = editableObject == null ? null : editableObject.GetComponent<BusEdit>();
        if (bus != null)
        {
            SelectBusForEditing(bus);
            return;
        }
        ClearEditedObject();
    }

    private void ClearEditedObject()
    {
        selectedWaitingPoint = null;
        selectedBus = null;
        selectedEditObject = null;
        Repaint();
    }

    private void DrawSceneOverlay()
    {
        Handles.BeginGUI();
        Rect panel = new Rect(12f, 12f, 390f, 54f);
        EditorGUI.DrawRect(panel, new Color(0.015f, 0.06f, 0.12f, 0.95f));
        EditorGUI.DrawRect(new Rect(panel.x, panel.y, 4f, panel.height), sceneToolMode == SceneToolMode.None ? SpiderStyles.Blue : SpiderStyles.Red);
        GUI.Label(new Rect(24f, 18f, 360f, 20f), "SPIDER LEVEL LAB  //  " + sceneToolMode, SpiderStyles.OverlayTitle);
        GUI.Label(new Rect(24f, 39f, 360f, 18f), GetModeInstruction(), SpiderStyles.OverlayBody);
        Handles.EndGUI();
    }

    private string GetModeInstruction()
    {
        switch (sceneToolMode)
        {
            case SceneToolMode.PlaceWaitingPoint:
                return "Click vi tri bat ky: tao Wait tai Y = 2 | Click Wait da co: chon de sua";
            case SceneToolMode.PlaceBus:
                return "Click Bus: chon de sua | Click Grid trong: tao Bus | Right click: xoa Bus";
            case SceneToolMode.DeleteBus:
                return "Click a grid or bus to delete that bus";
            case SceneToolMode.DeleteWaitingPoint:
                return "Click a waiting point to delete it";
            case SceneToolMode.DeleteStickMan:
                return "Click a stickman to delete it";
            default:
                return "Click truc tiep Bus/Wait de chon. Khung vang chi hien quanh object dang duoc sua.";
        }
    }

    private static GridEdit ResolveGrid(GameObject picked)
    {
        if (picked == null)
        {
            return null;
        }
        BusEdit bus = picked.GetComponentInParent<BusEdit>();
        if (bus != null && bus.CurrentGrid != null)
        {
            return bus.CurrentGrid;
        }
        return picked.GetComponentInParent<GridEdit>();
    }

    private void RegisterSceneUndo(string actionName)
    {
        if (toolLevel == null || !toolLevel.gameObject.scene.IsValid())
        {
            return;
        }
        GameObject[] roots = toolLevel.gameObject.scene.GetRootGameObjects();
        Object[] objects = new Object[roots.Length + 1];
        objects[0] = toolLevel;
        for (int i = 0; i < roots.Length; i++)
        {
            objects[i + 1] = roots[i];
        }
        Undo.RegisterCompleteObjectUndo(objects, actionName);
    }

private void FinishAction()
    {
        if (toolLevel != null)
        {
            EditorUtility.SetDirty(toolLevel);
        }
        SceneView.RepaintAll();
        RenderEmbeddedLevel();
        Repaint();
    }

private void EnsureToolWorkspaceLoaded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            workspaceStatus = "Hay Stop Play Mode truoc khi dung Level Tool.";
            return;
        }

        if (toolLevel != null
            && standaloneWorkspaceScene.IsValid()
            && standaloneWorkspaceScene.isLoaded)
        {
            return;
        }

        DisposeStandaloneWorkspace();
        standaloneWorkspaceScene = EditorSceneManager.NewPreviewScene();

        GameObject hostObject = new GameObject("LevelToolHost");
        SceneManager.MoveGameObjectToScene(hostObject, standaloneWorkspaceScene);
        ToolLevel standaloneTool = hostObject.AddComponent<ToolLevel>();
        standaloneTool.InitializeStandaloneWorkspace(standaloneWorkspaceScene);

        workspaceStatus = "Standalone workspace san sang (khong phu thuoc scene).";
        SetToolLevel(standaloneTool);
        EditorApplication.delayCall += RefocusToolWindow;
    }

private void DisposeStandaloneWorkspace()
    {
        DestroyInspector();
        if (toolLevel != null && standaloneWorkspaceScene.IsValid()
            && toolLevel.gameObject.scene == standaloneWorkspaceScene)
        {
            SetToolLevel(null);
        }

        if (standaloneWorkspaceScene.IsValid()
            && standaloneWorkspaceScene.isLoaded
            && EditorSceneManager.IsPreviewScene(standaloneWorkspaceScene))
        {
            EditorSceneManager.ClosePreviewScene(standaloneWorkspaceScene);
        }
        standaloneWorkspaceScene = default;
    }


    private void RefocusToolWindow()
    {
        if (this == null)
        {
            return;
        }

        Focus();
        Repaint();
    }





    private void SetToolLevel(ToolLevel newTarget)
    {
        if (toolLevel == newTarget)
        {
            return;
        }
        DestroyInspector();
        toolLevel = newTarget;
        if (toolLevel != null)
        {
            toolLevel.InitializeEditorReferences();
        }

        ClearEditedObject();
        previewInitialized = false;
        if (toolLevel != null)
        {
            toolLevel.EditorRefreshWaitingPointStickManVisuals();
            SerializedObject serializedTool = new SerializedObject(toolLevel);
            SerializedProperty levelProperty = serializedTool.FindProperty("currentLevelId");
            if (levelProperty != null)
            {
                levelId = Mathf.Max(1, levelProperty.intValue);
            }
        }
        Repaint();
    }

    private void EnsureInspector()
    {
        if (toolLevelInspector == null && toolLevel != null)
        {
            toolLevelInspector = UnityEditor.Editor.CreateEditor(toolLevel);
        }
    }

private void DestroyInspector()
    {
        if (toolLevelInspector != null)
        {
            DestroyImmediate(toolLevelInspector);
            toolLevelInspector = null;
        }
    }
}
