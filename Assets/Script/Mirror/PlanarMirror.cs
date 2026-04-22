using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 平面实时镜子。挂在镜面 Quad 上，会用反射相机把主相机画面反射后渲染到镜面材质。
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class PlanarMirror : MonoBehaviour
{
    private const string ReflectionCameraName = "MirrorReflectionCamera";

    [Header("引用")]
    [Tooltip("镜面渲染器。留空时使用当前物体上的 Renderer。")]
    public Renderer mirrorRenderer;
    [Tooltip("主视角摄像机。留空时使用当前正在渲染镜面的摄像机。")]
    public Camera sourceCamera;
    [Tooltip("用于渲染镜中画面的摄像机。建议在场景里放一个禁用的 Camera 后拖到这里。")]
    public Camera reflectionCamera;

    [Header("反射设置")]
    [Tooltip("反射贴图分辨率。数值越高越清晰，但性能消耗越大。")]
    public int textureSize = 1024;
    [Tooltip("自动让反射贴图宽高比匹配镜面 Quad，只影响反射贴图采样清晰度，不改变镜中透视。")]
    public bool matchTextureAspectToMirror = true;
    [Tooltip("关闭自动匹配时使用的反射贴图宽高比。")]
    public float manualTextureAspect = 1.777778f;
    [Tooltip("反射相机裁剪镜面背后物体时的微小偏移，避免闪烁。")]
    public float clipPlaneOffset = 0.05f;
    [Tooltip("反射相机渲染的图层。")]
    public LayerMask reflectionMask = ~0;

    [Header("摄像机参数覆盖")]
    [Tooltip("默认复制主摄像机参数。关闭后使用 Reflection Camera 自己的参数。")]
    public bool copySourceCameraSettings = true;
    public bool overrideProjectionMode;
    public bool orthographic;
    public float orthographicSize = 5f;
    public bool overrideFieldOfView;
    [Range(1f, 179f)] public float fieldOfView = 40f;
    public bool overrideClipPlanes;
    public float nearClipPlane = 0.3f;
    public float farClipPlane = 100f;
    public bool overrideClearFlags;
    public CameraClearFlags clearFlags = CameraClearFlags.Skybox;
    public Color backgroundColor = new Color(0.625f, 0.737f, 0.915f, 0f);
    public bool overrideRenderingPath;
    public RenderingPath renderingPath = RenderingPath.UsePlayerSettings;
    public bool overrideAntialiasing;
    public bool allowHDR;
    public bool allowMSAA = true;

    [Header("画面调整")]
    public Color tint = new Color(0.92f, 0.97f, 1f, 1f);
    [Range(0.1f, 2f)] public float brightness = 1f;

    private static bool isRenderingReflection;

    private RenderTexture reflectionTexture;
    private MaterialPropertyBlock propertyBlock;
    private int currentTextureWidth;
    private int currentTextureHeight;
    private bool ownsReflectionCamera;

    private static readonly int ReflectionTexId = Shader.PropertyToID("_ReflectionTex");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int MirrorViewProjectionId = Shader.PropertyToID("_MirrorVP");
    private static readonly int TintId = Shader.PropertyToID("_Tint");
    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
    private static readonly int UseProjectedUvId = Shader.PropertyToID("_UseProjectedUV");

    private void OnEnable()
    {
        if (mirrorRenderer == null)
        {
            mirrorRenderer = GetComponent<Renderer>();
        }

        EnsureReflectionCameraReference();
        RegisterEditorUpdate();
    }

    private void OnDisable()
    {
        UnregisterEditorUpdate();
        ReleaseResources();
    }

    private void OnValidate()
    {
        textureSize = Mathf.Clamp(textureSize, 128, 4096);
        manualTextureAspect = Mathf.Clamp(manualTextureAspect, 0.1f, 10f);
        clipPlaneOffset = Mathf.Max(0.001f, clipPlaneOffset);
    }

    private void OnWillRenderObject()
    {
        Camera currentCamera = Camera.current;
        if (currentCamera == null)
        {
            return;
        }

        Camera renderCamera = sourceCamera != null ? sourceCamera : currentCamera;
        TryRenderMirror(renderCamera);
    }

    private void TryRenderMirror()
    {
        Camera renderCamera = sourceCamera != null ? sourceCamera : Camera.main;
        TryRenderMirror(renderCamera);
    }

    private void TryRenderMirror(Camera renderCamera)
    {
        if (!enabled || mirrorRenderer == null || isRenderingReflection)
        {
            return;
        }

        if (renderCamera == null || renderCamera == reflectionCamera)
        {
            return;
        }

        RenderMirror(renderCamera);
    }

#if UNITY_EDITOR
    private void RegisterEditorUpdate()
    {
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
    }

    private void UnregisterEditorUpdate()
    {
        EditorApplication.update -= EditorTick;
    }

    private void EditorTick()
    {
        if (Application.isPlaying || this == null)
        {
            return;
        }

        TryRenderMirror();
    }
#else
    private void RegisterEditorUpdate()
    {
    }

    private void UnregisterEditorUpdate()
    {
    }
#endif

    private void RenderMirror(Camera renderCamera)
    {
        EnsureResources();
        if (reflectionCamera == null || reflectionTexture == null)
        {
            return;
        }

        Vector3 mirrorPosition = transform.position;
        Vector3 mirrorNormal = transform.forward;

        if (Vector3.Dot(mirrorNormal, renderCamera.transform.position - mirrorPosition) < 0f)
        {
            mirrorNormal = -mirrorNormal;
        }

        if (copySourceCameraSettings)
        {
            reflectionCamera.CopyFrom(renderCamera);
        }

        ApplyCameraOverrides();

        reflectionCamera.enabled = false;
        reflectionCamera.targetTexture = reflectionTexture;
        if (copySourceCameraSettings)
        {
            // Keep the reflected frustum identical to the viewer camera; texture aspect only affects sampling density.
            reflectionCamera.aspect = renderCamera.aspect;
        }
        reflectionCamera.cullingMask = reflectionMask;
        reflectionCamera.useOcclusionCulling = false;

        // Reflect around the real mirror plane. The offset is applied only to the oblique clip plane below.
        Vector4 reflectionPlane = new Vector4(
            mirrorNormal.x,
            mirrorNormal.y,
            mirrorNormal.z,
            -Vector3.Dot(mirrorNormal, mirrorPosition)
        );

        Matrix4x4 reflectionMatrix = Matrix4x4.zero;
        CalculateReflectionMatrix(ref reflectionMatrix, reflectionPlane);

        Vector3 reflectedCameraPosition = reflectionMatrix.MultiplyPoint(renderCamera.transform.position);
        reflectionCamera.worldToCameraMatrix = renderCamera.worldToCameraMatrix * reflectionMatrix;
        reflectionCamera.transform.position = reflectedCameraPosition;
        reflectionCamera.transform.rotation = Quaternion.LookRotation(
            Vector3.Reflect(renderCamera.transform.forward, mirrorNormal),
            Vector3.Reflect(renderCamera.transform.up, mirrorNormal)
        );

        Vector4 clipPlane = CameraSpacePlane(reflectionCamera, mirrorPosition, mirrorNormal, 1f);
        reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);

        bool oldInvertCulling = GL.invertCulling;
        try
        {
            isRenderingReflection = true;
            GL.invertCulling = true;
            reflectionCamera.Render();
        }
        finally
        {
            GL.invertCulling = oldInvertCulling;
            isRenderingReflection = false;
        }

        ApplyMaterialProperties();
    }

    private void ApplyCameraOverrides()
    {
        if (overrideProjectionMode)
        {
            reflectionCamera.orthographic = orthographic;
            reflectionCamera.orthographicSize = Mathf.Max(0.001f, orthographicSize);
        }

        if (overrideFieldOfView && !reflectionCamera.orthographic)
        {
            reflectionCamera.fieldOfView = fieldOfView;
        }

        if (overrideClipPlanes)
        {
            reflectionCamera.nearClipPlane = Mathf.Max(0.001f, nearClipPlane);
            reflectionCamera.farClipPlane = Mathf.Max(reflectionCamera.nearClipPlane + 0.01f, farClipPlane);
        }

        if (overrideClearFlags)
        {
            reflectionCamera.clearFlags = clearFlags;
            reflectionCamera.backgroundColor = backgroundColor;
        }

        if (overrideRenderingPath)
        {
            reflectionCamera.renderingPath = renderingPath;
        }

        if (overrideAntialiasing)
        {
            reflectionCamera.allowHDR = allowHDR;
            reflectionCamera.allowMSAA = allowMSAA;
        }
    }

    private void EnsureResources()
    {
        int safeTextureSize = Mathf.Clamp(textureSize, 128, 4096);
        float textureAspect = GetDesiredTextureAspect();
        int textureWidth = safeTextureSize;
        int textureHeight = safeTextureSize;

        if (textureAspect >= 1f)
        {
            textureHeight = Mathf.Clamp(Mathf.RoundToInt(safeTextureSize / textureAspect), 128, safeTextureSize);
        }
        else
        {
            textureWidth = Mathf.Clamp(Mathf.RoundToInt(safeTextureSize * textureAspect), 128, safeTextureSize);
        }

        if (reflectionTexture == null ||
            currentTextureWidth != textureWidth ||
            currentTextureHeight != textureHeight)
        {
            ReleaseTexture();

            reflectionTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "Mirror Reflection",
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            currentTextureWidth = textureWidth;
            currentTextureHeight = textureHeight;
        }

        EnsureReflectionCameraReference();

        if (reflectionCamera == null)
        {
            GameObject cameraObject = new GameObject("Mirror Reflection Camera (Runtime)")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            reflectionCamera = cameraObject.AddComponent<Camera>();
            reflectionCamera.enabled = false;
            ownsReflectionCamera = true;
        }
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private float GetDesiredTextureAspect()
    {
        if (!matchTextureAspectToMirror)
        {
            return Mathf.Clamp(manualTextureAspect, 0.1f, 10f);
        }

        float meshWidth = 1f;
        float meshHeight = 1f;
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
            meshWidth = Mathf.Max(0.001f, meshSize.x);
            meshHeight = Mathf.Max(0.001f, meshSize.y);
        }

        Vector3 scale = transform.lossyScale;
        float width = Mathf.Abs(meshWidth * scale.x);
        float height = Mathf.Abs(meshHeight * scale.y);

        if (height <= 0.001f)
        {
            return 1f;
        }

        return Mathf.Clamp(width / height, 0.1f, 10f);
    }

    private float GetReflectionTextureAspect()
    {
        if (reflectionTexture == null || reflectionTexture.height <= 0)
        {
            return GetDesiredTextureAspect();
        }

        return (float)reflectionTexture.width / reflectionTexture.height;
    }

    private void EnsureReflectionCameraReference()
    {
        if (reflectionCamera != null)
        {
            return;
        }

        reflectionCamera = FindSceneReflectionCamera();
        if (reflectionCamera != null)
        {
            return;
        }

        Scene scene = gameObject.scene;
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        GameObject cameraObject = new GameObject(ReflectionCameraName);
        SceneManager.MoveGameObjectToScene(cameraObject, scene);

        Camera templateCamera = sourceCamera != null ? sourceCamera : Camera.main;
        if (templateCamera != null)
        {
            cameraObject.transform.SetPositionAndRotation(
                templateCamera.transform.position,
                templateCamera.transform.rotation
            );
        }

        reflectionCamera = cameraObject.AddComponent<Camera>();
        reflectionCamera.enabled = false;
        ownsReflectionCamera = false;
    }

    private Camera FindSceneReflectionCamera()
    {
        Scene scene = gameObject.scene;
        if (!scene.IsValid())
        {
            return null;
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate != null &&
                candidate.gameObject.scene == scene &&
                candidate.name == ReflectionCameraName)
            {
                return candidate;
            }
        }

        return null;
    }

    private void ApplyMaterialProperties()
    {
        if (mirrorRenderer == null || reflectionCamera == null || reflectionTexture == null)
        {
            return;
        }

        Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true);
        Matrix4x4 mirrorViewProjection = gpuProjection * reflectionCamera.worldToCameraMatrix;

        mirrorRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture(ReflectionTexId, reflectionTexture);
        propertyBlock.SetTexture(MainTexId, reflectionTexture);
        propertyBlock.SetMatrix(MirrorViewProjectionId, mirrorViewProjection);
        propertyBlock.SetColor(TintId, tint);
        propertyBlock.SetFloat(BrightnessId, brightness);
        propertyBlock.SetFloat(UseProjectedUvId, 1f);
        mirrorRenderer.SetPropertyBlock(propertyBlock);

        Material material = mirrorRenderer.sharedMaterial;
        if (material != null)
        {
            material.SetTexture(ReflectionTexId, reflectionTexture);
            material.SetTexture(MainTexId, reflectionTexture);
            material.SetMatrix(MirrorViewProjectionId, mirrorViewProjection);
            material.SetColor(TintId, tint);
            material.SetFloat(BrightnessId, brightness);
            material.SetFloat(UseProjectedUvId, 1f);
        }
    }

    private Vector4 CameraSpacePlane(Camera cam, Vector3 position, Vector3 normal, float sideSign)
    {
        Vector3 offsetPosition = position + normal * clipPlaneOffset;
        Matrix4x4 matrix = cam.worldToCameraMatrix;
        Vector3 cameraPosition = matrix.MultiplyPoint(offsetPosition);
        Vector3 cameraNormal = matrix.MultiplyVector(normal).normalized * sideSign;

        return new Vector4(
            cameraNormal.x,
            cameraNormal.y,
            cameraNormal.z,
            -Vector3.Dot(cameraPosition, cameraNormal)
        );
    }

    private static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMatrix, Vector4 plane)
    {
        reflectionMatrix.m00 = 1f - 2f * plane[0] * plane[0];
        reflectionMatrix.m01 = -2f * plane[0] * plane[1];
        reflectionMatrix.m02 = -2f * plane[0] * plane[2];
        reflectionMatrix.m03 = -2f * plane[3] * plane[0];

        reflectionMatrix.m10 = -2f * plane[1] * plane[0];
        reflectionMatrix.m11 = 1f - 2f * plane[1] * plane[1];
        reflectionMatrix.m12 = -2f * plane[1] * plane[2];
        reflectionMatrix.m13 = -2f * plane[3] * plane[1];

        reflectionMatrix.m20 = -2f * plane[2] * plane[0];
        reflectionMatrix.m21 = -2f * plane[2] * plane[1];
        reflectionMatrix.m22 = 1f - 2f * plane[2] * plane[2];
        reflectionMatrix.m23 = -2f * plane[3] * plane[2];

        reflectionMatrix.m30 = 0f;
        reflectionMatrix.m31 = 0f;
        reflectionMatrix.m32 = 0f;
        reflectionMatrix.m33 = 1f;
    }

    private void ReleaseResources()
    {
        ReleaseTexture();

        if (reflectionCamera != null && ownsReflectionCamera)
        {
            DestroyObject(reflectionCamera.gameObject);
            reflectionCamera = null;
            ownsReflectionCamera = false;
        }
        else if (reflectionCamera != null)
        {
            reflectionCamera.targetTexture = null;
        }
    }

    private void ReleaseTexture()
    {
        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            DestroyObject(reflectionTexture);
            reflectionTexture = null;
            currentTextureWidth = 0;
            currentTextureHeight = 0;
        }
    }

    private static void DestroyObject(Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
