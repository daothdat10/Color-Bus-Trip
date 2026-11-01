using System.Collections.Generic;
using Dreamteck.Splines;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using DreamSplineMesh = Dreamteck.Splines.SplineMesh;

/// <summary>
/// Builds the visible road with Dreamteck SplineMesh. The gameplay path remains
/// a Unity Spline; this class mirrors it into Dreamteck and attaches the authored
/// testRoad15.001 lower board at the two open endpoints.
/// </summary>
public static class DreamteckSplineRoadRenderer
{
    public struct ParkingBayAlignment
    {
        public bool hasExitLane;
        public Vector3 exitLaneOffset;
        public Vector3 gatePosition;
    }

    private const string RoadPrefabResourcePath = "MyGameSplineMesh_Road";
    private const string BridgePrefabResourcePath = "MyGameSplineMesh_Bridge";
    private const string RoadProfileResourcePath = "MyGameSplineRoadPiece";
    private const string ThemeMaterialsResourcePath = "RoadThemeMaterials";

    public const string RimObjectName = "Road_Rim";
    private const string ImportedLowerResourcePath = "testRoad15.001";
    // testRoad15.001 is 2.6 wide; its crossbar starts at z = -0.7 and the
    // bay/exit-lane body runs towards +z (the mesh is flat in X/Z, Y up).
    private const float ImportedLowerWidth = 2.6f;
    private const float ImportedLowerTopZ = -0.7f;
    private const float RimWidthScale = 1.45f;
    private const float DefaultRoadHalfWidth = 0.14f;
    private const float DefaultParkingBayExtraWidthPerSide = 0.12f;
    private const float BridgeHeightThreshold = -0.05f;
    private const float BridgeVisualPadding = 0.035f;
    private const float BridgeScanStep = 0.005f;
    private const int BoundsSamples = 128;

    private struct OpenRoadLayout
    {
        public Vector3 start;
        public Vector3 end;
        public Vector3 side;
        public Vector3 outward;
        public float extensionLength;
    }

    public static bool TryBuild(
        SplineContainer source,
        Transform parent,
        string rootName,
        bool renderAllSplines,
        int selectedSplineIndex,
        RoadTheme theme,
        out GameObject generatedRoot,
        out string message)
    {
        return TryBuild(
            source, parent, rootName, renderAllSplines, selectedSplineIndex,
            theme, Vector2.one, DefaultRoadHalfWidth * 2f,
                        DefaultParkingBayExtraWidthPerSide, 0.09f, true, out generatedRoot, out message);
    }

    public static bool TryBuild(
        SplineContainer source,
        Transform parent,
        string rootName,
        bool renderAllSplines,
        int selectedSplineIndex,
        RoadTheme theme,
        Vector2 splinePlaneScale,
        out GameObject generatedRoot,
        out string message)
    {
        return TryBuild(
            source, parent, rootName, renderAllSplines, selectedSplineIndex,
            theme, splinePlaneScale, DefaultRoadHalfWidth * 2f,
                        DefaultParkingBayExtraWidthPerSide, 0.09f, true, out generatedRoot, out message);
    }

