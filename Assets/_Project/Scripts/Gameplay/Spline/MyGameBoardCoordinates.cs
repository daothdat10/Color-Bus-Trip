using System;

/// <summary>Reference board coordinates, independent of the camera and level shape.</summary>
public sealed class MyGameBoardCoordinates
{
    // Source GameScene camera rig (transform 526 + Main Camera) and Dock path.
    public const float CameraFieldOfView = 20f;
    public const float CameraPitchInXZ = 60f;
    public const float DockFrontY = -2.45f;
    public const float DockLaneSpacing = .25f;
    public const float DefaultBusExtent = .4f;
    public const float ExitLength = 20f;
    public const float ExitReachedProximity = .2f;
    public const float ExitSpeedCoefficient = 1.5f;
    // BoardItemVisual_Bus / exchange state; distance measured along the path.
    public const float ExchangeProximity = .2f;
    public static Point CameraPosition => new Point(0f, -7.65f, -10.5f);

    public readonly struct Point
    {
        public readonly float X, Y, Z;
        public Point(float x, float y, float z = 0f) { X = x; Y = y; Z = z; }
    }

    public readonly struct Knot
    {
        public readonly Point Position, TangentIn, TangentOut;
        public Knot(Point position, Point tangentIn, Point tangentOut)
        { Position = position; TangentIn = tangentIn; TangentOut = tangentOut; }
    }

    // BusDataNew GameScene/StaticBoardSpline (component 836). Stored in local
    // XZ at scale .04, rotated -90 degrees into the source game's XY plane.
    // Tangents below are relative handles, not absolute control positions.
    public static Knot[] CreateStaticKnots() => new[]
    {
        FromExport(18.918524f, -50.443066f, 18.918518f, -46.38308f, 18.918524f, -52.288017f),
        FromExport(14.026943f, -55.14973f, 18.115543f, -55.14973f, 10.196325f, -55.14973f),
        FromExport(-13.451777f, -55.07573f, -7.7225566f, -55.074474f, -16.795973f, -55.076466f),
        FromExport(-18.902983f, -50.44308f, -18.902983f, -51.988525f, -18.902983f, -46.89869f)
    };

    public static void GetRuntimeQueueIndices(int sourceColumn, int sourceRow, out int depth, out int lane)
    {
        // BoardItemCreator_DockConveyor groups by Col, then orders items by Row.
        // The local queue API uses i=depth and j=lane, the inverse naming.
        depth = sourceRow;
        lane = sourceColumn;
    }

    public static Point GetDockBusPosition(int lane, int laneCount, int row)
    {
        // InitDockConveyors centres the dock lanes. InsertBoardItems starts at
        // progress 1 and consumes half an extent before placing each bus centre.
        if (laneCount < 1 || lane < 0 || lane >= laneCount || row < 0)
            throw new ArgumentOutOfRangeException();
        return new Point((lane - (laneCount - 1) * .5f) * DockLaneSpacing,
            DockFrontY - (row + .5f) * DefaultBusExtent);
    }

    public static Knot[] CreateExitKnots()
    {
        Point start = CreateStaticKnots()[0].Position;
        Point end = new Point(start.X, start.Y - ExitLength, start.Z);
        return new[] {
            new Knot(start, new Point(0, 1), new Point(0, -1)),
            new Knot(end, new Point(0, 1), new Point(0, -1))
        };
    }

    public static Point EvaluateXZTransfer(Point start, Point target, float time, float curveX, float curveY)
    {
        // The original animated XY board maps to XZ. Height maps from source Z
        // and remains linear; applying curveY to world height changes the turn.
        return new Point(start.X + (target.X - start.X) * curveX,
            start.Y + (target.Y - start.Y) * Math.Max(0f, Math.Min(1f, time)),
            start.Z + (target.Z - start.Z) * curveY);
    }

    private static Knot FromExport(float x, float z, float inX, float inZ, float outX, float outZ) =>
        new Knot(new Point(x * .04f, z * .04f),
            new Point((inX - x) * .04f, (inZ - z) * .04f),
            new Point((outX - x) * .04f, (outZ - z) * .04f));

    public float Scale { get; }
    private readonly float sourceCenterX, sourceJoinY, floorY;

    public MyGameBoardCoordinates(float floorY)
    {
        Knot[] knots = CreateStaticKnots();
        Point right = knots[0].Position, left = knots[knots.Length - 1].Position;
        Scale = .38f / (right.X - left.X);
        sourceCenterX = (right.X + left.X) * .5f;
        sourceJoinY = (right.Y + left.Y) * .5f;
        this.floorY = floorY;
    }

    // A single similarity transform is used by road knots, handles, Stops and
    // PassengerStopPosition. Never fit the data to its own endpoint bounds.
    public Point ToBoardPoint(Point source) => new Point(
        (source.X - sourceCenterX) * Scale,
        floorY - source.Z * Scale,
        .3f + (source.Y - sourceJoinY) * Scale);

    public Point ToBoardVector(Point source) =>
        new Point(source.X * Scale, -source.Z * Scale, source.Y * Scale);

    public static int GetMergedPointCount(int levelPointCount) =>
        levelPointCount < 2 ? 0 : levelPointCount + CreateStaticKnots().Length - 2;
}
