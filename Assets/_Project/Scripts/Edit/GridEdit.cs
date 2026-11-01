using UnityEngine;

public class GridEdit : MonoBehaviour
{
    [SerializeField] private BusEdit currentBus;

    public BusEdit CurrentBus => currentBus;
    public bool HasBus => currentBus != null;

    public void SetBus(BusEdit bus)
    {
        currentBus = bus;
    }

    public void ClearBus(BusEdit bus = null)
    {
        if (bus == null || currentBus == bus)
        {
            currentBus = null;
        }
    }
}
