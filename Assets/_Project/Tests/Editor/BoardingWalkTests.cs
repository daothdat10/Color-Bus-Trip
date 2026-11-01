using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

public sealed class BoardingWalkTests
{
    private readonly List<GameObject> objects=new List<GameObject>();
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private T Create<T>(string name)where T:Component{var go=new GameObject(name);objects.Add(go);return go.AddComponent<T>();}
    private static void Set(object obj,string field,object value)=>obj.GetType().GetField(field,Private).SetValue(obj,value);
    private static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,Private).Invoke(obj,args);
    [TearDown] public void Cleanup(){DOTween.KillAll();foreach(var obj in objects)if(obj!=null)Object.DestroyImmediate(obj);objects.Clear();}
    private BusController Bus(int seats)
    {
        var bus=Create<BusController>("bus");var parent=new GameObject("seats");parent.transform.SetParent(bus.transform,false);
        parent.transform.localPosition=Vector3.up;
        var positions=new Vector3[seats];for(int i=0;i<seats;i++)positions[i]=Vector3.forward*(i*.1f);
        Set(bus,"trfParentStickMan",parent.transform);Set(bus,"trfListPoint",positions);
        Set(bus,"slotBus",seats);Set(bus,"slotEmpty",seats);Set(bus,"colorBus",1);return bus;
    }
    private StickManControl Passenger(WaitController wait,Vector3 position)
    {
        var passenger=Create<StickManControl>("passenger");passenger.transform.SetParent(wait.transform,false);
        passenger.transform.position=position;passenger.SetStickManColor(null,1);wait.listStickMans.Add(passenger);
        Call(wait,"TakeSlot",passenger);return passenger;
    }
    private static Sequence Boarding(WaitController wait)
    {
        var tweens=DOTween.TweensByTarget(wait);Assert.That(tweens,Is.Not.Null);Assert.That(tweens.Count,Is.EqualTo(1));
        var sequence=(Sequence)tweens[0];sequence.SetUpdate(UpdateType.Manual);return sequence;
    }
    [Test]
    public void PassengerWalksOnGroundThenJumpsOnlyWhenNearBus()
    {
        var wait=Create<WaitController>("wait");var bus=Bus(1);
        var passenger=Passenger(wait,new Vector3(3f,0f,0f));
        Call(wait,"JumpStickMenIntoBus",bus,new List<StickManControl>{passenger});
        var sequence=Boarding(wait);
        Assert.That(passenger.transform.parent,Is.EqualTo(wait.transform),"Do not attach a passenger to the bus before it walks there.");
        DOTween.ManualUpdate(.3f,.3f);
        Assert.That(passenger.transform.position.x,Is.EqualTo(2.1f).Within(.005f));
        Assert.That(passenger.transform.position.y,Is.EqualTo(0f).Within(.0001f));
        Assert.That(passenger.transform.parent,Is.EqualTo(wait.transform));
        Assert.That(bus.IsReceivingPassengers,Is.True);
        DOTween.ManualUpdate(.64f,.64f);
        Assert.That(passenger.transform.parent,Is.EqualTo(bus.StickManParent));
        Assert.That(passenger.transform.position.y,Is.GreaterThan(0f));
        Assert.That(Mathf.Abs(passenger.transform.position.x),Is.LessThanOrEqualTo(.251f));
        Assert.That(bus.IsReceivingPassengers,Is.True);
        sequence.Complete(true);
        Assert.That(passenger.transform.localPosition,Is.EqualTo(Vector3.zero));
        Assert.That(bus.IsReceivingPassengers,Is.False);Assert.That(bus.isDone,Is.True);
    }
    [Test]
    public void PassengerAlreadyBesideBusJumpsWithoutWalkingBackwards()
    {
        var wait=Create<WaitController>("wait");var bus=Bus(1);var passenger=Passenger(wait,new Vector3(.1f,0f,0f));
        Call(wait,"JumpStickMenIntoBus",bus,new List<StickManControl>{passenger});Boarding(wait);
        DOTween.ManualUpdate(.02f,.02f);
        Assert.That(passenger.transform.parent,Is.EqualTo(bus.StickManParent));
        Assert.That(passenger.transform.position.x,Is.LessThanOrEqualTo(.1f));
        Assert.That(passenger.transform.position.y,Is.GreaterThan(0f));
    }
    [TestCase(0f,.88f)]
    [TestCase(90f,1.08f)]
    public void ApproachPointRespectsOffsetRotatedBodyAndOriginalGroundHeight(float yaw,float edge)
    {
        var wait=Create<WaitController>("wait");var bus=Bus(1);
        var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.transform.SetParent(bus.transform,false);
        body.transform.localPosition=new Vector3(.3f,0f,0f);body.transform.localScale=new Vector3(1f,1f,2f);
        bus.transform.rotation=Quaternion.Euler(0f,yaw,0f);
        var point=(Vector3)Call(wait,"GetBoardingApproachPosition",bus,new Vector3(3f,-.2f,0f));
        Assert.That(point.x,Is.EqualTo(edge).Within(.001f));Assert.That(point.y,Is.EqualTo(-.2f));
    }
    [Test]
    public void WalkingOverlapsButJumpsDoNotMultiplyTheBusWaitByWalkingDistance()
    {
        var wait=Create<WaitController>("wait");var bus=Bus(3);
        var passengers=new List<StickManControl>();
        for(int i=0;i<3;i++)passengers.Add(Passenger(wait,new Vector3(3f,0f,(i-1)*.2f)));
        Call(wait,"JumpStickMenIntoBus",bus,passengers);var sequence=Boarding(wait);
        Assert.That(sequence.Duration(),Is.LessThan(1.7f),"Three long walks should overlap instead of making the bus wait for their sum.");
        DOTween.ManualUpdate(.3f,.3f);
        foreach(var passenger in passengers){Assert.That(passenger.transform.position.x,Is.LessThan(3f));Assert.That(passenger.transform.position.y,Is.EqualTo(0f).Within(.0001f));}
        Assert.That(bus.IsReceivingPassengers,Is.True);
        sequence.Complete(true);Assert.That(bus.IsReceivingPassengers,Is.False);Assert.That(bus.isDone,Is.True);
    }
    [Test]
    public void CancellingDuringWalkSettlesReservedPassengersAndNeverLeavesBusPaused()
    {
        var wait=Create<WaitController>("wait");var bus=Bus(1);var passenger=Passenger(wait,new Vector3(3f,0f,0f));
        Call(wait,"JumpStickMenIntoBus",bus,new List<StickManControl>{passenger});var sequence=Boarding(wait);
        DOTween.ManualUpdate(.1f,.1f);
        Call(wait,"OnDisable");
        DOTween.ManualUpdate(0f,0f); // DOTween dispatches queued OnKill at the next update.
        Assert.That(passenger.transform.parent,Is.SameAs(bus.StickManParent),
            "parent="+passenger.transform.parent.name+", receiving="+bus.IsReceivingPassengers+", activeTween="+sequence.IsActive());
        Assert.That(passenger.transform.localPosition,Is.EqualTo(Vector3.zero));
        Assert.That(bus.IsReceivingPassengers,Is.False);Assert.That(wait.IsTransferringPassengers,Is.False);
        Assert.That(bus.ListStickMans.Count,Is.EqualTo(1));
    }
}
