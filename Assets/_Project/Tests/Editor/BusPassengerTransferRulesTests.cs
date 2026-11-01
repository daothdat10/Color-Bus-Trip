using System.Collections.Generic;
using NUnit.Framework;

public sealed class BusPassengerTransferRulesTests
{
    [Test]
    public void EmptyWaitPrefersColorNeededByBoardBusOverMostCommonColor()
    {
        int chosen = BusPassengerTransferRules.ChooseColorForEmptyWait(
            new[] { 2, 2, 2, 5 }, new HashSet<int> { 5 });

        Assert.That(chosen, Is.EqualTo(5));
    }

    [Test]
    public void EmptyWaitFallsBackToMostCommonColorAndFirstReachedWinsTies()
    {
        Assert.That(BusPassengerTransferRules.ChooseColorForEmptyWait(
            new[] { 4, 2, 2, 7 }, new HashSet<int>()), Is.EqualTo(2));
        Assert.That(BusPassengerTransferRules.ChooseColorForEmptyWait(
            new[] { 4, 2, 4, 2 }, new HashSet<int>()), Is.EqualTo(4));
    }

    [Test]
    public void EmptyWaitHasNoColorWithoutCandidates()
    {
        Assert.That(BusPassengerTransferRules.ChooseColorForEmptyWait(
            new int[0], new HashSet<int> { 1 }), Is.EqualTo(-1));
    }
}
