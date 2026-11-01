using System;
using System.Collections.Generic;
using UnityEngine;

public static class SplineProgressSolver
{
    public const float Epsilon = 0.00001f;

    public static float Advance(float progress, float delta, bool loop)
    {
        if (!IsFinite(progress) || !IsFinite(delta))
        {
            return IsFinite(progress) ? Mathf.Clamp01(progress) : 0f;
        }

        return loop
            ? Mathf.Repeat(progress + delta, 1f)
            : Mathf.Clamp01(progress + delta);
    }

    public static float ForwardDistance(float from, float to, bool loop)
    {
        if (!IsFinite(from) || !IsFinite(to))
        {
            return 0f;
        }

        return loop
            ? Mathf.Repeat(to - from, 1f)
            : to - from;
    }

    public static float ClampAgainstBlockedPoints(
        float current,
        float proposed,
        IList<float> blockedProgress,
        bool loop)
    {
        if (!IsFinite(current) || !IsFinite(proposed))
        {
            return IsFinite(current) ? Mathf.Clamp01(current) : 0f;
        }

        float result = loop ? Mathf.Repeat(proposed, 1f) : Mathf.Clamp01(proposed);
        float desiredDistance = ForwardDistance(current, result, loop);

        if (desiredDistance <= Epsilon || blockedProgress == null)
        {
            return result;
        }

        for (int i = 0; i < blockedProgress.Count; i++)
        {
            float blocked = blockedProgress[i];
            if (!IsFinite(blocked))
            {
                continue;
            }

            blocked = loop ? Mathf.Repeat(blocked, 1f) : Mathf.Clamp01(blocked);
            float distanceAhead = ForwardDistance(current, blocked, loop);
            if (distanceAhead <= Epsilon || distanceAhead > desiredDistance + Epsilon)
            {
                continue;
            }

            desiredDistance = Mathf.Max(0f, distanceAhead - Epsilon);
        }

        return loop
            ? Mathf.Repeat(current + desiredDistance, 1f)
            : Mathf.Clamp01(current + desiredDistance);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
