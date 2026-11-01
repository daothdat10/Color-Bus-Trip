using UnityEngine;

[DefaultExecutionOrder(-100)]
public class CameraFit : MonoBehaviour
{
    [SerializeField] private Camera camBooster;
    [SerializeField] private SpriteRenderer sprCam;
    [SerializeField] private Camera cam;
    [SerializeField] private bool fitOnStart = true;
    [SerializeField, Min(0f)] private float padding = 0f;
    [SerializeField, Range(0.8f, 1f)] private float orthographicSizeMultiplier = 0.95f;

    private void Start()
    {
        if (fitOnStart)
        {
            FitToSprite();
        }
    }

    [ContextMenu("Fit Camera To Sprite")]
    public void FitToSprite()
    {
        if (sprCam == null)
        {
            Debug.LogWarning("CameraFit requires a SpriteRenderer.", this);
            return;
        }

        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null || !cam.orthographic)
        {
            Debug.LogWarning("CameraFit requires an orthographic Camera.", this);
            return;
        }

        FitToBounds(sprCam.bounds, 1.08f);
    }

    public static void ApplyTownSourceCamera(Camera camera, TownLevelPath frame)
    {
        if (camera == null || frame == null) return;
        // Use the exported perspective camera, transformed with the board.
        // Renderer bounds include generated helper meshes and are not camera framing data.
        camera.usePhysicalProperties = false;
        camera.orthographic = false;
        camera.fieldOfView = MyGameBoardCoordinates.CameraFieldOfView;
        camera.transform.SetPositionAndRotation(frame.SourceCameraPosition,
            Quaternion.Euler(MyGameBoardCoordinates.CameraPitchInXZ, 0f, 0f));
        camera.nearClipPlane = .01f * frame.WorldScale;
        camera.farClipPlane = 1000f * frame.WorldScale;
        camera.ResetProjectionMatrix();

        Transform environment = camera.transform.Find("Environment_Town");
        if (environment != null)
        {
            const float artDepth = 60f;
            SpriteRenderer sprite = environment.GetComponent<SpriteRenderer>();
            if (sprite != null && sprite.sprite != null)
            {
                float viewHeight = 2f * artDepth * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
                float scale = viewHeight * camera.aspect * .9f / sprite.sprite.bounds.size.x;
                environment.localScale = Vector3.one * scale;
                float halfHeight = sprite.sprite.bounds.size.y * scale * .5f;
                Vector3 center = new Vector3(0f, viewHeight * .5f - halfHeight, artDepth);
                environment.localPosition = center - sprite.sprite.bounds.center * scale;
            }
        }
        Transform ground = camera.transform.Find("Ground_Town");
        if (ground != null)
        {
            const float depth = 200f;
            float height = 2f * depth * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            ground.localPosition = new Vector3(0, 0, depth);
            ground.localScale = new Vector3(height * camera.aspect * 1.1f, height * 1.1f, 1f);
        }
    }

    private void FitToBounds(Bounds worldBounds, float minimumSizeMultiplier)
    {
        if (cam == null)
        {
            cam = Camera.main;
        }

        if (cam == null || !cam.orthographic)
        {
            Debug.LogWarning("CameraFit requires an orthographic Camera.", this);
            return;
        }

        Vector3 boundsMin = worldBounds.min;
        Vector3 boundsMax = worldBounds.max;

        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                for (int z = 0; z <= 1; z++)
                {
                    Vector3 corner = new Vector3(
                        x == 0 ? boundsMin.x : boundsMax.x,
                        y == 0 ? boundsMin.y : boundsMax.y,
                        z == 0 ? boundsMin.z : boundsMax.z
                    );
                    Vector3 cameraSpaceCorner = cam.transform.InverseTransformPoint(corner);

                    minX = Mathf.Min(minX, cameraSpaceCorner.x);
                    maxX = Mathf.Max(maxX, cameraSpaceCorner.x);
                    minY = Mathf.Min(minY, cameraSpaceCorner.y);
                    maxY = Mathf.Max(maxY, cameraSpaceCorner.y);
                }
            }
        }

        float halfWidth = (maxX - minX) * 0.5f;
        float halfHeight = (maxY - minY) * 0.5f;
        float requiredSize = Mathf.Max(halfHeight, halfWidth / cam.aspect);
        cam.orthographicSize = requiredSize * (1f + padding) *
                               Mathf.Max(orthographicSizeMultiplier, minimumSizeMultiplier);
        camBooster.orthographic = cam.orthographic;
        camBooster.gameObject.SetActive(false);
        Vector3 cameraSpaceCenter = new Vector3(
            (minX + maxX) * 0.5f,
            (minY + maxY) * 0.5f,
            0f
        );
        cam.transform.position += cam.transform.right * cameraSpaceCenter.x +
                                  cam.transform.up * cameraSpaceCenter.y;
    }
    private Camera savedMainCamera;
    private int savedMainMask;
    private bool hasSavedMainMask;

    public void SetCamBooster(int layer, bool isActive)
    {
        if (!isActive)
        {
            RestoreMainCameraMask();
            if (camBooster != null) camBooster.gameObject.SetActive(false);
            return;
        }
        if (camBooster == null) return;
        Camera main = cam != null ? cam : Camera.main;
        if (main == null || main == camBooster) return;
        // Preserve the original configuration, including layers already disabled.
        if (!hasSavedMainMask)
        {
            savedMainCamera = main;
            savedMainMask = main.cullingMask;
            hasSavedMainMask = true;
        }
        main.cullingMask = savedMainMask & ~(1 << layer);
        camBooster.cullingMask = 1 << layer;
        camBooster.gameObject.SetActive(true);
    }
    private void RestoreMainCameraMask()
    {
        if (hasSavedMainMask && savedMainCamera != null)
            savedMainCamera.cullingMask = savedMainMask;
        hasSavedMainMask = false;
        savedMainCamera = null;
    }
    private void OnDisable()
    {
        RestoreMainCameraMask();
        if (camBooster != null) camBooster.gameObject.SetActive(false);
    }
}
