using UnityEngine;

/// <summary>
/// Shared, allocation-free geometry checks for bus traffic collision/wait logic.
/// Keeping this independent from Physics makes the lane and hysteresis rules
/// directly testable without changing scene state.
/// </summary>
public static class BusTrafficMath
{
    /// <summary>
    /// MyGame's stop proximity uses XZ planar distance. Y is only the render
    /// layer in this project, so it must not make a bus appear farther from a
    /// waiting point.
    /// </summary>
    public static float GetXZPlanarDistance(Vector3 first, Vector3 second)
    {
        Vector3 offset = first - second;
        offset.y = 0f;
        return offset.magnitude;
    }

    public static bool IsWithinXZPlanarDistance(
        Vector3 first,
        Vector3 second,
        float maximumDistance)
    {
        float safeDistance = Mathf.Max(0f, maximumDistance);
        Vector3 offset = first - second;
        offset.y = 0f;
        return offset.sqrMagnitude <= safeDistance * safeDistance;
    }

    /// <summary>
    /// Returns whether a detected bus is still a valid traffic obstacle.
    /// A bus that has entered the exit spline has already left the entry queue
    /// and must not keep the following bus in a wait state.
    /// </summary>
    public static bool ShouldBlockTraffic(BusController sourceBus, BusController candidateBus)
    {
        return candidateBus != null &&
               candidateBus != sourceBus &&
               !candidateBus.isExit;
    }

    public static bool IsBusAhead(
        Vector3 sourcePosition,
        Vector3 travelDirection,
        Vector3 otherPosition,
        float maxForwardDistance,
        float laneHalfWidth,
        out float forwardDistance)
    {
        Vector3 planarDirection = Vector3.ProjectOnPlane(travelDirection, Vector3.up);
        if (planarDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            forwardDistance = float.MaxValue;
            return false;
        }

        planarDirection.Normalize();
        Vector3 planarOffset = Vector3.ProjectOnPlane(
            otherPosition - sourcePosition,
            Vector3.up
        );
        forwardDistance = Vector3.Dot(planarOffset, planarDirection);
        if (forwardDistance <= 0f || forwardDistance > Mathf.Max(0f, maxForwardDistance))
        {
            return false;
        }

        float lateralDistance = (planarOffset - planarDirection * forwardDistance).magnitude;
        return lateralDistance <= Mathf.Max(0f, laneHalfWidth);
    }

    /// <summary>
    /// Minimum center-to-center distance required to keep two rendered buses
    /// apart while they travel in the same direction. MyGame treats this as
    /// a visual following/braking distance instead of allowing colliders to
    /// decide when the follower may overlap the leader.
    /// </summary>
    public static float GetRequiredFollowingDistance(
        float followerVisualLength,
        float leaderVisualLength,
        float padding)
    {
        return Mathf.Max(0f, followerVisualLength * 0.5f) +
               Mathf.Max(0f, leaderVisualLength * 0.5f) +
               Mathf.Max(0f, padding);
    }


    /// <summary>Projects each actual tilted renderer, including its center offset, onto the road.</summary>
    public static bool OverlapsBusFootprint(
        BusController bus, Vector3 position, Quaternion rotation, BusController other, float gap)
    {
        return OverlapsBusFootprintsAtPoses(bus, position, rotation, other,
            other != null ? other.transform.position : Vector3.zero,
            other != null ? other.transform.rotation : Quaternion.identity, gap);
    }

    public static bool OverlapsBusFootprintsAtPoses(
        BusController bus, Vector3 position, Quaternion rotation,
        BusController other, Vector3 otherPosition, Quaternion otherRotation, float gap)
    {
        if (!ShouldBlockTraffic(bus, other))
            return false;
        Matrix4x4 poseDelta = Matrix4x4.TRS(position, rotation, Vector3.one) *
            Matrix4x4.TRS(bus.transform.position, bus.transform.rotation, Vector3.one).inverse;
        Matrix4x4 otherPoseDelta = Matrix4x4.TRS(otherPosition, otherRotation, Vector3.one) *
            Matrix4x4.TRS(other.transform.position, other.transform.rotation, Vector3.one).inverse;
        var bodies = bus.TrafficBodyRenderers;
        var others = other.TrafficBodyRenderers;
        for (int i = 0; i < bodies.Count; i++)
        {
            Renderer first = bodies[i];
            if (first == null || !first.enabled || !first.gameObject.activeInHierarchy) continue;
            Matrix4x4 firstMatrix = poseDelta * first.localToWorldMatrix;
            for (int j = 0; j < others.Count; j++)
            {
                Renderer second = others[j];
                if (second == null || !second.enabled || !second.gameObject.activeInHierarchy) continue;
                if (ProjectedBoundsOverlap(first.localBounds, firstMatrix,
                        second.localBounds, otherPoseDelta * second.localToWorldMatrix, gap))
                    return true;
            }
        }
        return false;
    }

    /// <summary>Conservative root radius including renderer offsets and tilted geometry.</summary>
    internal static float GetTrafficRadius(BusController bus)
    {
        if (bus == null) return 0f;
        float radius = 0f;
        var bodies = bus.TrafficBodyRenderers;
        for (int i = 0; i < bodies.Count; i++)
        {
            Renderer body = bodies[i];
            if (body == null) continue;
            Bounds bounds = body.localBounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 local = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                Vector3 offset = body.transform.TransformPoint(local) - bus.transform.position;
                // Use the full 3D radius, so turn sway cannot exceed the broad phase.
                radius = Mathf.Max(radius, offset.magnitude);
            }
        }
        return radius;
    }

    private static bool ProjectedBoundsOverlap(
        Bounds first, Matrix4x4 firstMatrix, Bounds second, Matrix4x4 secondMatrix, float gap)
    {
        Vector3 offset = secondMatrix.MultiplyPoint3x4(second.center) -
            firstMatrix.MultiplyPoint3x4(first.center);
        // A tilted box projects to a six-sided polygon, not an upright root rectangle.
        // Its edge normals come from all three local axes of each renderer.
        for (int i = 0; i < 6; i++)
        {
            Matrix4x4 matrix = i < 3 ? firstMatrix : secondMatrix;
            Vector3 edge = matrix.MultiplyVector(i % 3 == 0 ? Vector3.right :
                i % 3 == 1 ? Vector3.up : Vector3.forward);
            Vector3 axis = Vector3.Cross(Vector3.up, edge);
            if (axis.sqrMagnitude <= 0.00000001f) continue;
            axis.Normalize();
            float extent = ProjectedRadius(first, firstMatrix, axis) +
                ProjectedRadius(second, secondMatrix, axis);
            if (Mathf.Abs(Vector3.Dot(offset, axis)) >= extent + Mathf.Max(0f, gap))
                return false;
        }
        return true;
    }

    private static float ProjectedRadius(Bounds bounds, Matrix4x4 matrix, Vector3 axis)
    {
        Vector3 e = bounds.extents;
        return Mathf.Abs(Vector3.Dot(axis, matrix.MultiplyVector(Vector3.right * e.x))) +
            Mathf.Abs(Vector3.Dot(axis, matrix.MultiplyVector(Vector3.up * e.y))) +
            Mathf.Abs(Vector3.Dot(axis, matrix.MultiplyVector(Vector3.forward * e.z)));
    }
}
