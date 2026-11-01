using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class BoosterTests
{
    private GameObject root;
    private readonly string[] keys = { AddBussBooster.QuantityPrefsKey, BusFlyBooster.QuantityPrefsKey, BusHammerBooster.QuantityPrefsKey };
    private readonly System.Collections.Generic.Dictionary<string, int> savedPrefs = new System.Collections.Generic.Dictionary<string, int>();
    [SetUp] public void SetUp()
    {
        savedPrefs.Clear();
        foreach (string key in keys)
        {
            if (PlayerPrefs.HasKey(key)) savedPrefs[key] = PlayerPrefs.GetInt(key);
            PlayerPrefs.DeleteKey(key);
        }
        root = new GameObject("BoosterTests");
    }
    [TearDown] public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        foreach (string key in keys)
        {
            if (savedPrefs.TryGetValue(key, out int value)) PlayerPrefs.SetInt(key, value);
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
    }

    [TestCase(typeof(AddBussBooster))]
    [TestCase(typeof(BusFlyBooster))]
    [TestCase(typeof(BusHammerBooster))]
    public void Inventory_OnlySuccessfulActionConsumesStock(Type type)
    {
        var booster = root.AddComponent(type);
        var set = type.GetMethod("SetQuantity");
        var use = type.GetMethod("TryUse");
        int calls = 0;
        Func<bool> success = () => { calls++; return true; };
        Assert.That(use.Invoke(booster, new object[] { success }), Is.False);
        Assert.That(calls, Is.Zero);
        set.Invoke(booster, new object[] { 2 });
        Assert.That(use.Invoke(booster, new object[] { new Func<bool>(() => false) }), Is.False);
        Assert.That(type.GetProperty("Quantity").GetValue(booster), Is.EqualTo(2));
        Assert.That(use.Invoke(booster, new object[] { success }), Is.True);
        Assert.That(type.GetProperty("Quantity").GetValue(booster), Is.EqualTo(1));
    }

    [TestCase(typeof(AddBussBooster))]
    [TestCase(typeof(BusFlyBooster))]
    [TestCase(typeof(BusHammerBooster))]
    public void Inventory_ZeroShowsPlus_PositiveShowsNumber(Type type)
    {
        var booster = root.AddComponent(type);
        var plus = new GameObject("Plus"); plus.transform.SetParent(root.transform);
        var number = new GameObject("Number"); number.transform.SetParent(root.transform);
        type.GetField("goPlus", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(booster, plus);
        type.GetField("goNumber", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(booster, number);
        type.GetMethod("SetQuantity").Invoke(booster, new object[] { -5 });
        Assert.That(plus.activeSelf, Is.True); Assert.That(number.activeSelf, Is.False);
        type.GetMethod("SetQuantity").Invoke(booster, new object[] { 3 });
        Assert.That(plus.activeSelf, Is.False); Assert.That(number.activeSelf, Is.True);
    }

    [Test] public void CapacityUpgrade_IncreasesAvailableAndMaximum_ResetsOnInit()
    {
        var slots = root.AddComponent<PanelSlot>(); slots.Init();
        slots.ReSlot();
        slots.IncreaseCapacity();
        Assert.That(slots.CurrentCapacity, Is.EqualTo(6));
        Assert.That(slots.GetSlot, Is.EqualTo(5));
        slots.UpSlot(); Assert.That(slots.GetSlot, Is.EqualTo(6));
        slots.Init(); Assert.That(slots.CurrentCapacity, Is.EqualTo(5));
        Assert.That(slots.GetSlot, Is.EqualTo(5));
    }

    [Test] public void InvalidTargets_DoNotConsumeStock()
    {
        var fly = root.AddComponent<BusFlyBooster>(); fly.SetQuantity(1);
        var hammer = root.AddComponent<BusHammerBooster>(); hammer.SetQuantity(1);
        Assert.That(fly.Use(null), Is.False); Assert.That(hammer.Use(null), Is.False);
        Assert.That(fly.Quantity, Is.EqualTo(1)); Assert.That(hammer.Quantity, Is.EqualTo(1));
    }

    [Test] public void Selection_ClickAgainCancels_AndSwitchingKeepsOnlyOneActive()
    {
        var fly = root.AddComponent<BusFlyBooster>();
        var hammer = root.AddComponent<BusHammerBooster>();
        fly.SetQuantity(1); hammer.SetQuantity(1);
        var manager = root.AddComponent<ManagerBoosters>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ManagerBoosters).GetField("busFlyBooster", flags).SetValue(manager, fly);
        typeof(ManagerBoosters).GetField("busHammerBoosterz", flags).SetValue(manager, hammer);
        manager.SelectFly(); Assert.That(manager.IsSelecting, Is.True);
        manager.SelectFly(); Assert.That(manager.IsSelecting, Is.False);
        manager.SelectFly(); manager.SelectHammer();
        manager.SelectHammer(); Assert.That(manager.IsSelecting, Is.False);
        Assert.That(fly.Quantity, Is.EqualTo(1));
        Assert.That(hammer.Quantity, Is.EqualTo(1));
        fly.SetQuantity(0); manager.SelectFly(); Assert.That(manager.IsSelecting, Is.False);
    }
}
