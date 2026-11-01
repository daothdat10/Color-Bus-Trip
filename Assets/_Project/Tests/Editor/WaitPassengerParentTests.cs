using NUnit.Framework;
using UnityEngine;
using DG.Tweening;
using System.Reflection;
using System.Collections.Generic;
public class WaitPassengerParentTests
{
    [Test] public void CreatingUnloadSequenceKeepsWaitingPassengersOnBus()
    {
        var bus = new GameObject("SwayTray");
        var waitRoot = new GameObject("Wait");
        try
        {
            var wait = waitRoot.AddComponent<WaitController>();
            var passengers = new List<StickManControl>();
            for(int i=0;i<3;i++)
            {
                var go=new GameObject("Passenger"); go.transform.SetParent(bus.transform);
                passengers.Add(go.AddComponent<StickManControl>());
            }
            typeof(WaitController).GetMethod("JumpStickMenIntoWait", BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(wait,new object[]{passengers});
            foreach(var passenger in passengers) Assert.That(passenger.transform.parent,Is.EqualTo(bus.transform));
            bus.transform.rotation=Quaternion.Euler(0,0,15);
            foreach(var passenger in passengers) Assert.That(Quaternion.Angle(passenger.transform.rotation,bus.transform.rotation),Is.LessThan(0.001f));
        }
        finally { DOTween.KillAll(); Object.DestroyImmediate(bus); Object.DestroyImmediate(waitRoot); }
    }
}
