using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public sealed class BusExitTransferTests
{
    [TestCase(20f, 40f, 0.3f)]
    [TestCase(50f, 10f, 0.1f)]
    public void ExitPathPreservesWorldSpeedAcrossDifferentPathLengths(float boardLength, float exitLength, float speed)
    {
        float normalized = BusExitTransferMath.GetExitNormalizedSpeed(speed, boardLength, exitLength);
        Assert.That(normalized * exitLength, Is.EqualTo(speed * boardLength).Within(0.0001f),
            "An exit must use the board's metres per second without the old 1.5 multiplier.");
    }

    [TestCase(0f)]
    [TestCase(0.2f)]
    public void ExitTransferAndPathShareOneFrameAtTheBoardWorldSpeed(float gap)
    {
        WithExit(gap, (coordinator, bus) =>
        {
            TickExit(coordinator, 0.1f, gap);
            Assert.That(bus.transform.position.z, Is.EqualTo(0.6f).Within(0.001f),
                "Six metres per second must travel 0.6m, including transfer and leftover frame time.");
            TickExit(coordinator, 0.1f, gap);
            Assert.That(bus.transform.position.z, Is.EqualTo(1.2f).Within(0.001f),
                "Changing from the connector to the exit spline must keep the same speed.");
        });
    }

    [Test]
    public void CurvedExitTransferUsesDistanceInsteadOfAnimationCurveTime()
    {
        WithExit(4f, (coordinator, bus) =>
        {
            // Change the transfer target to make a curved connector; leave the
            // exit path untouched because this check stays inside the transfer.
            var runs = (System.Collections.IList)coordinator.GetType().GetField("exitingBuses",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(coordinator);
            object run = runs[0];
            run.GetType().GetField("transferTargetPosition").SetValue(run, new Vector3(3f, 0f, 4f));
            AnimationCurve eased = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            Vector3 previous = bus.transform.position;
            for (int i = 0; i < 20; i++)
            {
                TickExit(coordinator, 0.02f, 4f, eased);
                Assert.That(Vector3.Distance(previous, bus.transform.position),
                    Is.EqualTo(0.12f).Within(0.005f), "Curve easing must not change metres per second.");
                previous = bus.transform.position;
            }
        });
    }

    private static void TickExit(object coordinator, float deltaTime, float gap, AnimationCurve movementX = null)
    {
        var linear = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        coordinator.GetType().GetMethod("Tick").Invoke(coordinator, new object[]
        {
            new ExitSpeedTestPath(40f, Vector3.forward * gap), deltaTime,
            movementX ?? linear, linear, linear, linear,
        });
    }

    private static void WithExit(float gap, System.Action<object, BusController> check)
    {
        var go = new GameObject("exit-speed-bus");
        var assembly = typeof(SplineControll).Assembly;
        var motorType = assembly.GetType("SplineBusMotor");
        var motor = System.Activator.CreateInstance(motorType);
        object coordinator = null;
        try
        {
            var bus = go.AddComponent<BusController>();
            var runtime = System.Activator.CreateInstance(assembly.GetType("SplineBusRuntime"), bus, 0f);
            motorType.GetMethod("Add").Invoke(motor, new[] { runtime });
            var entry = System.Activator.CreateInstance(assembly.GetType("SplineBusEntryCoordinator"), motor);
            var exitType = assembly.GetType("SplineBusExitCoordinator");
            coordinator = System.Activator.CreateInstance(exitType, motor, entry);
            exitType.GetMethod("ConfigureSourceFrame").Invoke(coordinator, new object[] { true });
            exitType.GetMethod("BeginTransfer", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic).Invoke(coordinator, new object[]
                { bus, runtime, null, new ExitSpeedTestPath(20f, Vector3.zero),
                    new ExitSpeedTestPath(40f, Vector3.forward * gap), 0.3f });
            check(coordinator, bus);
        }
        finally
        {
            coordinator?.GetType().GetMethod("Clear").Invoke(coordinator, null);
            motorType.GetMethod("Clear").Invoke(motor, null);
            Object.DestroyImmediate(go);
        }
    }

    private sealed class ExitSpeedTestPath : IBusPath
    {
        public float Length { get; }
        private readonly Vector3 start;
        public ExitSpeedTestPath(float length, Vector3 origin) { Length = length; start = origin; }
        public Vector3 EvaluatePosition(float progress) => start + Vector3.forward * (Length * progress);
        public Vector3 EvaluateTangent(float progress) => Vector3.forward;
        public Vector3 EvaluateUpVector(float progress) => Vector3.up;
        public Quaternion EvaluateRotation(float progress) => Quaternion.identity;
        public float GetClosestProgress(Vector3 position) => Mathf.Clamp01((position.z - start.z) / Length);
    }

    [Test]
    public void TownTransferRemapsSourceDepthCurveToZAndKeepsHeightLinear()
    {
        var hold = AnimationCurve.Constant(0, 1, 0);
        var finish = AnimationCurve.Constant(0, 1, 1);
        var linear = AnimationCurve.Linear(0, 0, 1, 1);
        BusExitTransferMath.Evaluate(.5f, new Vector3(0, 1, 0), new Vector3(10, 5, 8),
            Quaternion.identity, Quaternion.identity, Vector3.one, Vector3.one,
            finish, hold, linear, linear, out Vector3 actual, out _, out _, true);
        Assert.That(actual, Is.EqualTo(new Vector3(10, 3, 0)));
    }

    [Test]
    public void TownExitStartsAtStaticFirstPointAndRetainsFullSourceLength()
    {
        var board = new GameObject("Board");
        var pathObject = new GameObject("ExitPath");
        try
        {
            board.transform.position = new Vector3(0, 0, -8);
            board.transform.localScale = Vector3.one * 12.64f;
            pathObject.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(-7.5f, 0, 0));
            var frame = new TownLevelPath(board.transform, .0632f);
            var exit = TownLevelPath.BuildFixedExitSpline(board.transform, pathObject.transform, .0632f);
            Assert.That(exit.Count, Is.EqualTo(2));
            Assert.That(exit.Closed, Is.False);
            var container = pathObject.AddComponent<SplineContainer>();
            container.Spline = exit;
            var sampler = new UnitySplinePath(container, 0);
            var source = MyGameBoardCoordinates.CreateStaticKnots()[0].Position;
            var expected = frame.ToGameplay(new Vector3(source.X, source.Y, source.Z));
            Assert.That(Vector3.Distance(sampler.EvaluatePosition(0), expected), Is.LessThan(.0001f));
            Assert.That(sampler.Length, Is.EqualTo(20f * frame.WorldScale).Within(.02f));
            Assert.That(sampler.EvaluatePosition(1).z, Is.LessThan(-60f));
            Assert.That(frame.ExitReachedDistance, Is.EqualTo(.2f * frame.WorldScale).Within(.0001f));
            Assert.That(board.transform.localScale, Is.EqualTo(Vector3.one * 12.64f));
        }
        finally { Object.DestroyImmediate(pathObject); Object.DestroyImmediate(board); }
    }

    [Test]
    public void CompletedBusMustReachProjectedExitPoint()
    {
        Vector3 connection = new Vector3(2f, 0f, -5f);

        Assert.That(BusExitTransferMath.HasReachedConnection(
            false, 0.99f, 1f, connection, connection, 0.2f), Is.False);
        Assert.That(BusExitTransferMath.HasReachedConnection(
            true, 0.9f, 1f, connection + Vector3.forward, connection, 0.2f), Is.False);
        Assert.That(BusExitTransferMath.HasReachedConnection(
            true, 1.01f, 1f, connection, connection, 0.2f), Is.False);
        Assert.That(BusExitTransferMath.HasReachedConnection(
            true, 0.99f, 1f, connection + Vector3.right * 0.19f,
            connection, 0.2f), Is.True);
    }

    [Test]
    public void TransferUsesSeparateAxisRotationAndScaleCurves()
    {
        AnimationCurve linear = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        AnimationCurve fixedStart = AnimationCurve.Constant(0f, 1f, 0f);
        AnimationCurve fixedEnd = AnimationCurve.Constant(0f, 1f, 1f);
        BusExitTransferMath.Evaluate(
            0.5f,
            new Vector3(0f, 1f, 0f), new Vector3(10f, 5f, 8f),
            Quaternion.identity, Quaternion.Euler(0f, 90f, 0f),
            Vector3.one, Vector3.one * 2f,
            fixedEnd, fixedStart, fixedEnd, linear,
            out Vector3 position, out Quaternion rotation, out Vector3 scale);

        Assert.That(position.x, Is.EqualTo(10f).Within(0.001f));
        Assert.That(position.y, Is.EqualTo(1f).Within(0.001f));
        Assert.That(position.z, Is.EqualTo(4f).Within(0.001f));
        Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(0f, 90f, 0f)),
            Is.LessThan(0.01f));
        Assert.That(scale.x, Is.EqualTo(1.5f).Within(0.001f));
    }

    [Test]
    public void TransferStartsAndEndsAtItsPathPositions()
    {
        AnimationCurve linear = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        Vector3 start = new Vector3(2f, 1f, -5f);
        Vector3 target = new Vector3(3f, 1f, -8f);
        foreach (float time in new[] { 0f, 1f })
        {
            BusExitTransferMath.Evaluate(time, start, target,
                Quaternion.identity, Quaternion.Euler(0f, 180f, 0f),
                Vector3.one, Vector3.one, linear, linear, linear, linear,
                out Vector3 position, out Quaternion rotation, out Vector3 scale);
            Assert.That(Vector3.Distance(position, time == 0f ? start : target),
                Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(rotation,
                time == 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f)),
                Is.LessThan(0.01f));
            Assert.That(scale, Is.EqualTo(Vector3.one));
        }
    }

    [Test]
    public void TransferMovesContinuouslyFromBoardToExitSpline()
    {
        AnimationCurve linear = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        Vector3 boardExit = new Vector3(2.67f, 2f, -4.69f);
        Vector3 exitStart = new Vector3(2.46f, 2f, -8.12f);
        Vector3 previous = boardExit;

        for (int step = 1; step <= 20; step++)
        {
            BusExitTransferMath.Evaluate(step / 20f, boardExit, exitStart,
                Quaternion.identity, Quaternion.Euler(0f, 180f, 0f),
                Vector3.one, Vector3.one, linear, linear, linear, linear,
                out Vector3 current, out _, out _);

            Assert.That(current.z, Is.LessThan(previous.z));
            Assert.That(Vector3.Distance(previous, current),
                Is.LessThan(0.2f));
            previous = current;
        }

        Assert.That(Vector3.Distance(previous, exitStart), Is.LessThan(0.001f));
    }

    [Test]
    public void OpenSplineProjectionKeepsItsEndInsteadOfWrappingToStart()
    {
        GameObject gameObject = new GameObject("BusPathProjectionTest");
        try
        {
            SplineContainer container = gameObject.AddComponent<SplineContainer>();
            Spline spline = new Spline();
            spline.Add(new BezierKnot(new float3(-2f, 0f, 0f)), TangentMode.AutoSmooth);
            spline.Add(new BezierKnot(new float3(2f, 0f, 0f)), TangentMode.AutoSmooth);
            container.Splines = new List<Spline> { spline };

            float progress = new UnitySplinePath(container, 0)
                .GetClosestProgress(new Vector3(2f, 0f, -4f));

            Assert.That(progress, Is.GreaterThan(0.99f));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
