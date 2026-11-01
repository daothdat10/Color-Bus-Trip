using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputClick : MonoBehaviour
{
    [SerializeField] private Camera camera;
    void Update()
    {
        if (LevelControl.Instance != null && LevelControl.Instance.IsGameOver)
        {
            return;
        }

        if (camera == null)
        {
            camera = Camera.main;
            if (camera == null)
            {
                return;
            }
        }

        Ray ray = camera.ScreenPointToRay(Input.mousePosition);
        
        if (Input.GetMouseButtonDown(0) && (UnityEngine.EventSystems.EventSystem.current == null || !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()))
        {
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit,100000))
            {
                Debug.DrawLine(ray.origin, hit.point, Color.red, 1f);
                BusController bus = hit.collider.GetComponentInParent<BusController>();
                ManagerBoosters managerBoosters = ManagerBoosters.GetActiveManager();
                if (bus != null && managerBoosters != null && managerBoosters.isActiveAndEnabled && managerBoosters.HandleBusClick(bus)) { }
                else if (bus != null && bus.IsSelectable)
                {
                    bus.MoveBus();
                }
            }
        }
        LevelControl levelControl = LevelControl.Instance;
        if (levelControl == null)
        {
            return;
        }

        List<BusController> buses = levelControl.GetListBus();
        if (buses == null)
        {
            return;
        }

        for (int i = 0; i < buses.Count; i++)
        {
            BusController bus = buses[i];
            if (bus != null && bus.isMove && bus.IsRay)
            {
                bus.RayBus();
            }
        }
    }
 }

   

