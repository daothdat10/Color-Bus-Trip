using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SplineTrafficSpacingTests
{
    [TestCase(0.95f, true)]
    [TestCase(0.05f, true)]
    [TestCase(0.25f, false)]
    [TestCase(0.5f, false)]
    public void ReceiveOccupancyUsesShortestLoopDistance(float progress, bool occupied)
    {
        WithTraffic((type, motor, coordinator, entry, loop) =>
        {
            type.GetField("Progress").SetValue(loop, progress);
            var buses = motor.GetType().GetProperty("Buses").GetValue(motor);
            var spacing = typeof(SplineControll).Assembly.GetType("SplineTrafficSpacing");
            Assert.That(spacing.GetMethod("IsReceivePointOccupied").Invoke(null,
                new[] { (object)0f, 0.2f, entry, buses }), Is.EqualTo(occupied));
        });
    }

    [Test]
    public void BlockedEntryWaitsOnceThenTransfersWithoutCheckingTheMovingBusAgain()
    {
        WithTraffic((type, motor, coordinator, entry, loop) =>
        {
            var advance = coordinator.GetType().GetMethod("AdvanceEntrance");
            void Tick(float dt) => advance.Invoke(coordinator,
                new object[] { entry, new StraightTestPath(), dt, 0.3f, true });
            type.GetField("Progress").SetValue(loop, 0.95f);
            Tick(1.1f);
            Assert.That(type.GetField("EntryElapsed").GetValue(entry), Is.EqualTo(0f));
            Tick(0.1f);
            Assert.That(type.GetField("EntryElapsed").GetValue(entry), Is.EqualTo(0f));
            Assert.That(type.GetField("EntryTransferStarted").GetValue(entry), Is.False);
            type.GetField("Progress").SetValue(loop, 0.3f);
            ((BusController)type.GetField("Bus").GetValue(loop)).transform.position = new Vector3(20f, 0f, 20f);
            Tick(0.01f);
            Assert.That((float)type.GetField("EntryElapsed").GetValue(entry), Is.GreaterThan(0f).And.LessThan(0.01f));
            Assert.That(type.GetField("EntryTransferStarted").GetValue(entry), Is.True);
            // A trailing bus reaches the reservation during the turn. TransferStep
            // must not start another collision wait, even with an overlapping body.
            type.GetField("Progress").SetValue(loop, 0.99f);
            for (int i = 0; i < 45 && (bool)type.GetField("IsEntering").GetValue(entry); i++)
            {
                float before = (float)type.GetField("EntryElapsed").GetValue(entry);
                Tick(0.1f);
                Assert.That((float)type.GetField("EntryElapsed").GetValue(entry), Is.GreaterThan(before),
                    "Recovering speed may take time, but a granted turn must never stop again.");
            }
            Assert.That(type.GetField("IsEntering").GetValue(entry), Is.False);
        });
    }

    [Test]
    public void ReservationStopsApproachingTrafficButLetsLeaderClearAndReleasesImmediately()
    {
        WithTraffic((type, motor, coordinator, entry, loop) =>
        {
            var spacing = typeof(SplineControll).Assembly.GetType("SplineTrafficSpacing");
            var buses = motor.GetType().GetProperty("Buses").GetValue(motor);
            type.GetField("EntryTransferReserved").SetValue(entry, true);
            type.GetField("EntryTransferStarted").SetValue(entry, true);
            var clamp = spacing.GetMethod("ClampAgainstTransfers");
            float Clamp(float proposed) => (float)clamp.Invoke(null,
                new[] { loop, (object)proposed, buses, new StraightTestPath() });
            type.GetField("Progress").SetValue(loop, 0.7f);
            Assert.That(Clamp(0.99f), Is.EqualTo(0.8f - SplineProgressSolver.Epsilon).Within(0.000001f));
            type.GetField("Progress").SetValue(loop, 0.05f);
            Assert.That(Clamp(0.1f), Is.EqualTo(0.1f).Within(0.0001f));
            type.GetField("Progress").SetValue(loop, 0.7f);
            type.GetField("IsEntering").SetValue(entry, false);
            Assert.That(Clamp(0.99f), Is.EqualTo(0.99f).Within(0.0001f));
        });
    }

    [Test]
    public void NoTrafficStartsAndFinishesInOneFrameWithoutLosingHandoffTime()
    {
        WithTraffic((type, motor, coordinator, entry, loop) =>
        {
            motor.GetType().GetMethod("Remove").Invoke(motor, new[] { loop });
            float unused = (float)coordinator.GetType().GetMethod("AdvanceEntrance").Invoke(coordinator,
                new object[] { entry, new StraightTestPath(), 4.02f, 0.3f, true });
            Assert.That(type.GetField("EntryTransferStarted").GetValue(entry), Is.True);
            Assert.That(type.GetField("IsEntering").GetValue(entry), Is.False);
            Assert.That(unused, Is.EqualTo(0.02f).Within(0.0001f));
        });
    }

    [Test]
    public void VisualCorridorBlocksEvenWhenReceiveProgressIsClear()
    {
        WithTraffic((type, motor, coordinator, entry, loop) =>
        {
            var movement = typeof(SplineControll).Assembly.GetType("SplineEntranceMovement");
            object[] pose = { entry, 1.5f, null, null };
            movement.GetMethod("EvaluatePose").Invoke(null, pose);
            var bus = (BusController)type.GetField("Bus").GetValue(loop);
            bus.transform.position = (Vector3)pose[2];
            type.GetField("Progress").SetValue(loop, 0.5f); // receive point itself is empty
            var advance = coordinator.GetType().GetMethod("AdvanceEntrance");
            advance.Invoke(coordinator, new object[] { entry, new StraightTestPath(), 0.1f, 0.3f, true });
            Assert.That(type.GetField("EntryTransferReserved").GetValue(entry), Is.False,
                "Do not reserve a crossing that is already occupied: its loop bus must clear first.");
            Assert.That(type.GetField("EntryElapsed").GetValue(entry), Is.EqualTo(0f));
            bus.transform.position = new Vector3(20f, 0f, 20f);
            advance.Invoke(coordinator, new object[] { entry, new StraightTestPath(), 0.1f, 0.3f, true });
            Assert.That(type.GetField("EntryTransferStarted").GetValue(entry), Is.True);
            Assert.That((float)type.GetField("EntryElapsed").GetValue(entry), Is.GreaterThan(0f).And.LessThan(0.1f));
        });
    }

    [TestCase(0.016f, 0.15f)]
    [TestCase(0.1f, 0.6f)]
    [TestCase(0.35f, 1.2f)]
    public void ReservedTurnKeepsMovingAndStopsLoopVisualOutsideItsSweptBody(float dt, float speed)
    {
        WithTraffic((type, motor, coordinator, entry, loop) =>
        {
            var entryBus = (BusController)type.GetField("Bus").GetValue(entry);
            var loopBus = (BusController)type.GetField("Bus").GetValue(loop);
            entryBus.transform.position = Vector3.back;
            loopBus.transform.position = Vector3.back * 4f;
            type.GetField("EntryTransferReserved").SetValue(entry, true);
            type.GetField("EntryTransferStarted").SetValue(entry, true);
            var advance = coordinator.GetType().GetMethod("AdvanceEntrance");
            var clamp = motor.GetType().GetMethod("ClampAgainstVisualContact", BindingFlags.NonPublic | BindingFlags.Instance);
            var path = new OffsetContactTestPath();
            for (int i = 0; i < 300 && (bool)type.GetField("IsEntering").GetValue(entry); i++)
            {
                float before = (float)type.GetField("EntryElapsed").GetValue(entry);
                advance.Invoke(coordinator, new object[] { entry, path, dt, speed, true });
                Assert.That((float)type.GetField("EntryElapsed").GetValue(entry), Is.GreaterThan(before),
                    "The clicked bus must never start a second wait inside its turn.");
                float current = (float)type.GetField("Progress").GetValue(loop);
                float proposed = Mathf.Min(0.3f, current + speed * dt);
                float allowed = (float)clamp.Invoke(motor, new object[] { loop, proposed, path, dt });
                type.GetField("Progress").SetValue(loop, allowed);
                loopBus.transform.SetPositionAndRotation(path.EvaluatePosition(allowed), path.EvaluateRotation(allowed));
                Assert.That(BusTrafficMath.OverlapsBusFootprint(entryBus,
                    entryBus.transform.position, entryBus.transform.rotation, loopBus, 0f), Is.False,
                    "Progress-only protection must not let a rotating visual body overlap spline traffic.");
            }
            Assert.That(type.GetField("IsEntering").GetValue(entry), Is.False);
        });
    }

    [Test]
    public void TrafficCorridorSamplesIncludeCornerTravelDuringRotation()
    {
        WithTraffic((type, motor, coordinator, entry, loop) =>
        {
            var movement = typeof(SplineControll).Assembly.GetType("SplineEntranceMovement");
            var other = (BusController)type.GetField("Bus").GetValue(loop);
            movement.GetMethod("OverlapsTrafficCorridor").Invoke(null,
                new object[] { entry, loop, new Vector3(20f, 0f, 20f), other.transform.rotation });
            var positions = (Vector3[])type.GetField("EntryTrafficPositions").GetValue(entry);
            var rotations = (Quaternion[])type.GetField("EntryTrafficRotations").GetValue(entry);
            float radius = (float)type.GetField("TrafficRadius").GetValue(entry);
            Assert.That(positions.Length, Is.EqualTo(rotations.Length));
            Assert.That(positions.Length, Is.GreaterThan(32));
            for (int i = 1; i < positions.Length; i++)
            {
                Assert.That(Vector3.Distance(positions[i - 1], positions[i]), Is.LessThanOrEqualTo(0.04001f));
                Assert.That(Quaternion.Angle(rotations[i - 1], rotations[i]) * Mathf.Deg2Rad * radius,
                    Is.LessThanOrEqualTo(0.02001f));
            }
        });
    }

    private sealed class OffsetContactTestPath : IBusPath
    {
        public float Length => 20f;
        public Vector3 EvaluatePosition(float p) => new Vector3(0f, 0f, -4f + 20f * p);
        public Vector3 EvaluateTangent(float p) => Vector3.forward;
        public Vector3 EvaluateUpVector(float p) => Vector3.up;
        public Quaternion EvaluateRotation(float p) => Quaternion.identity;
        public float GetClosestProgress(Vector3 p) => Mathf.Clamp01((p.z + 4f) / 20f);
    }

    private static void WithTraffic(Action<Type, object, object, object, object> check)
    {
        var first = new GameObject("entry");
        var second = new GameObject("loop");
        Type motorType = typeof(SplineControll).Assembly.GetType("SplineBusMotor");
        object motor = Activator.CreateInstance(motorType);
        try
        {
            AddBody(first); AddBody(second);
            var bus = first.AddComponent<BusController>();
            var other = second.AddComponent<BusController>();
            Type runtimeType = typeof(SplineControll).Assembly.GetType("SplineBusRuntime");
            Type coordinatorType = typeof(SplineControll).Assembly.GetType("SplineBusEntryCoordinator");
            object coordinator = Activator.CreateInstance(coordinatorType, motor);
            coordinatorType.GetMethod("Configure").Invoke(coordinator, new object[]
                { 2.2f, 2.8f, 0.55f, 0.2f, (LayerMask)0, 4f, 0.3f, 0f, 0.6f, 0f });
            object entry = CreateEntranceRuntime(runtimeType, bus, 0f);
            runtimeType.GetField("EntryNormalizedExtent").SetValue(entry, 0.2f);
            object loop = Activator.CreateInstance(runtimeType, other, 0.5f);
            motorType.GetMethod("Add").Invoke(motor, new[] { entry });
            motorType.GetMethod("Add").Invoke(motor, new[] { loop });
            check(runtimeType, motor, coordinator, entry, loop);
        }
        finally
        {
            motorType.GetMethod("Clear").Invoke(motor, null);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [TestCase(0f, 1.99f, true)]
    [TestCase(0f, 2.03f, false)]
    [TestCase(90f, 1.49f, true)]
    [TestCase(90f, 1.53f, false)]
    public void FootprintContactAccountsForTurnedBody(float yaw, float separation, bool blocked)
    {
        GameObject source = new GameObject("entry");
        GameObject other = new GameObject("other");
        try
        {
            AddBody(source);
            AddBody(other);
            var first = source.AddComponent<BusController>();
            var second = other.AddComponent<BusController>();
            other.transform.position = Vector3.forward * separation;
            Assert.That(BusTrafficMath.OverlapsBusFootprint(first, Vector3.zero,
                Quaternion.Euler(0f, yaw, 0f), second, 0.015f), Is.EqualTo(blocked));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(other);
        }
    }

    [TestCase(0f, 0f)]
    [TestCase(90f, 18f)]
    public void ContactUsesOffsetAndTiltedRendererInsteadOfRootCenter(float yaw, float tilt)
    {
        GameObject first = new GameObject("first");
        GameObject second = new GameObject("second");
        try
        {
            AddBody(first);
            AddBody(second);
            var bus = first.AddComponent<BusController>();
            var other = second.AddComponent<BusController>();
            Transform body = first.transform.GetChild(0);
            body.localPosition = new Vector3(0f, 0.5f, 0.3f);
            body.localRotation = Quaternion.Euler(0f, 0f, tilt);
            Quaternion pose = Quaternion.Euler(0f, yaw, 0f);
            Vector3 axis = pose * Vector3.forward;
            second.transform.rotation = pose;
            second.transform.position = axis * 2.2f;
            Assert.That(BusTrafficMath.OverlapsBusFootprint(bus, Vector3.zero, pose, other, 0.015f),
                Is.True, "Offset body extends beyond the root-centered traffic rectangle.");
            second.transform.position = axis * 2.5f;
            Assert.That(BusTrafficMath.OverlapsBusFootprint(bus, Vector3.zero, pose, other, 0.015f),
                Is.False, "Use the actual geometry without a large early-stop margin.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    [Test]
    public void LoopMovementAlsoStopsBeforeTheOtherVisualBody()
    {
        GameObject first = new GameObject("loop");
        GameObject second = new GameObject("stopped");
        object motor = null;
        Type motorType = typeof(SplineControll).Assembly.GetType("SplineBusMotor");
        try
        {
            AddBody(first);
            AddBody(second);
            var bus = first.AddComponent<BusController>();
            var other = second.AddComponent<BusController>();
            second.transform.position = Vector3.forward * 2.1f;
            other.SetPassengerTransferInProgress(true);
            Type runtimeType = typeof(SplineControll).Assembly.GetType("SplineBusRuntime");
            motor = Activator.CreateInstance(motorType);
            object stopped = Activator.CreateInstance(runtimeType, other, 0.105f);
            object moving = Activator.CreateInstance(runtimeType, bus, 0f);
            motorType.GetMethod("Add").Invoke(motor, new[] { stopped });
            motorType.GetMethod("Add").Invoke(motor, new[] { moving });
            motorType.GetMethod("Tick").Invoke(motor, new object[]
            {
                new ForwardContactTestPath(), null, 0.2f, 0.3f, 0.2f, false, false, null,
            });
            Assert.That(first.transform.position.z, Is.EqualTo(0.085f).Within(0.003f),
                "The loop motor must sweep contact even when progress spacing is disabled.");
            Assert.That(BusTrafficMath.OverlapsBusFootprint(bus,
                first.transform.position, first.transform.rotation, other, 0f), Is.False);
        }
        finally
        {
            if (motor != null) motorType.GetMethod("Clear").Invoke(motor, null);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }

    private sealed class ForwardContactTestPath : IBusPath
    {
        public float Length => 20f;
        public Vector3 EvaluatePosition(float p) => Vector3.forward * (20f * p);
        public Vector3 EvaluateTangent(float p) => Vector3.forward;
        public Vector3 EvaluateUpVector(float p) => Vector3.up;
        public Quaternion EvaluateRotation(float p) => Quaternion.identity;
        public float GetClosestProgress(Vector3 p) => Mathf.Clamp01(p.z / 20f);
    }

    private static object CreateEntranceRuntime(Type type, BusController bus, float elapsed)
    {
        object runtime = Activator.CreateInstance(type, bus, 0f);
        void Set(string name, object value) => type.GetField(name).SetValue(runtime, value);
        Set("IsEntering", true);
        Set("EntryElapsed", elapsed);
        Set("EntryStraightDuration", 1f);
        Set("EntryTurnDuration", 1f);
        Set("EntryLeftDuration", 1f);
        Set("EntryAlignDuration", 1f);
        Set("EntryDuration", 4f);
        Set("EntryStartPosition", Vector3.back);
        Set("EntryCurveStart", Vector3.zero);
        Set("EntryTurnPosition", Vector3.forward);
        Set("EntryCurveEnd", new Vector3(-1f, 0f, 1f));
        Set("EntryLeftPosition", new Vector3(-2f, 0f, 1f));
        Set("EntryTargetPosition", new Vector3(-2f, 0f, 2f));
        Set("EntryStartRotation", Quaternion.identity);
        Set("EntryLeftRotation", Quaternion.LookRotation(Vector3.left));
        Set("EntryTargetRotation", Quaternion.identity);
        Set("EntryAlignControl1", new Vector3(-2.55f, 0f, 1f));
        Set("EntryAlignControl2", new Vector3(-2f, 0f, 1.45f));
        return runtime;
    }

    private static void AddBody(GameObject bus)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.transform.SetParent(bus.transform, false);
        body.transform.localScale = new Vector3(1f, 1f, 2f);
    }

    private sealed class StraightTestPath : IBusPath
    {
        public float Length => 10f;
        public Vector3 EvaluatePosition(float progress) => new Vector3(-10f * progress, 0f, 0f);
        public Vector3 EvaluateTangent(float progress) => Vector3.left;
        public Vector3 EvaluateUpVector(float progress) => Vector3.up;
        public Quaternion EvaluateRotation(float progress) => Quaternion.LookRotation(Vector3.left);
        public float GetClosestProgress(Vector3 worldPosition) => Mathf.Clamp01(-worldPosition.x / 10f);
    }
}