    public static bool TryBuild(
        SplineContainer source,
        Transform parent,
        string rootName,
        bool renderAllSplines,
        int selectedSplineIndex,
        RoadTheme theme,
        Vector2 splinePlaneScale,
        float splineRoadWidth,
        float parkingBayExtraWidthPerSide,
        float grassyDividerWidth,
        bool includeParkingBay,
        out GameObject generatedRoot,
        out string message)
    {
        generatedRoot = null;
        message = string.Empty;
        if (source == null || parent == null)
        {
            message = "Spline or road parent is missing.";
            return false;
        }

        GameObject roadTemplate = Resources.Load<GameObject>(RoadPrefabResourcePath);
        GameObject bridgeTemplate = Resources.Load<GameObject>(BridgePrefabResourcePath);
        Mesh roadProfile = Resources.Load<Mesh>(RoadProfileResourcePath);
        RoadThemeMaterials palette = Resources.Load<RoadThemeMaterials>(ThemeMaterialsResourcePath);
        Material groundMaterial;
        Material roadMaterial;
        if (roadTemplate == null || roadProfile == null || roadProfile.vertexCount == 0 ||
            palette == null || !palette.TryGet(theme, out groundMaterial, out roadMaterial))
        {
            message = "Dreamteck road template, road profile, or theme material is missing.";
            return false;
        }

        List<int> splineIndices = CollectSplineIndices(
            source, renderAllSplines, selectedSplineIndex);
        if (splineIndices.Count == 0)
        {
            message = "No valid Unity spline to render.";
            return false;
        }

        Transform previous = parent.Find(rootName);
        if (previous != null)
        {
            DestroyObject(previous.gameObject);
        }

        generatedRoot = new GameObject(rootName);
        generatedRoot.transform.SetParent(parent, false);

        float targetRoadHalfWidth = Mathf.Max(0.025f, splineRoadWidth * 0.5f);
        float profileHalfWidth = Mathf.Max(0.0001f, roadProfile.bounds.extents.x);
        float roadSize = targetRoadHalfWidth / profileHalfWidth;
        int roadSegmentCount = 0;
        int bridgeCount = 0;

        for (int listIndex = 0; listIndex < splineIndices.Count; listIndex++)
        {
            int splineIndex = splineIndices[listIndex];
            Vector2 planeCenter = FindSplinePlaneCenter(source, splineIndex);
            List<SplinePoint> mainPoints = BuildSourcePoints(
                source, splineIndex, splinePlaneScale, planeCenter);
            if (mainPoints.Count < 2)
            {
                continue;
            }

            var roadGroupObject = new GameObject("MyGameSplineMesh_Road_" + splineIndex);
            Transform roadGroup = roadGroupObject.transform;
            roadGroup.SetParent(generatedRoot.transform, false);

            bool isClosed = source.Splines[splineIndex].Closed;
            // A wider, darker-green copy of the road under it reads as the
            // grass rim that hugs the road in the source game.
            var rimMaterial = new Material(groundMaterial);
            Color rimColor = groundMaterial.HasProperty("_BaseColor")
                ? groundMaterial.GetColor("_BaseColor") : groundMaterial.color;
            rimColor = Color.Lerp(rimColor, new Color(0.18f, 0.5f, 0.2f, 1f),
                theme == RoadTheme.Town ? 0.18f : 0.45f);
            if (rimMaterial.HasProperty("_BaseColor")) rimMaterial.SetColor("_BaseColor", rimColor);
            rimMaterial.color = rimColor;
            // Town uses the same tan material as the authored lower board,
            // without the enlarged green spline border.
            if (theme != RoadTheme.Town && !CreateSplineMesh(
                    roadTemplate, mainPoints, isClosed, roadGroup, RimObjectName,
                    rimMaterial, roadProfile, roadSize, RimWidthScale, 0f, 1f, out message))
            {
                DestroyObject(generatedRoot);
                generatedRoot = null;
                return false;
            }

            if (!CreateSplineMesh(
                    roadTemplate,
                    mainPoints,
                    isClosed,
                    roadGroup,
                    "Road_Main",
                    roadMaterial,
                    roadProfile,
                    roadSize,
                    1f,
                    0f,
                    1f,
                    out message))
            {
                DestroyObject(generatedRoot);
                generatedRoot = null;
                return false;
            }
            roadSegmentCount++;

            if (!isClosed)
            {
                OpenRoadLayout layout;
                if (TryBuildOpenRoadLayout(
                        source, splineIndex, mainPoints, splinePlaneScale,
                        targetRoadHalfWidth,
                        planeCenter, out layout))
                {
                    MeshFilter mainFilter = roadGroup.Find("Road_Main")
                        .GetComponent<MeshFilter>();
                    float splineHalfWidth = MeasureEndpointHalfWidth(
                        mainFilter.sharedMesh, layout, targetRoadHalfWidth);
                    if (includeParkingBay && !CreateLower(
                            roadGroup, layout, splineHalfWidth,
                            parkingBayExtraWidthPerSide,
                            grassyDividerWidth,
                            roadMaterial,
                            rimMaterial,
                            out message))
                    {
                        DestroyObject(generatedRoot);
                        generatedRoot = null;
                        return false;
                    }
                }
            }

            if (bridgeTemplate != null)
            {
                List<Vector2> bridgeRanges = ExtractBridgeRanges(source, splineIndex);
                for (int rangeIndex = 0; rangeIndex < bridgeRanges.Count; rangeIndex++)
                {
                    Vector2 range = bridgeRanges[rangeIndex];
                    if (!CreateSplineMesh(
                            bridgeTemplate,
                            mainPoints,
                            isClosed,
                            generatedRoot.transform,
                            "MyGameSplineMesh_Bridge_" + splineIndex + "_" + rangeIndex,
                            null,
                            null,
                            1f,
                            1f,
                            range.x,
                            range.y,
                            out message))
                    {
                        DestroyObject(generatedRoot);
                        generatedRoot = null;
                        return false;
                    }
                    bridgeCount++;
                }
            }
        }

        if (generatedRoot.transform.childCount == 0)
        {
            DestroyObject(generatedRoot);
            generatedRoot = null;
            message = "Dreamteck did not generate any spline road objects.";
            return false;
        }

        message = "Dreamteck generated " + roadSegmentCount +
                  " road segment(s) with source board lower parts, and " +
                  bridgeCount + " bridge range(s).";
        return true;
    }

