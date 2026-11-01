using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIInGame : ScreenUI
{
    [SerializeField] private ManagerBoosters managerBoosters;

    public ManagerBoosters ManagerBoosters => managerBoosters;

    protected override void OnScreenDestroyed()
    {
        throw new System.NotImplementedException();
    }
}
