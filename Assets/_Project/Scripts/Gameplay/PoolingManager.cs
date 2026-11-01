using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolingManager : MonoBehaviour
{
    public static PoolingManager Instance { get; private set; }

    [SerializeField] private BusController preBus;


    [SerializeField] private WaitController preWait;

    [SerializeField] private StickManControl preStickMan;

    [SerializeField] private List<BusController> listBus = new List<BusController>();
    [SerializeField] private List<WaitController> listWait = new List<WaitController>();
    public List<StickManControl> listStickMan = new List<StickManControl>();


    private void Awake()
    {
        Instance = this;
    }

    public BusController SpawnBus(Transform _trfBus)
    {
        for (int i = 0; i < listBus.Count; i++)
        {
            if (listBus[i] != null && !listBus[i].gameObject.activeSelf)
            {
                listBus[i].transform.SetParent(_trfBus);
                // A previous board can fit the bus grid to a smaller parking bay.
                // Start from the prefab scale before fitting the next level.
                listBus[i].transform.localScale = preBus.transform.localScale;
                listBus[i].gameObject.SetActive(true);
                return listBus[i];
            }
        }

        BusController bus = Instantiate(preBus, _trfBus);
        listBus.Add(bus);
        return bus;
    }

    public WaitController SpawnWait(Transform _trfWait)
    {
        for (int i = 0; i < listWait.Count; i++)
        {
            if (listWait[i] != null && !listWait[i].gameObject.activeSelf)
            {
                listWait[i].transform.SetParent(_trfWait);
                listWait[i].gameObject.SetActive(true);
                return listWait[i];
            }
        }

        WaitController wait = Instantiate(preWait, _trfWait);
        listWait.Add(wait);
        return wait;

    }

    public StickManControl SpawnStickMan(Transform _trfStick)
    {
        for (int i = 0; i < listStickMan.Count; i++)
        {
            if (listStickMan[i] != null && !listStickMan[i].gameObject.activeSelf)
            {
                listStickMan[i].transform.SetParent(_trfStick);
                listStickMan[i].gameObject.SetActive(true);
                return listStickMan[i];
            }
        }
        StickManControl stickManControl = Instantiate(preStickMan, _trfStick);
        listStickMan.Add(stickManControl);
        return stickManControl;

    }
}