    /// <summary>Fits only the rendered pavement after the rigid bus grid has spawned.</summary>
    public static bool FitParkingBayToBuses(
        GameObject generatedRoot,
        SplineContainer gameplayPath,
        IReadOnlyList<BusController> buses,
        Camera camera,
        float parkingBayExtraWidthPerSide,
        float grassyDividerWidth,
        out ParkingBayAlignment alignment,
        out string message)
    {
        alignment = default(ParkingBayAlignment);
        message = string.Empty;
        Transform roadGroup = generatedRoot != null
            ? generatedRoot.transform.Find("MyGameSplineMesh_Road_0") : null;
        Transform main = roadGroup != null ? roadGroup.Find("Road_Main") : null;
        Transform lower = roadGroup != null ? roadGroup.Find("Road_StaticBoardLower") : null;
        if (main == null || lower == null || camera == null || buses == null || buses.Count == 0)
        {
            message = "Road, camera or spawned bus footprint is missing.";
            return false;
        }

        SplinePoint[] points = main.GetComponent<SplineComputer>()
            .GetPoints(SplineComputer.Space.Local);
        if (points.Length < 2)
        {
            message = "The rendered road has no open endpoints.";
            return false;
        }

        // Read the original rendered spline: the Unity container already holds
        // its projected gameplay counterpart at this point in level setup.
        Vector3 start = points[0].position;
        Vector3 end = points[points.Length - 1].position;
        Vector3 center = (start + end) * 0.5f;
        Vector3 side = Vector3.ProjectOnPlane(end - start, Vector3.forward).normalized;
        Vector3 outward = Vector3.Cross(side, Vector3.forward).normalized;
        Vector3 average = Vector3.zero;
        for (int i = 0; i < points.Length; i++) average += points[i].position;
        if (Vector3.Dot(outward, center - average / points.Length) < 0f)
            outward = -outward;

        float length = 0f;
        Mesh lowerMesh = lower.GetComponent<MeshFilter>().sharedMesh;
        bool importedLower = lowerMesh != null &&
            lowerMesh == Resources.Load<Mesh>(ImportedLowerResourcePath);
        if (!importedLower)
        {
            message = "The lower board must use the authored testRoad15.001 mesh.";
            return false;
        }
        if (lowerMesh != null && lowerMesh.isReadable)
        {
            foreach (Vector3 vertex in lowerMesh.vertices)
                length = Mathf.Max(length, Vector3.Dot(lower.localPosition + lower.localRotation *
                    Vector3.Scale(vertex, lower.localScale) - center, outward));
        }
        var layout = new OpenRoadLayout
        {
            start = start, end = end, side = side, outward = outward,
            extensionLength = length
        };
        var visualPlane = new Plane(roadGroup.forward, roadGroup.position);
        Vector2 busMin = Vector2.one * float.PositiveInfinity;
        Vector2 busMax = Vector2.one * float.NegativeInfinity;
        Bounds referenceBus = default(Bounds);
        Vector3 referenceOrigin = Vector3.zero;
        bool hasReference = false;
        for (int i = 0; i < buses.Count; i++)
        {
            BusController bus = buses[i];
            if (bus == null) continue;
            bool hasBounds = false;
            Bounds busBounds = default(Bounds);
            foreach (Renderer renderer in bus.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!hasBounds) { busBounds = renderer.bounds; hasBounds = true; }
                else busBounds.Encapsulate(renderer.bounds);
            }
            if (!hasBounds) continue;
            IncludeProjectedBounds(busBounds, camera, roadGroup, visualPlane,
                center, side, outward, ref busMin, ref busMax);
            if (!hasReference)
            {
                referenceBus = busBounds;
                referenceOrigin = bus.transform.position;
                hasReference = true;
            }
            else referenceBus.extents = Vector3.Max(referenceBus.extents, busBounds.extents);
        }
        if (!hasReference || float.IsInfinity(busMin.x))
        {
            message = "The buses have no visible bounds on the road plane.";
            return false;
        }

