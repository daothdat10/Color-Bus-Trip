using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameExit : MonoBehaviour
{
    [SerializeField] private GameObject editGo;

    public void PlayAnimExit()
    {
        if (editGo == null)
        {
            return;
        }
        editGo.transform.DOKill();
        editGo.transform
            .DOLocalRotate(new Vector3(0, 0, 90), 0.25f)
            .SetEase(Ease.OutQuad);
    }

public void PlaySitExit()
    {
        if (editGo == null)
        {
            return;
        }

        editGo.transform.DOKill();
        editGo.transform
            .DOLocalRotate(Vector3.zero, 0.25f)
            .SetEase(Ease.OutQuad);
    }
}
