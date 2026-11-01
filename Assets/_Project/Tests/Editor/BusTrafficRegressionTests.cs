using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

/// <summary>Runs the production motor and coordinators against actual Unity transforms and meshes.</summary>
public sealed class BusTrafficRegressionTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private Type runtimeType, motorType, entryType;
    private object motor, entry;
    private bool usePrefab;
    private const BindingFlags Fields = BindingFlags.Public | BindingFlags.Instance;

    [SetUp] public void SetUp()
    {
        usePrefab=false;
        var assembly = typeof(SplineControll).Assembly;
        runtimeType = assembly.GetType("SplineBusRuntime");
        motorType = assembly.GetType("SplineBusMotor");
        entryType = assembly.GetType("SplineBusEntryCoordinator");
        motor = Activator.CreateInstance(motorType);
        entry = Activator.CreateInstance(entryType, motor);
        entryType.GetMethod("Configure").Invoke(entry, new object[]
            {2.2f, 2.8f, .55f, .2f, (LayerMask)0, 4f, .35f, 0f, .6f, .15f});
    }
    [TearDown] public void TearDown()
    {
        motorType.GetMethod("Clear").Invoke(motor, null);
        DOTween.KillAll();
        foreach (var go in objects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
        objects.Clear();
    }
    private BusController Bus(Vector3 position, Quaternion rotation)
    {
        GameObject go;
        if (usePrefab)
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Bus.prefab");
            Assert.That(prefab,Is.Not.Null);
            go=UnityEngine.Object.Instantiate(prefab);
        }
        else
        {
            go=new GameObject("traffic-regression-bus");
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(go.transform,false);
            body.transform.localScale=new Vector3(.8f,.6f,1.6f);
            go.AddComponent<BusController>();
        }
        objects.Add(go);
        go.transform.SetPositionAndRotation(position,rotation);
        return go.GetComponent<BusController>();
    }
    private object AddLoop(IBusPath path, float progress)
    {
        var bus = Bus(path.EvaluatePosition(progress), path.EvaluateRotation(progress));
        object runtime = Activator.CreateInstance(runtimeType,bus,progress);
        motorType.GetMethod("Add").Invoke(motor,new[]{runtime});
        return runtime;
    }
    private object AddEntrance(IBusPath path, float x=1.5f, float z=-3f)
    {
        var bus = Bus(new Vector3(x,0f,z),Quaternion.identity);
        entryType.GetMethod("AddStickMan").Invoke(entry,new object[]{bus,path});
        foreach(object runtime in (IEnumerable)motorType.GetProperty("Buses").GetValue(motor))
            if (Get<BusController>(runtime,"Bus")==bus) return runtime;
        Assert.Fail("Every clicked bus must have a tracked entrance, never an untracked gate tween.");
        return null;
    }
    private T Get<T>(object value,string name)=>(T)runtimeType.GetField(name,Fields).GetValue(value);
    private void Set(object value,string name,object data)=>runtimeType.GetField(name,Fields).SetValue(value,data);
    private void Tick(IBusPath path,float dt=.02f,float speed=.15f,bool spacing=true)=>motorType.GetMethod("Tick").Invoke(motor,
        new object[]{path,entry,dt,speed,.2f,spacing,false,null});

    [Test]
    public void BusyReceiverDoesNotDelayClickOnAClearStraight()
    {
        var path = new RectanglePath();
        object entrant = AddEntrance(path, 1.5f, -5f);
        object traffic = AddLoop(path, .99f);
        var bus = Get<BusController>(entrant, "Bus");
        Vector3 before = bus.transform.position;
        entryType.GetMethod("AdvanceEntrance").Invoke(entry,
            new object[] { entrant, path, .016f, .15f, true });
        Assert.That(Get<bool>(entrant, "EntryTransferStarted"), Is.False);
        Assert.That(Get<float>(entrant, "EntryElapsed"), Is.EqualTo(.016f).Within(.0001f),
            "A blocked receiver must allow the clicked bus to approach on the first frame.");
        Assert.That(Vector3.Distance(before, bus.transform.position), Is.GreaterThan(.01f));
        // A long frame must stop before traffic rather than jump through it.
        entryType.GetMethod("AdvanceEntrance").Invoke(entry,
            new object[] { entrant, path, 10f, .15f, true });
        Assert.That(Get<float>(entrant, "EntryElapsed"),
            Is.LessThanOrEqualTo(Get<float>(entrant, "EntryStraightDuration")));
        AssertNoVisualOverlap(new[] { entrant, traffic }, "blocked approach");
    }

    [Test]
    public void SecondLaneClickApproachesWhileFirstLaneOwnsTransfer()
    {
        var path = new RectanglePath();
        object first = AddEntrance(path, -3f, -5f);
        Tick(path);
        Assert.That(Get<bool>(first, "EntryTransferStarted"), Is.True);
        object second = AddEntrance(path, 3f, -5f);
        Tick(path, .016f);
        Assert.That(Get<bool>(second, "EntryTransferStarted"), Is.True);
        Assert.That(Get<float>(second, "EntryElapsed"), Is.GreaterThan(0f),
            "A different lane must react immediately while the shared turn is busy.");
        AssertNoVisualOverlap(new[] { first, second }, "two clicks");
    }


    [Test]
    public void ReservedButUnstartedEntryMustLetAnAlreadyArrivingBusClear()
    {
        var path=new RectanglePath();
        object entrant=AddEntrance(path);
        Set(entrant,"EntryTransferReserved",true); // simulate a previously granted, unstarted reservation
        // Registration and the next motor tick are separate callbacks. Traffic
        // can arrive, or its visual can sway, after the reservation was created.
        object arriving=AddLoop(path,.99f);
        for(int i=0;i<800 && Get<bool>(entrant,"IsEntering");i++) Tick(path);
        Assert.That(Get<bool>(entrant,"IsEntering"),Is.False,
            "Reservation owner and arriving spline bus must not wait for each other forever.");
    }

    [TestCase(.016f,.05f)]
    [TestCase(.05f,.15f)]
    [TestCase(.1f,.3f)]
    public void FiveBusConvoyKeepsCirculatingThroughCorners(float dt,float speed)
    {
        var path=new RectanglePath();
        var buses=new List<object>();
        for(int i=0;i<5;i++) buses.Add(AddLoop(path,Mathf.Repeat(.12f-i*1.7f/path.Length,1f)));
        var travelled=new float[buses.Count];
        for(int frame=0;frame<Mathf.CeilToInt(15f/dt);frame++)
        {
            var previous=new float[buses.Count];
            for(int i=0;i<buses.Count;i++) previous[i]=Get<float>(buses[i],"Progress");
            Tick(path,dt,speed);
            for(int i=0;i<buses.Count;i++) travelled[i]+=SplineProgressSolver.ForwardDistance(
                previous[i],Get<float>(buses[i],"Progress"),true);
            for(int i=0;i<buses.Count;i++)for(int j=i+1;j<buses.Count;j++)
            {
                var first=Get<BusController>(buses[i],"Bus");var second=Get<BusController>(buses[j],"Bus");
                Assert.That(BusTrafficMath.OverlapsBusFootprint(first,first.transform.position,first.transform.rotation,second,0f),
                    Is.False,"Convoy visual overlap at frame "+frame+", pair "+i+"/"+j);
            }
        }
        for(int i=0;i<buses.Count;i++) Assert.That(travelled[i],Is.GreaterThan(speed*10f),
            "A free convoy must keep moving; corner visual checks must not create a circular wait. Bus "+i);
    }

    [Test]
    public void AlreadyTouchingFollowerCannotKeepItsLeaderStopped()
    {
        var path=new RectanglePath();
        object leader=AddLoop(path,.10f);
        object follower=AddLoop(path,.10f-1.59f/path.Length);
        float initial=Get<float>(leader,"Progress");
        for(int i=0;i<300;i++)Tick(path);
        Assert.That(SplineProgressSolver.ForwardDistance(initial,Get<float>(leader,"Progress"),true),
            Is.GreaterThan(.2f),"The leading bus must be allowed to clear an existing rear contact.");
    }

    [Test]
    public void InactiveReservationOwnerCannotBlockTheNextClickedBus()
    {
        var path=new RectanglePath();
        object removed=AddEntrance(path,1.5f);
        Get<BusController>(removed,"Bus").gameObject.SetActive(false);
        object next=AddEntrance(path,-1.5f);
        Tick(path);
        Assert.That(Get<bool>(next,"EntryTransferStarted"),Is.True,
            "A disabled or pooled bus must release its entrance on the next tick.");
    }

    [Test]
    public void PassengerStopReleasesFollowingTrafficOnCompletion()
    {
        var path=new RectanglePath();
        object leader=AddLoop(path,.1f);
        object follower=AddLoop(path,.02f);
        var bus=Get<BusController>(leader,"Bus");
        bus.SetPassengerTransferInProgress(true);
        for(int i=0;i<100;i++)Tick(path);
        float stopped=Get<float>(follower,"Progress");
        bus.SetPassengerTransferInProgress(false);
        for(int i=0;i<200;i++)Tick(path);
        Assert.That(SplineProgressSolver.ForwardDistance(stopped,Get<float>(follower,"Progress"),true),Is.GreaterThan(.2f));
    }

    [Test]
    public void ThreeLanesClickedTogetherAllFinishWithoutOverlap()
    {
        var path=new RectanglePath();
        var entrants=new[]{AddEntrance(path,-.8f),AddEntrance(path,0f),AddEntrance(path,.8f)};
        var started=new bool[entrants.Length];
        for(int frame=0;frame<1500;frame++)
        {
            var previous=new float[entrants.Length];
            for(int i=0;i<entrants.Length;i++) previous[i]=Get<float>(entrants[i],"EntryElapsed");
            Tick(path);
            for(int i=0;i<entrants.Length;i++)
            {
                if(started[i] && Get<bool>(entrants[i],"IsEntering"))
                    Assert.That(Get<float>(entrants[i],"EntryElapsed"),Is.GreaterThanOrEqualTo(previous[i]),"Started entry stopped again.");
                started[i]|=Get<bool>(entrants[i],"EntryTransferStarted");
            }
        }
        foreach(object entrant in entrants)Assert.That(Get<bool>(entrant,"IsEntering"),Is.False);
    }

    [Test]
    public void AppliedRotationMustMatchTheCollisionCheckedPoseWhenProgressIsClamped()
    {
        var path=new StraightForwardPath();
        object moving=AddLoop(path,0f);
        var bus=Get<BusController>(moving,"Bus");
        bus.transform.rotation=Quaternion.Euler(0f,90f,0f);
        object parked=AddLoop(path,.105f);
        var obstacle=Get<BusController>(parked,"Bus");
        obstacle.SetPassengerTransferInProgress(true);
        Tick(path,.1f,1f,false);
        Assert.That(BusTrafficMath.OverlapsBusFootprint(bus,bus.transform.position,bus.transform.rotation,obstacle,0f),Is.False,
            "A progress-clamped step must not apply the full, unchecked frame rotation afterwards.");
    }

    [Test]
    public void RepeatedClickCannotReplaceAnActiveEntryWithAGateTween()
    {
        var path=new RectanglePath();
        object entrant=AddEntrance(path);
        var bus=Get<BusController>(entrant,"Bus");
        for(int i=0;i<10;i++)entryType.GetMethod("AddStickMan").Invoke(entry,new object[]{bus,path});
        Assert.That(((ICollection)motorType.GetProperty("Buses").GetValue(motor)).Count,Is.EqualTo(1));
        Assert.That(bus.HasActiveSplineApproach,Is.False);
        for(int i=0;i<400&&Get<bool>(entrant,"IsEntering");i++)Tick(path);
        Assert.That(Get<bool>(entrant,"IsEntering"),Is.False);
    }

    [Test]
    public void MissingPathAndZeroDeltaTimeCannotTeleportOrRotateTheBus()
    {
        var path=new RectanglePath();
        object runtime=AddLoop(path,.12f);
        var bus=Get<BusController>(runtime,"Bus");
        Vector3 before=bus.transform.position;
        Quaternion rotation=bus.transform.rotation;
        Tick(null);
        Tick(path,0f);
        Assert.That(bus.transform.position,Is.EqualTo(before));
        Assert.That(Quaternion.Angle(rotation,bus.transform.rotation),Is.LessThan(.001f));
        Assert.That(Get<float>(runtime,"Progress"),Is.EqualTo(.12f));
        Tick(path);
        Assert.That(Get<float>(runtime,"Progress"),Is.GreaterThan(.12f));
    }

    [Test]
    public void RemovingAStartedTransferImmediatelyReleasesItsReservation()
    {
        var path=new RectanglePath();
        object owner=AddEntrance(path,1.5f);
        Tick(path);
        Assert.That(Get<bool>(owner,"EntryTransferStarted"),Is.True);
        object next=AddEntrance(path,-1.5f);
        motorType.GetMethod("Remove").Invoke(motor,new[]{owner});
        Get<BusController>(owner,"Bus").gameObject.SetActive(false);
        Tick(path);
        Assert.That(Get<bool>(next,"EntryTransferStarted"),Is.True);
    }

    [Test]
    public void EqualProgressRearContactHasOneLeaderAndDoesNotFreezeBothBuses()
    {
        var path=new RectanglePath();
        object older=AddLoop(path,.1f);
        object newer=AddLoop(path,.1f);
        for(int i=0;i<200;i++)Tick(path);
        Assert.That(Get<float>(older,"Progress"),Is.GreaterThan(.3f));
        Assert.That(Get<float>(newer,"Progress"),Is.GreaterThan(.2f));
    }

    [TestCase(.016f)]
    [TestCase(.1f)]
    public void ActualPrefabConvoyCompletesACircuitWithNoVisualOverlap(float dt)
    {
        usePrefab=true;
        var path=new RectanglePath();
        var runtimes=new List<object>();
        object first=AddLoop(path,.12f);
        runtimes.Add(first);
        float gap=(Get<float>(first,"ConveyorExtent")+.3f)/path.Length;
        for(int i=1;i<5;i++)runtimes.Add(AddLoop(path,Mathf.Repeat(.12f-i*gap,1f)));
        var travelled=new float[runtimes.Count];
        for(int frame=0;frame<Mathf.CeilToInt(15f/dt);frame++)
        {
            var previous=new float[runtimes.Count];
            for(int i=0;i<runtimes.Count;i++)previous[i]=Get<float>(runtimes[i],"Progress");
            Tick(path,dt);
            for(int i=0;i<runtimes.Count;i++)travelled[i]+=SplineProgressSolver.ForwardDistance(previous[i],Get<float>(runtimes[i],"Progress"),true);
            AssertNoVisualOverlap(runtimes,"prefab convoy frame "+frame);
        }
        foreach(float distance in travelled)Assert.That(distance,Is.GreaterThan(1f),"Actual prefab did not complete one circuit.");
    }

    [Test]
    public void ActualPrefabThreeClicksFinishWithoutOverlap()
    {
        usePrefab=true;
        var path=new RectanglePath();
        var runtimes=new List<object>{AddEntrance(path,-1.2f),AddEntrance(path,0f),AddEntrance(path,1.2f)};
        var started=new bool[3];
        for(int frame=0;frame<1200;frame++)
        {
            var previous=new float[3];
            for(int i=0;i<3;i++)previous[i]=Get<float>(runtimes[i],"EntryElapsed");
            Tick(path);
            AssertNoVisualOverlap(runtimes,"prefab multi-entry frame "+frame);
            for(int i=0;i<3;i++)
            {
                if(started[i]&&Get<bool>(runtimes[i],"IsEntering"))
                    Assert.That(Get<float>(runtimes[i],"EntryElapsed"),Is.GreaterThanOrEqualTo(previous[i]));
                started[i]|=Get<bool>(runtimes[i],"EntryTransferStarted");
            }
        }
        foreach(object runtime in runtimes)Assert.That(Get<bool>(runtime,"IsEntering"),Is.False, string.Join(";",runtimes.ConvertAll(r=>"entry="+Get<bool>(r,"IsEntering")+" elapsed="+Get<float>(r,"EntryElapsed")+" pos="+Get<BusController>(r,"Bus").transform.position)));
    }

    private void AssertNoVisualOverlap(IList<object> runtimes,string context)
    {
        for(int i=0;i<runtimes.Count;i++)for(int j=i+1;j<runtimes.Count;j++)
        {
            var a=Get<BusController>(runtimes[i],"Bus");var b=Get<BusController>(runtimes[j],"Bus");
            Assert.That(BusTrafficMath.OverlapsBusFootprint(a,a.transform.position,a.transform.rotation,b,0f),
                Is.False,context+", pair "+i+"/"+j);
        }
    }

    private sealed class StraightForwardPath:IBusPath
    {
        public float Length=>20f;
        public Vector3 EvaluatePosition(float p)=>Vector3.forward*(p*Length);
        public Vector3 EvaluateTangent(float p)=>Vector3.forward;
        public Vector3 EvaluateUpVector(float p)=>Vector3.up;
        public Quaternion EvaluateRotation(float p)=>Quaternion.identity;
        public float GetClosestProgress(Vector3 p)=>Mathf.Clamp01(p.z/Length);
    }

    private sealed class RectanglePath:IBusPath
    {
        private static readonly Vector3[] Points={Vector3.zero,new Vector3(-5,0,0),new Vector3(-5,0,7),new Vector3(5,0,7),new Vector3(5,0,0),Vector3.zero};
        public float Length=>34f;
        private void Segment(float p,out int index,out float local)
        {
            float d=Mathf.Repeat(p,1f)*Length;index=0;
            while(index<Points.Length-2 && d>Vector3.Distance(Points[index],Points[index+1]))
            {d-=Vector3.Distance(Points[index],Points[index+1]);index++;}
            local=d/Vector3.Distance(Points[index],Points[index+1]);
        }
        public Vector3 EvaluatePosition(float p){Segment(p,out int i,out float u);return Vector3.Lerp(Points[i],Points[i+1],u);}
        public Vector3 EvaluateTangent(float p){Segment(p,out int i,out _);return (Points[i+1]-Points[i]).normalized;}
        public Vector3 EvaluateUpVector(float p)=>Vector3.up;
        public Quaternion EvaluateRotation(float p)=>Quaternion.LookRotation(EvaluateTangent(p));
        public float GetClosestProgress(Vector3 p)=>0f;
    }
}
