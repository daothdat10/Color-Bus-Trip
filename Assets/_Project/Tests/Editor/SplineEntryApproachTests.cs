using System;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class SplineEntryApproachTests
{
    private GameObject lane;

    [TearDown]
    public void TearDown()
    {
        DOTween.KillAll();
        if (lane != null) UnityEngine.Object.DestroyImmediate(lane);
    }

    [Test]
    public void ScanGateApproachNeverMovesBusBackwards()
    {
        // The lane sits 12.7 behind the world origin, so gate local Z 0 is far behind a front-row bus.
        lane = new GameObject("lane");
        lane.transform.position = new Vector3(0f, 0f, -12.7f);
        GameObject busObject = new GameObject("bus");
        busObject.transform.SetParent(lane.transform, false);
        busObject.transform.localPosition = new Vector3(0f, 0.06f, 6.48f);
        BusController bus = busObject.AddComponent<BusController>();

        Type motorType = typeof(SplineControll).Assembly.GetType("SplineBusMotor");
        Type coordinatorType = typeof(SplineControll).Assembly.GetType("SplineBusEntryCoordinator");
        object coordinator = Activator.CreateInstance(
            coordinatorType, Activator.CreateInstance(motorType));
        coordinatorType.GetMethod("Configure").Invoke(coordinator, new object[]
        {
            2.2f, 2.8f, 0.55f, 0.2f, (LayerMask)(~0), 4f, 0.3f, 0f, 0.6f, 0f,
        });

        coordinatorType.GetMethod("MoveBusToScanGate", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(coordinator, new object[] { bus });
        DOTween.CompleteAll();

        Assert.That(busObject.transform.localPosition.z, Is.GreaterThanOrEqualTo(6.48f - 0.001f),
            "A bus that is already past the gate must wait where it is instead of driving back to the gate.");
    }

    [Test]
    public void ScanGateApproachStillAdvancesBusBehindTheGate()
    {
        lane = new GameObject("lane");
        GameObject busObject = new GameObject("bus");
        busObject.transform.SetParent(lane.transform, false);
        busObject.transform.localPosition = new Vector3(0f, 0.06f, -3f);
        BusController bus = busObject.AddComponent<BusController>();

        Type motorType = typeof(SplineControll).Assembly.GetType("SplineBusMotor");
        Type coordinatorType = typeof(SplineControll).Assembly.GetType("SplineBusEntryCoordinator");
        object coordinator = Activator.CreateInstance(
            coordinatorType, Activator.CreateInstance(motorType));
        coordinatorType.GetMethod("Configure").Invoke(coordinator, new object[]
        {
            2.2f, 2.8f, 0.55f, 0.2f, (LayerMask)(~0), 4f, 0.3f, 0f, 0.6f, 0f,
        });

        coordinatorType.GetMethod("MoveBusToScanGate", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(coordinator, new object[] { bus });
        DOTween.CompleteAll();

        Assert.That(busObject.transform.localPosition.z, Is.EqualTo(0f).Within(0.001f));
    }

    [TestCase(-0.8f)]
    [TestCase(0f)]
    [TestCase(0.8f)]
    public void EntranceMaintainsSpeedAndContinuousHeadingWithoutTraffic(float laneX)
    {
        object runtime = CreateSmoothEntrance(laneX, out _, out _);
        Type type = runtime.GetType();
        Type movement = typeof(SplineControll).Assembly.GetType("SplineEntranceMovement");
        MethodInfo evaluate = movement.GetMethod("EvaluatePose");
        float Get(string name) => (float)type.GetField(name).GetValue(runtime);
        Vector3 Sample(float time, out Vector3 tangent)
        {
            object[] args = { runtime, time, null, null };
            evaluate.Invoke(null, args);
            tangent = (Vector3)args[3];
            return (Vector3)args[2];
        }
        float straightEnd = Get("EntryStraightDuration");
        float curveEnd = straightEnd + Get("EntryTurnDuration");
        float alignStart = curveEnd + Get("EntryLeftDuration");
        float duration = Get("EntryDuration");
        const float step = 0.002f;

        // Arc length, not the Bezier parameter, must determine speed on both turns.
        for (float time = step; time < duration - step; time += step)
        {
            Vector3 before = Sample(time, out _);
            Vector3 after = Sample(time + step, out _);
            float expected = Get("EntryMoveSpeed");
            if (time >= alignStart)
                expected = Mathf.Lerp(expected, Get("EntryEndSpeed"),
                    (time + step * 0.5f - alignStart) / Get("EntryAlignDuration"));
            float actual = Vector3.Distance(before, after) / step;
            Assert.That(actual, Is.EqualTo(expected).Within(expected * 0.03f),
                "The entrance must not slow down in the middle of a turn or pause at a segment boundary.");
        }

        foreach (float boundary in new[] { straightEnd, curveEnd, alignStart })
        {
            Sample(boundary - 0.0001f, out Vector3 before);
            Sample(boundary + 0.0001f, out Vector3 after);
            Assert.That(Vector3.Angle(before, after), Is.LessThan(0.5f),
                "Travel heading must remain continuous when switching entrance segments.");

            const float angularStep = 0.0005f;
            Sample(boundary - angularStep, out Vector3 incoming);
            Sample(boundary, out Vector3 atJoin);
            Sample(boundary + angularStep, out Vector3 outgoing);
            // At sub-millisecond steps acos-based Vector3.Angle loses precision.
            // atan2(cross, dot) resolves the small signed angle at a smooth join.
            float AngularRate(Vector3 from, Vector3 to) =>
                Mathf.Atan2(Vector3.Cross(from, to).y, Vector3.Dot(from, to)) *
                Mathf.Rad2Deg / angularStep;
            float incomingRate = AngularRate(incoming, atJoin);
            float outgoingRate = AngularRate(atJoin, outgoing);
            Assert.That(Mathf.Abs(outgoingRate - incomingRate), Is.LessThan(10f),
                "Turning speed must ramp from/to zero; matching only heading still produces a visible jerk.");
        }

        Vector3 end = Sample(duration, out Vector3 endTangent);
        Vector3 target = (Vector3)type.GetField("EntryTargetPosition").GetValue(runtime);
        Quaternion targetRotation = (Quaternion)type.GetField("EntryTargetRotation").GetValue(runtime);
        Assert.That(Vector3.Distance(end, target), Is.LessThan(0.0001f));
        Assert.That(Vector3.Angle(endTangent, targetRotation * Vector3.forward), Is.LessThan(0.1f));
        Assert.That(Get("EntryEndSpeed"), Is.EqualTo(6f).Within(0.001f));
    }

    [Test]
    public void EntranceHandsUnusedFrameTimeToConveyor()
    {
        object runtime = CreateSmoothEntrance(0f, out object motor, out object coordinator);
        Type type = runtime.GetType();
        float duration = (float)type.GetField("EntryDuration").GetValue(runtime);
        type.GetField("EntryElapsed").SetValue(runtime, duration - 0.01f);
        motor.GetType().GetMethod("Tick").Invoke(motor, new object[]
        {
            new EntranceTestPath(), coordinator, 0.03f, 0.3f, 0.2f, true, false, null,
        });
        Assert.That(type.GetField("IsEntering").GetValue(runtime), Is.False);
        Assert.That((float)type.GetField("Progress").GetValue(runtime),
            Is.EqualTo(0.006f).Within(0.0001f),
            "The remaining 0.02 seconds must move the bus on the spline in the same frame.");
        motor.GetType().GetMethod("Clear").Invoke(motor, null);
    }

    [TestCase(0.016f)]
    [TestCase(0.2f)]
    public void ReservedEntranceStillStopsForBusInPhysicalCorridor(float delta)
    {
        object runtime = CreateSmoothEntrance(0f, out object motor, out object coordinator);
        Type type = runtime.GetType();
        type.GetField("EntryTransferStarted").SetValue(runtime, true);
        GameObject obstacle = new GameObject("obstacle");
        obstacle.transform.SetParent(lane.transform);
        obstacle.transform.position = new Vector3(0f, 0f, -2.8f);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(obstacle.transform, false);
        body.transform.localScale = new Vector3(1f, 1f, 2f);
        BusController blockedBy = obstacle.AddComponent<BusController>();
        object other = Activator.CreateInstance(type, new object[] { blockedBy, 0.5f });
        motor.GetType().GetMethod("Add").Invoke(motor, new[] { other });
        coordinator.GetType().GetMethod("AdvanceEntrance").Invoke(coordinator,
            new object[] { runtime, new EntranceTestPath(), delta, 0.3f, true });
        Assert.That((float)type.GetField("EntryElapsed").GetValue(runtime), Is.Zero);
        motor.GetType().GetMethod("Clear").Invoke(motor, null);
    }

    [Test] public void RapidDuplicateRegistrationDoesNotAddAnotherRuntime()
    {
        object runtime = CreateSmoothEntrance(0f, out object motor, out object coordinator);
        var bus = (BusController)runtime.GetType().GetField("Bus").GetValue(runtime);
        for (int i = 0; i < 20; i++)
            coordinator.GetType().GetMethod("AddStickMan").Invoke(coordinator,
                new object[] { bus, new EntranceTestPath() });
        int count = 0;
        foreach (object item in (System.Collections.IEnumerable)motor.GetType().GetProperty("Buses").GetValue(motor)) count++;
        Assert.That(count, Is.EqualTo(1));
        motor.GetType().GetMethod("Clear").Invoke(motor, null);
    }

    [Test] public void AnotherCommittedEntrantDoesNotLockTheWholeEntrance()
    {
        object runtime = CreateSmoothEntrance(0f, out object motor, out object coordinator);
        Type type = runtime.GetType();
        GameObject otherObject = new GameObject("other lane");
        otherObject.transform.SetParent(lane.transform);
        otherObject.transform.position = new Vector3(8f, 0f, -3f);
        BusController otherBus = otherObject.AddComponent<BusController>();
        object other = Activator.CreateInstance(type, new object[] { otherBus, 0f });
        type.GetField("IsEntering").SetValue(other, true);
        type.GetField("EntryTransferStarted").SetValue(other, true);
        motor.GetType().GetMethod("Add").Invoke(motor, new[] { other });
        coordinator.GetType().GetMethod("AdvanceEntrance").Invoke(coordinator,
            new object[] { runtime, new EntranceTestPath(), 0.016f, 0.3f, true });
        Assert.That(type.GetField("EntryTransferStarted").GetValue(runtime), Is.True);
        Assert.That((float)type.GetField("EntryElapsed").GetValue(runtime), Is.GreaterThan(0f));
        motor.GetType().GetMethod("Clear").Invoke(motor, null);
    }

    private object CreateSmoothEntrance(float laneX, out object motor, out object coordinator)
    {
        lane = new GameObject("entrance-test-root");
        GameObject busObject = new GameObject("bus");
        busObject.transform.SetParent(lane.transform, false);
        busObject.transform.position = new Vector3(laneX, 0f, -3f);
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(busObject.transform, false);
        body.transform.localScale = new Vector3(1f, 1f, 2f);
        BusController bus = busObject.AddComponent<BusController>();

        Type motorType = typeof(SplineControll).Assembly.GetType("SplineBusMotor");
        Type coordinatorType = typeof(SplineControll).Assembly.GetType("SplineBusEntryCoordinator");
        motor = Activator.CreateInstance(motorType);
        coordinator = Activator.CreateInstance(coordinatorType, motor);
        coordinatorType.GetMethod("Configure").Invoke(coordinator, new object[]
        {
            2.2f, 2.8f, 0.55f, 0.2f, (LayerMask)0, 5f, 0.25f, 0f, 3f, 0.3f,
        });
        coordinatorType.GetMethod("AddStickMan").Invoke(coordinator,
            new object[] { bus, new EntranceTestPath() });
        // Registration detaches a bus from its queue; restore ownership for test cleanup.
        busObject.transform.SetParent(lane.transform, true);
        var list = (System.Collections.IEnumerable)motorType.GetProperty("Buses").GetValue(motor);
        foreach (object runtime in list) return runtime;
        throw new InvalidOperationException("Test entrance was not registered.");
    }

    private sealed class EntranceTestPath : IBusPath
    {
        public float Length => 20f;
        public Vector3 EvaluatePosition(float progress) => new Vector3(-2.4f, 0f, 20f * progress);
        public Vector3 EvaluateTangent(float progress) => Vector3.forward;
        public Vector3 EvaluateUpVector(float progress) => Vector3.up;
        public Quaternion EvaluateRotation(float progress) => Quaternion.identity;
        public float GetClosestProgress(Vector3 position) => Mathf.Clamp01(position.z / 20f);
    }
}
