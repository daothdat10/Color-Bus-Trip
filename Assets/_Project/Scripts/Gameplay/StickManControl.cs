using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StickManControl : MonoBehaviour
{
    [SerializeField] private MeshRenderer stickManMeshRenderer;
    private int Color;
    private Material originalMaterial;
    private Material stencilMaterial;
    
    public int GetColorId => Color;

    public void SetStickManColor(Material color , int data)
    {
        
        Color = data;
        if (stickManMeshRenderer != null)
        {
            originalMaterial = color;
            if (stencilMaterial != null) Destroy(stencilMaterial);
            stencilMaterial = null;
            ApplyPassengerMaterial();
        }
    }

    public void SetLayer(int Layer)
    {
        gameObject.layer = Layer;
    }
    

    private bool ignoresStencil;
    // true means boarding/seated: render normally, without the external mask.
    public void SetSeatedStencil(bool value)
    {
        ignoresStencil = value;
        ApplyPassengerMaterial();
    }
    private void OnTransformParentChanged()
    {
        SetSeatedStencil(GetComponentInParent<BusController>() != null);
    }
    private void ApplyPassengerMaterial()
    {
        if (stickManMeshRenderer == null || originalMaterial == null) return;
        if (ignoresStencil) { stickManMeshRenderer.sharedMaterial = originalMaterial; return; }
        if (stencilMaterial == null)
        {
            Shader shader = Shader.Find("Bus/Toon Passenger Stencil");
            if (shader == null) return;
            stencilMaterial = new Material(originalMaterial) { shader = shader, renderQueue = 2010 };
            stencilMaterial.SetFloat("_BusStencilComp", 6); // NotEqual
            stencilMaterial.SetFloat("_BusStencilPass", 0); // Keep
        }
        stickManMeshRenderer.sharedMaterial = stencilMaterial;
    }
    private void OnDestroy() { if (stencilMaterial != null) Destroy(stencilMaterial); }
}
