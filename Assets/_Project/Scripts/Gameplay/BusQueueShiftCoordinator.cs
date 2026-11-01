using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Moves queued buses into the gap left by a departing bus.</summary>
internal sealed class BusQueueShiftCoordinator
{
    /// <summary>Stores one follower's reserved queue destination.</summary>
    private sealed class Shift
    {
        public BusController Bus;
        public Vector3 TargetPosition;
        public int TargetColumn;
    }

    private readonly float speed;
    private readonly float minimumDuration;
    private readonly float minimumClearDistance;
    private readonly float followStartDelay;

    public BusQueueShiftCoordinator(
        float speed,
        float minimumDuration,
        float minimumClearDistance,
        float followStartDelay)
    {
        this.speed = speed;
        this.minimumDuration = minimumDuration;
        this.minimumClearDistance = minimumClearDistance;
        this.followStartDelay = followStartDelay;
    }

    public IEnumerator ShiftAfterDeparture(
        BusController departingBus,
        IList<BusController> buses)
    {
        if (departingBus == null || buses == null)
        {
            yield break;
        }

        Vector3 nextQueuePosition = departingBus.transform.position;
        int nextQueueColumn = departingBus.i;
        List<BusController> queuedBuses = new List<BusController>();
        for (int i = 0; i < buses.Count; i++)
        {
            BusController candidate = buses[i];
            if (candidate == null || candidate == departingBus ||
                candidate.j != departingBus.j || candidate.i <= departingBus.i ||
                candidate.isMove || candidate.isExit ||
                candidate.IsOnSplineConveyor)
            {
                continue;
            }

            queuedBuses.Add(candidate);
        }

        queuedBuses.Sort((a, b) => a.i.CompareTo(b.i));
        List<Shift> shifts = new List<Shift>(queuedBuses.Count);
        for (int i = 0; i < queuedBuses.Count; i++)
        {
            BusController queuedBus = queuedBuses[i];
            Vector3 oldQueuePosition = queuedBus.transform.position;
            int oldQueueColumn = queuedBus.i;
            shifts.Add(new Shift
            {
                Bus = queuedBus,
                TargetPosition = nextQueuePosition,
                TargetColumn = nextQueueColumn
            });

            queuedBus.i = nextQueueColumn;
            queuedBus.ReserveQueueShift();
            nextQueuePosition = oldQueuePosition;
            nextQueueColumn = oldQueueColumn;
        }

        if (shifts.Count == 0)
        {
            yield break;
        }

        Shift firstShift = shifts[0];
        float queueCellDistance = firstShift.Bus == null
            ? 0f
            : Vector3.Distance(firstShift.Bus.transform.position, firstShift.TargetPosition);
        float requiredClearDistance = Mathf.Min(minimumClearDistance, queueCellDistance);
        while (departingBus != null &&
               Vector3.Distance(departingBus.transform.position, firstShift.TargetPosition) <
               requiredClearDistance)
        {
            yield return null;
        }

        if (departingBus == null)
        {
            ReleaseReservations(shifts);
            yield break;
        }

        departingBus.MarkQueueSlotVacated();

        float longestQueueStep = 0f;
        for (int i = 0; i < shifts.Count; i++)
        {
            Shift shift = shifts[i];
            if (shift.Bus != null)
            {
                longestQueueStep = Mathf.Max(
                    longestQueueStep,
                    Vector3.Distance(shift.Bus.transform.position, shift.TargetPosition));
            }
        }

        float duration = Mathf.Max(minimumDuration, longestQueueStep / Mathf.Max(0.1f, speed));
        for (int i = 0; i < shifts.Count; i++)
        {
            Shift shift = shifts[i];
            if (shift.Bus == null)
            {
                continue;
            }

            shift.Bus.i = shift.TargetColumn;
            shift.Bus.ShiftQueuePosition(shift.TargetPosition, duration);
            if (i < shifts.Count - 1)
            {
                float safeFollowDelay = Mathf.Min(followStartDelay, duration * 0.6f);
                if (safeFollowDelay > Mathf.Epsilon)
                {
                    yield return new WaitForSeconds(safeFollowDelay);
                }
                else
                {
                    yield return null;
                }
            }
        }
    }

    private static void ReleaseReservations(List<Shift> shifts)
    {
        for (int i = 0; i < shifts.Count; i++)
        {
            shifts[i].Bus?.CancelQueueShiftReservation();
        }
    }
}
