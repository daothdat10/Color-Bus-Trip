using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine.Splines;

/// <summary>
/// Reconstructs MyGame Conveyor LevelPath knot data preserved in each Bus level JSON.
/// Positions and Bezier handles stay in the serialized spline-local coordinate space.
/// </summary>
public static class MyGameLevelPathPort
{
    public static bool TryCreateSpline(
        LevelData levelData,
        out Spline spline,
        out List<float> knotSizes,
        out string error)
    {
        spline = null;
        knotSizes = new List<float>();
        error = string.Empty;

        if (levelData == null || levelData.BoardItems == null)
        {
            error = "Level data has no board items.";
            return false;
        }

        LevelBoardItem conveyor = null;
        int conveyorCount = 0;
        IReadOnlyList<LevelBoardItem> items = levelData.BoardItems;
        for (int i = 0; i < items.Count; i++)
        {
            LevelBoardItem item = items[i];
            if (item == null || item.data == null ||
                !string.Equals(item.type, "Conveyor", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            conveyor = item;
            conveyorCount++;
        }

        if (conveyorCount != 1)
        {
            error = "Expected exactly one MyGame Conveyor path, found " + conveyorCount + ".";
            return false;
        }

        LevelBoardItemData data = conveyor.data;
        int pointCount = data.positionsColl != null ? data.positionsColl.Count : 0;
        if (pointCount < 2 ||
            data.tanInsColl == null || data.tanInsColl.Count != pointCount ||
            data.tanOutsColl == null || data.tanOutsColl.Count != pointCount ||
            (data.sizesColl != null && data.sizesColl.Count != pointCount))
        {
            error = "Conveyor positions, tangent handles, or size arrays are missing or mismatched.";
            return false;
        }

        var result = new Spline();
        for (int i = 0; i < pointCount; i++)
        {
            LevelVector3Data position = data.positionsColl[i];
            LevelVector3Data tangentIn = data.tanInsColl[i];
            LevelVector3Data tangentOut = data.tanOutsColl[i];
            if (position == null || tangentIn == null || tangentOut == null)
            {
                error = "Conveyor contains a null point or tangent at index " + i + ".";
                return false;
            }

            float size = data.sizesColl != null ? data.sizesColl[i] : 1f;
            if (float.IsNaN(size) || float.IsInfinity(size) || size < 0f)
            {
                error = "Conveyor contains an invalid size at index " + i + ".";
                return false;
            }

            var knot = new BezierKnot(
                new float3(position.x, position.y, position.z),
                new float3(tangentIn.x, tangentIn.y, tangentIn.z),
                new float3(tangentOut.x, tangentOut.y, tangentOut.z),
                quaternion.identity);
            result.Add(knot, TangentMode.Broken);
            knotSizes.Add(size);
        }

        spline = result;
        return true;
    }
}
