using System;
using System.Diagnostics;

public class UIWin : PopupUI
{
    public override void Initialize(UIManager manager)
    {
        base.Initialize(manager);
        GameManager.Instance.NextLevel();
        LevelControl.Instance.isWin += () =>
        {
            Show(() =>
            {
               
              
            });
        };
    }
}
