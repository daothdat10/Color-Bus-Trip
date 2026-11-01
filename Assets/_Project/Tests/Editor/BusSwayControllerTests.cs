using DG.Tweening;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class BusSwayControllerTests
{
    private const string BusPrefabPath = "Assets/_Project/Prefabs/Bus.prefab";
    private const string ConfigAssetPath = "Assets/_Project/Data/Gameplay/BusSwayConfig.asset";

    [Test]
    public void ConfigAssetExistsWithSafeValues()
    {
        BusSwayConfig config =
            AssetDatabase.LoadAssetAtPath<BusSwayConfig>(ConfigAssetPath);

        Assert.That(config, Is.Not.Null);
        Assert.That(config.maxTiltDegrees, Is.InRange(0f, 30f));
        Assert.That(config.horizontalSwayMultiplier, Is.GreaterThan(0f));
        Assert.That(config.maxImpulseAngle, Is.GreaterThan(0f));
        Assert.That(config.turnSwayVelocityPerDegree, Is.GreaterThan(0f));
        Assert.That(config.maxAcceleration, Is.GreaterThanOrEqualTo(0f));
        Assert.That(config.spring, Is.GreaterThanOrEqualTo(0f));
        Assert.That(config.damping, Is.GreaterThanOrEqualTo(0f));        Assert.That(config.boardPunchDuration, Is.GreaterThanOrEqualTo(0f));

    }

    [Test]
    public void BusPrefabContainsSwayControllerAndResolvesBodyTray()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BusPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        GameObject instance = Object.Instantiate(prefab);

        try
        {
            BusSwayController controller =
                instance.GetComponent<BusSwayController>();

            InvokePrivate(controller, "Awake");

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Tray, Is.Not.Null);
            Assert.That(controller.Tray.name, Is.EqualTo("Car 1"));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void NonFiniteImpulseDoesNotCorruptSwayState()
    {
        GameObject root = new GameObject("BusSwayTestRoot");
        GameObject trayObject = new GameObject("Car 1");
        trayObject.transform.SetParent(root.transform);

        try
        {
            BusSwayController controller = root.AddComponent<BusSwayController>();
            InvokePrivate(controller, "Awake");
            controller.SimulateSway(
                new Vector3(float.NaN, float.PositiveInfinity, 0f)
            );

            Vector3 angle = controller.CurrentSwayAngle;

            Assert.That(float.IsNaN(angle.x), Is.False);
            Assert.That(float.IsNaN(angle.y), Is.False);
            Assert.That(float.IsNaN(angle.z), Is.False);
            Assert.That(float.IsInfinity(angle.x), Is.False);
            Assert.That(float.IsInfinity(angle.y), Is.False);
            Assert.That(float.IsInfinity(angle.z), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void MotionSamplingProducesFiniteBoundedSway()
    {
        GameObject root = new GameObject("BusSwayMotionTestRoot");
        GameObject trayObject = new GameObject("Car 1");
        trayObject.transform.SetParent(root.transform);

        try
        {
            BusSwayController controller = root.AddComponent<BusSwayController>();
            InvokePrivate(controller, "Awake");

            InvokePrivate(controller, "LateUpdate");

            root.transform.position = new Vector3(0f, 0f, 1f);
            InvokePrivate(controller, "LateUpdate");

            root.transform.position = new Vector3(1f, 0f, 2f);
            InvokePrivate(controller, "LateUpdate");

            Vector3 angle = controller.CurrentSwayAngle;

            Assert.That(float.IsNaN(angle.x), Is.False);
            Assert.That(float.IsNaN(angle.y), Is.False);
            Assert.That(float.IsNaN(angle.z), Is.False);
            Assert.That(Mathf.Abs(angle.x), Is.LessThanOrEqualTo(6f));
            Assert.That(Mathf.Abs(angle.z), Is.LessThanOrEqualTo(6f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void LeftAndRightAccelerationProduceOppositeBodyRoll()
    {
        GameObject root = new GameObject("BusSwayDirectionTestRoot");
        GameObject trayObject = new GameObject("Car 1");
        trayObject.transform.SetParent(root.transform);

        try
        {
            BusSwayController controller = root.AddComponent<BusSwayController>();
            InvokePrivate(controller, "Awake");
            MethodInfo targetAngle = typeof(BusSwayController).GetMethod(
                "CalculateTargetAngle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(targetAngle, Is.Not.Null);

            Vector3 left = (Vector3)targetAngle.Invoke(
                controller, new object[] { new Vector3(-3f, 0f, 0f) });
            Vector3 right = (Vector3)targetAngle.Invoke(
                controller, new object[] { new Vector3(3f, 0f, 0f) });

            Assert.That(left.z, Is.LessThan(0f));
            Assert.That(right.z, Is.GreaterThan(0f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void TurnTweenLeansOppositeThenReturnsToCenter()
    {
        GameObject root = new GameObject("BusTurnImpulseTestRoot");
        GameObject trayObject = new GameObject("Car 1");
        trayObject.transform.SetParent(root.transform);

        try
        {
            BusSwayController controller = root.AddComponent<BusSwayController>();
            InvokePrivate(controller, "Awake");

            controller.PlayTurnSway(90f);
            Sequence sequence = GetTurnSequence(controller);
            controller.SetTurnSwaySpeed(0f);
            Assert.That(sequence.IsPlaying(), Is.False);
            controller.SetTurnSwaySpeed(0.5f);
            Assert.That(sequence.IsPlaying(), Is.True);
            Assert.That(sequence.timeScale, Is.EqualTo(0.5f));
            sequence.Goto(0.18f, false);
            Assert.That(controller.CurrentSwayAngle.z, Is.GreaterThan(1f));

            sequence.Goto(0.4f, false);
            Assert.That(controller.CurrentSwayAngle.z, Is.LessThan(-0.5f));

            sequence.Goto(0.68f, false);
            Assert.That(Mathf.Abs(controller.CurrentSwayAngle.z),
                Is.LessThan(0.05f));

            controller.PlayTurnSway(-90f);
            GetTurnSequence(controller).Goto(0.18f, false);
            Assert.That(controller.CurrentSwayAngle.z, Is.LessThan(-1f));

            controller.ResetSway();
            Assert.That(controller.CurrentSwayAngle, Is.EqualTo(Vector3.zero));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void EntranceTriggersOneTurnPulseAtEachDirectionChange()
    {
        GameObject root = new GameObject("BusEntrancePulseTestRoot");
        GameObject trayObject = new GameObject("Car 1");
        trayObject.transform.SetParent(root.transform);

        try
        {
            BusController bus = root.AddComponent<BusController>();
            BusSwayController sway = root.AddComponent<BusSwayController>();
            InvokePrivate(sway, "Awake");

            System.Type runtimeType = typeof(SplineControll).Assembly.GetType("SplineBusRuntime");
            System.Type entranceType = typeof(SplineControll).Assembly.GetType("SplineEntranceMovement");
            Assert.That(runtimeType, Is.Not.Null);
            Assert.That(entranceType, Is.Not.Null);

            object runtime = System.Activator.CreateInstance(runtimeType, bus, 0f);
            SetRuntimeField(runtimeType, runtime, "IsEntering", true);
            SetRuntimeField(runtimeType, runtime, "EntryStraightDuration", 0.1f);
            SetRuntimeField(runtimeType, runtime, "EntryTurnDuration", 0.1f);
            SetRuntimeField(runtimeType, runtime, "EntryLeftDuration", 0.1f);
            SetRuntimeField(runtimeType, runtime, "EntryAlignDuration", 0.1f);
            SetRuntimeField(runtimeType, runtime, "EntryDuration", 0.4f);
            SetRuntimeField(runtimeType, runtime, "EntryCurveStart", Vector3.forward);
            SetRuntimeField(runtimeType, runtime, "EntryTurnPosition", 2f * Vector3.forward);
            SetRuntimeField(runtimeType, runtime, "EntryCurveEnd", new Vector3(-1f, 0f, 2f));
            SetRuntimeField(runtimeType, runtime, "EntryLeftPosition", new Vector3(-2f, 0f, 2f));
            SetRuntimeField(runtimeType, runtime, "EntryTargetPosition", new Vector3(-2f, 0f, 3f));
            SetRuntimeField(runtimeType, runtime, "EntryStartRotation", Quaternion.identity);
            SetRuntimeField(runtimeType, runtime, "EntryLeftRotation", Quaternion.LookRotation(Vector3.left));
            SetRuntimeField(runtimeType, runtime, "EntryTargetRotation", Quaternion.identity);

            MethodInfo advance = entranceType.GetMethod("Advance", BindingFlags.Public | BindingFlags.Static);
            Assert.That(advance, Is.Not.Null);
            advance.Invoke(null, new object[] { runtime, 0.11f });
            Assert.That((bool)runtimeType.GetField("EntryCurveSwayTriggered").GetValue(runtime), Is.True);
            Assert.That((bool)runtimeType.GetField("EntryAlignSwayTriggered").GetValue(runtime), Is.False);

            advance.Invoke(null, new object[] { runtime, 0.2f });
            Assert.That((bool)runtimeType.GetField("EntryAlignSwayTriggered").GetValue(runtime), Is.True);
            Tween tween = runtimeType.GetField("EntryMotionTween").GetValue(runtime) as Tween;
            Assert.That(tween, Is.Not.Null);
            Assert.That(tween.IsPlaying(), Is.False);
            tween.Kill();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Sequence GetTurnSequence(BusSwayController controller)
    {
        FieldInfo field = typeof(BusSwayController).GetField(
            "turnSwaySequence", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        Sequence sequence = field.GetValue(controller) as Sequence;
        Assert.That(sequence, Is.Not.Null);
        return sequence;
    }

    private static void SetRuntimeField(System.Type runtimeType, object runtime,
        string fieldName, object value)
    {
        FieldInfo field = runtimeType.GetField(fieldName);
        Assert.That(field, Is.Not.Null);
        field.SetValue(runtime, value);
    }

    [Test]
    public void ResetSwayReturnsToZero()
    {
        GameObject root = new GameObject("BusSwayResetTestRoot");
        GameObject trayObject = new GameObject("Car 1");
        trayObject.transform.SetParent(root.transform);

        try
        {
            BusSwayController controller = root.AddComponent<BusSwayController>();
            InvokePrivate(controller, "Awake");
            controller.SimulateSway(new Vector3(2f, 0f, 3f));
            controller.ResetSway();

            Assert.That(controller.CurrentSwayAngle, Is.EqualTo(Vector3.zero));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void PassengerBoardPunchCanStartAndResetSafely()
    {
        GameObject root = new GameObject("BusSwayBoardPunchTestRoot");
        GameObject trayObject = new GameObject("Car 1");
        trayObject.transform.SetParent(root.transform);

        try
        {
            BusSwayController controller = root.AddComponent<BusSwayController>();
            InvokePrivate(controller, "Awake");

            controller.PlayPassengerBoardedAnimation();

            FieldInfo sequenceField = typeof(BusSwayController).GetField(
                "passengerBoardSequence",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            Assert.That(sequenceField, Is.Not.Null);
            Assert.That(sequenceField.GetValue(controller), Is.Not.Null);

            controller.ResetSway();

            Assert.That(sequenceField.GetValue(controller), Is.Null);
            Assert.That(controller.CurrentSwayAngle, Is.EqualTo(Vector3.zero));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void InvokePrivate(
        BusSwayController controller,
        string methodName
    )
    {
        Assert.That(controller, Is.Not.Null);

        MethodInfo method = typeof(BusSwayController).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        Assert.That(method, Is.Not.Null);
        method.Invoke(controller, null);
    }
}
