using System;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Applies the Town art around a generated conveyor road.
/// </summary>
public static class LevelTownPresentation
{
    // Opaque bus materials render at 2000. Ground and road draw first without
    // writing depth, so the bus can never be hidden by the flat road mesh.
    // The broad backdrop precedes the authored spawn-board ground and fill.
    private const int GroundRenderQueue = 1880;
    private const int RimRenderQueue = 1925;
    private const int RoadRenderQueue = 1950;

    private static void DrawBehindBuses(Renderer target, int renderQueue)
    {
        Material[] materials = target.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            // Generated splines and the backdrop use XY; the authored lower
            // board uses XZ. Keep bevel shading relative to each mesh's top.
            if (materials[i].HasProperty("_SurfaceNormal"))
            {
                bool isXY = target.name == "Ground_Town" ||
                            target.name == "Road_Main" ||
                            target.name == DreamteckSplineRoadRenderer.RimObjectName;
                materials[i].SetVector("_SurfaceNormal", isXY
                    ? new Vector4(0f, 0f, -1f, 0f)
                    : new Vector4(0f, 1f, 0f, 0f));
                materials[i].SetVector("_LightDirection", isXY
                    ? new Vector4(0f, -1f, -5f, 0f)
                    : new Vector4(0f, 5f, -1f, 0f));
                materials[i].SetFloat("_RibbonMode", target.name == "Road_Main" ? 1f :
                    target.name == DreamteckSplineRoadRenderer.RimObjectName ? 2f : 0f);
            }
            if (materials[i].HasProperty("_ZWrite"))
            {
                materials[i].SetFloat("_ZWrite", 0f);
            }
            materials[i].renderQueue = renderQueue;
        }
        target.materials = materials;
    }

    public static TownRoadSurfaceLayout Apply(
        SplineContainer levelPath,
        GameObject generatedRoad,
        Sprite environmentSprite,
        Transform authoredBoard,
        System.Collections.Generic.IReadOnlyList<float> knotSizes, float gameplayY)
    {
        Transform roadMesh = generatedRoad.transform.Find("MyGameSplineMesh_Road_0");
        foreach (Transform child in generatedRoad.transform)
        {
            if (child.name.StartsWith("MyGameSplineMesh_Bridge_", StringComparison.Ordinal))
            {
                child.gameObject.SetActive(false);
            }
        }

        RoadThemeMaterials palette = Resources.Load<RoadThemeMaterials>("RoadThemeMaterials");
        if (palette == null ||
            !palette.TryGet(RoadTheme.Town, out Material groundMaterial, out Material roadMaterial))
        {
            return null;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null) return null;
        Transform ground = mainCamera.transform.Find("Ground_Town");
        if (ground == null)
        {
            GameObject groundObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            groundObject.name = "Ground_Town";
            UnityEngine.Object.Destroy(groundObject.GetComponent<Collider>());
            ground = groundObject.transform;
            ground.SetParent(mainCamera.transform, false);
        }

        ground.localPosition = new Vector3(0f, -1f, 5.60f);
        ground.localRotation = Quaternion.identity;
        ground.localScale = new Vector3(20f, 40f, 1f);
        ground.gameObject.SetActive(true);
        MeshRenderer groundRenderer = ground.GetComponent<MeshRenderer>();
        groundRenderer.sharedMaterial = groundMaterial;
        DrawBehindBuses(groundRenderer, GroundRenderQueue);
        foreach (MeshRenderer roadRenderer in generatedRoad.GetComponentsInChildren<MeshRenderer>(true))
        {
            bool isRim = roadRenderer.name.StartsWith(
                DreamteckSplineRoadRenderer.RimObjectName, StringComparison.Ordinal);
            DrawBehindBuses(roadRenderer, isRim ? RimRenderQueue : RoadRenderQueue);
        }

        TownRoadSurfaceLayout layout = TownRoadSurfaceLayout.Build(
            roadMesh, authoredBoard, roadMaterial, knotSizes, gameplayY);

        mainCamera.backgroundColor = groundMaterial.GetColor("_BaseColor");
        Transform environment = mainCamera.transform.Find("Environment_Town");
        if (environment == null)
        {
            GameObject environmentObject = new GameObject("Environment_Town");
            environment = environmentObject.transform;
            environment.SetParent(mainCamera.transform, false);
            environmentObject.AddComponent<SpriteRenderer>();
        }

        environment.localPosition = new Vector3(0f, 5.10f, 18f);
        environment.localRotation = Quaternion.identity;
        environment.localScale = Vector3.one * 0.45f;
        environment.GetComponent<SpriteRenderer>().sprite = environmentSprite;
        environment.gameObject.SetActive(true);
        return layout;
    }

    public static void Hide(SplineContainer levelPath)
    {
        Transform townGround = Camera.main == null ? null : Camera.main.transform.Find("Ground_Town");
        if (townGround != null)
        {
            townGround.gameObject.SetActive(false);
        }

        Camera sceneCamera = Camera.main;
        Transform townArt = sceneCamera != null
            ? sceneCamera.transform.Find("Environment_Town")
            : null;
        if (townArt != null)
        {
            townArt.gameObject.SetActive(false);
        }
    }
}
