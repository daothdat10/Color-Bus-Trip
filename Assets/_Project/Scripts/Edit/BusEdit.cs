using System;
using System.Collections.Generic;
using UnityEngine;

public class BusEdit : MonoBehaviour
{
    public const int DefaultSlotBus = 16;
    private const float MaxSeatPositionMagnitude = 1000f;

    [Serializable]
    public class StickManSeatState
    {
        public int seatIndex;
        public bool occupied;
        public int colorIndex = -1;
        public StickManEdit stickMan;
    }

    [SerializeField] private ColorData colorData;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private GridEdit currentGrid;
    [SerializeField, Min(0)] private int colorIndex;
    [SerializeField] private Transform trfParent;
    [SerializeField, HideInInspector] private int slotBus = DefaultSlotBus;

    [Min(0)]
    public int slotBusPerson;

    [Min(0)]
    public int slotEmpty = DefaultSlotBus;

    [SerializeField] private Vector3[] trfStickmans;
    [SerializeField] private StickManEdit stickmanEdit;
    [SerializeField] private List<StickManSeatState> seatStates = new List<StickManSeatState>();

    public int ColorIndex => colorIndex;
    public int SlotBus => slotBus;
    public int SlotBusPerson => slotBusPerson;
    public int SlotEmpty => slotEmpty;
    public GridEdit CurrentGrid => currentGrid;
    public IReadOnlyList<StickManSeatState> SeatStates => seatStates;

    public void SetGrid(GridEdit grid)
    {
        currentGrid = grid;
    }

    public void BusData(int idxColor, int _slotBus, int _slotPerson)
    {
        ApplyData(idxColor, _slotPerson);
    }

    public void BusData(int idxColor, int _slotPerson)
    {
        ApplyData(idxColor, _slotPerson);
    }

    public void ApplyData(int idxColor, int totalSeats, int occupiedSeats)
    {
        ApplyData(idxColor, occupiedSeats);
    }

    public void ApplyData(int idxColor, int occupiedSeats)
    {
        colorIndex = ClampColorIndex(idxColor);
        slotBus = DefaultSlotBus;
        EnsureSeatStates();
        SyncSeatStateFromChildren();
        RefreshSeatData();
        ApplyColor();
    }

    public void ChangeColor(int idxColor)
    {
        colorIndex = ClampColorIndex(idxColor);
        ApplyColor();
    }

    public void SaveSeatState(int idxColor)
    {
        colorIndex = ClampColorIndex(idxColor);
        EnsureSeatStates();
        SyncSeatStateFromChildren();
        RefreshSeatData();
        ApplyColor();
    }

    public int SpawnStickMan(int number, int colorIdx)
    {
        EnsureSeatStates();
        SyncSeatStateFromChildren();

        if (stickmanEdit == null || trfStickmans == null || trfStickmans.Length == 0)
        {
            return 0;
        }

        int requestedCount = Mathf.Max(0, number);
        int safeColorIndex = ClampColorIndex(colorIdx);
        int spawnedCount = 0;
        int seatLimit = Mathf.Min(DefaultSlotBus, trfStickmans.Length);

        for (int i = 0; i < seatLimit && spawnedCount < requestedCount; i++)
        {
            if (!IsSeatEmpty(i))
            {
                continue;
            }

            if (CreateStickManAtSeat(i, safeColorIndex))
            {
                spawnedCount++;
            }
        }

        RefreshSeatData();
        return spawnedCount;
    }

    public bool SpawnStickManAtSeat(int seatIndex, int colorIdx)
    {
        EnsureSeatStates();
        SyncSeatStateFromChildren();

        if (!IsValidSeatIndex(seatIndex) || !IsSeatEmpty(seatIndex))
        {
            return false;
        }

        bool created = CreateStickManAtSeat(seatIndex, ClampColorIndex(colorIdx));
        RefreshSeatData();
        return created;
    }

