using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>Prepares distance tables, moves along the entrance, then completes the handoff.</summary>
internal static class SplineEntranceMovement
{
    private const int CurveSamples = 32;

    public static void Prepare(
        SplineBusRuntime runtime, float requestedSpeed, float minimumTurnDuration, float exitSpeed)
    {
        BuildDistanceTables(runtime);
        float speed = Mathf.Max(0.01f, requestedSpeed);
        if (runtime.EntryCurveLength > 0.0001f && minimumTurnDuration > 0f)
            speed = Mathf.Min(speed, runtime.EntryCurveLength / minimumTurnDuration);
        runtime.EntryMoveSpeed = Mathf.Max(0.01f, speed);
        runtime.EntryEndSpeed = exitSpeed > 0f ? exitSpeed : runtime.EntryMoveSpeed;
        runtime.EntryStraightDuration = Vector3.Distance(
            runtime.EntryStartPosition, runtime.EntryCurveStart) / runtime.EntryMoveSpeed;
        runtime.EntryTurnDuration = runtime.EntryCurveLength / runtime.EntryMoveSpeed;
        runtime.EntryLeftDuration = Vector3.Distance(
            runtime.EntryCurveEnd, runtime.EntryLeftPosition) / runtime.EntryMoveSpeed;
        runtime.EntryAlignDuration = runtime.EntryAlignLength /
            ((runtime.EntryMoveSpeed + runtime.EntryEndSpeed) * 0.5f);
        runtime.EntryDuration = Mathf.Max(0.0001f,
            runtime.EntryStraightDuration + runtime.EntryTurnDuration +
            runtime.EntryLeftDuration + runtime.EntryAlignDuration);
    }

    /// <summary>Reserve the animated body corridor, including its corners during rotation.</summary>
    public static bool OverlapsTrafficCorridor(SplineBusRuntime entering,
        SplineBusRuntime other, Vector3 position, Quaternion rotation) =>
        OverlapsTrafficCorridorWithGap(entering, other, position, rotation, 0.04f);

