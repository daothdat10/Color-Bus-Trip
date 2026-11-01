using System;

/// <summary>Conveyor distance rules shared by stop selection and transfer checks.</summary>
public static class BusStopProximity
{
    public static bool IsValidProgress(float progress) =>
        !float.IsNaN(progress) && !float.IsInfinity(progress) && progress >= 0f && progress <= 1f;

    /// <summary>Same metric as BusDataNew: shortest progress gap times path length.</summary>
    public static float GetDistance(float busProgress, float stopProgress, float pathLength, bool closed)
    {
        if (!IsValidProgress(busProgress) || !IsValidProgress(stopProgress) ||
            float.IsNaN(pathLength) || float.IsInfinity(pathLength) || pathLength <= 0f)
            return float.PositiveInfinity;

        float gap = Math.Abs(busProgress - stopProgress);
        if (closed) gap = Math.Min(gap, 1f - gap);
        return gap * pathLength;
    }

    public static bool IsInRange(float distance, float maximumDistance) =>
        !float.IsNaN(distance) && !float.IsInfinity(distance) && distance >= 0f &&
        !float.IsNaN(maximumDistance) && !float.IsInfinity(maximumDistance) &&
        maximumDistance >= 0f && distance <= maximumDistance;
}
