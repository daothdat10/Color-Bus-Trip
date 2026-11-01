using System.Collections.Generic;

/// <summary>
/// Shared transfer predicates used by runtime gameplay and the Level Tool.
/// The tool must validate the same capacity/type rules that the game executes.
/// </summary>
public static class BusPassengerTransferRules
{
    public static bool CanUnloadToWait(
        int busColor,
        int waitColor,
        int passengerColor)
    {
        if (passengerColor == busColor)
        {
            return false;
        }

        return waitColor == -1 || passengerColor == waitColor;
    }

    /// <summary>
    /// BusDataNew's PassengerType_Stop filter, FilterAvailablePassengerTypesForNoneAssign: an
    /// unassigned stop first takes a passenger type that some board bus is going to need, in the
    /// order the passengers are listed, and otherwise the type with the most passengers
    /// (the first one reached wins a tie). Returns -1 when there is no candidate.
    /// </summary>
    public static int ChooseColorForEmptyWait(
        IReadOnlyList<int> candidateColors,
        ICollection<int> boardBusColors)
    {
        if (candidateColors == null || candidateColors.Count == 0)
        {
            return -1;
        }

        for (int i = 0; i < candidateColors.Count; i++)
        {
            if (boardBusColors != null && boardBusColors.Contains(candidateColors[i]))
            {
                return candidateColors[i];
            }
        }

        int bestColor = -1;
        int bestCount = 0;
        for (int i = 0; i < candidateColors.Count; i++)
        {
            int count = CountPassengersOfColor(candidateColors, candidateColors[i]);
            if (count > bestCount)
            {
                bestCount = count;
                bestColor = candidateColors[i];
            }
        }

        return bestColor;
    }

    public static bool CanLoadFromWait(
        int busColor,
        int waitColor,
        int availableSeats,
        int waitingPassengerCount)
    {
        return busColor >= 0 &&
               waitColor >= 0 &&
               busColor == waitColor &&
               availableSeats > 0 &&
               waitingPassengerCount > 0;
    }

    public static int GetTransferCount(
        int candidateCount,
        int availableSeats)
    {
        return UnityEngine.Mathf.Clamp(
            UnityEngine.Mathf.Min(candidateCount, availableSeats),
            0,
            UnityEngine.Mathf.Max(0, candidateCount)
        );
    }

    public static int CountPassengersOfColor(
        IReadOnlyList<int> passengerColors,
        int color)
    {
        if (passengerColors == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < passengerColors.Count; i++)
        {
            if (passengerColors[i] == color)
            {
                count++;
            }
        }

        return count;
    }
}
