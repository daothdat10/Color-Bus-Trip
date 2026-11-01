using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnController : MonoBehaviour
{

    private LevelData resource;
    [SerializeField] private Transform trfBus;
    [SerializeField, Min(0f), Tooltip("Distance to move the entire Bus parent down the world Z axis after all buses spawn.")]
    private float busGroupSpawnOffsetZ = 2f;


    [SerializeField] private Transform trfWait;

    public List<BusController> listBuses = new List<BusController>();

    [SerializeField] private List<WaitController> listWaits = new List<WaitController>();
    [SerializeField] private PanelSlot panelSlot;


    public Action InputClick;

    public int idxLevel;

    public PanelSlot PanelSlot => panelSlot;




    public void SetUpLevel(LevelControl levelControl, Action onComplete, LevelData _resource)
    {
        resource = _resource;
        idxLevel = 0;

        if (resource == null || resource.buses == null)
        {
            onComplete?.Invoke();
            return;
        }

        Vector3 busParentStartPosition = trfBus != null ? trfBus.position : Vector3.zero;
        for (int i = 0; i < resource.buses.Count; i++)
        {
            BusController bus = PoolingManager.Instance.SpawnBus(trfBus);
            bus.gameObject.SetActive(true);
            bus.transform.position = levelControl.GetBusSpawnPosition(resource.buses[i]);
            bus.Init(levelControl, resource.buses[i]);
            listBuses.Add(bus);
        }

        for (int busIndex = 0; busIndex < listBuses.Count; busIndex++)
        {
            BusData busData = busIndex < resource.buses.Count
                ? resource.buses[busIndex]
                : null;
            int fallbackColumn = busIndex / Mathf.Max(1, resource.j);
            int fallbackRow = busIndex % Mathf.Max(1, resource.j);
            int column = busData != null && busData.column >= 0
                ? busData.column
                : fallbackColumn;
            int row = busData != null && busData.row >= 0
                ? busData.row
                : fallbackRow;
            if (levelControl.UsesSourceTownLayout && busData != null && busData.usesDefaultPosition)
                MyGameBoardCoordinates.GetRuntimeQueueIndices(busData.column, busData.row, out column, out row);
            listBuses[busIndex].InitGrid(column, row);
        }

        // Move only the Bus parent after the complete grid is spawned so all buses keep their spacing.
        if (trfBus != null)
        {
            trfBus.position = busParentStartPosition + Vector3.back * busGroupSpawnOffsetZ;
        }

        //Spawn Wait
        for (int i = 0; i < resource.waitingPassengerColumns.Count; i++)
        {
            WaitController wait = PoolingManager.Instance.SpawnWait(trfWait);
            wait.gameObject.SetActive(true);
            WaitingPassengerColumnData waiting = resource.waitingPassengerColumns[i];
            wait.ResetImportedLayout();
            Vector3 waitPosition = new Vector3(waiting.posX, waiting.posY, waiting.posZ);
            float stopProgress = float.NaN;
            if (waiting.columnSlotId >= 0 && waiting.columnSlotId < resource.BoardItems.Count &&
                string.Equals(resource.BoardItems[waiting.columnSlotId].type, "Stop", StringComparison.OrdinalIgnoreCase))
            {
                waitPosition = levelControl.ProjectImportedStopPosition(
                    waitPosition,
                    resource.BoardItems[waiting.columnSlotId].data.splineProgressData);
                LevelBoardItemData stop = resource.BoardItems[waiting.columnSlotId].data;
                stopProgress = levelControl.GetSplineControll().ConvertImportedStopProgress(stop.splineProgressData);
                wait.transform.rotation = Quaternion.Euler(0f, -stop.rotation, 0f);
            }
            wait.transform.position = waitPosition;
            levelControl.AlignImportedWaitToGround(wait);
            if (waiting.columnSlotId >= 0 && waiting.columnSlotId < resource.BoardItems.Count)
            {
                LevelBoardItemData stop = resource.BoardItems[waiting.columnSlotId].data;
                if (stop.passengerStopPosition != null)
                {
                    Vector3 passengerAnchor = levelControl.ProjectImportedStopPosition(
                        new Vector3(stop.passengerStopPosition.x, waiting.posY, stop.passengerStopPosition.y),
                        stop.splineProgressData);
                    wait.SetImportedPassengerAnchor(passengerAnchor);
                }
            }
            wait.Init(levelControl, resource.waitingPassengerColumns[i]);
            // Legacy waiting points have no Dreamteck progress. Project them once,
            // from their authored position and cache the result.
            if (float.IsNaN(stopProgress) &&
                !(waiting.columnSlotId >= 0 && waiting.columnSlotId < resource.BoardItems.Count &&
                  string.Equals(resource.BoardItems[waiting.columnSlotId].type, "Stop", StringComparison.OrdinalIgnoreCase)))
                stopProgress = levelControl.GetSplineControll().ProjectStopProgress(waitPosition);
            wait.SetStopProgress(stopProgress);
            // Keep the authored Stop position. A concave conveyor can surround
            // several grass regions; no island polygon may relocate its Stops.
            listWaits.Add(wait);
        }

        levelControl.GetSplineControll().Setup();
        onComplete?.Invoke();
    }

    public List<BusController> GetListBus()
    {
        return listBuses;
    }

 

    public List<WaitController> GetWaitControllers()
    {
        return listWaits;
    }

    
}
