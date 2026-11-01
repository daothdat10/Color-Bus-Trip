using UnityEngine;
using UnityEngine.Splines;

public interface IBusPath
{
    float Length { get; }
    Vector3 EvaluatePosition(float progress);
    Vector3 EvaluateTangent(float progress);
    Vector3 EvaluateUpVector(float progress);
    Quaternion EvaluateRotation(float progress);
    float GetClosestProgress(Vector3 worldPosition);
}

public sealed class UnitySplinePath : IBusPath
{
    private readonly SplineContainer container;
    private readonly int splineIndex;

    public UnitySplinePath(SplineContainer container, int splineIndex)
    {
        this.container = container;
        this.splineIndex = splineIndex;
    }

    public float Length
    {
        get
        {
            return container == null ||
                   splineIndex < 0 ||
                   splineIndex >= container.Splines.Count
                ? 0f
                : container.CalculateLength(splineIndex);
        }
    }

    public Vector3 EvaluatePosition(float progress)
    {
        return container == null
            ? Vector3.zero
            : container.EvaluatePosition(splineIndex, Mathf.Clamp01(progress));
    }

    public Vector3 EvaluateTangent(float progress)
    {
        return container == null
            ? Vector3.forward
            : container.EvaluateTangent(splineIndex, Mathf.Clamp01(progress));
    }

    public Vector3 EvaluateUpVector(float progress)
    {
        return container == null
            ? Vector3.up
            : container.EvaluateUpVector(splineIndex, Mathf.Clamp01(progress));
    }

    public Quaternion EvaluateRotation(float progress)
    {
        return EvaluateUprightRotation(EvaluateTangent(progress));
    }

    public static Quaternion EvaluateUprightRotation(Vector3 tangent)
    {
        Vector3 forward = Vector3.ProjectOnPlane(tangent, Vector3.up);
        return forward.sqrMagnitude <= Mathf.Epsilon
            ? Quaternion.identity
            : Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    /// <summary>
    /// Projects a world point onto this spline and returns its normalized path
    /// progress. Keeping the conversion here means gameplay code never has to
    /// mix world-space bus positions with the spline's local-space API.
    /// </summary>
    public float GetClosestProgress(Vector3 worldPosition)
    {
        if (container == null || splineIndex < 0 ||
            splineIndex >= container.Splines.Count)
        {
            return 0f;
        }

        Vector3 localPosition = container.transform.InverseTransformPoint(
            worldPosition
        );
        Unity.Mathematics.float3 nearestPosition;
        float progress;
        SplineUtility.GetNearestPoint(
            container.Splines[splineIndex],
            localPosition,
            out nearestPosition,
            out progress,
            64,
            4
        );
        return container.Splines[splineIndex].Closed
            ? Mathf.Repeat(progress, 1f)
            : Mathf.Clamp01(progress);
    }

    /// <summary>
    /// Dreamteck assigns equal parameter ranges to Bezier curves. Unity assigns
    /// normalized progress by length, including the added closing curve. Convert
    /// the authored curve and its Bezier t without projecting onto a nearby lane.
    /// </summary>
    public float ConvertImportedProgress(float progress, int sourceCurveCount)
    {
        if (!BusStopProximity.IsValidProgress(progress) || container == null ||
            splineIndex < 0 || splineIndex >= container.Splines.Count)
            return float.NaN;
        Spline spline = container.Splines[splineIndex];
        // SplineContainer evaluates scaled transforms through NativeSpline.
        // Use the same world-space length parametrization for stop markers.
        using (var worldSpline = new NativeSpline(spline, container.transform.localToWorldMatrix))
            return ConvertImportedProgressOnPath(worldSpline, progress, sourceCurveCount);
    }

    private static float ConvertImportedProgressOnPath<T>(T spline, float progress, int sourceCurveCount)
        where T : ISpline
    {
        int curveCount = spline.Closed ? spline.Count : spline.Count - 1;
        if (sourceCurveCount < 1 || sourceCurveCount > curveCount) return float.NaN;
        float curveParameter = progress * sourceCurveCount;
        int curveIndex = Mathf.Min(Mathf.FloorToInt(curveParameter), sourceCurveCount - 1);
        float curveT = curveParameter - curveIndex;
        float distanceBeforeCurve = 0f;
        for (int i = 0; i < curveIndex; i++) distanceBeforeCurve += spline.GetCurveLength(i);
        float low = 0f, high = spline.GetCurveLength(curveIndex);
        for (int i = 0; i < 24; i++)
        {
            float middle = (low + high) * 0.5f;
            if (spline.GetCurveInterpolation(curveIndex, middle) < curveT) low = middle;
            else high = middle;
        }
        float length = spline.GetLength();
        return length > Mathf.Epsilon
            ? Mathf.Clamp01((distanceBeforeCurve + (low + high) * 0.5f) / length)
            : float.NaN;
    }
}
