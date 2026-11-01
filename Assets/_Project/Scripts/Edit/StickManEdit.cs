using UnityEngine;

public class StickManEdit : MonoBehaviour
{
    [SerializeField] private MeshRenderer meshStickMan;
    [SerializeField, HideInInspector] private BusEdit currentBus;
    [SerializeField, HideInInspector] private int seatIndex = -1;
    [SerializeField, HideInInspector] private int colorIndex = -1;

    public ColorData colorStickMan;

    public BusEdit CurrentBus => currentBus;
    public int SeatIndex => seatIndex;
    public int ColorIndex => colorIndex;

    public void SetSeat(BusEdit bus, int index, int colorIdx)
    {
        currentBus = bus;
        seatIndex = index;
        ChangeColor(colorIdx);
    }

    public void ClearSeat()
    {
        currentBus = null;
        seatIndex = -1;
    }

    public void ChangeColor(int idx)
    {
        colorIndex = Mathf.Max(0, idx);
        if (meshStickMan == null)
        {
            meshStickMan = GetComponentInChildren<MeshRenderer>();
        }

        if (meshStickMan == null || colorStickMan == null || colorStickMan.matColor == null || colorStickMan.matColor.Length == 0)
        {
            return;
        }

        colorIndex = Mathf.Clamp(colorIndex, 0, colorStickMan.matColor.Length - 1);
        Material material = colorStickMan.matColor[colorIndex];
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            meshStickMan.material = material;
        }
        else
        {
            meshStickMan.sharedMaterial = material;
        }
    }

    public void SetLayer(int layer)
    {
        gameObject.layer = layer;
    }

    private void OnMouseDown()
    {
#if UNITY_EDITOR
        ToolLevel toolLevel = ToolLevel.ActiveInstance;
        if (toolLevel != null)
        {
            toolLevel.TryDeleteStickMan(this);
        }
#endif
    }

}
