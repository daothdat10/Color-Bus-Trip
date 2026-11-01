using System.Collections.Generic;

/// <summary>Tracks a bus's passengers, occupied seats, and seat reservations.</summary>
internal sealed class BusPassengerRoster
{
    private readonly Dictionary<int, StickManControl> seatedPassengers =
        new Dictionary<int, StickManControl>();
    private readonly List<StickManControl> passengers = new List<StickManControl>();

    public IReadOnlyList<StickManControl> Passengers => passengers;
    public IEnumerable<StickManControl> SeatedPassengers => seatedPassengers.Values;
    public int OccupiedSeatCount => seatedPassengers.Count;
    public int PassengerCount => passengers.Count;

    public bool IsSeatOccupied(int seatIndex)
    {
        return seatedPassengers.ContainsKey(seatIndex);
    }

    public void AddSeatedPassenger(int seatIndex, StickManControl passenger)
    {
        seatedPassengers.Add(seatIndex, passenger);
        AddPassenger(passenger);
    }

    public void AddPassenger(StickManControl passenger)
    {
        if (passenger != null && !passengers.Contains(passenger))
        {
            passengers.Add(passenger);
        }
    }

    public bool RemovePassenger(StickManControl passenger)
    {
        if (passenger == null || !passengers.Remove(passenger))
        {
            return false;
        }

        int occupiedSeatIndex = -1;
        foreach (KeyValuePair<int, StickManControl> seat in seatedPassengers)
        {
            if (seat.Value == passenger)
            {
                occupiedSeatIndex = seat.Key;
                break;
            }
        }

        if (occupiedSeatIndex >= 0)
        {
            seatedPassengers.Remove(occupiedSeatIndex);
        }

        return true;
    }

    public bool TryGetEmptySeat(int seatLimit, out int seatIndex)
    {
        for (int i = 0; i < seatLimit; i++)
        {
            if (!seatedPassengers.ContainsKey(i))
            {
                seatIndex = i;
                return true;
            }
        }

        seatIndex = -1;
        return false;
    }

    public bool TryReserveEmptySeat(StickManControl passenger, int seatLimit, out int seatIndex)
    {
        if (passenger != null && TryGetEmptySeat(seatLimit, out seatIndex))
        {
            seatedPassengers.Add(seatIndex, passenger);
            return true;
        }

        seatIndex = -1;
        return false;
    }

    public bool AllPassengersMatchColor(int colorId)
    {
        for (int i = 0; i < passengers.Count; i++)
        {
            StickManControl passenger = passengers[i];
            if (passenger == null || passenger.GetColorId != colorId)
            {
                return false;
            }
        }

        return true;
    }

    public void Clear()
    {
        seatedPassengers.Clear();
        passengers.Clear();
    }
}
