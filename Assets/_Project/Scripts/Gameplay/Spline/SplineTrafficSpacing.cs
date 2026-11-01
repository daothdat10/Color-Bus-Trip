using System.Collections.Generic;
using UnityEngine;

/// <summary>Limits spline progress to keep buses apart and the entry unblocked.</summary>
internal static class SplineTrafficSpacing
{
    public static float ClampProgress(
        SplineBusRuntime current,
        float proposed,
        IReadOnlyDictionary<BusController, float> previousProgress,
        IReadOnlyList<SplineBusRuntime> buses,
        IBusPath path,
        float padding,
        bool useSpacing,
        bool useBlockedPoints,
        IList<float> blockedProgressPoints)
    {
        float pathLength = path == null ? 0f : path.Length;
        if (current == null || current.Bus == null ||
            !useSpacing || pathLength <= Mathf.Epsilon)
        {
            return proposed;
        }

        float currentProgress = current.Progress;
        float desiredDistance = SplineProgressSolver.ForwardDistance(
            currentProgress, proposed, true);
        if (desiredDistance <= SplineProgressSolver.Epsilon)
        {
            return proposed;
        }

        float currentLength = current.ConveyorExtent;
        float allowedDistance = desiredDistance;

        for (int i = 0; i < buses.Count; i++)
        {
            SplineBusRuntime other = buses[i];
            if (other == null || other == current || !other.IsTrafficActive ||
                other.IsEntering ||
                !previousProgress.TryGetValue(other.Bus, out float otherProgress))
            {
                continue;
            }

            float forwardDistance = SplineProgressSolver.ForwardDistance(
                currentProgress, otherProgress, true);
            if (forwardDistance <= SplineProgressSolver.Epsilon)
            {
                continue;
            }

            float otherLength = other.ConveyorExtent;
            // Preserve the configured visual clearance around measured vehicle extents.
            float requiredWorldDistance = (currentLength + otherLength) * 0.5f + Mathf.Max(0f, padding);
            float requiredProgress = requiredWorldDistance / pathLength;
            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, forwardDistance - requiredProgress));
        }

        float result = Mathf.Repeat(currentProgress + allowedDistance, 1f);
        return useBlockedPoints
            ? SplineProgressSolver.ClampAgainstBlockedPoints(
                currentProgress, result, blockedProgressPoints, true)
            : result;
    }

    /// <summary>BusDataNew IsOccupied: shortest loop distance strictly less than extent.</summary>
    public static bool IsReceivePointOccupied(float point, float normalizedExtent,
        SplineBusRuntime entering, IReadOnlyList<SplineBusRuntime> buses)
    {
        for (int i = 0; i < buses.Count; i++)
        {
            SplineBusRuntime other = buses[i];
            if (other == null || other == entering || !other.IsTrafficActive || other.IsEntering)
                continue;
            float difference = Mathf.Abs(Mathf.Repeat(other.Progress, 1f) - Mathf.Repeat(point, 1f));
            float distance = Mathf.Min(difference, 1f - difference);
            if (distance < normalizedExtent) return true;
        }
        return false;
    }

    /// <summary>BlockHolderProgress reserves start/end until EndTransfer, without a timer.</summary>
    public static float ClampAgainstTransfers(SplineBusRuntime current, float proposed,
        IReadOnlyList<SplineBusRuntime> buses, IBusPath path)
    {
        if (path == null || path.Length <= Mathf.Epsilon) return proposed;
        float desired = SplineProgressSolver.ForwardDistance(current.Progress, proposed, true);
        // Extent is a stable longitudinal size, independent of the current turn angle.
        float extent = current.ConveyorExtent / path.Length;
        for (int i = 0; i < buses.Count; i++)
        {
            SplineBusRuntime receiver = buses[i];
            if (receiver == null || receiver == current || !receiver.IsTrafficActive ||
                !receiver.IsEntering || !receiver.EntryTransferStarted || receiver.EntryWaitingForTraffic ||
                receiver.EntryElapsed < receiver.EntryStraightDuration) continue;
            // Original buses share one extent. With differently sized local models,
            // keep CanStart's larger clearance so a small follower cannot pin it shut.
            float receiveExtent = Mathf.Max(extent, receiver.EntryNormalizedExtent);
            ClampPoint(receiver.EntryReceiveStart, receiveExtent);
            ClampPoint(receiver.Progress, receiveExtent);
        }
        return Mathf.Repeat(current.Progress + desired, 1f);

        void ClampPoint(float point, float receiveExtent)
        {
            float ahead = SplineProgressSolver.ForwardDistance(current.Progress, point, true);
            // Traffic already beyond a receive point must clear it, not wait for itself.
            if (ahead <= SplineProgressSolver.Epsilon || ahead > 0.5f) return;
            desired = Mathf.Min(desired, Mathf.Max(0f, ahead - receiveExtent - SplineProgressSolver.Epsilon));
        }
    }

    /// <summary>Có xe khác đang nhập đủ gần xe này để hai xe chồng lên nhau không.</summary>
    public static bool IsEntranceCrowded(
        BusController enteringBus,
        IBusPath path,
        float entryProgress,
        IReadOnlyList<SplineBusRuntime> buses,
        float padding)
    {
        if (path == null || path.Length <= Mathf.Epsilon)
        {
            return true;
        }

        Vector3 entryDirection = Vector3.ProjectOnPlane(
            path.EvaluateTangent(entryProgress), Vector3.up);

        for (int i = 0; i < buses.Count; i++)
        {
            SplineBusRuntime runtime = buses[i];
            if (runtime == null || runtime.Bus == null || !runtime.IsEntering ||
                runtime.Bus == enteringBus)
            {
                continue;
            }

            Vector3 separation = runtime.Bus.transform.position -
                enteringBus.transform.position;
            separation.y = 0f;
            Vector3 separationDirection = separation.sqrMagnitude > Mathf.Epsilon
                ? separation.normalized
                : entryDirection;
            float requiredDistance = BusTrafficMath.GetRequiredFollowingDistance(
                enteringBus.GetVisualLengthAlong(separationDirection),
                runtime.Bus.GetVisualLengthAlong(separationDirection),
                padding);

            if (separation.sqrMagnitude <= requiredDistance * requiredDistance)
            {
                return true;
            }
        }

        return false;
    }

}
