using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public sealed class RoadContactShadowLink : MonoBehaviour
{
    [SerializeField] private Material roadMaterial;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;

    private static readonly int OutsideColorId = Shader.PropertyToID("_OutsideShadowColor");
    private static readonly int InsideColorId = Shader.PropertyToID("_InsideShadowColor");
    private static readonly int OutsideStrengthId = Shader.PropertyToID("_OutsideShadowStrength");
    private static readonly int InsideStrengthId = Shader.PropertyToID("_InsideShadowStrength");
    private static readonly int OutsideWidthId = Shader.PropertyToID("_OutsideShadowWidth");
    private static readonly int InsideWidthId = Shader.PropertyToID("_InsideShadowWidth");
    private static readonly int InsideOffsetId = Shader.PropertyToID("_InsideShadowOffset");

    public void Configure(Material material)
    {
        roadMaterial = material;
        Sync();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += SyncInEditor;
#endif
        Sync();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= SyncInEditor;
#endif
    }

    private void OnValidate()
    {
        Sync();
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Sync();
        }
    }

#if UNITY_EDITOR
    private void SyncInEditor()
    {
        if (!Application.isPlaying)
        {
            Sync();
        }
    }
#endif

    private void Sync()
    {
        if (roadMaterial == null)
        {
            return;
        }

        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
        if (meshRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(OutsideColorId,
            roadMaterial.GetColor("_RoadEdgeShadeColor"));
        propertyBlock.SetColor(InsideColorId,
            roadMaterial.GetColor("_RoadInsideShadowColor"));
        propertyBlock.SetFloat(OutsideStrengthId,
            roadMaterial.GetFloat("_RoadEdgeShadeStrength"));
        // The inside control shades the tan road surface, not the grass island.
        propertyBlock.SetFloat(InsideStrengthId, 0f);
        propertyBlock.SetFloat(OutsideWidthId,
            roadMaterial.GetFloat("_RoadOutsideShadowWidth"));
        propertyBlock.SetFloat(InsideWidthId,
            roadMaterial.GetFloat("_RoadEdgeShadeWidth"));
        propertyBlock.SetFloat(InsideOffsetId,
            roadMaterial.GetFloat("_RoadInsideShadowOffset"));
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