    public bool RemoveStickMan(StickManEdit stickMan)
    {
        if (stickMan == null)
        {
            return false;
        }

        EnsureSeatStates();
        SyncSeatStateFromChildren();

        int seatIndex = stickMan.SeatIndex;
        if (!IsValidSeatIndex(seatIndex) || seatStates[seatIndex].stickMan != stickMan)
        {
            seatIndex = FindSeatIndex(stickMan);
        }

        if (!IsValidSeatIndex(seatIndex))
        {
            return false;
        }

        ClearSeat(seatIndex, destroyStickMan: true);
        RefreshSeatData();
        return true;
    }

    public bool IsSeatOccupied(int seatIndex)
    {
        EnsureSeatStates();
        return IsValidSeatIndex(seatIndex) && seatStates[seatIndex].occupied;
    }

    private void Awake()
    {
        EnsureSeatStates();
        SyncSeatStateFromChildren();
        RefreshSeatData();
    }

    private void OnValidate()
    {
        colorIndex = ClampColorIndex(colorIndex);
        slotBus = DefaultSlotBus;
        EnsureSeatStates();
        RefreshSeatData();
        ApplyColor();
    }

    private void EnsureSeatStates()
    {
        if (seatStates == null)
        {
            seatStates = new List<StickManSeatState>();
        }

        for (int i = 0; i < seatStates.Count; i++)
        {
            if (seatStates[i] == null)
            {
                seatStates[i] = new StickManSeatState();
            }

            seatStates[i].seatIndex = i;
        }

        while (seatStates.Count < DefaultSlotBus)
        {
            seatStates.Add(new StickManSeatState
            {
                seatIndex = seatStates.Count
            });
        }

        while (seatStates.Count > DefaultSlotBus)
        {
            seatStates.RemoveAt(seatStates.Count - 1);
        }
    }

    private void SyncSeatStateFromChildren()
    {
        EnsureSeatStates();

        for (int i = 0; i < seatStates.Count; i++)
        {
            StickManEdit stickMan = seatStates[i].stickMan;
            if (stickMan == null || !stickMan.gameObject.activeSelf)
            {
                MarkSeatEmpty(i);
                continue;
            }

            seatStates[i].occupied = true;
            seatStates[i].colorIndex = Mathf.Max(0, stickMan.ColorIndex);
        }

        StickManEdit[] stickMans = GetComponentsInChildren<StickManEdit>(true);
        for (int i = 0; i < stickMans.Length; i++)
        {
            StickManEdit stickMan = stickMans[i];
            if (stickMan == null || stickMan == stickmanEdit || !stickMan.gameObject.activeSelf)
            {
                continue;
            }

            int seatIndex = FindSeatIndexForStickMan(stickMan);
            if (!IsValidSeatIndex(seatIndex))
            {
                continue;
            }

            int safeColorIndex = stickMan.ColorIndex >= 0 ? stickMan.ColorIndex : colorIndex;
            AssignStickManToSeat(stickMan, seatIndex, safeColorIndex);
        }
    }

    private void RefreshSeatData()
    {
        EnsureSeatStates();
        slotBus = DefaultSlotBus;

        int occupiedCount = 0;
        for (int i = 0; i < seatStates.Count; i++)
        {
            if (seatStates[i].occupied && seatStates[i].stickMan != null)
            {
                occupiedCount++;
            }
            else
            {
                MarkSeatEmpty(i);
            }
        }

        slotBusPerson = Mathf.Clamp(occupiedCount, 0, DefaultSlotBus);
        slotEmpty = Mathf.Max(0, DefaultSlotBus - slotBusPerson);
    }

    private bool IsSeatEmpty(int seatIndex)
    {
        return IsValidSeatIndex(seatIndex) && (!seatStates[seatIndex].occupied || seatStates[seatIndex].stickMan == null);
    }

    private bool IsValidSeatIndex(int seatIndex)
    {
        return seatIndex >= 0 && seatIndex < seatStates.Count;
    }

    private int FindSeatIndex(StickManEdit stickMan)
    {
        for (int i = 0; i < seatStates.Count; i++)
        {
            if (seatStates[i].stickMan == stickMan)
            {
                return i;
            }
        }

        return -1;
    }

