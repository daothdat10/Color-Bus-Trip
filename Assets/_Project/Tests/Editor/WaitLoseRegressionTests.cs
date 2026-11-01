using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class WaitLoseRegressionTests
{
    private readonly List<GameObject> objects=new List<GameObject>();
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private T Create<T>(string name) where T:Component
    {var go=new GameObject(name);objects.Add(go);return go.AddComponent<T>();}
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    private static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,Private).Invoke(target,args);
    [TearDown] public void Cleanup(){DOTween.KillAll();foreach(var go in objects)if(go!=null)UnityEngine.Object.DestroyImmediate(go);objects.Clear();}

    [TestCase(5,true,false,true)]
    [TestCase(4,true,false,false)]
    [TestCase(2,false,false,true)]
    [TestCase(2,false,true,false)]
    [TestCase(0,false,false,false)]
    public void LossMatchesFullBoardOrExhaustedDockWithNoFurtherExchange(int boardCount,bool dock,bool exchange,bool lose)
    {Assert.That(TrayLossRule.ShouldLose(5,boardCount,false,false,false,false,exchange,dock),Is.EqualTo(lose));}

    [Test]
    public void FullTrayMustNotLoseWhileMatchingWaitIsFarFromEveryBus()
    {
        var level=Create<LevelControl>("level");var spawn=Create<SpawnController>("spawn");
        Set(level,"spawnController",spawn);
        var wait=Create<WaitController>("far-wait");wait.transform.position=Vector3.one*100f;
        Set(wait,"colorId",2);
        var passenger=Passenger(wait,0);passenger.SetStickManColor(null,2);
        Set(spawn,"listWaits",new List<WaitController>{wait});
        var buses=new List<BusController>();
        for(int i=0;i<5;i++){var bus=Create<BusController>("bus");Set(bus,"colorBus",2);Set(bus,"slotEmpty",1);buses.Add(bus);}
        Assert.That(wait.CanInteractWithBusByDistance(buses[0]),Is.False);
        Assert.That((bool)Call(level,"HasPassengerTransferOpportunity",buses),Is.True,
            "GameUtilities checks potential exchanges across the board, not just current proximity.");
        wait.gameObject.SetActive(false);
        Assert.That((bool)Call(level,"HasPassengerTransferOpportunity",buses),Is.False,
            "An inactive pooled stop must not prevent the loss check forever.");
    }

    [Test]
    public void PassengerExchangeInProgressOrCompletedBusDefersLoss()
    {
        Assert.That(TrayLossRule.ShouldLose(5,5,false,false,true,false,false,false),Is.False);
        Assert.That(TrayLossRule.ShouldLose(5,5,false,false,false,true,false,false),Is.False);
        Assert.That(TrayLossRule.ShouldLose(5,5,true,false,false,false,false,false),Is.False);
    }

    [TestCase(5,true)]
    [TestCase(2,false)]
    public void ActualLoseCheckWaitsForEntryThenFiresOnceWhenNoMoveRemains(int boardCount,bool moving)
    {
        var level=Create<LevelControl>("level");var spawn=Create<SpawnController>("spawn");
        Set(level,"spawnController",spawn);Set(spawn,"panelSlot",Create<PanelSlot>("slots"));
        for(int i=0;i<boardCount;i++)
        {
            var bus=Create<BusController>("board-bus");bus.SetOnSplineConveyor(true);
            bus.SetSplineMoving(moving);spawn.listBuses.Add(bus);
        }
        int failures=0;level.isLose=()=>failures++;
        if(moving){level.CheckLose();Assert.That(failures,Is.EqualTo(0),"Entrance movement must resolve before declaring loss.");}
        foreach(var bus in spawn.listBuses)bus.SetSplineMoving(false);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
            "Level failed: no board entry or passenger exchange can progress the remaining buses.");
        level.CheckLose();level.CheckLose();
        Assert.That(failures,Is.EqualTo(1));
    }

    private WaitController Wait()
    {
        var wait=Create<WaitController>("wait");
        Set(wait,"waitingStickManPositions",new List<Vector3>{Vector3.zero,Vector3.left,Vector3.right,new Vector3(0,0,-1)});
        Set(wait,"waitingGroundOffsetY",0f);Set(wait,"stickManCameraOffset",0f);
        return wait;
    }
    private StickManControl Passenger(WaitController wait,int slot)
    {
        var passenger=Create<StickManControl>("passenger");passenger.transform.SetParent(wait.transform,false);
        passenger.transform.localPosition=(Vector3)Call(wait,"GetWaitingPositionInFrontOfWait",slot);
        wait.listStickMans.Add(passenger);
        Call(wait,"TakeSlot",passenger);
        return passenger;
    }

    [Test]
    public void RemainingPassengersMoveSmoothlyIntoTheVacatedCentreWithoutChangingCapacity()
    {
        var wait=Wait();var first=Passenger(wait,0);var left=Passenger(wait,1);var right=Passenger(wait,2);
        wait.listStickMans.Remove(first);
        var slots=(IDictionary)typeof(WaitController).GetField("slotByStickMan",Private).GetValue(wait);slots.Remove(first);
        Vector3 beforeLeft=left.transform.localPosition,beforeRight=right.transform.localPosition;
        Call(wait,"ReflowWaitingPassengers");
        Assert.That(left.transform.localPosition,Is.EqualTo(beforeLeft),"Reflow must animate, not teleport.");
        Assert.That(right.transform.localPosition,Is.EqualTo(beforeRight));
        DOTween.CompleteAll();
        Assert.That(slots.Count,Is.EqualTo(2));
        Assert.That(wait.Capacity,Is.EqualTo(4));Assert.That(wait.FreeSlots,Is.EqualTo(2));
        Assert.That(left.transform.localPosition==Vector3.zero || right.transform.localPosition==Vector3.zero,Is.True);
        Assert.That(left.transform.localPosition,Is.Not.EqualTo(right.transform.localPosition));
        Assert.That(left.transform.parent,Is.EqualTo(wait.transform));
    }

    [Test]
    public void PendingBoardersKeepTheirStandingSlotUntilTheirJumpCompletes()
    {
        var wait=Wait();var pending=Passenger(wait,0);var remaining=Passenger(wait,1);
        wait.listStickMans.Remove(pending); // still standing here while its sequential jump has not begun
        var slots=(IDictionary)typeof(WaitController).GetField("slotByStickMan",Private).GetValue(wait);
        Call(wait,"ReflowWaitingPassengers");DOTween.CompleteAll();
        Assert.That(slots[pending],Is.EqualTo(0));Assert.That(slots[remaining],Is.Not.EqualTo(0));
        slots.Remove(pending);Call(wait,"ReflowWaitingPassengers");DOTween.CompleteAll();
        Assert.That(slots[remaining],Is.EqualTo(0));
    }

    [Test]
    public void BoardingOnePassengerReflowsTheRemainingCrowdAndReleasesTheBus()
    {
        var wait=Wait();var first=Passenger(wait,0);var second=Passenger(wait,1);var third=Passenger(wait,2);
        var bus=Create<BusController>("boarding-bus");var seats=new GameObject("seats");seats.transform.SetParent(bus.transform,false);
        Set(bus,"trfParentStickMan",seats.transform);Set(bus,"trfListPoint",new[]{Vector3.zero});Set(bus,"slotBus",1);Set(bus,"slotEmpty",1);
        first.SetStickManColor(null,2);Set(bus,"colorBus",2);
        Call(wait,"JumpStickMenIntoBus",bus,new List<StickManControl>{first});
        Assert.That(bus.IsReceivingPassengers,Is.True);Assert.That(wait.IsTransferringPassengers,Is.True);
        // Run the real paused sequence through its boarding-complete callback.
        DOTween.CompleteAll(true);DOTween.CompleteAll(true);
        Assert.That(bus.IsReceivingPassengers,Is.False);Assert.That(wait.IsTransferringPassengers,Is.False);
        Assert.That(wait.listStickMans.Count,Is.EqualTo(2));Assert.That(bus.ListStickMans.Count,Is.EqualTo(1));
        Assert.That(first.transform.parent,Is.EqualTo(seats.transform));
        Assert.That(second.transform.localPosition==Vector3.zero || third.transform.localPosition==Vector3.zero,Is.True);
    }
    [Test]
    public void SequentialBoardingKeepsBusPausedUntilLastPassengerAndReservesPendingSlots()
    {
        var wait=Wait();var first=Passenger(wait,0);var second=Passenger(wait,1);var remaining=Passenger(wait,2);
        var bus=Create<BusController>("bus");var seats=new GameObject("seats");seats.transform.SetParent(bus.transform,false);
        Set(bus,"trfParentStickMan",seats.transform);Set(bus,"trfListPoint",new[]{Vector3.zero,Vector3.right});
        Set(bus,"slotBus",2);Set(bus,"slotEmpty",2);Set(bus,"colorBus",2);
        first.SetStickManColor(null,2);second.SetStickManColor(null,2);
        Call(wait,"JumpStickMenIntoBus",bus,new List<StickManControl>{first,second});
        var active=DOTween.PlayingTweens();Assert.That(active,Is.Not.Null);Assert.That(active.Count,Is.EqualTo(1));
        var sequence=(Sequence)active[0];
        sequence.SetUpdate(UpdateType.Manual);
        DOTween.ManualUpdate(.025f,.025f);
        var slots=(IDictionary)typeof(WaitController).GetField("slotByStickMan",Private).GetValue(wait);
        Assert.That(bus.IsReceivingPassengers,Is.True);Assert.That(bus.isDone,Is.False);
        Assert.That(slots.Contains(first),Is.False);Assert.That(slots.Contains(second),Is.True);
        Assert.That(slots[remaining],Is.Not.EqualTo(slots[second]));
        DOTween.ManualUpdate(.126f,.126f);
        Assert.That(slots.Contains(second),Is.False,"A boarder releases its standing slot when walking starts.");
        Assert.That(bus.IsReceivingPassengers,Is.True);Assert.That(bus.isDone,Is.False);
        sequence.Complete(true);DOTween.CompleteAll(true);
        Assert.That(bus.IsReceivingPassengers,Is.False);Assert.That(bus.isDone,Is.True);
        Assert.That(slots.Contains(second),Is.False);Assert.That(slots.Count,Is.EqualTo(1));
    }

}
