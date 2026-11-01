using UnityEngine;

/// <summary>Exit transfer rules recovered from MyGame's board-to-exit controller.</summary>
public static class BusExitTransferMath
{
    /// <summary>Changing paths preserves metres per second, not normalized progress per second.</summary>
    public static float GetExitNormalizedSpeed(float boardSpeed, float boardLength, float exitLength)
    {
        return exitLength > Mathf.Epsilon
            ? Mathf.Max(0f, boardSpeed) * Mathf.Max(0f, boardLength) / exitLength : 0f;
    }

    public static float DistanceToParameter(float[] distances, float travelled)
    {
        if (distances == null || distances.Length < 2) return 1f;
        int last = distances.Length - 1;
        if (travelled >= distances[last]) return 1f;
        if (travelled <= 0f) return 0f;
        int high = 1;
        while (high < last && distances[high] < travelled) high++;
        float length = distances[high] - distances[high - 1];
        float local = length > Mathf.Epsilon ? (travelled - distances[high - 1]) / length : 0f;
        return (high - 1 + local) / last;
    }

    public static bool HasReachedConnection(
        bool isCompleted,
        float busProgress,
        float connectionProgress,
        Vector3 busPosition,
        Vector3 connectionPosition,
        float proximity)
    {
        if (!isCompleted || float.IsNaN(busProgress) ||
            float.IsNaN(connectionProgress) || proximity < 0f)
        {
            return false;
        }

        // MyGame checks that the bus has not passed the projected exit
        // progress, then checks its world distance from that point.
        return busProgress <= connectionProgress + 0.0001f &&
               (busPosition - connectionPosition).sqrMagnitude <=
               proximity * proximity;
    }

    public static void Evaluate(
        float normalizedTime,
        Vector3 startPosition,
        Vector3 targetPosition,
        Quaternion startRotation,
        Quaternion targetRotation,
        Vector3 startScale,
        Vector3 targetScale,
        AnimationCurve movementCurveX,
        AnimationCurve movementCurveY,
        AnimationCurve rotationCurve,
        AnimationCurve scaleCurve,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 scale,
        bool useXZBoard = false)
    {
        float time = Mathf.Clamp01(normalizedTime);
        float x = movementCurveX != null ? movementCurveX.Evaluate(time) : time;
        float y = movementCurveY != null ? movementCurveY.Evaluate(time) : time;
        float turn = rotationCurve != null ? rotationCurve.Evaluate(time) : time;
        float size = scaleCurve != null ? scaleCurve.Evaluate(time) : time;

        if (useXZBoard)
        {
            var p = MyGameBoardCoordinates.EvaluateXZTransfer(
                new MyGameBoardCoordinates.Point(startPosition.x, startPosition.y, startPosition.z),
                new MyGameBoardCoordinates.Point(targetPosition.x, targetPosition.y, targetPosition.z), time, x, y);
            position = new Vector3(p.X, p.Y, p.Z);
        }
        else
        {
            position = new Vector3(
                Mathf.LerpUnclamped(startPosition.x, targetPosition.x, x),
                Mathf.LerpUnclamped(startPosition.y, targetPosition.y, y),
                Mathf.Lerp(startPosition.z, targetPosition.z, time));
        }
        rotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, turn);
        scale = Vector3.LerpUnclamped(startScale, targetScale, size);
    }
}
