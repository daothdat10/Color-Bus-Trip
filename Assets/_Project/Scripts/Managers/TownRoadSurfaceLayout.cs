using System.Collections.Generic;
using Dreamteck.Splines;
using UnityEngine;

/// <summary>Fits the Town road profile to the authored board without adding a grass overlay.</summary>
public sealed class TownRoadSurfaceLayout
{
    private Transform board;
    private Material roadMaterial;
    public float GrassHeight { get; private set; }
    public TownLevelPath Path { get; private set; }
    public float JoinError { get; private set; }

    private TownRoadSurfaceLayout(float grassHeight)
    {
        GrassHeight = grassHeight;
    }

    public static TownRoadSurfaceLayout Build(Transform group, Transform board, Material road,
        IReadOnlyList<float> knotSizes, float gameplayY)
    {
        if (group == null || board == null) return null;
        Transform main = group.Find("Road_Main");
        Transform fill = board.Find("Road_InteriorFill");
        Mesh profile = Resources.Load<Mesh>("MyGameSplineRoadPiece");
        Mesh authored = Resources.Load<Mesh>("testRoad15.001");
        if (main == null || fill == null || profile == null || authored == null) return null;
        SplineComputer computer = main.GetComponent<SplineComputer>();
        SplinePoint[] points = computer.GetPoints(SplineComputer.Space.Local);
        if (points.Length < 2) return null;

        // The source art joins at z=0.3; its two bank centres are x=-0.19/+0.19.
        float grassY = board.TransformPoint(new Vector3(0f, authored.bounds.max.y, 0f)).y;
        float floorY = fill.position.y;
        Vector3 leftJoin = board.TransformPoint(new Vector3(-0.19f, fill.localPosition.y, 0.3f));
        Vector3 rightJoin = board.TransformPoint(new Vector3(0.19f, fill.localPosition.y, 0.3f));
        var frame = new TownLevelPath(board, gameplayY);
        var worldPath = new List<Vector3>();
        // Evaluate the saved local Bezier points: Dreamteck's world sample cache still
        // contains the previous transform during this initial layout pass.
        const int samples = 64;
        var widths = new List<float>();
        for (int segment = 0; segment < points.Length - 1; segment++)
        for (int i = segment == 0 ? 0 : 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            float u = 1f - t;
            Vector3 local = u * u * u * points[segment].position +
                3f * u * u * t * points[segment].tangent2 +
                3f * u * t * t * points[segment + 1].tangent +
                t * t * t * points[segment + 1].position;
            worldPath.Add(frame.ToWorld(new Vector3(local.x, local.y, 0f)));
            widths.Add(knotSizes != null && knotSizes.Count == points.Length
                ? Mathf.Lerp(knotSizes[segment], knotSizes[segment + 1], t) : 1f);
        }

        // Keep the source curve unchanged. Only the profile at the fixed join
        // blends into the lower board's cross-section.
        const float joinBlendLength = 0.45f;
        float textureScale = Mathf.Max(0.001f, Mathf.Abs(road.mainTextureScale.x));
        Vector3[] profileVertices = profile.vertices;
        Vector3[] profileNormals = profile.normals;
        int count = profileVertices.Length;
        var vertices = new Vector3[count * (worldPath.Count - 1)];
        var uv = new Vector2[vertices.Length];
        var normals = new Vector3[vertices.Length];
        var triangles = new List<int>[profile.subMeshCount];
        var profileTriangles = new int[profile.subMeshCount][];
        for (int s = 0; s < triangles.Length; s++)
        {
            triangles[s] = new List<int>();
            profileTriangles[s] = profile.GetTriangles(s);
        }
        float floorMin = float.PositiveInfinity, floorMax = float.NegativeInfinity;
        foreach (int id in profile.GetTriangles(2))
        {
            floorMin = Mathf.Min(floorMin, profileVertices[id].x);
            floorMax = Mathf.Max(floorMax, profileVertices[id].x);
        }
        var floorIds = new HashSet<int>(profile.GetTriangles(2));
        for (int segment = 0; segment < worldPath.Count - 1; segment++)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 v = profileVertices[i];
                int station = v.z > 0f ? segment + 1 : segment;
                float endpointDistance = Mathf.Min(Vector3.Distance(worldPath[station], leftJoin),
                    Vector3.Distance(worldPath[station], rightJoin));
                float joinWeight = 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(endpointDistance / joinBlendLength));
                Vector3 forward = worldPath[Mathf.Min(station + 1, worldPath.Count - 1)] -
                                  worldPath[Mathf.Max(station - 1, 0)];
                Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                // The source profile's positive bank has an asymmetric bottom
                // (0.02766), while both fixed joins use exactly 0.03.
                float crossX = v.x;
                if (v.x > 0.027f && v.x < 0.03f && v.y < 0.001f)
                    crossX = Mathf.Lerp(v.x, 0.03f, joinWeight);
                Vector3 world = worldPath[station] + right * (crossX * frame.ProfileWorldScale * widths[station]);
                // Lift the profile's floor by the same amount as the fill plane.
                // Otherwise its bank ends below the plane and leaves a visible seam.
                world.y = floorY + v.y * frame.ProfileWorldScale * widths[station];
                if (!floorIds.Contains(i))
                    world.y = Mathf.Lerp(world.y, board.TransformPoint(new Vector3(0f, v.y, 0f)).y, joinWeight);
                vertices[segment * count + i] = main.InverseTransformPoint(world);
                Vector3 sourceNormal = profileNormals[i];
                // The lower board uses a flat fill plane. Keep its floor normals
                // flat too, while retaining the source bank's rounded normals.
                Vector3 worldNormal = floorIds.Contains(i) ? Vector3.up :
                    right * sourceNormal.x + Vector3.up * sourceNormal.y + forward.normalized * sourceNormal.z;
                normals[segment * count + i] = main.localToWorldMatrix.transpose.MultiplyVector(worldNormal).normalized;
                float across = floorIds.Contains(i) ? Mathf.InverseLerp(floorMin, floorMax, v.x) :
                    Mathf.InverseLerp(profile.bounds.min.x, profile.bounds.max.x, v.x);
                uv[segment * count + i] = new Vector2(across / textureScale, station / (float)(worldPath.Count - 1));
            }
            for (int s = 0; s < triangles.Length; s++)
                foreach (int id in profileTriangles[s]) triangles[s].Add(segment * count + id);
        }
        Mesh mesh = new Mesh { name = "TownRoad_AuthoredProfile",
            indexFormat = vertices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16,
            subMeshCount = triangles.Length };
        mesh.vertices = vertices;
        mesh.uv = uv;
        for (int s = 0; s < triangles.Length; s++)
        {
            int[] indices = triangles[s].ToArray();
            // The profile's longitudinal axis is reflected by this route's winding.
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = main.TransformPoint(vertices[indices[i]]);
                Vector3 b = main.TransformPoint(vertices[indices[i + 1]]);
                Vector3 c = main.TransformPoint(vertices[indices[i + 2]]);
                if (s != 1 && Vector3.Cross(b - a, c - a).y < 0f)
                {
                    int swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap;
                }
            }
            mesh.SetTriangles(indices, s);
        }
        mesh.normals = normals;
        mesh.RecalculateBounds();
        main.GetComponent<MeshFilter>().sharedMesh = mesh;
        Material mask = Resources.Load<Material>("TownSplineMask");
        Material bankTemplate = Resources.Load<Material>("TownSplineBank");
        if (mask != null && bankTemplate != null)
        {
            Material bank = new Material(bankTemplate);
            Material floor = new Material(road);
            Vector3 up = main.InverseTransformDirection(Vector3.up);
            Vector3 light = main.InverseTransformDirection(new Vector3(0f, 5f, -1f));
            foreach (Material material in new[] { bank, floor })
            {
                material.SetVector("_SurfaceNormal", up);
                material.SetVector("_LightDirection", light);
                material.SetFloat("_ZWrite", 1f);
            }
            bank.SetFloat("_Cull", 0f);
            bank.SetFloat("_RibbonMode", 0f);
            bank.renderQueue = 1930;
            floor.SetFloat("_RibbonMode", 1f);
            floor.SetFloat("_RibbonDistanceScale", floorMax - floorMin);
            floor.renderQueue = 1950;
            main.GetComponent<MeshRenderer>().sharedMaterials = new[] { mask, bank, floor };
        }

        var layout = new TownRoadSurfaceLayout(grassY);
        layout.board = board;
        layout.Path = frame;
        layout.roadMaterial = road;
        if (board.gameObject.scene.IsValid()) layout.ConfigureStaticFill(board);
        layout.JoinError = Mathf.Max(Vector3.Distance(worldPath[0], leftJoin),
            Vector3.Distance(worldPath[worldPath.Count - 1], rightJoin));
        return layout;
    }

    public void ConfigureStaticFill(Transform board)
    {
        this.board = board;
        Transform fill = board == null ? null : board.Find("Road_InteriorFill");
        Renderer renderer = fill == null ? null : fill.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) return;
        Material lowerFill = renderer.material;
        if (roadMaterial != null)
        {
            lowerFill.SetColor("_BaseColor", roadMaterial.GetColor("_BaseColor"));
            lowerFill.SetColor("_DarkTint", roadMaterial.GetColor("_DarkTint"));
            lowerFill.mainTexture = roadMaterial.mainTexture;
            lowerFill.mainTextureScale = roadMaterial.mainTextureScale;
            lowerFill.mainTextureOffset = roadMaterial.mainTextureOffset;
        }
        // Keep the authored Plane mesh and its UVs. Its shadow map was baked
        // against those coordinates, not a reconstructed parking grid.
        lowerFill.SetFloat("_ContactJoinRepair", 1f);
        lowerFill.SetMatrix("_ContactWorldToBoard", board.worldToLocalMatrix);
    }

    public void FitBusesToParkingBay(IReadOnlyList<BusController> buses)
    {
        if (board == null || buses == null || buses.Count == 0) return;
        int minLane = int.MaxValue, maxLane = int.MinValue, frontRow = int.MaxValue;
        foreach (BusController bus in buses)
        {
            if (bus == null) continue;
            minLane = Mathf.Min(minLane, bus.j);
            maxLane = Mathf.Max(maxLane, bus.j);
            frontRow = Mathf.Min(frontRow, bus.i);
        }
        if (minLane == int.MaxValue) return;
        int laneCount = maxLane - minLane + 1;
        foreach (BusController bus in buses)
        {
            if (bus == null) continue;
            // Model and lane spacing share the same source frame as the road.
            // A renderer bound must never change this layout or model scale.
            bus.ApplyTownModelScale(Path.ProfileWorldScale, 0f);
            bus.transform.position = Path.GetDockBusPosition(bus.j - minLane, laneCount, bus.i - frontRow);
        }
    }
}
