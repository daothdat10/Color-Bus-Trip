using NUnit.Framework;
using UnityEngine;

public sealed class BusQueueMovementTests
{
    [TestCase(1, 0, 0, 0, false, true)]
    [TestCase(1, 0, 0, 0, true, false)]
    [TestCase(1, 0, 0, 1, false, false)]
    [TestCase(0, 0, 0, 0, false, false)]
    public void QueueGateExplainsWhyBackBusCannotMove(
        int candidateColumn,
        int candidateRow,
        int otherColumn,
        int otherRow,
        bool otherBusIsOut,
        bool expectedBlocked
    )
    {
        Assert.That(
            BusQueueMoveRules.IsBlockedByBusAhead(
                candidateColumn,
                candidateRow,
                otherColumn,
                otherRow,
                otherBusIsOut
            ),
            Is.EqualTo(expectedBlocked)
        );
    }

    [Test]
    public void FollowingBusCanStartAfterFrontLeavesGridBeforeFrontLeavesTray()
    {
        // isOut means the front bus has vacated the grid. It may still be
        // travelling on the main spline and must not need isExit=true.
        bool blocked = BusQueueMoveRules.IsBlockedByBusAhead(
            candidateColumn: 1,
            candidateRow: 0,
            otherColumn: 0,
            otherRow: 0,
            otherBusIsOut: true
        );

        Assert.That(blocked, Is.False);
    }

    [Test]
    public void TrayLossRule_Loses_WhenFiveTrayBusesAreStuck()
    {
        Assert.That(
            TrayLossRule.ShouldLose(5, 5, false, false, false, false, false),
            Is.True
        );
    }

    [TestCase(4, false, false, false)]
    [TestCase(5, true, false, false)]
    [TestCase(5, false, true, false)]
    [TestCase(5, false, false, true)]
    public void TrayLossRule_DoesNotLose_WhenTrayStillHasAWayToFreeSpace(
        int occupiedBusCount,
        bool hasBusResolving,
        bool hasCompletedBus,
        bool hasPassengerTransferOpportunity
    )
    {
        Assert.That(
            TrayLossRule.ShouldLose(
                5,
                occupiedBusCount,
                false,
                false,
                hasBusResolving,
                hasCompletedBus,
                hasPassengerTransferOpportunity
            ),
            Is.False
        );
    }

    [Test]
    public void QueuedBusCanMoveIntoVacatedPosition()
    {
        GameObject busObject = new GameObject("queued-bus");
        try
        {
            BusController bus = busObject.AddComponent<BusController>();
            bus.transform.position = new Vector3(0f, 2f, -9.72f);

            bus.ShiftQueuePosition(new Vector3(0f, 2f, -7.40f), 0f);

            Assert.That(bus.transform.position.z, Is.EqualTo(-7.40f).Within(0.001f));
            Assert.That(bus.IsShiftingQueue, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(busObject);
        }
    }

    [Test]
    public void ReservedQueuedBusCannotBeSelectedUntilItsShiftIsReleased()
    {
        GameObject busObject = new GameObject("reserved-queued-bus");
        try
        {
            BusController bus = busObject.AddComponent<BusController>();

            bus.ReserveQueueShift();

            Assert.That(bus.IsShiftingQueue, Is.True);
            Assert.That(bus.IsSelectable, Is.False);

            bus.CancelQueueShiftReservation();

            Assert.That(bus.IsShiftingQueue, Is.False);
            Assert.That(bus.IsSelectable, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(busObject);
        }
    }

    [Test]
    public void PassengerTransferRules_RejectSameColorPassengerAtWait()
    {
        Assert.That(
            BusPassengerTransferRules.CanUnloadToWait(2, -1, 2),
            Is.False
        );
        Assert.That(
            BusPassengerTransferRules.CanUnloadToWait(2, 1, 2),
            Is.False
        );
    }

    [Test]
    public void PassengerTransferRules_AcceptDifferentColorForUnassignedWait()
    {
        Assert.That(
            BusPassengerTransferRules.CanUnloadToWait(2, -1, 1),
            Is.True
        );
    }

    [TestCase(1, 1, 3, 2, true)]
    [TestCase(1, 2, 3, 2, false)]
    [TestCase(1, 1, 0, 2, false)]
    [TestCase(1, 1, 3, 0, false)]
    public void PassengerTransferRules_CheckBusCapacityAndType(
        int busColor,
        int waitColor,
        int availableSeats,
        int waitingPassengerCount,
        bool expected
    )
    {
        Assert.That(
            BusPassengerTransferRules.CanLoadFromWait(
                busColor,
                waitColor,
                availableSeats,
                waitingPassengerCount
            ),
            Is.EqualTo(expected)
        );
    }

    [TestCase(6, 4, 4)]
    [TestCase(2, 4, 2)]
    [TestCase(0, 4, 0)]
    public void PassengerTransferRules_RespectsAvailableBusSeats(
        int candidateCount,
        int availableSeats,
        int expectedTransferCount
    )
    {
        Assert.That(
            BusPassengerTransferRules.GetTransferCount(
                candidateCount,
                availableSeats
            ),
            Is.EqualTo(expectedTransferCount)
        );
    }
}
