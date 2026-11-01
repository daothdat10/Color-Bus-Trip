using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Town-sized buses, tight dock lanes, and real production entrance/motor ticks.</summary>
public sealed class BusEntrySmoothTrafficTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private Type runtimeType, motorType, entryType;
    private object motor, entry;
    private IBusPath path;
    private readonly List<object> runtimes = new List<object>();
    [SetUp] public void SetUp()
    {
        var assembly = typeof(BusController).Assembly;
        runtimeType = assembly.GetType("SplineBusRuntime");
        motorType = assembly.GetType("SplineBusMotor");
        entryType = assembly.GetType("SplineBusEntryCoordinator");
        motor = Activator.CreateInstance(motorType);
        entry = Activator.CreateInstance(entryType, motor);
        entryType.GetMethod("Configure").Invoke(entry, new object[]
            { 2.2f, 2.8f, .55f, .2f, (LayerMask)0, 3f, .35f, 0f, .72f, .25f });
        path = new DockLoop();
    }
    [TearDown] public void TearDown()
    {
        motorType.GetMethod("Clear").Invoke(motor, null);
        foreach(var go in objects) if(go != null) UnityEngine.Object.DestroyImmediate(go);
        objects.Clear(); runtimes.Clear();
    }
    private BusController Bus(float x, float z)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Bus.prefab");
        var go = UnityEngine.Object.Instantiate(prefab);
        objects.Add(go);
        go.transform.localScale = Vector3.one * .77f;
        go.transform.SetPositionAndRotation(new Vector3(x, .0632f, z), Quaternion.identity);
        return go.GetComponent<BusController>();
    }
    private T Get<T>(object r, string field) => (T)runtimeType.GetField(field).GetValue(r);
    private object Enter(float x, float z = -6.22f)
    {
        var bus = Bus(x, z);
        entryType.GetMethod("AddStickMan").Invoke(entry, new object[] { bus, path });
        foreach(object r in (IEnumerable)motorType.GetProperty("Buses").GetValue(motor))
            if(Get<BusController>(r, "Bus") == bus) { runtimes.Add(r); return r; }
        throw new InvalidOperationException("Untracked entrance.");
    }
    private object Loop(float progress)
    {
        var bus = Bus(0, 0);
        bus.transform.SetPositionAndRotation(path.EvaluatePosition(progress), path.EvaluateRotation(progress));
        var r = Activator.CreateInstance(runtimeType, bus, progress);
        motorType.GetMethod("Add").Invoke(motor, new[] { r });
        runtimes.Add(r); return r;
    }
    private void Tick(float dt) => motorType.GetMethod("Tick").Invoke(motor,
        new object[] { path, entry, dt, .25f, .2f, true, false, null });
    private void NoOverlap()
    {
        for(int i=0;i<runtimes.Count;i++) for(int j=i+1;j<runtimes.Count;j++)
        {
            var a=Get<BusController>(runtimes[i],"Bus");var b=Get<BusController>(runtimes[j],"Bus");
            Assert.That(BusTrafficMath.OverlapsBusFootprint(a,a.transform.position,a.transform.rotation,b,0f),
                Is.False, "Visual overlap, buses "+i+"/"+j);
        }
    }

    [TestCase(-.79f, .016f)] [TestCase(0f, .016f)] [TestCase(.79f, .016f)]
    [TestCase(-.79f, .033f)] [TestCase(0f, .033f)] [TestCase(.79f, .033f)]
    [TestCase(-.79f, .1f)] [TestCase(0f, .1f)] [TestCase(.79f, .1f)]
    public void PassingLoopTrafficDoesNotProduceAHardStopBeforeTheTurn(float lane, float dt)
    {
        Loop(.98f);
        var entrant=Enter(lane);
        bool slowed=false, granted=false;
        for(int frame=0;frame<Mathf.CeilToInt(6f/dt)&&Get<bool>(entrant,"IsEntering");frame++)
        {
            float before=Get<float>(entrant,"EntryElapsed");
            Tick(dt);
            Assert.That(Get<float>(entrant,"EntryElapsed"),Is.GreaterThan(before),
                "Passing traffic must cause a rolling approach rather than a frozen frame at the turn.");
            slowed |= Get<float>(entrant,"EntrySpeedFactor") < .95f;
            granted |= Get<bool>(entrant,"EntryTransferStarted");
            NoOverlap();
        }
        Assert.That(slowed,Is.True);
        Assert.That(granted,Is.True);
        Assert.That(Get<bool>(entrant,"IsEntering"),Is.False);
    }

    [Test]
    public void ProlongedTrafficWaitRecoversSpeedGraduallyAndFinishes()
    {
        var blocker=Loop(.98f);
        var blockerBus=Get<BusController>(blocker,"Bus");
        blockerBus.SetPassengerTransferInProgress(true);
        var entrant=Enter(.79f);
        for(int frame=0;frame<150;frame++) { Tick(.016f); NoOverlap(); }
        Assert.That(Get<bool>(entrant,"EntryTransferStarted"),Is.False);
        Assert.That(Get<float>(entrant,"EntrySpeedFactor"),Is.LessThan(.01f));
        motorType.GetMethod("Remove").Invoke(motor,new[] {blocker});
        runtimes.Remove(blocker);blockerBus.gameObject.SetActive(false);
        float previous=Get<float>(entrant,"EntrySpeedFactor");
        for(int frame=0;frame<500&&Get<bool>(entrant,"IsEntering");frame++)
        {
            float before=Get<float>(entrant,"EntryElapsed");
            Tick(.016f);
            Assert.That(Get<float>(entrant,"EntryElapsed"),Is.GreaterThan(before));
            float factor=Get<float>(entrant,"EntrySpeedFactor");
            Assert.That(factor-previous,Is.LessThanOrEqualTo(.0641f),
                "Release from traffic must not jump directly from stopped to cruise speed.");
            previous=factor;
        }
        Assert.That(Get<bool>(entrant,"IsEntering"),Is.False);
    }

    [TestCase(-.79f, 0f, .79f, .016f)] [TestCase(.79f, 0f, -.79f, .016f)]
    [TestCase(-.79f, .79f, 0f, .016f)] [TestCase(0f, -.79f, .79f, .016f)]
    [TestCase(0f, .79f, -.79f, .016f)] [TestCase(.79f, -.79f, 0f, .016f)]
    [TestCase(-.79f, 0f, .79f, .1f)] [TestCase(.79f, 0f, -.79f, .1f)]
    public void TightDockThreeClicksFinishWithoutOverlapOrASecondStop(float a,float b,float c,float dt)
    {
        var entries=new[] {Enter(a),Enter(b),Enter(c)};
        var granted=new bool[entries.Length];
        for(int frame=0;frame<1000;frame++)
        {
            var before=new float[entries.Length];
            for(int i=0;i<entries.Length;i++)before[i]=Get<float>(entries[i],"EntryElapsed");
            Tick(dt); NoOverlap();
            bool done=true;
            for(int i=0;i<entries.Length;i++)
            {
                if(granted[i]&&Get<bool>(entries[i],"IsEntering"))
                    Assert.That(Get<float>(entries[i],"EntryElapsed"),Is.GreaterThan(before[i]));
                granted[i]|=Get<bool>(entries[i],"EntryTransferStarted");
                done &= !Get<bool>(entries[i],"IsEntering");
            }
            if(done)return;
        }
        Assert.Fail("Clicked entrances never finished.");
    }

    [Test]
    public void SameLaneFollowerCannotAcquireTheTurnAheadOfItsLeader()
    {
        var leader=Enter(.79f);
        var follower=Enter(.79f,-8.2f);
        Tick(.016f);
        Assert.That(Get<bool>(leader,"EntryTransferStarted"),Is.True);
        Assert.That(Get<bool>(follower,"EntryTransferStarted"),Is.False);
        for(int frame=0;frame<800;frame++) { Tick(.016f); NoOverlap(); }
        Assert.That(Get<bool>(leader,"IsEntering"),Is.False);
        Assert.That(Get<bool>(follower,"IsEntering"),Is.False);
    }

    [Test]
    public void PassedApproachNoLongerBlocksABusBehindTheEntrance()
    {
        var entrant=Enter(.79f,-9f);
        var bus=Get<BusController>(entrant,"Bus");
        var obstacle=Loop(.5f);
        var other=Get<BusController>(obstacle,"Bus");
        other.transform.position=bus.transform.position;
        var movement=typeof(BusController).Assembly.GetType("SplineEntranceMovement");
        var overlap=movement.GetMethod("OverlapsTrafficCorridor");
        Assert.That(overlap.Invoke(null,new object[]{entrant,obstacle,other.transform.position,other.transform.rotation}),Is.True);
        float straight=Get<float>(entrant,"EntryStraightDuration");
        movement.GetMethod("Advance").Invoke(null,new object[]{entrant,straight});
        Assert.That(overlap.Invoke(null,new object[]{entrant,obstacle,other.transform.position,other.transform.rotation}),Is.False,
            "A completed stretch of the approach must no longer reserve its old position.");
    }

    private sealed class DockLoop : IBusPath
    {
        private static readonly Vector3[] Points = {
            new Vector3(-2.41f,.0632f,-4.15f),new Vector3(-2.41f,.0632f,1f),
            new Vector3(3f,.0632f,1f),new Vector3(3f,.0632f,-7f),
            new Vector3(-2.41f,.0632f,-7f),new Vector3(-2.41f,.0632f,-4.15f)};
        public float Length => 26.82f;
        private void Segment(float p,out int i,out float u)
        {
            float d=Mathf.Repeat(p,1f)*Length;i=0;
            while(i<Points.Length-2 && d>Vector3.Distance(Points[i],Points[i+1]))
            {d-=Vector3.Distance(Points[i],Points[i+1]);i++;}
            u=d/Vector3.Distance(Points[i],Points[i+1]);
        }
        public Vector3 EvaluatePosition(float p){Segment(p,out int i,out float u);return Vector3.Lerp(Points[i],Points[i+1],u);}
        public Vector3 EvaluateTangent(float p){Segment(p,out int i,out _);return (Points[i+1]-Points[i]).normalized;}
        public Vector3 EvaluateUpVector(float p)=>Vector3.up;
        public Quaternion EvaluateRotation(float p)=>Quaternion.LookRotation(EvaluateTangent(p));
        public float GetClosestProgress(Vector3 p)=>0f;
    }
}
