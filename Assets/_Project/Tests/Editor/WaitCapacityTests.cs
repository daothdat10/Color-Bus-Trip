using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WaitCapacityTests
{
    private GameObject waitObject;
    private readonly System.Collections.Generic.List<GameObject> stickmen =
        new System.Collections.Generic.List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        if (waitObject != null) Object.DestroyImmediate(waitObject);
        for (int i = 0; i < stickmen.Count; i++)
            if (stickmen[i] != null) Object.DestroyImmediate(stickmen[i]);
        stickmen.Clear();
    }

    [Test]
    public void FreeSlotsShrinkAsPassengersFillTheFixedCapacity()
    {
        WaitController wait = CreateWait();
        Assert.That(wait.Capacity, Is.EqualTo(16));
        Assert.That(wait.FreeSlots, Is.EqualTo(16));

        for (int i = 0; i < 14; i++) wait.listStickMans.Add(CreateStickMan());
        Assert.That(wait.FreeSlots, Is.EqualTo(2));

        for (int i = 0; i < 5; i++) wait.listStickMans.Add(CreateStickMan());
        Assert.That(wait.FreeSlots, Is.EqualTo(0), "Free slots must never go negative.");
    }

    [Test]
    public void FreedSlotIsReusedByTheNextPassengerAndOthersKeepTheirs()
    {
        WaitController wait = CreateWait();
        MethodInfo takeSlot = typeof(WaitController).GetMethod(
            "TakeSlot", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo slots = typeof(WaitController).GetField(
            "slotByStickMan", BindingFlags.NonPublic | BindingFlags.Instance);

        StickManControl first = CreateStickMan();
        StickManControl second = CreateStickMan();
        StickManControl third = CreateStickMan();
        Assert.That(takeSlot.Invoke(wait, new object[] { first }), Is.EqualTo(0));
        Assert.That(takeSlot.Invoke(wait, new object[] { second }), Is.EqualTo(1));
        Assert.That(takeSlot.Invoke(wait, new object[] { third }), Is.EqualTo(2));

        ((IDictionary)slots.GetValue(wait)).Remove(second);

        StickManControl fourth = CreateStickMan();
        Assert.That(takeSlot.Invoke(wait, new object[] { fourth }), Is.EqualTo(1));
        Assert.That(takeSlot.Invoke(wait, new object[] { third }), Is.EqualTo(2),
            "A passenger that stays must keep its slot.");
    }

    [Test]
    public void WaitThatLosesAllPassengersForgetsItsColorAndAcceptsAnyColor()
    {
        WaitController wait = CreateWait();
        FieldInfo color = typeof(WaitController).GetField(
            "colorId", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo complete = typeof(WaitController).GetMethod(
            "CompleteIncomingStickMen", BindingFlags.NonPublic | BindingFlags.Instance);

        color.SetValue(wait, 3);
        wait.listStickMans.Add(CreateStickMan());
        complete.Invoke(wait, null);
        Assert.That(wait.GetColorId, Is.EqualTo(3), "A Wait that still has passengers keeps its colour.");

        wait.listStickMans.Clear();
        complete.Invoke(wait, null);
        Assert.That(wait.GetColorId, Is.EqualTo(-1));
        Assert.That(BusPassengerTransferRules.CanUnloadToWait(1, wait.GetColorId, 3), Is.True,
            "An empty Wait must accept the colour it just lost.");
    }

    private WaitController CreateWait()
    {
        waitObject = new GameObject("wait");
        return waitObject.AddComponent<WaitController>();
    }

    private StickManControl CreateStickMan()
    {
        GameObject go = new GameObject("stickman");
        stickmen.Add(go);
        return go.AddComponent<StickManControl>();
    }
}