    public static bool OverlapsTrafficCorridorWithGap(SplineBusRuntime entering,
        SplineBusRuntime other, Vector3 position, Quaternion rotation, float gap)
    {
        if (entering.EntryTrafficPositions == null)
        {
            var times = new List<float>();
            var positions = new List<Vector3>();
            var rotations = new List<Quaternion>();
            float speed = Mathf.Max(entering.EntryMoveSpeed, entering.EntryEndSpeed, 0.01f);
            int steps = Mathf.Max(32, Mathf.CeilToInt(entering.EntryDuration * speed / 0.1f));
            Sample(0f, out Vector3 first, out Quaternion firstRotation);
            times.Add(0f);
            positions.Add(first);
            rotations.Add(firstRotation);
            for (int i = 1; i <= steps; i++)
                Append((i - 1f) * entering.EntryDuration / steps,
                    i * entering.EntryDuration / steps, 0);
            entering.EntryTrafficTimes = times.ToArray();
            entering.EntryTrafficPositions = positions.ToArray();
            entering.EntryTrafficRotations = rotations.ToArray();

            void Sample(float time, out Vector3 point, out Quaternion orientation)
            {
                EvaluatePose(entering, time, out point, out Vector3 tangent);
                orientation = tangent.sqrMagnitude > Mathf.Epsilon
                    ? Quaternion.LookRotation(tangent.normalized, Vector3.up)
                    : entering.EntryTargetRotation;
            }

            void Append(float from, float to, int depth)
            {
                Sample(to, out Vector3 point, out Quaternion orientation);
                int last = positions.Count - 1;
                float rotationTravel = Quaternion.Angle(rotations[last], orientation) *
                    Mathf.Deg2Rad * entering.TrafficRadius;
                if (depth < 12 && (Vector3.Distance(positions[last], point) > 0.04f || rotationTravel > 0.02f))
                {
                    float middle = (from + to) * 0.5f;
                    Append(from, middle, depth + 1);
                    Append(middle, to, depth + 1);
                    return;
                }
                times.Add(to);
                positions.Add(point);
                rotations.Add(orientation);
            }
        }
        // Samples limit translation to 4 cm and corner travel from rotation to
        // 2 cm. A 4 cm gap covers the intermediate body poses plus contact room.
        float radius = entering.TrafficRadius + other.TrafficRadius + gap;
        // Release the already-travelled approach instead of reserving its
        // entire history until the turn ends. Keep the preceding sample to
        // cover the body pose between two cached times.
        int firstSample = 0;
        if (entering.EntryTrafficTimes != null)
        {
            int found = System.Array.BinarySearch(entering.EntryTrafficTimes, entering.EntryElapsed);
            firstSample = Mathf.Max(0, found >= 0 ? found - 1 : ~found - 1);
        }
        for (int i = firstSample; i < entering.EntryTrafficPositions.Length; i++)
        {
            Vector3 offset = entering.EntryTrafficPositions[i] - position;
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius) continue;
            if (BusTrafficMath.OverlapsBusFootprintsAtPoses(
                entering.Bus, entering.EntryTrafficPositions[i], entering.EntryTrafficRotations[i],
                other.Bus, position, rotation, gap)) return true;
        }
        return false;
    }

    public static Vector3 GetTravelDirection(SplineBusRuntime runtime)
    {
        EvaluatePose(runtime, runtime.EntryElapsed, out _, out Vector3 tangent);
        return tangent;
    }

    public static void Advance(SplineBusRuntime runtime, float deltaTime)
    {
        if (runtime.EntryMotionTween == null || !runtime.EntryMotionTween.IsActive())
        {
            runtime.EntryMotionTween = DOTween.To(
                    () => 0f,
                    elapsed => ApplyPose(runtime, elapsed),
                    runtime.EntryDuration, runtime.EntryDuration)
                .SetEase(Ease.Linear)
                .SetAutoKill(false)
                .SetLink(runtime.Bus.gameObject, LinkBehaviour.KillOnDestroy)
                .Pause();
        }

        runtime.EntryMotionTween.Goto(
            Mathf.Min(runtime.EntryDuration, runtime.EntryElapsed + deltaTime), false);
    }


    private static void ApplyPose(SplineBusRuntime runtime, float elapsed)
    {
        BusController bus = runtime.Bus;
        float previousElapsed = runtime.EntryElapsed;
        runtime.EntryElapsed = elapsed;
        float leftEnd = runtime.EntryStraightDuration +
            runtime.EntryTurnDuration + runtime.EntryLeftDuration;

        if (!runtime.EntryCurveSwayTriggered &&
            previousElapsed < runtime.EntryStraightDuration &&
            elapsed >= runtime.EntryStraightDuration)
        {
            runtime.EntryCurveSwayTriggered = true;
            bus.PlayTurnSway(Mathf.DeltaAngle(
                runtime.EntryStartRotation.eulerAngles.y, runtime.EntryLeftRotation.eulerAngles.y));
        }

        if (!runtime.EntryAlignSwayTriggered && previousElapsed < leftEnd && elapsed >= leftEnd)
        {
            runtime.EntryAlignSwayTriggered = true;
            bus.PlayTurnSway(Mathf.DeltaAngle(
                runtime.EntryLeftRotation.eulerAngles.y, runtime.EntryTargetRotation.eulerAngles.y));
        }

        EvaluatePose(runtime, elapsed, out Vector3 position, out Vector3 tangent);
        Quaternion rotation = tangent.sqrMagnitude > Mathf.Epsilon
            ? Quaternion.LookRotation(tangent.normalized, Vector3.up)
            : runtime.EntryTargetRotation;
        bus.SetPositionAndRotationFromSpline(position, rotation);

        if (elapsed >= runtime.EntryDuration)
        {
            runtime.IsEntering = false;
            bus.isSpline = true;
            bus.SetSplineMoving(false);
            bus.SetTurnSwaySpeed(1f);
        }
    }

    /// <summary>Pure pose sampling shared by movement and traffic probes.</summary>
    public static void EvaluatePose(
        SplineBusRuntime runtime, float elapsed, out Vector3 position, out Vector3 tangent)
    {
        if (runtime.EntryCurveDistances == null)
            BuildDistanceTables(runtime);
        float curveEnd = runtime.EntryStraightDuration + runtime.EntryTurnDuration;
        float leftEnd = curveEnd + runtime.EntryLeftDuration;
        if (elapsed < runtime.EntryStraightDuration)
        {
            position = Vector3.Lerp(runtime.EntryStartPosition, runtime.EntryCurveStart,
                elapsed / runtime.EntryStraightDuration);
            tangent = runtime.EntryCurveStart - runtime.EntryStartPosition;
        }
        else if (elapsed < curveEnd)
        {
            float fraction = (elapsed - runtime.EntryStraightDuration) / runtime.EntryTurnDuration;
            float t = DistanceToParameter(runtime.EntryCurveDistances, fraction);
            position = CurvePosition(runtime, t, false);
            tangent = CurveTangent(runtime, t, false);
        }
        else if (elapsed < leftEnd)
        {
            position = Vector3.Lerp(runtime.EntryCurveEnd, runtime.EntryLeftPosition,
                (elapsed - curveEnd) / runtime.EntryLeftDuration);
            tangent = runtime.EntryLeftPosition - runtime.EntryCurveEnd;
        }
        else
        {
            float u = runtime.EntryAlignDuration > Mathf.Epsilon
                ? Mathf.Clamp01((elapsed - leftEnd) / runtime.EntryAlignDuration) : 1f;
            // Integrate a linear speed transition so the endpoint matches conveyor speed.
            float sum = runtime.EntryMoveSpeed + runtime.EntryEndSpeed;
            float fraction = sum > Mathf.Epsilon
                ? (2f * runtime.EntryMoveSpeed * u +
                   (runtime.EntryEndSpeed - runtime.EntryMoveSpeed) * u * u) / sum
                : u;
            float t = DistanceToParameter(runtime.EntryAlignDistances, fraction);
            position = CurvePosition(runtime, t, true);
            tangent = CurveTangent(runtime, t, true);
        }
    }

    // Seventh-degree turns have zero curvature and curvature slope at straight joins.
    // Position, heading, turning speed and turning acceleration enter/leave smoothly.
    private static void GetTurnControls(
        SplineBusRuntime runtime, bool align,
        out Vector3 p0, out Vector3 p1, out Vector3 p2,
        out Vector3 p3, out Vector3 p4, out Vector3 p5,
        out Vector3 p6, out Vector3 p7)
    {
        Vector3 incoming;
        Vector3 outgoing;
        if (align)
        {
            p0 = runtime.EntryLeftPosition;
            p7 = runtime.EntryTargetPosition;
            // Convert the authored quarter-circle cubic handles to smooth turn handles.
            const float handleScale = 0.2f / 0.5522848f;
            incoming = (runtime.EntryAlignControl1 - p0) * handleScale;
            outgoing = (p7 - runtime.EntryAlignControl2) * handleScale;
        }
        else
        {
            p0 = runtime.EntryCurveStart;
            p7 = runtime.EntryCurveEnd;
            incoming = (runtime.EntryTurnPosition - p0) * 0.2f;
            outgoing = (p7 - runtime.EntryTurnPosition) * 0.2f;
        }
        p1 = p0 + incoming;
        p2 = p0 + 2f * incoming;
        p3 = p0 + 3f * incoming;
        p6 = p7 - outgoing;
        p5 = p7 - 2f * outgoing;
        p4 = p7 - 3f * outgoing;
    }

    private static Vector3 CurvePosition(SplineBusRuntime runtime, float t, bool align)
    {
        GetTurnControls(runtime, align, out Vector3 p0, out Vector3 p1,
            out Vector3 p2, out Vector3 p3, out Vector3 p4, out Vector3 p5,
            out Vector3 p6, out Vector3 p7);
        float u = 1f - t;
        float u2 = u * u, t2 = t * t;
        float u3 = u2 * u, t3 = t2 * t;
        float u4 = u2 * u2, t4 = t2 * t2;
        return u4 * u3 * p0 + 7f * u3 * u3 * t * p1 +
            21f * u4 * u * t2 * p2 + 35f * u4 * t3 * p3 +
            35f * u3 * t4 * p4 + 21f * u2 * t4 * t * p5 +
            7f * u * t3 * t3 * p6 + t4 * t3 * p7;
    }

    private static Vector3 CurveTangent(SplineBusRuntime runtime, float t, bool align)
    {
        GetTurnControls(runtime, align, out Vector3 p0, out Vector3 p1,
            out Vector3 p2, out Vector3 p3, out Vector3 p4, out Vector3 p5,
            out Vector3 p6, out Vector3 p7);
        float u = 1f - t;
        float u2 = u * u, t2 = t * t;
        float u3 = u2 * u, t3 = t2 * t;
        float u4 = u2 * u2, t4 = t2 * t2;
        return 7f * (u3 * u3 * (p1 - p0) + 6f * u4 * u * t * (p2 - p1) +
            15f * u4 * t2 * (p3 - p2) + 20f * u3 * t3 * (p4 - p3) +
            15f * u2 * t4 * (p5 - p4) + 6f * u * t4 * t * (p6 - p5) +
            t3 * t3 * (p7 - p6));
    }

    private static void BuildDistanceTables(SplineBusRuntime runtime)
    {
        runtime.EntryCurveDistances = BuildDistanceTable(runtime, false);
        runtime.EntryAlignDistances = BuildDistanceTable(runtime, true);
        runtime.EntryCurveLength = runtime.EntryCurveDistances[CurveSamples];
        runtime.EntryAlignLength = runtime.EntryAlignDistances[CurveSamples];
    }

    private static float[] BuildDistanceTable(SplineBusRuntime runtime, bool align)
    {
        var distances = new float[CurveSamples + 1];
        Vector3 previous = CurvePosition(runtime, 0f, align);
        for (int i = 1; i <= CurveSamples; i++)
        {
            Vector3 current = CurvePosition(runtime, i / (float)CurveSamples, align);
            distances[i] = distances[i - 1] + Vector3.Distance(previous, current);
            previous = current;
        }
        return distances;
    }

    private static float DistanceToParameter(float[] distances, float fraction)
    {
        float target = Mathf.Clamp01(fraction) * distances[CurveSamples];
        if (distances[CurveSamples] <= Mathf.Epsilon)
            return Mathf.Clamp01(fraction);
        int high = 1;
        while (high < CurveSamples && distances[high] < target)
            high++;
        float segment = distances[high] - distances[high - 1];
        float local = segment > Mathf.Epsilon ? (target - distances[high - 1]) / segment : 0f;
        return (high - 1 + local) / CurveSamples;
    }
}