        // Extend the authored board with its Transform, keeping its top join fixed.
        // Do not apply procedural-divider offsets to the gameplay exit spline or gate.
        float halfWidth = MeasureEndpointHalfWidth(
            main.GetComponent<MeshFilter>().sharedMesh, layout, DefaultRoadHalfWidth);
        float padding = Mathf.Max(0.025f, halfWidth * 0.2f) +
                        Mathf.Max(0f, parkingBayExtraWidthPerSide);
        Vector3 meshOutward = lower.localRotation * Vector3.forward;
        Vector3 scale = lower.localScale;
        Vector3 topAnchor = lower.localPosition +
            meshOutward * (ImportedLowerTopZ * scale.z);
        float topDistance = Vector3.Dot(topAnchor - center, outward);
        float sourceLength = lowerMesh.bounds.max.z - ImportedLowerTopZ;
        scale.z = Mathf.Max(Mathf.Abs(scale.x),
            (busMax.y + padding - topDistance) / Mathf.Max(0.001f, sourceLength));
        lower.localScale = scale;
        lower.localPosition = topAnchor - meshOutward * (ImportedLowerTopZ * scale.z);
        BuildLowerRim(roadGroup, lower, lowerMesh, halfWidth * (RimWidthScale - 1f));
        return true;
    }

    private static void IncludeProjectedBounds(
        Bounds bounds, Camera camera, Transform roadGroup, Plane visualPlane,
        Vector3 center, Vector3 side, Vector3 outward,
        ref Vector2 minimum, ref Vector2 maximum)
    {
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                (corner & 1) == 0 ? -1f : 1f,
                (corner & 2) == 0 ? -1f : 1f,
                (corner & 4) == 0 ? -1f : 1f));
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0f) continue;
            Ray ray = camera.ViewportPointToRay(viewport);
            if (!visualPlane.Raycast(ray, out float distance)) continue;
            Vector3 local = roadGroup.InverseTransformPoint(ray.GetPoint(distance)) - center;
            var planePoint = new Vector2(Vector3.Dot(local, side), Vector3.Dot(local, outward));
            minimum = Vector2.Min(minimum, planePoint);
            maximum = Vector2.Max(maximum, planePoint);
        }
    }

    /// <summary>
    /// Uses the authored testRoad15.001 mesh for everything below the spline
    /// ends. Missing source art is reported instead of generating replacement geometry.
    /// </summary>
    private static bool CreateLower(
        Transform roadGroup,
        OpenRoadLayout layout,
        float splineHalfWidth,
        float parkingBayExtraWidthPerSide,
        float grassyDividerWidth,
        Material roadMaterial,
        Material rimMaterial,
        out string message)
    {
        Mesh imported = Resources.Load<Mesh>(ImportedLowerResourcePath);
        if (imported == null)
        {
            message = "Missing authored lower board mesh: Resources/" + ImportedLowerResourcePath;
            return false;
        }

        float separation = Vector3.Distance(layout.start, layout.end);
        float joinMargin = Mathf.Min(0.018f, splineHalfWidth * 0.13f);
        float fullWidth = (separation * 0.5f + splineHalfWidth + joinMargin +
                           Mathf.Max(0f, parkingBayExtraWidthPerSide)) * 2f;
        float scale = fullWidth / ImportedLowerWidth;
        Vector3 center = (layout.start + layout.end) * 0.5f;
        // Mesh +Z -> outward (below the spline ends), mesh +Y -> towards the
        // camera, mesh +X -> side (negative X scale undoes the mirroring).
        Quaternion rotation = Quaternion.LookRotation(layout.outward, Vector3.back);
        Vector3 topAnchor = center - layout.outward * (splineHalfWidth * 0.5f);

        Transform previous = roadGroup.Find("Road_StaticBoardLower");
        GameObject lower = previous != null ? previous.gameObject : new GameObject("Road_StaticBoardLower");
        lower.transform.SetParent(roadGroup, false);
        lower.transform.localRotation = rotation;
        lower.transform.localScale = new Vector3(-scale, scale, scale);
        lower.transform.localPosition = topAnchor - layout.outward * (ImportedLowerTopZ * scale);
        MeshFilter filter = lower.GetComponent<MeshFilter>();
        if (filter == null) filter = lower.AddComponent<MeshFilter>();
        filter.sharedMesh = imported;
        MeshRenderer renderer = lower.GetComponent<MeshRenderer>();
        if (renderer == null) renderer = lower.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { roadMaterial, rimMaterial };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        BuildLowerRim(roadGroup, lower.transform, imported,
            splineHalfWidth * (RimWidthScale - 1f));
        message = string.Empty;
        return true;
    }

    /// <summary>
    /// Outlines the bay by drawing its mesh in the dark rim colour at eight
    /// in-plane offsets, so any bay shape gets an even grass-green border.
    /// </summary>
    private static void BuildLowerRim(
        Transform roadGroup, Transform lower, Mesh mesh, float radius)
    {
        for (int i = roadGroup.childCount - 1; i >= 0; i--)
        {
            Transform child = roadGroup.GetChild(i);
            if (child.name.StartsWith(RimObjectName + "_Lower_", System.StringComparison.Ordinal))
                DestroyObject(child.gameObject);
        }

        Transform mainRim = roadGroup.Find(RimObjectName);
        Renderer mainRimRenderer = mainRim != null ? mainRim.GetComponent<Renderer>() : null;
        if (mainRimRenderer == null || mesh == null || radius <= 0f) return;

        const int Directions = 8;
        for (int i = 0; i < Directions; i++)
        {
            float angle = Mathf.PI * 2f * i / Directions;
            var copy = new GameObject(RimObjectName + "_Lower_" + i);
            copy.transform.SetParent(roadGroup, false);
            copy.transform.localRotation = lower.localRotation;
            copy.transform.localScale = lower.localScale;
            copy.transform.localPosition = lower.localPosition + new Vector3(
                Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer copyRenderer = copy.AddComponent<MeshRenderer>();
            copyRenderer.sharedMaterial = mainRimRenderer.sharedMaterial;
            copyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static float MeasureEndpointHalfWidth(
        Mesh roadMesh,
        OpenRoadLayout layout,
        float fallbackHalfWidth)
    {
        if (roadMesh == null) return fallbackHalfWidth;

        Vector3[] positions = roadMesh.vertices;
        float sum = 0f;
        int validEndpoints = 0;
        Vector3[] endpoints = { layout.start, layout.end };
        float endpointLimit = Vector3.Distance(layout.start, layout.end) * 0.35f;
        for (int endpointIndex = 0; endpointIndex < endpoints.Length; endpointIndex++)
        {
            float capOutward = float.NegativeInfinity;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 delta = positions[i] - endpoints[endpointIndex];
                if (Mathf.Abs(Vector3.Dot(delta, layout.side)) > endpointLimit)
                    continue;
                capOutward = Mathf.Max(capOutward,
                    Vector3.Dot(delta, layout.outward));
            }
            if (float.IsNegativeInfinity(capOutward)) continue;

            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            int count = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 delta = positions[i] - endpoints[endpointIndex];
                float across = Vector3.Dot(delta, layout.side);
                float outward = Vector3.Dot(delta, layout.outward);
                if (capOutward - outward > 0.0045f ||
                    Mathf.Abs(across) > endpointLimit)
                    continue;
                minimum = Mathf.Min(minimum, across);
                maximum = Mathf.Max(maximum, across);
                count++;
            }
            if (count == 0) continue;
            sum += (maximum - minimum) * 0.5f;
            validEndpoints++;
        }

        return validEndpoints == 0
            ? fallbackHalfWidth
            : Mathf.Clamp(sum / validEndpoints, 0.015f, endpointLimit * 0.85f);
    }

    private static bool CreateSplineMesh(
        GameObject template,
        List<SplinePoint> points,
        bool closed,
        Transform parent,
        string objectName,
        Material roadMaterial,
        Mesh fallbackProfile,
        float size,
        float widthScale,
        float clipFrom,
        float clipTo,
        out string message)
    {
        message = string.Empty;
        if (template == null || points == null || points.Count < 2)
        {
            message = objectName + " has no Dreamteck template or path points.";
            return false;
        }

        GameObject instance = Object.Instantiate(template, parent, false);
        instance.name = objectName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        SplineComputer computer = instance.GetComponent<SplineComputer>();
        if (computer == null)
        {
            computer = instance.AddComponent<SplineComputer>();
        }
        computer.type = Dreamteck.Splines.Spline.Type.Bezier;

        // Dreamteck 3.0.5 applies an Extrude channel's thickness from the
        // evaluated SplinePoint.size. MeshGenerator.size is not used by the
        // Extrude path, so copy the requested road scale into every point.
        // Keep the source list untouched because it is reused by bridge parts.
        SplinePoint[] renderPoints = points.ToArray();
        for (int pointIndex = 0; pointIndex < renderPoints.Length; pointIndex++)
        {
            renderPoints[pointIndex].size *= size;
        }
        computer.SetPoints(renderPoints, SplineComputer.Space.Local);
        if (closed)
        {
            computer.Close();
        }
        computer.RebuildImmediate(true, true);

        DreamSplineMesh splineMesh = instance.GetComponent<DreamSplineMesh>();
        if (splineMesh == null)
        {
            splineMesh = instance.AddComponent<DreamSplineMesh>();
        }

        if (splineMesh.GetChannelCount() == 0)
        {
            if (fallbackProfile == null)
            {
                DestroyObject(instance);
                message = objectName + " has no serialized Dreamteck channel.";
                return false;
            }
            splineMesh.AddChannel(fallbackProfile, "Road");
        }
        else if (splineMesh.GetChannel(0).GetMeshCount() == 0 && fallbackProfile != null)
        {
            splineMesh.GetChannel(0).AddMesh(fallbackProfile);
        }

        for (int channelIndex = 0; channelIndex < splineMesh.GetChannelCount(); channelIndex++)
        {
            DreamSplineMesh.Channel channel = splineMesh.GetChannel(channelIndex);
            channel.type = DreamSplineMesh.Channel.Type.Extrude;
            channel.clipFrom = clipFrom;
            channel.clipTo = clipTo;
            // A fixed density gives smooth curves without autoCount producing
            // thousands of copies from the source profile's 0.02 unit length.
            channel.autoCount = false;
            channel.count = Mathf.Clamp(
                Mathf.CeilToInt(computer.CalculateLength() * 32f), 8, 192);
            channel.minScale = new Vector3(widthScale, 1f, 1f);
            channel.maxScale = channel.minScale;

            if (roadMaterial != null)
            {
                // The imported profile repeats U=0/1 on each strip. This
                // project's road shader interprets U as distance across the
                // entire road, so those repeats shade the whole bed. Give
                // every profile vertex one continuous cross-road coordinate.
                float textureScale = Mathf.Max(0.001f,
                    Mathf.Abs(roadMaterial.mainTextureScale.x));
                for (int meshIndex = 0; meshIndex < channel.GetMeshCount(); meshIndex++)
                {
                    DreamSplineMesh.Channel.MeshDefinition definition =
                        channel.GetMesh(meshIndex);
                    Vector2[] definitionUV = definition.uv;
                    float minX = definition.bounds.min.x;
                    float width = Mathf.Max(0.0001f, definition.bounds.size.x);
                    for (int vertexIndex = 0; vertexIndex < definitionUV.Length; vertexIndex++)
                    {
                        definitionUV[vertexIndex].x =
                            Mathf.Clamp01((definition.vertices[vertexIndex].x - minX) / width)
                            / textureScale;
                    }
                }
            }
        }

        splineMesh.autoUpdate = false;
        splineMesh.spline = computer;
        splineMesh.clipFrom = clipFrom;
        splineMesh.clipTo = clipTo;
        splineMesh.size = size;
        // Dreamteck 3.0.5 SplineMesh uses MakeDoublesidedHalf on its combined
        // mesh without reserving a second half; that overwrites the latter
        // half of an open spline. Flip the faces to expose the road top to the
        // camera while preserving every segment of the curve.
        splineMesh.doubleSided = false;
        splineMesh.flipFaces = true;
        splineMesh.RebuildImmediate();

        MeshFilter filter = instance.GetComponent<MeshFilter>();
        MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0 ||
            renderer == null)
        {
            DestroyObject(instance);
            message = objectName + " produced an empty Dreamteck SplineMesh.";
            return false;
        }

        if (roadMaterial != null)
        {
            int materialCount = Mathf.Max(1, filter.sharedMesh.subMeshCount);
            var materials = new Material[materialCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = roadMaterial;
            renderer.sharedMaterials = materials;
        }

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        return true;
    }

    private static List<SplinePoint> BuildSourcePoints(
        SplineContainer source,
        int splineIndex,
        Vector2 planeScale,
        Vector2 planeCenter)
    {
        UnityEngine.Splines.Spline spline = source.Splines[splineIndex];
        var points = new List<SplinePoint>(spline.Count);
        for (int i = 0; i < spline.Count; i++)
        {
            BezierKnot knot = spline[i];
            Vector3 position = ToVector3(knot.Position);
            Vector3 tangentIn = position + ToVector3(math.rotate(knot.Rotation, knot.TangentIn));
            Vector3 tangentOut = position + ToVector3(math.rotate(knot.Rotation, knot.TangentOut));
            position = ScalePlanePoint(position, planeCenter, planeScale);
            tangentIn = ScalePlanePoint(tangentIn, planeCenter, planeScale);
            tangentOut = ScalePlanePoint(tangentOut, planeCenter, planeScale);

            var point = new SplinePoint(
                position,
                tangentIn,
                tangentOut,
                Vector3.forward,
                1f,
                Color.white);
            point.type = SplinePoint.Type.Broken;
            points.Add(point);
        }
        return points;
    }

    private static bool TryBuildOpenRoadLayout(
        SplineContainer source,
        int splineIndex,
        List<SplinePoint> points,
        Vector2 planeScale,
        float splineHalfWidth,
        Vector2 planeCenter,
        out OpenRoadLayout layout)
    {
        layout = default(OpenRoadLayout);
        if (points.Count < 2) return false;

        Vector3 start = points[0].position;
        Vector3 end = points[points.Count - 1].position;
        Vector3 side = Vector3.ProjectOnPlane(end - start, Vector3.forward);
        float separation = side.magnitude;
        if (separation <= splineHalfWidth * 2.2f) return false;
        side /= separation;

        Vector3 startTangent = points[0].tangent2 - start;
        Vector3 endTangent = end - points[points.Count - 1].tangent;
        startTangent = Vector3.ProjectOnPlane(startTangent, Vector3.forward).normalized;
        endTangent = Vector3.ProjectOnPlane(endTangent, Vector3.forward).normalized;

        Vector3 sampleCenter;
        float splineDepth;
        FindSplineDepth(
            source, splineIndex, planeScale, planeCenter,
            out sampleCenter, out splineDepth);

        Vector3 outward = Vector3.Cross(side, Vector3.forward);
        Vector3 endpointCenter = (start + end) * 0.5f;
        Vector3 awayFromSpline = Vector3.ProjectOnPlane(endpointCenter - sampleCenter, Vector3.forward);
        if (outward.sqrMagnitude < 0.000001f) outward = awayFromSpline;
        if (outward.sqrMagnitude < 0.000001f) outward = Vector3.Cross(side, Vector3.forward);
        outward.Normalize();
        if (awayFromSpline.sqrMagnitude > 0.000001f && Vector3.Dot(outward, awayFromSpline) < 0f)
        {
            outward = -outward;
        }

        float extensionLength = Mathf.Max(
            splineDepth * 2.1f,
            separation * 1.8f);

        layout = new OpenRoadLayout
        {
            start = start,
            end = end,
            side = side,
            outward = outward,
            extensionLength = extensionLength
        };
        return true;
    }

    private static void FindSplineDepth(
        SplineContainer source,
        int splineIndex,
        Vector2 planeScale,
        Vector2 planeCenter,
        out Vector3 center,
        out float depth)
    {
        Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);
        Vector3 sum = Vector3.zero;
        for (int i = 0; i <= BoundsSamples; i++)
        {
            float t = i / (float)BoundsSamples;
            float3 worldData = source.EvaluatePosition(splineIndex, t);
            Vector3 local = source.transform.InverseTransformPoint(ToVector3(worldData));
            local = ScalePlanePoint(local, planeCenter, planeScale);
            minimum = Vector3.Min(minimum, local);
            maximum = Vector3.Max(maximum, local);
            sum += local;
        }
        center = sum / (BoundsSamples + 1f);
        depth = Mathf.Max(0.01f, maximum.y - minimum.y);
    }

    private static Vector2 FindSplinePlaneCenter(SplineContainer source, int splineIndex)
    {
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i <= BoundsSamples; i++)
        {
            float3 worldData = source.EvaluatePosition(splineIndex, i / (float)BoundsSamples);
            Vector3 local = source.transform.InverseTransformPoint(ToVector3(worldData));
            minimum = Vector2.Min(minimum, new Vector2(local.x, local.y));
            maximum = Vector2.Max(maximum, new Vector2(local.x, local.y));
        }
        return (minimum + maximum) * 0.5f;
    }

    private static Vector3 ScalePlanePoint(Vector3 point, Vector2 center, Vector2 scale)
    {
        point.x = center.x + (point.x - center.x) * scale.x;
        point.y = center.y + (point.y - center.y) * scale.y;
        return point;
    }

    private static List<Vector2> ExtractBridgeRanges(SplineContainer source, int splineIndex)
    {
        var ranges = new List<Vector2>();
        bool detected = false;
        float start = 0f;
        for (float sample = 0f; sample <= 1.00001f; sample += BridgeScanStep)
        {
            float t = Mathf.Min(sample, 1f);
            float3 point = source.EvaluatePosition(splineIndex, t);
            bool isBridge = point.y <= BridgeHeightThreshold;
            if (isBridge && !detected)
            {
                start = t;
                detected = true;
            }
            else if (!isBridge && detected)
            {
                AddBridgeRange(ranges, start, t);
                detected = false;
            }
        }
        if (detected) AddBridgeRange(ranges, start, 1f);
        return ranges;
    }

    private static void AddBridgeRange(List<Vector2> ranges, float start, float end)
    {
        start = Mathf.Max(0f, start - BridgeVisualPadding);
        end = Mathf.Min(1f, end + BridgeVisualPadding);
        if (end > start) ranges.Add(new Vector2(start, end));
    }

    private static List<int> CollectSplineIndices(
        SplineContainer source,
        bool renderAllSplines,
        int selectedSplineIndex)
    {
        var indices = new List<int>();
        if (renderAllSplines)
        {
            for (int i = 0; i < source.Splines.Count; i++)
            {
                if (source.Splines[i] != null && source.Splines[i].Count >= 2) indices.Add(i);
            }
        }
        else if (selectedSplineIndex >= 0 && selectedSplineIndex < source.Splines.Count &&
                 source.Splines[selectedSplineIndex] != null &&
                 source.Splines[selectedSplineIndex].Count >= 2)
        {
            indices.Add(selectedSplineIndex);
        }
        return indices;
    }

    private static Vector3 ToVector3(float3 value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    private static void DestroyObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Object.Destroy(target);
        else Object.DestroyImmediate(target);
    }
}
