using UnityEngine;

/// <summary>
/// Changes a camera culling mask from Timeline Signal Receiver events.
/// </summary>
public class TimelineCameraLayerVisibility : MonoBehaviour
{
    [Header("Target")]
    public Camera targetCamera;
    public bool useMainCameraIfEmpty = true;

    [Header("Layers")]
    public LayerMask layersToHide;
    public bool hideOnEnable;

    private int originalCullingMask;
    private bool hasOriginalCullingMask;

    private void Reset()
    {
        ResolveCamera();
    }

    private void Awake()
    {
        CacheOriginalCullingMask();
    }

    private void OnEnable()
    {
        CacheOriginalCullingMask();

        if (hideOnEnable)
        {
            HideConfiguredLayers();
        }
    }

    public void HideConfiguredLayers()
    {
        ResolveCamera();
        if (targetCamera == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraLayerVisibility)}] Missing target camera on {name}.", this);
            return;
        }

        CacheOriginalCullingMask();
        targetCamera.cullingMask &= ~layersToHide.value;
    }

    public void ShowConfiguredLayers()
    {
        ResolveCamera();
        if (targetCamera == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraLayerVisibility)}] Missing target camera on {name}.", this);
            return;
        }

        targetCamera.cullingMask |= layersToHide.value;
    }

    public void RestoreOriginalCullingMask()
    {
        ResolveCamera();
        if (targetCamera == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraLayerVisibility)}] Missing target camera on {name}.", this);
            return;
        }

        if (hasOriginalCullingMask)
        {
            targetCamera.cullingMask = originalCullingMask;
        }
    }

    public void HideLayerByName(string layerName)
    {
        SetLayerVisibleByName(layerName, false);
    }

    public void ShowLayerByName(string layerName)
    {
        SetLayerVisibleByName(layerName, true);
    }

    public void HideLayerByIndex(int layerIndex)
    {
        SetLayerVisibleByIndex(layerIndex, false);
    }

    public void ShowLayerByIndex(int layerIndex)
    {
        SetLayerVisibleByIndex(layerIndex, true);
    }

    private void SetLayerVisibleByName(string layerName, bool visible)
    {
        int layerIndex = LayerMask.NameToLayer(layerName);
        if (layerIndex < 0)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraLayerVisibility)}] Layer named '{layerName}' does not exist.", this);
            return;
        }

        SetLayerVisibleByIndex(layerIndex, visible);
    }

    private void SetLayerVisibleByIndex(int layerIndex, bool visible)
    {
        ResolveCamera();
        if (targetCamera == null)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraLayerVisibility)}] Missing target camera on {name}.", this);
            return;
        }

        if (layerIndex < 0 || layerIndex > 31)
        {
            Debug.LogWarning($"[{nameof(TimelineCameraLayerVisibility)}] Layer index {layerIndex} is out of range.", this);
            return;
        }

        CacheOriginalCullingMask();

        int layerMask = 1 << layerIndex;
        if (visible)
        {
            targetCamera.cullingMask |= layerMask;
        }
        else
        {
            targetCamera.cullingMask &= ~layerMask;
        }
    }

    private void CacheOriginalCullingMask()
    {
        ResolveCamera();
        if (targetCamera == null || hasOriginalCullingMask)
        {
            return;
        }

        originalCullingMask = targetCamera.cullingMask;
        hasOriginalCullingMask = true;
    }

    private void ResolveCamera()
    {
        if (targetCamera == null && useMainCameraIfEmpty)
        {
            targetCamera = Camera.main;
        }
    }
}
