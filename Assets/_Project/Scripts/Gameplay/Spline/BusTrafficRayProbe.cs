using System.Collections.Generic;
using UnityEngine;

/// <summary>Checks physical ray hits for a bus blocking travel ahead.</summary>
internal sealed class BusTrafficRayProbe
{
    private readonly RaycastHit[] hits = new RaycastHit[8];

    /// <summary>
    /// Bắn ba ray theo hướng chạy và trả về true nếu gặp xe khác trong khoảng cách dừng.
    /// Dùng cho xe đang tiếp cận cổng; bỏ qua chính xe đó và xe đã rời spline.
    /// </summary>
    public bool HasBusAhead(
        BusController sourceBus,
        Vector3 travelDirection,
        float rayDistance,
        float laneHalfWidth,
        float spacingPadding,
        LayerMask rayLayer)
    {
        if (sourceBus == null)
        {
            return false;
        }

        if (travelDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            travelDirection = sourceBus.transform.forward;
        }

        if (travelDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 rayDirection = travelDirection.normalized;
        float sourceVisualLength = sourceBus.GetVisualLengthAlong(rayDirection);
        float raycastDistance = Mathf.Max(
            rayDistance,
            sourceVisualLength + spacingPadding + 1f);
        Vector3 lateralDirection = Vector3.Cross(Vector3.up, rayDirection);
        if (lateralDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            lateralDirection = sourceBus.transform.right;
        }

        lateralDirection.Normalize();
        for (int rayIndex = -1; rayIndex <= 1; rayIndex++)
        {
            Vector3 rayOrigin = sourceBus.transform.position +
                lateralDirection * (rayIndex * laneHalfWidth);
            Debug.DrawRay(rayOrigin, rayDirection * raycastDistance, Color.cyan);

            int hitCount = Physics.RaycastNonAlloc(
                rayOrigin,
                rayDirection,
                hits,
                raycastDistance,
                rayLayer,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                BusController hitBus = hits[i].collider.GetComponentInParent<BusController>();
                if (!BusTrafficMath.ShouldBlockTraffic(sourceBus, hitBus))
                {
                    continue;
                }

                float requiredDistance = BusTrafficMath.GetRequiredFollowingDistance(
                    sourceVisualLength,
                    hitBus.GetVisualLengthAlong(rayDirection),
                    spacingPadding);
                if (BusTrafficMath.IsBusAhead(
                    sourceBus.transform.position,
                    rayDirection,
                    hitBus.transform.position,
                    Mathf.Max(rayDistance, requiredDistance),
                    laneHalfWidth,
                    out _))
                {
                    return true;
                }
            }
        }

        return false;
    }
    /// <summary>
    /// Bắn ba ray và tìm khoảng trống nhỏ nhất giữa hai thân xe theo hướng chạy.
    /// Trả về dương vô cực khi không thấy xe trước; giá trị âm nghĩa là đã quá gần.
    /// </summary>
    public float GetForwardClearance(
    BusController sourceBus,
    Vector3 travelDirection,
    float rayDistance,
    float laneHalfWidth,
    float spacingPadding,
    LayerMask rayLayer,
    float brakingDistance,
    ISet<BusController> ignoredBuses = null)
    {
        if (sourceBus == null)
            return float.PositiveInfinity;

        if (travelDirection.sqrMagnitude <= Mathf.Epsilon)
            travelDirection = sourceBus.transform.forward;

        if (travelDirection.sqrMagnitude <= Mathf.Epsilon)
            return float.PositiveInfinity;

        Vector3 forward = travelDirection.normalized;
        Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
        float sourceLength = sourceBus.GetVisualLengthAlong(forward);

        float castDistance = Mathf.Max(
            rayDistance,
            sourceLength + spacingPadding + brakingDistance + 2f);

        float nearestClearance = float.PositiveInfinity;

        for (int rayIndex = -1; rayIndex <= 1; rayIndex++)
        {
            Vector3 origin = sourceBus.transform.position +
                             lateral * (rayIndex * laneHalfWidth);

            int count = Physics.RaycastNonAlloc(
                origin, forward, hits, castDistance,
                rayLayer, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                BusController other =
                    hits[i].collider.GetComponentInParent<BusController>();

                if (!BusTrafficMath.ShouldBlockTraffic(sourceBus, other) ||
                    (ignoredBuses != null && ignoredBuses.Contains(other)))
                    continue;

                if (!BusTrafficMath.IsBusAhead(
                        sourceBus.transform.position,
                        forward,
                        other.transform.position,
                        castDistance,
                        laneHalfWidth,
                        out float forwardDistance))
                    continue;

                float requiredDistance =
                    BusTrafficMath.GetRequiredFollowingDistance(
                        sourceLength,
                        other.GetVisualLengthAlong(forward),
                        spacingPadding);

                nearestClearance = Mathf.Min(
                    nearestClearance,
                    forwardDistance - requiredDistance);
            }
        }

        return nearestClearance;
    }
}
