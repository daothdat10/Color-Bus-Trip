using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Projects visual road splines and imported stops onto the gameplay plane.</summary>
internal sealed class GameplayPathProjector
{
    private readonly SplineContainer container;
    private readonly Transform visualRoadPlane;
    private readonly Camera camera;
    private readonly Vector2 visualRoadCenter;
    private readonly Vector2 visualRoadScale;
    private readonly float gameplayPlaneY;

    public GameplayPathProjector(
        SplineContainer container,
        Transform visualRoadPlane,
        Camera camera,
        Vector2 visualRoadScale,
        float gameplayPlaneY)
    {
        this.container = container;
        this.visualRoadPlane = visualRoadPlane;
        this.camera = camera;
        this.visualRoadScale = visualRoadScale;
        this.gameplayPlaneY = gameplayPlaneY;
        visualRoadCenter = FindSplinePlaneCenter(container, 0);
    }

    public bool ProjectGameplaySplines(SplineControll controller)
    {
        var projectedSplines = new List<Spline>();
        for (int splineIndex = 0; splineIndex < container.Splines.Count; splineIndex++)
        {
            Spline source = container.Splines[splineIndex];
            var projected = new Spline();
            Transform visualTransform = splineIndex == 0
                ? visualRoadPlane : container.transform;

            for (int knotIndex = 0; knotIndex < source.Count; knotIndex++)
            {
                BezierKnot knot = source[knotIndex];
                Vector3 position = (Vector3)knot.Position;
                Vector3 inControl = position + (Vector3)math.rotate(knot.Rotation, knot.TangentIn);
                Vector3 outControl = position + (Vector3)math.rotate(knot.Rotation, knot.TangentOut);
                if (splineIndex == 0)
                {
                    position = ScaleRoadPoint(position);
                    inControl = ScaleRoadPoint(inControl);
                    outControl = ScaleRoadPoint(outControl);
                }

                if (!TryProjectVisualPoint(position, visualTransform, out Vector3 worldPosition) ||
                    !TryProjectVisualPoint(inControl, visualTransform, out Vector3 worldIn) ||
                    !TryProjectVisualPoint(outControl, visualTransform, out Vector3 worldOut))
                {
                    return false;
                }

                Vector3 localPosition = container.transform.InverseTransformPoint(worldPosition);
                Vector3 localIn = container.transform.InverseTransformPoint(worldIn) - localPosition;
                Vector3 localOut = container.transform.InverseTransformPoint(worldOut) - localPosition;
                projected.Add(new BezierKnot(localPosition, localIn, localOut, quaternion.identity),
                    TangentMode.Broken);
            }

            projected.Closed = splineIndex == 0 || source.Closed;
            projectedSplines.Add(projected);
        }

        controller.ApplyGameplaySplines(projectedSplines);
        return true;
    }

    public Vector3 ProjectImportedStopPosition(Vector3 sourcePosition)
    {
        if (!TryProjectVisualPoint(
                ScaleRoadPoint(new Vector3(sourcePosition.x, sourcePosition.z, 0f)),
                visualRoadPlane,
                out Vector3 boardPosition))
        {
            return sourcePosition;
        }

        // Position data is authoritative. Presentation must never clamp a Stop
        // or its passenger anchor into a generated island.
        return boardPosition;
    }

    public Vector3 EnsureBusParkingClearance(Vector3 position, float minimumClearance)
    {
        if (container == null || container.Splines.Count == 0 || minimumClearance <= 0f)
        {
            return position;
        }

        const int sampleCount = 256;
        float nearestProgress = 0f;
        Vector3 nearestPoint = Vector3.zero;
        float nearestDistanceSquared = float.PositiveInfinity;
        Vector3 planarPosition = new Vector3(position.x, 0f, position.z);

        for (int sample = 0; sample <= sampleCount; sample++)
        {
            float progress = sample / (float)sampleCount;
            Vector3 candidate = container.EvaluatePosition(0, progress);
            candidate.y = 0f;
            float distanceSquared = (planarPosition - candidate).sqrMagnitude;
            if (distanceSquared >= nearestDistanceSquared)
            {
                continue;
            }

            nearestDistanceSquared = distanceSquared;
            nearestProgress = progress;
            nearestPoint = candidate;
        }

        Vector3 awayFromRoad = planarPosition - nearestPoint;
        float distance = awayFromRoad.magnitude;
        if (distance >= minimumClearance)
        {
            return position;
        }

        if (distance <= 0.001f)
        {
            Vector3 tangent = container.EvaluateTangent(0, nearestProgress);
            tangent.y = 0f;
            awayFromRoad = tangent.sqrMagnitude > 0.001f
                ? Vector3.Cross(tangent.normalized, Vector3.up)
                : Vector3.left;

            // The default parking grid is on the left side of the entry road.
            if (awayFromRoad.x > 0f)
            {
                awayFromRoad = -awayFromRoad;
            }
        }
        else
        {
            awayFromRoad /= distance;
        }

        Vector3 adjusted = position + awayFromRoad * (minimumClearance - distance);
        adjusted.y = position.y;
        return adjusted;
    }

    private Vector3 ScaleRoadPoint(Vector3 point)
    {
        point.x = visualRoadCenter.x + (point.x - visualRoadCenter.x) * visualRoadScale.x;
        point.y = visualRoadCenter.y + (point.y - visualRoadCenter.y) * visualRoadScale.y;
        return point;
    }

    private static Vector2 FindSplinePlaneCenter(SplineContainer path, int splineIndex)
    {
        Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int sample = 0; sample <= 128; sample++)
        {
            Vector3 world = path.EvaluatePosition(splineIndex, sample / 128f);
            Vector3 local = path.transform.InverseTransformPoint(world);
            minimum = Vector2.Min(minimum, new Vector2(local.x, local.y));
            maximum = Vector2.Max(maximum, new Vector2(local.x, local.y));
        }

        return (minimum + maximum) * 0.5f;
    }

    private bool TryProjectVisualPoint(
        Vector3 visualLocal,
        Transform visualPlane,
        out Vector3 boardPosition)
    {
        Vector3 viewport = camera.WorldToViewportPoint(
            visualPlane.TransformPoint(visualLocal));
        Ray ray = camera.ViewportPointToRay(viewport);
        Plane board = new Plane(Vector3.up, new Vector3(0f, gameplayPlaneY, 0f));
        board.Raycast(ray, out float distance);
        if (viewport.z <= 0f || float.IsNaN(distance) ||
            float.IsInfinity(distance) || Mathf.Abs(distance) < 0.001f)
        {
            boardPosition = Vector3.zero;
            return false;
        }

        boardPosition = ray.GetPoint(distance);
        return true;
    }
}
