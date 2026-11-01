using System.Collections.Generic;
using Dreamteck.Splines;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using DreamSplineMesh = Dreamteck.Splines.SplineMesh;

/// <summary>
/// Builds the visible road with Dreamteck SplineMesh. The gameplay path remains
/// a Unity Spline; this class mirrors it into Dreamteck and derives the lower
/// connector, bus staging area and exit lane from the two open endpoints.
/// The lower area follows the same material and rounded branch proportions
/// as the static board shown in the source game.
/// </summary>
public static class DreamteckSplineRoadRenderer
{
    private const string RoadPrefabResourcePath = "MyGameSplineMesh_Road";
    private const string BridgePrefabResourcePath = "MyGameSplineMesh_Bridge";
    private const string RoadProfileResourcePath = "MyGameSplineRoadPiece";
    private const string ThemeMaterialsResourcePath = "RoadThemeMaterials";

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
                    if (includeParkingBay && !CreateStaticBoardLower(
                            roadGroup, layout, splineHalfWidth,
                            parkingBayExtraWidthPerSide,
                            grassyDividerWidth,
                            roadMaterial,
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

    private static bool CreateStaticBoardLower(
        Transform roadGroup,
        OpenRoadLayout layout,
        float splineHalfWidth,
        float parkingBayExtraWidthPerSide,
        float grassyDividerWidth,
        Material roadMaterial,
        out string message)
    {
        // The lower road has one continuous crossbar. Below it, the road
        // branches into a wide bus bay and a narrow exit lane. All positions
        // derive from the two open spline endpoints, so other open levels use
        // the same rounded layout without fixed scene coordinates.
        float separation = Vector3.Distance(layout.start, layout.end);
        float joinMargin = Mathf.Min(0.018f, splineHalfWidth * 0.13f);
        // Add room around the full queue footprint so every bus stays on the
        // paved bay with a visible shoulder. The generated lower map apron
        // grows from the endpoints, so this also expands its outer footprint.
        float halfWidth = separation * 0.5f + splineHalfWidth + joinMargin +
                          Mathf.Max(0f, parkingBayExtraWidthPerSide);
        float leftOuter = -halfWidth;
        float rightOuter = halfWidth;
        float busOuter = Mathf.Lerp(leftOuter, rightOuter, 0.16f);
        float dividerCenter = Mathf.Lerp(leftOuter, rightOuter, 0.825f);
        float dividerHalfWidth = halfWidth * Mathf.Clamp(grassyDividerWidth, 0f, 0.18f);
        float busInner = dividerCenter - dividerHalfWidth;
        float exitInner = dividerCenter + dividerHalfWidth;
        float dividerTip = (busInner + exitInner) * 0.5f;
        float length = Mathf.Max(layout.extensionLength, splineHalfWidth * 4f);
        float shoulderStart = Mathf.Min(0.325f, length * 0.07f + 0.055f);
        float shoulderEnd = Mathf.Min(0.875f, length * 0.30f + 0.055f);
        float outerCornerRadius = Mathf.Min(splineHalfWidth * 0.48f,
            (shoulderEnd - shoulderStart) * 0.25f);
        float shoulderTop = shoulderStart - outerCornerRadius;
        float islandRoadTop = -Mathf.Min(0.055f, splineHalfWidth * 0.7f);
        float joinStart = -Mathf.Min(0.06f, splineHalfWidth * 0.45f);
        float edgeWidth = Mathf.Min(splineHalfWidth * 0.32f, halfWidth * 0.08f);
        float textureScale = Mathf.Max(0.001f,
            Mathf.Abs(roadMaterial.mainTextureScale.x));
        Vector3 center = (layout.start + layout.end) * 0.5f;
        bool reverseWinding = Vector3.Dot(
            Vector3.Cross(layout.side, layout.outward), Vector3.back) < 0f;

        var vertices = new List<Vector3>(1100);
        var uv = new List<Vector2>(1100);
        var triangles = new List<int>(1800);

        AppendMainToLowerTransition(
            vertices, uv, triangles, center, layout.side, layout.outward,
            joinStart, islandRoadTop, shoulderTop,
            leftOuter, rightOuter, separation, splineHalfWidth,
            edgeWidth, textureScale, reverseWinding);

        const int ShoulderSegments = 24;
        for (int segment = 0; segment < ShoulderSegments; segment++)
        {
            float t0 = segment / (float)ShoulderSegments;
            float t1 = (segment + 1f) / ShoulderSegments;
            float y0 = Mathf.Lerp(shoulderTop, shoulderEnd, t0);
            float y1 = Mathf.Lerp(shoulderTop, shoulderEnd, t1);
            float outerLeft0 = RoundedOuterShoulder(
                y0, shoulderTop, shoulderStart, shoulderEnd,
                leftOuter, busOuter, outerCornerRadius);
            float outerLeft1 = RoundedOuterShoulder(
                y1, shoulderTop, shoulderStart, shoulderEnd,
                leftOuter, busOuter, outerCornerRadius);
            float dividerT0 = Mathf.InverseLerp(shoulderStart, shoulderEnd, y0);
            float dividerT1 = Mathf.InverseLerp(shoulderStart, shoulderEnd, y1);
            float divider0 = Mathf.Sqrt(1f - (1f - dividerT0) * (1f - dividerT0));
            float divider1 = Mathf.Sqrt(1f - (1f - dividerT1) * (1f - dividerT1));

            AppendLowerRoadSpan(
                vertices, uv, triangles, center, layout.side, layout.outward,
                y0, y1,
                outerLeft0,
                Mathf.Lerp(dividerTip, busInner, divider0),
                outerLeft1,
                Mathf.Lerp(dividerTip, busInner, divider1),
                edgeWidth, textureScale, reverseWinding);
            AppendLowerRoadSpan(
                vertices, uv, triangles, center, layout.side, layout.outward,
                y0, y1,
                Mathf.Lerp(dividerTip, exitInner, divider0), rightOuter,
                Mathf.Lerp(dividerTip, exitInner, divider1), rightOuter,
                edgeWidth, textureScale, reverseWinding);
        }

        AppendLowerRoadSpan(
            vertices, uv, triangles, center, layout.side, layout.outward,
            shoulderEnd, length,
            busOuter, busInner, busOuter, busInner,
            edgeWidth, textureScale, reverseWinding);
        AppendLowerRoadSpan(
            vertices, uv, triangles, center, layout.side, layout.outward,
            shoulderEnd, length,
            exitInner, rightOuter, exitInner, rightOuter,
            edgeWidth, textureScale, reverseWinding);
        var mesh = new Mesh { name = "MyGame_RoundedBoardLower" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        var normals = new Vector3[vertices.Count];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.back;
        mesh.normals = normals;
        mesh.RecalculateBounds();

        var lower = new GameObject("Road_StaticBoardLower");
        lower.transform.SetParent(roadGroup, false);
        lower.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = lower.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = roadMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        message = string.Empty;
        return true;
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

    private static float RoundedOuterShoulder(
        float y,
        float top,
        float turn,
        float bottom,
        float outer,
        float bus,
        float radius)
    {
        if (y <= turn)
        {
            float t = Mathf.InverseLerp(top, turn, y);
            return outer + radius *
                (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - t * t)));
        }

        float progress = Mathf.InverseLerp(turn, bottom, y);
        float round = Mathf.Sqrt(Mathf.Max(0f,
            1f - (1f - progress) * (1f - progress)));
        return outer + radius + (bus - outer - radius) * round;
    }

    private static void AppendMainToLowerTransition(
        List<Vector3> vertices,
        List<Vector2> uv,
        List<int> triangles,
        Vector3 center,
        Vector3 side,
        Vector3 outward,
        float from,
        float islandRoadTop,
        float to,
        float leftOuter,
        float rightOuter,
        float separation,
        float splineHalfWidth,
        float edgeWidth,
        float textureScale,
        bool reverseWinding)
    {
        float leftCenter = -separation * 0.5f;
        float rightCenter = separation * 0.5f;
        float innerJoin = Mathf.Min(0.03f, splineHalfWidth * 0.22f);
        float leftInner = leftCenter + splineHalfWidth - innerJoin;
        float rightInner = rightCenter - splineHalfWidth + innerJoin;
        float cornerRadius = Mathf.Min(splineHalfWidth * 0.55f + innerJoin * 0.6f,
            (rightInner - leftInner) * 0.10f);
        float innerCornerStart = Mathf.Min(
            from + Mathf.Min(0.03f, splineHalfWidth * 0.22f),
            islandRoadTop + Mathf.Min(0.02f, splineHalfWidth * 0.45f));
        var nearX = new List<float>(32);
        var nearY = new List<float>(32);
        var nearU = new List<float>(32);
        var farU = new List<float>(32);
        AddTransitionColumn(nearX, nearY, nearU, farU,
            leftOuter, from, 1f, 0f);
        AddTransitionColumn(nearX, nearY, nearU, farU,
            leftOuter + edgeWidth, from, 0.7f, 0.5f);
        AddTransitionColumn(nearX, nearY, nearU, farU,
            leftCenter, from, 0.5f, 0.5f);
        const int CornerSegments = 8;
        for (int i = 0; i <= CornerSegments; i++)
        {
            float t = i / (float)CornerSegments;
            float angle = Mathf.PI * 0.5f * t;
            AddTransitionColumn(nearX, nearY, nearU, farU,
                leftInner + cornerRadius * (1f - Mathf.Cos(angle)),
                Mathf.Lerp(innerCornerStart, islandRoadTop, Mathf.Sin(angle)),
                0.5f * t, 0.5f);
        }
        for (int i = 0; i <= CornerSegments; i++)
        {
            float t = i / (float)CornerSegments;
            float angle = Mathf.PI * 0.5f * (1f - t);
            AddTransitionColumn(nearX, nearY, nearU, farU,
                rightInner - cornerRadius * (1f - Mathf.Cos(angle)),
                Mathf.Lerp(innerCornerStart, islandRoadTop, Mathf.Sin(angle)),
                0.5f * (1f - t), 0.5f);
        }
        AddTransitionColumn(nearX, nearY, nearU, farU,
            rightCenter, from, 0.5f, 0.5f);
        AddTransitionColumn(nearX, nearY, nearU, farU,
            rightOuter - edgeWidth, from, 0.7f, 0.5f);
        AddTransitionColumn(nearX, nearY, nearU, farU,
            rightOuter, from, 1f, 1f);
        var farX = new List<float>(nearX);
        nearX[0] = leftCenter - splineHalfWidth;
        nearX[nearX.Count - 1] = rightCenter + splineHalfWidth;
        // Dreamteck's profile runs from U=1 on each outside bank to U=0 on
        // each inside bank. Match that at the join before blending into the
        // broad lower surface; the material itself is shared unchanged.
        float actualLeftInner = leftCenter + splineHalfWidth;
        float actualRightInner = rightCenter - splineHalfWidth;
        int first = vertices.Count;
        for (int i = 0; i < nearX.Count; i++)
        {
            float x = nearX[i];
            float profileU = nearU[i];
            if (i <= 3)
                profileU = Mathf.Clamp01(
                    (actualLeftInner - x) / (2f * splineHalfWidth));
            else if (i >= nearX.Count - 4)
                profileU = Mathf.Clamp01(
                    (x - actualRightInner) / (2f * splineHalfWidth));
            vertices.Add(center + side * nearX[i] + outward * nearY[i] +
                         Vector3.back * 0.02f);
            uv.Add(new Vector2(profileU / textureScale, 0.83f));
        }
        for (int i = 0; i < nearX.Count; i++)
        {
            vertices.Add(center + side * farX[i] + outward * to +
                         Vector3.forward * 0.02f);
            uv.Add(new Vector2(farU[i] / textureScale, 0.83f));
        }
        for (int i = 0; i < nearX.Count - 1; i++)
        {
            int a = first + i;
            int b = first + i + 1;
            int c = first + nearX.Count + i + 1;
            int d = first + nearX.Count + i;
            if (reverseWinding)
            {
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
            }
            else
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
        }
    }

    private static void AddTransitionColumn(
        List<float> x,
        List<float> y,
        List<float> nearU,
        List<float> farU,
        float across,
        float along,
        float mainUv,
        float lowerUv)
    {
        x.Add(across);
        y.Add(along);
        nearU.Add(mainUv);
        farU.Add(lowerUv);
    }

    private static void AppendLowerRoadSpan(
        List<Vector3> vertices,
        List<Vector2> uv,
        List<int> triangles,
        Vector3 center,
        Vector3 side,
        Vector3 outward,
        float from,
        float to,
        float leftFrom,
        float rightFrom,
        float leftTo,
        float rightTo,
        float edgeWidth,
        float textureScale,
        bool reverseWinding)
    {
        float fromBand = Mathf.Min(edgeWidth, (rightFrom - leftFrom) * 0.25f);
        float toBand = Mathf.Min(edgeWidth, (rightTo - leftTo) * 0.25f);
        float[] nearX = {
            leftFrom, leftFrom + fromBand, rightFrom - fromBand, rightFrom
        };
        float[] farX = {
            leftTo, leftTo + toBand, rightTo - toBand, rightTo
        };
        float[] across = { 0f, 0.5f, 0.5f, 1f };
        int first = vertices.Count;
        Vector3 surface = Vector3.forward * 0.02f;
        for (int i = 0; i < 4; i++)
        {
            vertices.Add(center + side * nearX[i] + outward * from + surface);
            uv.Add(new Vector2(across[i] / textureScale, 0.83f));
        }
        for (int i = 0; i < 4; i++)
        {
            vertices.Add(center + side * farX[i] + outward * to + surface);
            uv.Add(new Vector2(across[i] / textureScale, 0.83f));
        }
        for (int i = 0; i < 3; i++)
        {
            int a = first + i;
            int b = first + i + 1;
            int c = first + i + 5;
            int d = first + i + 4;
            if (reverseWinding)
            {
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
            }
            else
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
        }
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

        Vector3 outward = Vector3.ProjectOnPlane(endTangent - startTangent, Vector3.forward);
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
