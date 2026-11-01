using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PanelSlot : MonoBehaviour
{
    public const int Capacity = 5;
    [SerializeField] private TextMeshPro _txtSlot;
    private int slot = Capacity;
    private int extraCapacity;
    public int CurrentCapacity => Capacity + extraCapacity;
    public void IncreaseCapacity() { extraCapacity++; GetSlot = slot + 1; }

    public void OnEnable()
    {
        LevelControl.Instance.onAdd += UpSlot;
    }


    public void Init()
    {
        extraCapacity = 0;
        slot = Capacity;
        RefreshText();
    }

    public int GetSlot
    {
        get { return slot; }
        set
        {
            slot = Mathf.Clamp(value, 0, CurrentCapacity);
            RefreshText();
        }
    }

    public void ReSlot()
    {
        GetSlot = slot - 1;
    }
    public void UpSlot()
    {
        GetSlot = slot + 1;
       
    }
    public void AddSlotBooster()
    {
        UpSlot();
        _txtSlot.text = (slot-1) + "/" + GetSlot;
    }

    private void RefreshText()
    {
        if (_txtSlot != null)
        {
            _txtSlot.text = slot + "/" + CurrentCapacity;
        }
    }
}
