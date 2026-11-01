using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>Applies the source board frame to visual and gameplay data together.</summary>
public sealed class TownLevelPath
{
    private readonly Matrix4x4 boardToWorld;
    private readonly MyGameBoardCoordinates coordinates;
    public float GameplayY { get; }
    public float WorldScale { get; }

    public TownLevelPath(Transform board, float gameplayY)
    {
        Transform fill = board.Find("Road_InteriorFill");
        coordinates = new MyGameBoardCoordinates(fill == null ? 0f : fill.localPosition.y);
        boardToWorld = board.localToWorldMatrix;
        GameplayY = gameplayY;
        WorldScale = coordinates.Scale * Mathf.Abs(board.lossyScale.x);
    }

    public Vector3 SourceCameraPosition
    {
        get
        {
            var p = MyGameBoardCoordinates.CameraPosition;
            return ToWorld(new Vector3(p.X, p.Y, p.Z));
        }
    }
    public float ExchangeDistance => MyGameBoardCoordinates.ExchangeProximity * WorldScale;
    public float ExitReachedDistance => MyGameBoardCoordinates.ExitReachedProximity * WorldScale;
    public Vector3 GetDockBusPosition(int lane, int laneCount, int row)
    {
        var p = MyGameBoardCoordinates.GetDockBusPosition(lane, laneCount, row);
        return ToGameplay(new Vector3(p.X, p.Y, p.Z));
    }

    public Vector3 ToWorld(Vector3 source)
    {
        var p = coordinates.ToBoardPoint(new MyGameBoardCoordinates.Point(source.x, source.y, source.z));
        return boardToWorld.MultiplyPoint3x4(new Vector3(p.X, p.Y, p.Z));
    }

    public Vector3 ToGameplay(Vector3 source)
    {
        Vector3 result = ToWorld(source);
        result.y = GameplayY;
        return result;
    }

    public Vector3 ToGameplayStop(Vector3 importedPosition) =>
        ToGameplay(new Vector3(importedPosition.x, importedPosition.z, 0f));

    public float ProfileWorldScale => WorldScale * 4f;

    private BezierKnot MapKnot(BezierKnot knot, Transform target)
    {
        Vector3 source = knot.Position;
        Vector3 position = target.InverseTransformPoint(ToGameplay(source));
        Vector3 inPoint = target.InverseTransformPoint(ToGameplay(source +
            (Vector3)math.rotate(knot.Rotation, knot.TangentIn)));
        Vector3 outPoint = target.InverseTransformPoint(ToGameplay(source +
            (Vector3)math.rotate(knot.Rotation, knot.TangentOut)));
        return new BezierKnot(position, inPoint - position, outPoint - position, quaternion.identity);
    }

    public Spline BuildMergedGameplaySpline(Spline source, Transform target)
    {
        var result = new Spline();
        for (int i = 0; i < source.Count; i++) result.Add(MapKnot(source[i], target), TangentMode.Broken);
        // Same ordering as MergeWithStaticPath: all level points followed by
        // the static path's interior points. Shared endpoints are not duplicated.
        MyGameBoardCoordinates.Knot[] fixedKnots = MyGameBoardCoordinates.CreateStaticKnots();
        for (int i = 1; i < fixedKnots.Length - 1; i++)
        {
            var k = fixedKnots[i];
            var knot = new BezierKnot(new float3(k.Position.X, k.Position.Y, k.Position.Z),
                new float3(k.TangentIn.X, k.TangentIn.Y, k.TangentIn.Z),
                new float3(k.TangentOut.X, k.TangentOut.Y, k.TangentOut.Z), quaternion.identity);
            result.Add(MapKnot(knot, target), TangentMode.Broken);
        }
        result.Closed = true;
        return result;
    }

    public static Spline BuildFixedExitSpline(Transform board, Transform target, float gameplayY)
    {
        var frame = new TownLevelPath(board, gameplayY);
        var result = new Spline();
        foreach (var k in MyGameBoardCoordinates.CreateExitKnots())
        {
            var knot = new BezierKnot(new float3(k.Position.X, k.Position.Y, k.Position.Z),
                new float3(k.TangentIn.X, k.TangentIn.Y, k.TangentIn.Z),
                new float3(k.TangentOut.X, k.TangentOut.Y, k.TangentOut.Z), quaternion.identity);
            result.Add(frame.MapKnot(knot, target), TangentMode.Broken);
        }
        return result;
    }
}
