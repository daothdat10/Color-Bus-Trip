using DG.Tweening;
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public sealed class BusPathSamplerTests
{
    [Test]
    public void TownCameraUsesExportedPerspectiveRigWithoutRendererBoundsFit()
    {
        var board = new GameObject("FixedBoard");
        var go = new GameObject("Camera");
        try
        {
            board.transform.position = new Vector3(0, 0, -8);
            board.transform.localScale = Vector3.one * 12.64f;
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 100f;
            var frame = new TownLevelPath(board.transform, 0f);
            CameraFit.ApplyTownSourceCamera(camera, frame);
            Assert.That(camera.orthographic, Is.False);
            Assert.That(camera.fieldOfView, Is.EqualTo(20f).Within(.0001f));
            Assert.That(Vector3.Distance(camera.transform.position, frame.SourceCameraPosition), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(60, 0, 0)), Is.LessThan(.001f));
            Vector3 sourcePoint = frame.ToWorld(new Vector3(0f, MyGameBoardCoordinates.DockFrontY, 0));
            Vector3 viewport = camera.WorldToViewportPoint(sourcePoint);
            Assert.That(viewport.z, Is.GreaterThan(0));
            Assert.That(viewport.y, Is.InRange(.1f, .5f), "The front queue row belongs in the visible lower board.");
            Vector3 before = camera.transform.position;
            CameraFit.ApplyTownSourceCamera(camera, frame);
            Assert.That(camera.transform.position, Is.EqualTo(before));
        }
        finally { Object.DestroyImmediate(go); Object.DestroyImmediate(board); }
    }

    [Test]
    public void ImportedExchangeDistanceUsesSourceScaleAndResetsWithPool()
    {
        var go = new GameObject("Wait");
        try
        {
            var wait = go.AddComponent<WaitController>();
            wait.SetImportedExchangeDistance(.2f * 3.175f);
            Assert.That(wait.ExchangeDistance, Is.EqualTo(.635f).Within(.0001f));
            Assert.That(BusStopProximity.IsInRange(1f, wait.ExchangeDistance), Is.False,
                "Legacy 1.5 world units allowed a visibly distant transfer.");
            wait.ResetImportedLayout();
            Assert.That(wait.ExchangeDistance, Is.EqualTo(1.5f).Within(.0001f));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void TownFillMatchesUpperColorAndRetainsOriginalMeshAndShadowMap()
    {
        var board = new GameObject("Board");
        var fill = GameObject.CreatePrimitive(PrimitiveType.Plane);
        var texture = new Texture2D(2, 2);
        var distanceMap = new Texture2D(2, 2);
        var shader = Shader.Find("Custom/TownToonSurface");
        Assert.That(shader, Is.Not.Null);
        var upper = new Material(shader);
        var lower = new Material(shader);
        Material configured = null;
        try
        {
            fill.name = "Road_InteriorFill";
            fill.transform.SetParent(board.transform, false);
            Mesh original = fill.GetComponent<MeshFilter>().sharedMesh;
            lower.SetColor("_BaseColor", new Color(.3f, .2f, .1f));
            lower.SetTexture("_ContactShadowMap", distanceMap);
            upper.SetColor("_BaseColor", new Color(1f, .8253637f, .5424528f));
            upper.SetColor("_DarkTint", new Color(.8f, .5f, 0f));
            upper.mainTexture = texture;
            upper.mainTextureScale = new Vector2(2.81f, 1f);
            fill.GetComponent<Renderer>().sharedMaterial = lower;
            var layout = (TownRoadSurfaceLayout)System.Activator.CreateInstance(typeof(TownRoadSurfaceLayout),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { .2528f }, null);
            typeof(TownRoadSurfaceLayout).GetField("roadMaterial", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(layout, upper);
            // This production method instantiates a runtime material. Unity
            // reports its known edit-mode-only warning as an error in this test.
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("^Instantiating material due to calling renderer.material during edit mode"));
            layout.ConfigureStaticFill(board.transform);
            configured = fill.GetComponent<Renderer>().sharedMaterial;
            Assert.That(fill.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(original));
            Assert.That(Vector4.Distance(configured.GetColor("_BaseColor"), upper.GetColor("_BaseColor")), Is.LessThan(0.00001f));
            Assert.That(Vector4.Distance(configured.GetColor("_DarkTint"), upper.GetColor("_DarkTint")), Is.LessThan(0.00001f));
            Assert.That(configured.mainTexture, Is.SameAs(texture));
            Assert.That(configured.mainTextureScale, Is.EqualTo(upper.mainTextureScale));
            Assert.That(configured.GetTexture("_ContactShadowMap"), Is.SameAs(distanceMap));
        }
        finally
        {
            Object.DestroyImmediate(board);
            if (configured != null && configured != lower) Object.DestroyImmediate(configured);
            Object.DestroyImmediate(upper); Object.DestroyImmediate(lower);
            Object.DestroyImmediate(texture); Object.DestroyImmediate(distanceMap);
        }
    }

    [Test]
    public void TownParkingUsesSourceSpacingAndKeepsScaleForTwoAndFourLanes()
    {
        var board = new GameObject("FixedBoard");
        var buses = new List<BusController>();
        var mesh = new Mesh();
        try
        {
            board.transform.position = new Vector3(0, 0, -8);
            board.transform.localScale = Vector3.one * 12.64f;
            Mesh originalBoard = Resources.Load<Mesh>("testRoad15.001");
            board.AddComponent<MeshFilter>().sharedMesh = originalBoard;
            var fill = GameObject.CreatePrimitive(PrimitiveType.Plane);
            fill.name = "Road_InteriorFill";
            fill.transform.SetParent(board.transform, false);
            fill.transform.localPosition = new Vector3(0f, .005f, .84958285f);
            fill.transform.localScale = new Vector3(.26001555f, 1f, .30992442f);
            Mesh originalFill = fill.GetComponent<MeshFilter>().sharedMesh;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(.05490315f, .06f, .1f));
            for (int row = 0; row < 2; row++)
            for (int lane = 0; lane < 4; lane++)
            {
                var root = new GameObject("Bus");
                root.transform.position = new Vector3(lane, 0, -10.6f - row * 2.32f);
                var bus = root.AddComponent<BusController>();
                bus.InitGrid(row, lane);
                var body = new GameObject("Body");
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = Vector3.one * 16.5f;
                body.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = body.AddComponent<MeshRenderer>();
                typeof(BusController).GetField("meshRenderer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bus, renderer);
                buses.Add(bus);
            }
            var layout = (TownRoadSurfaceLayout)System.Activator.CreateInstance(typeof(TownRoadSurfaceLayout),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { .2528f }, null);
            typeof(TownRoadSurfaceLayout).GetField("board", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(layout, board.transform);
            typeof(TownRoadSurfaceLayout).GetProperty("Path").SetValue(layout, new TownLevelPath(board.transform, .0632f));
            layout.FitBusesToParkingBay(buses);
            Mesh parkingMesh = board.GetComponent<MeshFilter>().sharedMesh;
            Mesh parkingFill = fill.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(parkingMesh, Is.SameAs(originalBoard));
            Assert.That(parkingMesh.vertexCount, Is.EqualTo(originalBoard.vertexCount));
            Assert.That(parkingFill, Is.SameAs(originalFill));
            float expectedFrontZ = new TownLevelPath(board.transform, .0632f).GetDockBusPosition(0, 4, 0).z;
            Assert.That(buses[0].transform.position.z, Is.EqualTo(expectedFrontZ).Within(.0001f));
            Vector3 scale = buses[0].transform.localScale;
            float worldScale = new TownLevelPath(board.transform, .0632f).WorldScale;
            Assert.That(buses[1].transform.position.x - buses[0].transform.position.x,
                Is.EqualTo(.25f * worldScale).Within(.0001f));
            Assert.That(buses[0].transform.position.z - buses[4].transform.position.z,
                Is.EqualTo(.4f * worldScale).Within(.0001f));
            Assert.That(buses[0].transform.GetChild(0).lossyScale.x,
                Is.EqualTo(4f * worldScale).Within(.0001f));
            var positions = buses.ConvertAll(bus => bus.transform.position);
            layout.FitBusesToParkingBay(buses);
            for (int i = 0; i < buses.Count; i++)
            {
                Assert.That(Vector3.Distance(buses[i].transform.localScale, scale), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(buses[i].transform.position, positions[i]), Is.LessThan(.0001f));
            }
            layout.FitBusesToParkingBay(new List<BusController> { buses[0], buses[1] });
            Assert.That(Vector3.Distance(buses[0].transform.localScale, scale), Is.LessThan(.0001f));
            Assert.That(board.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(parkingMesh));
            Assert.That(fill.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(parkingFill));

        }
        finally
        {
            foreach (BusController bus in buses) Object.DestroyImmediate(bus.gameObject);
            Object.DestroyImmediate(board); Object.DestroyImmediate(mesh);
        }
    }

    [TestCase(.4f)]
    [TestCase(1f)]
    [TestCase(1.7f)]
    public void TownBusScaleIsAbsoluteAndIndependentOfPoolScale(float previousRootScale)
    {
        var root = new GameObject("Bus");
        var body = new GameObject("Base");
        var mesh = new Mesh();
        try
        {
            root.transform.localScale = Vector3.one * previousRootScale;
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 16.5f;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(.05490315f, .06f, .1f));
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = body.AddComponent<MeshRenderer>();
            var bus = root.AddComponent<BusController>();
            typeof(BusController).GetField("meshRenderer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bus, renderer);
            float originalScale = 4f * new MyGameBoardCoordinates(.005f).Scale * 12.64f;
            float lanePitch = (.12f + .1001f) * 12.64f / 4f;
            float expected = Mathf.Min(originalScale, lanePitch / mesh.bounds.size.x);
            bus.ApplyTownModelScale(originalScale, lanePitch);
            Assert.That(body.transform.lossyScale.x, Is.EqualTo(expected).Within(.0001f));
            Vector3 firstScale = root.transform.localScale;
            // Passenger population/bounds must not affect body scale.
            var passenger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            passenger.transform.SetParent(root.transform, false);
            passenger.transform.localScale = Vector3.one * 10f;
            bus.ApplyTownModelScale(originalScale, lanePitch);
            Assert.That(Vector3.Distance(root.transform.localScale, firstScale), Is.LessThan(.0001f));
            Assert.That(body.transform.lossyScale.x * mesh.bounds.size.x, Is.LessThanOrEqualTo(lanePitch + .0001f));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(mesh); }
    }

    [Test]
    public void TownMergeRetainsAuthoredKnotsAndAddsOnlyStaticInterior()
    {
        var board = new GameObject("FixedBoard");
        var target = new GameObject("IndependentPath");
        try
        {
            board.transform.position = new Vector3(0, 0, -8);
            board.transform.localScale = Vector3.one * 12.64f;
            target.transform.position = new Vector3(2, 3, 4);
            target.transform.rotation = Quaternion.Euler(-7.5f, 0, 0);
            var source = new UnityEngine.Splines.Spline();
            source.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(-.764f, -2.04f, 0),
                new Unity.Mathematics.float3(0, -.13f, 0), new Unity.Mathematics.float3(0, .13f, 0), Unity.Mathematics.quaternion.identity),
                UnityEngine.Splines.TangentMode.Broken);
            source.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(0, -.2f, 0)), UnityEngine.Splines.TangentMode.Broken);
            source.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(.75f, -2.04f, 0)), UnityEngine.Splines.TangentMode.Broken);
            var frame = new TownLevelPath(board.transform, 2f);
            var merged = frame.BuildMergedGameplaySpline(source, target.transform);
            Assert.That(merged.Closed, Is.True);
            Assert.That(merged.Count, Is.EqualTo(source.Count + 2));
            for (int i = 0; i < source.Count; i++)
            {
                Vector3 expected = frame.ToGameplay((Vector3)source[i].Position);
                Vector3 actual = target.transform.TransformPoint((Vector3)merged[i].Position);
                Assert.That(Vector3.Distance(expected, actual), Is.LessThan(.0001f));
            }
            Vector3 expectedHandle = frame.ToGameplay((Vector3)(source[0].Position + source[0].TangentOut)) -
                                     frame.ToGameplay((Vector3)source[0].Position);
            Vector3 handle = target.transform.TransformVector((Vector3)merged[0].TangentOut);
            Assert.That(Vector3.Distance(handle, expectedHandle), Is.LessThan(.0001f));
            Assert.That(board.transform.position, Is.EqualTo(new Vector3(0, 0, -8)));
            Assert.That(board.transform.localScale, Is.EqualTo(Vector3.one * 12.64f));
            // Source t=1 reaches the last level knot, before the static return.
            var container = target.AddComponent<UnityEngine.Splines.SplineContainer>();
            container.Spline = merged;
            var sampler = new UnitySplinePath(container, 0);
            float endProgress = sampler.ConvertImportedProgress(1f, source.Count - 1);
            Assert.That(endProgress, Is.LessThan(1f));
            Assert.That(Vector3.Distance(sampler.EvaluatePosition(endProgress),
                frame.ToGameplay((Vector3)source[source.Count - 1].Position)), Is.LessThan(.01f));
        }
        finally { Object.DestroyImmediate(board); Object.DestroyImmediate(target); }
    }

    [Test]
    public void TownStopUsesRoadFrameWithoutCameraProjectionOrIslandClamp()
    {
        var board = new GameObject("FixedBoard");
        var cameraObject = new GameObject("Camera");
        try
        {
            board.transform.localScale = Vector3.one * 12.64f;
            var frame = new TownLevelPath(board.transform, 2f);
            Vector3 source = new Vector3(20, 2, -.5f);
            Vector3 before = frame.ToGameplayStop(source);
            cameraObject.AddComponent<Camera>();
            cameraObject.transform.position = new Vector3(15, 30, -80);
            cameraObject.transform.rotation = Quaternion.Euler(60, 0, 0);
            Assert.That(frame.ToGameplayStop(source), Is.EqualTo(before));
            Assert.That(before.x, Is.GreaterThan(50f));
            Assert.That(Vector3.Distance(before, frame.ToGameplay(new Vector3(20, -.5f, 0))), Is.LessThan(.0001f));
        }
        finally { Object.DestroyImmediate(board); Object.DestroyImmediate(cameraObject); }
    }

    [Test]
    public void TownWaitGroundingHonorsItsPostOffsetAndScale()
    {
        var go = new GameObject("GroundedWait");
        try
        {
            go.transform.position = new Vector3(3, 2, 7);
            go.transform.localScale = Vector3.one * 2f;
            var wait = go.AddComponent<WaitController>();
            wait.PlaceOnGround(.25f);
            Assert.That(go.transform.TransformPoint(new Vector3(0, -1, 0)).y,
                Is.EqualTo(.25f).Within(.0001f));
            Assert.That(go.transform.position.x, Is.EqualTo(3f));
            Assert.That(go.transform.position.z, Is.EqualTo(7f));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [TestCase(-20f)]
    [TestCase(20f)]
    public void ImportedWaitPositionIsNotClampedIntoIsland(float authoredX)
    {
        var cameraObject = new GameObject("ProjectionCamera");
        var roadObject = new GameObject("VisualRoad");
        var pathObject = new GameObject("ProjectionPath");
        try
        {
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 10f, -10f);
            camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            var container = pathObject.AddComponent<UnityEngine.Splines.SplineContainer>();
            var spline = new UnityEngine.Splines.Spline();
            spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(-1, 0, 0)));
            spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(1, 0, 0)));
            container.Spline = spline;
            var type = typeof(LevelControl).Assembly.GetType("GameplayPathProjector");
            object projector = System.Activator.CreateInstance(type,
                new object[] { container, roadObject.transform, camera, Vector2.one, 2f });
            var source = new Vector3(authoredX, 2f, 0f);
            var expectedRay = camera.ViewportPointToRay(camera.WorldToViewportPoint(
                roadObject.transform.TransformPoint(new Vector3(authoredX, 0f, 0f))));
            new Plane(Vector3.up, new Vector3(0f, 2f, 0f)).Raycast(expectedRay, out float distance);
            Vector3 expected = expectedRay.GetPoint(distance);
            Vector3 actual = (Vector3)type.GetMethod("ProjectImportedStopPosition")
                .Invoke(projector, new object[] { source });
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f));
            Assert.That(Mathf.Abs(actual.x), Is.GreaterThan(10f),
                "Authored Stops outside the small island must not be moved into it.");
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(roadObject);
            Object.DestroyImmediate(pathObject);
        }
    }

    [Test]
    public void StopDistanceWrapsOnlyForClosedConveyor()
    {
        Assert.That(BusStopProximity.GetDistance(.98f, .02f, 20f, true), Is.EqualTo(.8f).Within(.0001f));
        Assert.That(BusStopProximity.GetDistance(.98f, .02f, 20f, false), Is.EqualTo(19.2f).Within(.0001f));
    }

    [Test]
    public void StopProgressDoesNotFollowMovedVisualSign()
    {
        var go = new GameObject("MovedWait");
        try
        {
            var wait = go.AddComponent<WaitController>();
            wait.SetStopProgress(.37f);
            go.transform.position = new Vector3(100f, 2f, -300f);
            Assert.That(wait.StopProgress, Is.EqualTo(.37f));
            wait.ResetImportedLayout();
            Assert.That(float.IsNaN(wait.StopProgress), Is.True, "Pooled Wait must not retain its previous stop marker.");
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void ImportedStopProgressAccountsForUnequalCurvesAndClosingSegment()
    {
        var go = new GameObject("UnequalConveyor");
        try
        {
            var container = go.AddComponent<UnityEngine.Splines.SplineContainer>();
            var spline = new UnityEngine.Splines.Spline();
            spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(0, 0, 0)), UnityEngine.Splines.TangentMode.Linear);
            spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(2, 0, 0)), UnityEngine.Splines.TangentMode.Linear);
            spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(10, 0, 0)), UnityEngine.Splines.TangentMode.Linear);
            spline.Closed = true;
            container.Spline = spline;
            var path = new UnitySplinePath(container, 0);
            // Dreamteck .5 is knot 1, not the middle of this now-closed path.
            float progress = path.ConvertImportedProgress(.5f, 2);
            Assert.That(Vector3.Distance(path.EvaluatePosition(progress), new Vector3(2, 0, 0)), Is.LessThan(.01f));
            float end = path.ConvertImportedProgress(1f, 2);
            Assert.That(Vector3.Distance(path.EvaluatePosition(end), new Vector3(10, 0, 0)), Is.LessThan(.01f));
            Assert.That(end, Is.LessThan(1f), "Source end precedes the added closing segment.");
            Assert.That(float.IsNaN(path.ConvertImportedProgress(float.NaN, 2)), Is.True);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void MissingSplineContainerReturnsSafeSamplingDefaults()
    {
        UnitySplinePath path = new UnitySplinePath(null, 0);

        Assert.That(path.Length, Is.EqualTo(0f));
        Assert.That(path.EvaluatePosition(0.5f), Is.EqualTo(Vector3.zero));
        Assert.That(path.EvaluateTangent(0.5f), Is.EqualTo(Vector3.forward));
        Assert.That(path.EvaluateUpVector(0.5f), Is.EqualTo(Vector3.up));
        Assert.That(path.EvaluateRotation(0.5f), Is.EqualTo(Quaternion.identity));
    }

    [Test]
    public void BusMotorUsesRotationProvidedByPath()
    {
        IBusPath path = new RotationTestPath();
        System.Type motorType = typeof(SplineControll).Assembly.GetType("SplineBusMotor");
        Assert.That(motorType, Is.Not.Null);

        MethodInfo evaluate = motorType.GetMethod(
            "EvaluatePathPose", BindingFlags.Public | BindingFlags.Static);
        Assert.That(evaluate, Is.Not.Null);

        object[] arguments = { path, 0.5f, Vector3.zero };
        Quaternion rotation = (Quaternion)evaluate.Invoke(null, arguments);

        Assert.That(Quaternion.Angle(rotation, path.EvaluateRotation(0.5f)),
            Is.LessThan(0.001f));
        Assert.That((Vector3)arguments[2], Is.EqualTo(path.EvaluatePosition(0.5f)));
    }

    [Test]
    public void BusMotorLimitsAbruptRotationChanges()
    {
        System.Type motorType = typeof(SplineControll).Assembly.GetType("SplineBusMotor");
        Assert.That(motorType, Is.Not.Null);

        MethodInfo follow = motorType.GetMethod(
            "FollowPathRotation", BindingFlags.Public | BindingFlags.Static);
        Assert.That(follow, Is.Not.Null);

        Quaternion target = Quaternion.Euler(0f, 90f, 0f);
        Quaternion rotation = (Quaternion)follow.Invoke(
            null, new object[] { Quaternion.identity, target, 0.02f });

        Assert.That(Quaternion.Angle(Quaternion.identity, rotation),
            Is.LessThanOrEqualTo(7.201f));
        Assert.That(Quaternion.Angle(rotation, target), Is.GreaterThan(80f));
    }

    [Test]
    public void MainSplineMotionUsesPausedTweenAtTrafficApprovedProgress()
    {
        GameObject busObject = new GameObject("TweenedPathBus");
        object motor = null;
        try
        {
            BusController bus = busObject.AddComponent<BusController>();
            System.Type assemblyType = typeof(SplineControll);
            System.Type motorType = assemblyType.Assembly.GetType("SplineBusMotor");
            System.Type runtimeType = assemblyType.Assembly.GetType("SplineBusRuntime");
            System.Type entryType = assemblyType.Assembly.GetType("SplineBusEntryCoordinator");
            Assert.That(motorType, Is.Not.Null);
            Assert.That(runtimeType, Is.Not.Null);
            Assert.That(entryType, Is.Not.Null);

            motor = System.Activator.CreateInstance(motorType);
            object runtime = System.Activator.CreateInstance(runtimeType, bus, 0f);
            object entry = System.Activator.CreateInstance(entryType, motor);
            motorType.GetMethod("Add").Invoke(motor, new[] { runtime });

            motorType.GetMethod("Tick").Invoke(motor, new object[] {
                new RotationTestPath(), entry, 0.1f, 0.5f, 0f,
                false, false, new List<float>()
            });

            float progress = (float)runtimeType.GetField("Progress").GetValue(runtime);
            Tween tween = runtimeType.GetField("PathMotionTween").GetValue(runtime) as Tween;
            Assert.That(progress, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(tween, Is.Not.Null);
            Assert.That(tween.IsPlaying(), Is.False);
            Assert.That(bus.transform.position.x, Is.EqualTo(progress).Within(0.0001f));
        }
        finally
        {
            if (motor != null)
                motor.GetType().GetMethod("Clear").Invoke(motor, null);
            Object.DestroyImmediate(busObject);
        }
    }

    [Test]
    public void SplineRotationKeepsBusUprightOnSlopedTangent()
    {
        Quaternion rotation = UnitySplinePath.EvaluateUprightRotation(
            new Vector3(1f, 2f, 0f));

        Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.up),
            Is.LessThan(0.001f));
        Assert.That(Vector3.Angle(rotation * Vector3.forward, Vector3.right),
            Is.LessThan(0.001f));
    }

    private sealed class RotationTestPath : IBusPath
    {
        public float Length => 1f;
        public Vector3 EvaluatePosition(float progress) => new Vector3(progress, 0f, 2f);
        public Vector3 EvaluateTangent(float progress) => Vector3.forward;
        public Vector3 EvaluateUpVector(float progress) => Vector3.up;
        public Quaternion EvaluateRotation(float progress) =>
            Quaternion.Euler(0f, 90f, 12f);
        public float GetClosestProgress(Vector3 worldPosition) => 0f;
    }
}
