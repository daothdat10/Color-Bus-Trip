using System.Collections.Generic;
using UnityEngine;

public class WaitingPointEdit : MonoBehaviour
{
    [SerializeField, Min(-1)] private int colorIndex = -1;
    [SerializeField] private int direction = 1;
    [SerializeField] private List<int> stickManColorIndices = new List<int>();
    [SerializeField, HideInInspector] private Material defaultWaitMaterial;

    public int ColorIndex => colorIndex;
    public int Direction => NormalizeDirection(direction);
    public IReadOnlyList<int> StickManColorIndices => stickManColorIndices;

    public void SetDirection(int value)
    {
        direction = NormalizeDirection(value);
    }

    public void SetStickManColors(IList<StickManData> stickMans)
    {
        EnsureStickManColorList();
        stickManColorIndices.Clear();
        if (stickMans == null)
        {
            return;
        }

        for (int i = 0; i < stickMans.Count; i++)
        {
            if (stickMans[i] != null)
            {
                stickManColorIndices.Add(Mathf.Max(0, stickMans[i].colorId));
            }
        }
    }

    public bool SetStickManColor(int index, int value)
    {
        EnsureStickManColorList();
        if (index < 0 || index >= stickManColorIndices.Count)
        {
            return false;
        }

        stickManColorIndices[index] = Mathf.Max(0, value);
        return true;
    }

    public void AddStickManColor(int value)
    {
        EnsureStickManColorList();
        stickManColorIndices.Add(Mathf.Max(0, value));
    }

    public bool RemoveStickManColorAt(int index)
    {
        EnsureStickManColorList();
        if (index < 0 || index >= stickManColorIndices.Count)
        {
            return false;
        }

        stickManColorIndices.RemoveAt(index);
        return true;
    }

    public void SetColor(int idxColor, ColorData colorData)
    {
        CacheDefaultWaitMaterial();
        colorIndex = Mathf.Max(-1, idxColor);
        if (colorIndex == -1)
        {
            ApplyMaterial(defaultWaitMaterial);
            return;
        }

        if (colorData != null && colorData.matColor != null && colorData.matColor.Length > 0)
        {
            colorIndex = Mathf.Clamp(colorIndex, 0, colorData.matColor.Length - 1);
            Material material = colorData.matColor[colorIndex];
            if (material != null)
            {
                ApplyMaterial(material);
                return;
            }
        }

        ApplyFallbackColor(GetFallbackColor(colorIndex));
    }

    private void ApplyMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].GetComponentInParent<StickManEdit>() != null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                renderers[i].material = material;
            }
            else
            {
                renderers[i].sharedMaterial = material;
            }
        }
    }

    private void CacheDefaultWaitMaterial()
    {
        if (defaultWaitMaterial != null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].GetComponentInParent<StickManEdit>() == null && renderers[i].sharedMaterial != null)
            {
                defaultWaitMaterial = renderers[i].sharedMaterial;
                return;
            }
        }
    }

    private void ApplyFallbackColor(Color color)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].GetComponentInParent<StickManEdit>() != null)
            {
                continue;
            }

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderers[i].SetPropertyBlock(block);
        }
    }

    private static Color GetFallbackColor(int index)
    {
        Color[] colors =
        {
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.magenta,
            Color.cyan
        };

        return colors[Mathf.Abs(index) % colors.Length];
    }

    private void OnValidate()
    {
        direction = NormalizeDirection(direction);
        EnsureStickManColorList();
        for (int i = 0; i < stickManColorIndices.Count; i++)
        {
            stickManColorIndices[i] = Mathf.Max(0, stickManColorIndices[i]);
        }
    }

    private void EnsureStickManColorList()
    {
        if (stickManColorIndices == null)
        {
            stickManColorIndices = new List<int>();
        }
    }

    private static int NormalizeDirection(int value)
    {
        return value == -2 || value == 2 || value == 1 || value == -1 ? value : 1;
    }
}
