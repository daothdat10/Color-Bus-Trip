using System.Collections.Generic;
using NUnit.Framework;


public sealed class SplineProgressSolverTests
{
    [Test]
    public void AdvanceWrapsLoopProgress()
    {
        float result = SplineProgressSolver.Advance(0.98f, 0.05f, true);

        Assert.That(result, Is.EqualTo(0.03f).Within(0.0001f));
    }

    [Test]
    public void AdvanceClampsNonLoopProgressAtEnd()
    {
        float result = SplineProgressSolver.Advance(0.8f, 0.5f, false);

        Assert.That(result, Is.EqualTo(1f));
    }

    [Test]
    public void BlockedPointStopsBeforePointOnWrappedSegment()
    {
        float result = SplineProgressSolver.ClampAgainstBlockedPoints(
            0.95f,
            0.05f,
            new List<float> { 0.01f },
            true
        );

        Assert.That(result, Is.LessThan(0.01f));
        Assert.That(result, Is.GreaterThan(0.009f));
    }

    [Test]
    public void InvalidInputDoesNotProduceInvalidProgress()
    {
        float result = SplineProgressSolver.Advance(
            float.NaN,
            float.PositiveInfinity,
            true
        );

        Assert.That(float.IsNaN(result), Is.False);
        Assert.That(float.IsInfinity(result), Is.False);
        Assert.That(result, Is.EqualTo(0f));
    }
}