    private int FindSeatIndexForStickMan(StickManEdit stickMan)
    {
        int seatIndex = stickMan.SeatIndex;
        if (stickMan.CurrentBus == this && IsValidSeatIndex(seatIndex) && (IsSeatEmpty(seatIndex) || seatStates[seatIndex].stickMan == stickMan))
        {
            return seatIndex;
        }

        int nearestEmptySeat = -1;
        float nearestDistance = float.MaxValue;
        int seatLimit = trfStickmans == null ? 0 : Mathf.Min(DefaultSlotBus, trfStickmans.Length);

        for (int i = 0; i < seatLimit; i++)
        {
            if (!IsSeatEmpty(i) || !HasValidSeatPosition(i))
            {
                continue;
            }

            float distance = (stickMan.transform.localPosition - trfStickmans[i]).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEmptySeat = i;
            }
        }

        return nearestEmptySeat;
    }

    private bool CreateStickManAtSeat(int seatIndex, int colorIdx)
    {
        if (!HasValidSeatPosition(seatIndex))
        {
            return false;
        }

        StickManEdit stickMan = Instantiate(stickmanEdit, trfParent);
        stickMan.gameObject.SetActive(true);
        stickMan.transform.localPosition = trfStickmans[seatIndex];
        stickMan.transform.localRotation = stickmanEdit.transform.localRotation;
        stickMan.transform.localScale = stickmanEdit.transform.localScale;
        stickMan.gameObject.name = $"Stickman_Seat_{seatIndex:00}_Color_{colorIdx}";
        AssignStickManToSeat(stickMan, seatIndex, colorIdx);
        return true;
    }

    private bool HasValidSeatPosition(int seatIndex)
    {
        if (trfStickmans == null || seatIndex < 0 || seatIndex >= trfStickmans.Length)
        {
            return false;
        }

        Vector3 position = trfStickmans[seatIndex];
        return IsFinite(position.x)
            && IsFinite(position.y)
            && IsFinite(position.z)
            && position.sqrMagnitude <= MaxSeatPositionMagnitude * MaxSeatPositionMagnitude;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private void AssignStickManToSeat(StickManEdit stickMan, int seatIndex, int colorIdx)
    {
        if (stickMan == null || !IsValidSeatIndex(seatIndex))
        {
            return;
        }

        StickManSeatState seatState = seatStates[seatIndex];
        seatState.occupied = true;
        seatState.colorIndex = colorIdx;
        seatState.stickMan = stickMan;
        stickMan.SetSeat(this, seatIndex, colorIdx);
    }

    private void ClearSeat(int seatIndex, bool destroyStickMan)
    {
        if (!IsValidSeatIndex(seatIndex))
        {
            return;
        }

        StickManEdit stickMan = seatStates[seatIndex].stickMan;
        MarkSeatEmpty(seatIndex);

        if (stickMan == null)
        {
            return;
        }

        stickMan.ClearSeat();
        if (!destroyStickMan)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(stickMan.gameObject);
        }
        else
        {
            DestroyImmediate(stickMan.gameObject);
        }
    }

    private void MarkSeatEmpty(int seatIndex)
    {
        if (!IsValidSeatIndex(seatIndex))
        {
            return;
        }

        seatStates[seatIndex].occupied = false;
        seatStates[seatIndex].colorIndex = -1;
        seatStates[seatIndex].stickMan = null;
    }

    private int ClampColorIndex(int idxColor)
    {
        int safeIndex = Mathf.Max(0, idxColor);
        if (colorData != null && colorData.matColor != null && colorData.matColor.Length > 0)
        {
            safeIndex = Mathf.Clamp(safeIndex, 0, colorData.matColor.Length - 1);
        }

        return safeIndex;
    }

    private void ApplyColor()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponentInChildren<MeshRenderer>();
        }

        if (meshRenderer == null || colorData == null || colorData.matColor == null || colorData.matColor.Length == 0)
        {
            return;
        }

        Material material = colorData.matColor[colorIndex];
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            meshRenderer.material = material;
        }
        else
        {
            meshRenderer.sharedMaterial = material;
        }
    }
}
