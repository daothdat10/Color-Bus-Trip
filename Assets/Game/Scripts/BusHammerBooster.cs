using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BusHammerBooster : MonoBehaviour
{
    [SerializeField, Min(0)] private int quantity;
    public const string QuantityPrefsKey = "Bus.Boosters.Hammer.Quantity";
    public int Quantity => quantity;
    private bool isExecuting;
    [SerializeField] private Button btnHammer;
    private void Awake()
    {
        if (btnHammer == null) btnHammer = GetComponent<Button>() ?? GetComponentInChildren<Button>(true);
        if (btnHammer != null) btnHammer.onClick.AddListener(OnBoosterButtonClicked);
        quantity = Mathf.Max(0, PlayerPrefs.GetInt(QuantityPrefsKey, quantity));
        RefreshUI();
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
        PlayerPrefs.SetInt(QuantityPrefsKey, quantity);
        PlayerPrefs.Save();
        RefreshUI();
    }

    public void AddQuantity(int amount)
    {
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

    [SerializeField] private Text _txrNumber;
    [SerializeField] private GameObject goPlus;
    [SerializeField] private GameObject goNumber;
public bool Use(BusController bus) => TryUse(() => {
        var level = LevelControl.Instance;
        return level != null && !level.IsGameOver && bus != null && bus.TryStartHammerBooster();
    });
    private void OnBoosterButtonClicked()
    {
        ManagerBoosters manager = GetComponentInParent<ManagerBoosters>();
        if (manager == null) manager = ManagerBoosters.GetActiveManager();
        manager?.SelectHammer();
    }
    private void OnDestroy()
    {
        if (btnHammer != null) btnHammer.onClick.RemoveListener(OnBoosterButtonClicked);
    }
}
