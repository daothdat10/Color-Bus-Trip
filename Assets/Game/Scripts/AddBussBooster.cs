using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddBussBooster : MonoBehaviour
{
    [SerializeField, Min(0)] private int quantity;
    [SerializeField] private Text txtAddBus;
    public const string QuantityPrefsKey = "Bus.Boosters.AddBus.Quantity";
    public int Quantity => quantity;
    private bool isExecuting;
    [SerializeField] private Button btnAdd;

    private void Awake()
    {
        quantity = Mathf.Max(0, PlayerPrefs.GetInt(QuantityPrefsKey, quantity));
        RefreshUI();
        if (btnAdd != null) btnAdd.onClick.AddListener(OnAddClicked);
    }

    private void OnEnable() => RefreshUI();

    private void OnValidate()
    {
        quantity = Mathf.Max(0, quantity);
        RefreshUI();
    }

    public void SetQuantity(int value)
    {
       
        quantity = Mathf.Max(0, value);
        if (txtAddBus != null) txtAddBus.text = quantity.ToString();
        LevelControl.Instance?.onAdd?.Invoke();
        PlayerPrefs.SetInt(QuantityPrefsKey, quantity);
        PlayerPrefs.Save();
        RefreshUI();
    }

    public void AddQuantity(int amount)
    {
        amount--;
        if (amount > 0) SetQuantity((int)System.Math.Min(int.MaxValue, (long)quantity + amount));

    }

    // Consume only after the gameplay action confirms success.
    public bool TryUse(System.Func<bool> execute)
    {
        if (quantity <= 0 || isExecuting || execute == null) return false;
        isExecuting = true;
        try
        {
            if (!execute()) return false;
            SetQuantity(quantity - 1);
            return true;
        }
        finally { isExecuting = false; }
    }

    public void RefreshUI()
    {
        if (_txrNumber != null) _txrNumber.text = quantity.ToString();
        if (goPlus != null) goPlus.SetActive(quantity == 0);
        if (goNumber != null) goNumber.SetActive(quantity > 0);
    }

[SerializeField] private TextMeshPro _txrNumber;
    [SerializeField] private GameObject goPlus;
    [SerializeField] private GameObject goNumber;
public bool Use() => TryUse(() => {
        var level = LevelControl.Instance;
        if (level == null || level.IsGameOver || level.GetPanelSlot() == null) return false;
        level.GetPanelSlot().IncreaseCapacity();
        return true;
    });
    private void OnAddClicked() => Use();
    private void OnDestroy()
    {
        if (btnAdd != null) btnAdd.onClick.RemoveListener(OnAddClicked);
    }
}
