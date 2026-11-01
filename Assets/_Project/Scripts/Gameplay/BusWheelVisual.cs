using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class BusWheelVisual : MonoBehaviour
{
    [SerializeField, Range(0.5f, 1f)] private float tireHeightThreshold = 0.8f;

    private MeshRenderer wheelRenderer;
    private MeshFilter meshFilter;
    private Mesh generatedMesh;
    private Material tireMaterial;
    private Material[] assignedMaterials;

    private void Awake()
    {
        EnsureInitialized();
    }

    public void SetRimMaterial(Material rimMaterial)
    {
        if (rimMaterial == null || !EnsureInitialized())
        {
            return;
        }

        assignedMaterials[1] = rimMaterial;
        wheelRenderer.sharedMaterials = assignedMaterials;
    }

private bool EnsureInitialized()
    {
        if (wheelRenderer == null)
        {
            wheelRenderer = GetComponent<MeshRenderer>();
        }

        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (wheelRenderer == null || meshFilter == null)
        {
            return false;
        }

        if (generatedMesh == null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.subMeshCount < 2)
        {
            generatedMesh = CreateSplitMesh(meshFilter.sharedMesh);
            meshFilter.sharedMesh = generatedMesh;
        }

        if (meshFilter.sharedMesh == null)
        {
            return false;
        }

        if (tireMaterial == null)
        {
            Material[] sourceMaterials = wheelRenderer.sharedMaterials;
            Material sourceMaterial = sourceMaterials != null && sourceMaterials.Length > 0 ? sourceMaterials[0] : null;
            tireMaterial = CreateBlackTireMaterial(sourceMaterial);
        }

        if (assignedMaterials == null)
        {
            assignedMaterials = new Material[2];
            assignedMaterials[0] = tireMaterial;
        }

        return true;
    }

    private Mesh CreateSplitMesh(Mesh sourceMesh)
    {
        Mesh splitMesh = Instantiate(sourceMesh);
        splitMesh.name = sourceMesh.name + "_WheelSplit";

        int[] triangles = sourceMesh.triangles;
        Vector3[] vertices = sourceMesh.vertices;
        List<int>[] trianglesByVertex = BuildTriangleLookup(vertices.Length, triangles);
        bool[] visited = new bool[triangles.Length / 3];
        List<int> tireTriangles = new List<int>();
        List<int> rimTriangles = new List<int>();
        float minimumTireHeight = sourceMesh.bounds.size.y * tireHeightThreshold;

        for (int triangle = 0; triangle < visited.Length; triangle++)
        {
            if (visited[triangle])
            {
                continue;
            }

            List<int> component = CollectConnectedTriangles(triangle, triangles, trianglesByVertex, visited);
            float componentHeight = GetComponentHeight(component, triangles, vertices);
            List<int> target = componentHeight >= minimumTireHeight ? tireTriangles : rimTriangles;

            for (int i = 0; i < component.Count; i++)
            {
                int triangleIndex = component[i] * 3;
                target.Add(triangles[triangleIndex]);
                target.Add(triangles[triangleIndex + 1]);
                target.Add(triangles[triangleIndex + 2]);
            }
        }

        if (rimTriangles.Count == 0)
        {
            rimTriangles.AddRange(tireTriangles);
        }

        splitMesh.subMeshCount = 2;
        splitMesh.SetTriangles(tireTriangles, 0, false);
        splitMesh.SetTriangles(rimTriangles, 1, false);
        splitMesh.RecalculateBounds();
        return splitMesh;
    }

    private static List<int>[] BuildTriangleLookup(int vertexCount, int[] triangles)
    {
        List<int>[] lookup = new List<int>[vertexCount];
        for (int vertex = 0; vertex < vertexCount; vertex++)
        {
            lookup[vertex] = new List<int>();
        }

        for (int triangle = 0; triangle < triangles.Length / 3; triangle++)
        {
            int triangleStart = triangle * 3;
            lookup[triangles[triangleStart]].Add(triangle);
            lookup[triangles[triangleStart + 1]].Add(triangle);
            lookup[triangles[triangleStart + 2]].Add(triangle);
        }

        return lookup;
    }

    private static List<int> CollectConnectedTriangles(int startTriangle, int[] triangles, List<int>[] trianglesByVertex, bool[] visited)
    {
        Queue<int> pending = new Queue<int>();
        List<int> component = new List<int>();
        pending.Enqueue(startTriangle);
        visited[startTriangle] = true;

        while (pending.Count > 0)
        {
            int triangle = pending.Dequeue();
            component.Add(triangle);
            int triangleStart = triangle * 3;

            for (int corner = 0; corner < 3; corner++)
            {
                List<int> neighbours = trianglesByVertex[triangles[triangleStart + corner]];
                for (int i = 0; i < neighbours.Count; i++)
                {
                    int neighbour = neighbours[i];
                    if (!visited[neighbour])
                    {
                        visited[neighbour] = true;
                        pending.Enqueue(neighbour);
                    }
                }
            }
        }

        return component;
    }

    private static float GetComponentHeight(List<int> component, int[] triangles, Vector3[] vertices)
    {
        float minimumY = float.MaxValue;
        float maximumY = float.MinValue;

        for (int i = 0; i < component.Count; i++)
        {
            int triangleStart = component[i] * 3;
            for (int corner = 0; corner < 3; corner++)
            {
                float y = vertices[triangles[triangleStart + corner]].y;
                minimumY = Mathf.Min(minimumY, y);
                maximumY = Mathf.Max(maximumY, y);
            }
        }

        return maximumY - minimumY;
    }

    private static Material CreateBlackTireMaterial(Material sourceMaterial)
    {
        Material material = sourceMaterial == null
            ? new Material(Shader.Find("Standard"))
            : new Material(sourceMaterial);

        material.name = "Bus Tire (Black)";
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.black);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.black);
        }

        return material;
    }

    private void OnDestroy()
    {
        if (generatedMesh != null)
        {
            Destroy(generatedMesh);
        }

        if (tireMaterial != null)
        {
            Destroy(tireMaterial);
        }
    }
}