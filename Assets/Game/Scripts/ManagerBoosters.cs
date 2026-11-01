using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ManagerBoosters : PopupUI
{
    [SerializeField] private AddBussBooster addBussBooster;
    [SerializeField] private BusFlyBooster busFlyBooster;
    [SerializeField] private BusHammerBooster busHammerBoosterz;
    private int selection;
    private static ManagerBoosters activeManager;
    private void OnEnable() { activeManager = this; }
    public bool IsSelecting => selection != 0;
    public static ManagerBoosters GetActiveManager()
    {
        if (activeManager != null && activeManager.isActiveAndEnabled) return activeManager;
        if (UIManager.Instance == null) return null;
        ManagerBoosters popup = UIManager.Instance.GetPopupActive<ManagerBoosters>();
        if (popup != null) return popup;
        UIInGame screen = UIManager.Instance.GetScreenActive<UIInGame>();
        if (screen == null || !screen.isActiveAndEnabled) return null;
        ManagerBoosters embedded = screen.ManagerBoosters;
        if (embedded == null) embedded = screen.GetComponentInChildren<ManagerBoosters>();
        return embedded != null && embedded.isActiveAndEnabled ? embedded : null;
    }
    private void Awake()
    {
        if (addBussBooster == null) addBussBooster = GetComponentInChildren<AddBussBooster>(true);
        if (busFlyBooster == null) busFlyBooster = GetComponentInChildren<BusFlyBooster>(true);
        if (busHammerBoosterz == null) busHammerBoosterz = GetComponentInChildren<BusHammerBooster>(true);
    }
    public void UseAddBus() { if (isActiveAndEnabled && selection == 0) addBussBooster?.Use(); }
    private CameraFit boosterCamera;
    private SplineControll pausedSpline;
    private bool HasTarget(bool onSpline)
    {
        LevelControl level = LevelControl.Instance;
        if (level == null || level.IsGameOver) return false;
        var buses = level.GetListBus();
        if (buses == null) return false;
        foreach (BusController bus in buses)
        {
            if (bus == null || !bus.gameObject.activeInHierarchy || bus.isExit) continue;
            if (onSpline ? bus.IsOnSplineConveyor : bus.IsSelectable && !bus.isOut)
                return true;
        }
        return false;
    }
    public void SelectFly()
    {
        if (selection == 1) { CancelSelection(); return; }
        if (!isActiveAndEnabled || busFlyBooster == null || busFlyBooster.Quantity <= 0 || !HasTarget(false)) return;
        CancelSelection();
        selection = 1;
        SetBoosterCamera(9, true);
    }
    public void SelectHammer()
    {
        if (selection == 2) { CancelSelection(); return; }
        if (!isActiveAndEnabled || busHammerBoosterz == null || busHammerBoosterz.Quantity <= 0 || !HasTarget(true)) return;
        CancelSelection();
        selection = 2;
        pausedSpline = LevelControl.Instance.GetSplineControll();
        if (pausedSpline != null) pausedSpline.SetBoosterPaused(true);
        SetBoosterCamera(10, true);
    }
    private void SetBoosterCamera(int layer, bool active)
    {
        if (boosterCamera == null) boosterCamera = FindObjectOfType<CameraFit>();
        if (boosterCamera != null) boosterCamera.SetCamBooster(layer, active);
    }
    public void CancelSelection()
    {
        int previous = selection;
        selection = 0;
        if (pausedSpline != null) pausedSpline.SetBoosterPaused(false);
        pausedSpline = null;
        if (previous != 0) SetBoosterCamera(previous == 1 ? 9 : 10, false);
    }
    private void OnDisable() { CancelSelection(); if (activeManager == this) activeManager = null; }
    public override void Hide() { CancelSelection(); base.Hide(); }
    public bool HandleBusClick(BusController bus)
    {
        if (!isActiveAndEnabled || selection == 0) return false;
        bool success = selection == 1
            ? busFlyBooster != null && busFlyBooster.Use(bus)
            : busHammerBoosterz != null && busHammerBoosterz.Use(bus);
        if (success) CancelSelection();
        return true;
    }
}
