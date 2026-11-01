using NUnit.Framework;
using UnityEngine;

public sealed class BusTrafficMathTests
{
    [Test]
    public void ExitingBusDoesNotKeepFollowingBusWaiting()
    {
        GameObject sourceObject = new GameObject("source-bus");
        GameObject exitingObject = new GameObject("exiting-bus");
        try
        {
            BusController sourceBus = sourceObject.AddComponent<BusController>();
            BusController exitingBus = exitingObject.AddComponent<BusController>();

            Assert.That(BusTrafficMath.ShouldBlockTraffic(sourceBus, exitingBus), Is.True);

            exitingBus.MarkExitStarted();

            Assert.That(BusTrafficMath.ShouldBlockTraffic(sourceBus, exitingBus), Is.False);
            Assert.That(BusTrafficMath.ShouldBlockTraffic(sourceBus, sourceBus), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
            Object.DestroyImmediate(exitingObject);
        }
    }

    [Test]
    public void BusBehindOrOutsideLaneDoesNotBlock()
    {
        Vector3 source = Vector3.zero;
        Vector3 forward = Vector3.forward;

        Assert.That(
            BusTrafficMath.IsBusAhead(source, forward, new Vector3(0f, 0f, 1f), 2f, 0.5f, out _),
            Is.True
        );
        Assert.That(
            BusTrafficMath.IsBusAhead(source, forward, new Vector3(0.7f, 0f, 1f), 2f, 0.5f, out _),
            Is.False
        );
        Assert.That(
            BusTrafficMath.IsBusAhead(source, forward, new Vector3(0f, 0f, -1f), 2f, 0.5f, out _),
            Is.False
        );
    }

    [Test]
    public void MyGameStyleTransferDistanceUsesXZOnly()
    {
        Vector3 waitPosition = new Vector3(0.42f, 2f, -1.36f);
        Vector3 busPosition = new Vector3(0.42f, 200f, -2.86f);

        Assert.That(
            BusTrafficMath.GetXZPlanarDistance(busPosition, waitPosition),
            Is.EqualTo(1.5f).Within(0.0001f)
        );
        Assert.That(
            BusTrafficMath.IsWithinXZPlanarDistance(busPosition, waitPosition, 1.5f),
            Is.True,
            "The render-layer Y difference must not affect stop proximity."
        );
    }

    [TestCase(1.49f, true)]
    [TestCase(1.50f, true)]
    [TestCase(1.51f, false)]
    public void TransferDistanceHasAStableBoundary(float distance, bool expected)
    {
        bool canTransfer = BusTrafficMath.IsWithinXZPlanarDistance(
            Vector3.zero,
            new Vector3(0f, 2f, distance),
            1.5f
        );

        Assert.That(canTransfer, Is.EqualTo(expected));
    }

    [Test]
    public void BusThatHasNotReachedTheWaitCannotTransfer()
    {
        Vector3 waitPosition = new Vector3(-0.46f, 2f, -0.81f);
        Vector3 busPosition = new Vector3(-0.46f, 2f, -2.42f);

        Assert.That(
            BusTrafficMath.IsWithinXZPlanarDistance(busPosition, waitPosition, 1.5f),
            Is.False,
            "A bus 1.61 units before the wait must not receive passengers early."
        );
    }

    [Test]
    public void FollowingDistanceUsesBothRenderedBusLengthsAndPadding()
    {
        Assert.That(
            BusTrafficMath.GetRequiredFollowingDistance(2f, 3f, 0.2f),
            Is.EqualTo(2.7f).Within(0.0001f)
        );
    }

    [Test]
    public void RotatingBusDoesNotInflateItsVisualLengthAlongItsOwnForward()
    {
        GameObject busObject = new GameObject("rotating-bus");
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            body.transform.SetParent(busObject.transform, false);
            body.transform.localScale = new Vector3(1f, 1f, 3f);
            busObject.transform.rotation = Quaternion.Euler(0f, 45f, 0f);

            BusController bus = busObject.AddComponent<BusController>();
            Assert.That(bus.GetVisualLengthAlong(busObject.transform.forward),
                Is.EqualTo(3f).Within(0.01f));
        }
        finally
        {
            Object.DestroyImmediate(busObject);
        }
    }
}
