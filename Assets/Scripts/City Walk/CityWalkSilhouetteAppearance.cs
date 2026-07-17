using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Non-destructively replaces every renderer below a Civil Characters Pack
/// prefab instance with the shared background-silhouette material.
/// Original materials and renderer settings are retained so they can be restored.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Story/City Walk/Civilian Silhouette Appearance")]
public sealed class CityWalkSilhouetteAppearance : MonoBehaviour
{
    private const string DefaultMaterialPath =
        "Assets/Material/CityWalkBackgroundSilhouette.mat";

    [Serializable]
    private sealed class RendererBackup
    {
        public Renderer renderer;
        public Material[] sharedMaterials;
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public MotionVectorGenerationMode motionVectorGenerationMode;
    }

    [Header("Silhouette Material")]
    [SerializeField]
    [Tooltip("Shared material applied to every active and inactive model part, including all LOD levels.")]
    private Material silhouetteMaterial;

    [Header("Renderer Treatment")]
    [SerializeField]
    [Tooltip("Include inactive clothing pieces and inactive LOD objects so later switches stay silhouetted.")]
    private bool includeInactiveRenderers = true;

    [SerializeField]
    [Tooltip("Let dynamic pedestrians cast and receive the main light's realtime shadows.")]
    private bool enableShadows = true;

    [SerializeField]
    [Tooltip("Avoid motion-vector trails from unimportant background pedestrians.")]
    private bool disableMotionVectors = true;

    [SerializeField]
    [Tooltip("Show the final silhouette while arranging pedestrians in the editor.")]
    private bool previewInEditMode = true;

    [SerializeField, HideInInspector]
    private List<RendererBackup> rendererBackups = new List<RendererBackup>();

    [SerializeField, HideInInspector]
    private bool appearanceApplied;

    public Material SilhouetteMaterial => silhouetteMaterial;
    public bool AppearanceApplied => appearanceApplied;

    private void Reset()
    {
#if UNITY_EDITOR
        silhouetteMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
#endif
    }

    private void OnEnable()
    {
        if (Application.isPlaying || previewInEditMode)
        {
            ApplySilhouette();
        }
    }

    private void OnDisable()
    {
        RestoreOriginalAppearance();
    }

    private void OnDestroy()
    {
        RestoreOriginalAppearance();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || !isActiveAndEnabled)
        {
            return;
        }

        EditorApplication.delayCall -= ApplyEditorPreview;
        EditorApplication.delayCall += ApplyEditorPreview;
#endif
    }

#if UNITY_EDITOR
    private void ApplyEditorPreview()
    {
        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        if (previewInEditMode)
        {
            RefreshRendererList();
        }
        else
        {
            RestoreOriginalAppearance();
        }
    }
#endif

    /// <summary>
    /// Assigns the shared material and immediately reapplies the appearance.
    /// </summary>
    public void SetSilhouetteMaterial(Material material)
    {
        silhouetteMaterial = material;
        ApplySilhouette();
    }

    /// <summary>
    /// Applies the silhouette to all cached renderers without touching source assets.
    /// </summary>
    [ContextMenu("Apply Silhouette")]
    public void ApplySilhouette()
    {
        if (silhouetteMaterial == null)
        {
            RestoreOriginalAppearance();
            return;
        }

        if (!appearanceApplied || rendererBackups.Count == 0)
        {
            CaptureRendererState();
        }

        for (int i = 0; i < rendererBackups.Count; i++)
        {
            RendererBackup backup = rendererBackups[i];
            Renderer targetRenderer = backup.renderer;
            if (targetRenderer == null)
            {
                continue;
            }

            int materialSlotCount = Mathf.Max(backup.sharedMaterials.Length, 1);
            Material[] replacementMaterials = new Material[materialSlotCount];
            for (int slot = 0; slot < materialSlotCount; slot++)
            {
                replacementMaterials[slot] = silhouetteMaterial;
            }

            targetRenderer.sharedMaterials = replacementMaterials;

            if (enableShadows)
            {
                targetRenderer.shadowCastingMode = ShadowCastingMode.On;
                targetRenderer.receiveShadows = true;
            }
            else
            {
                targetRenderer.shadowCastingMode = ShadowCastingMode.Off;
                targetRenderer.receiveShadows = false;
            }

            if (disableMotionVectors)
            {
                targetRenderer.motionVectorGenerationMode =
                    MotionVectorGenerationMode.ForceNoMotion;
            }
        }

        appearanceApplied = true;
    }

    /// <summary>
    /// Restores the renderer state captured before the silhouette was applied.
    /// </summary>
    [ContextMenu("Restore Original Appearance")]
    public void RestoreOriginalAppearance()
    {
        if (!appearanceApplied)
        {
            return;
        }

        for (int i = 0; i < rendererBackups.Count; i++)
        {
            RendererBackup backup = rendererBackups[i];
            Renderer targetRenderer = backup.renderer;
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.sharedMaterials = backup.sharedMaterials;
            targetRenderer.shadowCastingMode = backup.shadowCastingMode;
            targetRenderer.receiveShadows = backup.receiveShadows;
            targetRenderer.motionVectorGenerationMode =
                backup.motionVectorGenerationMode;
        }

        appearanceApplied = false;
    }

    /// <summary>
    /// Rebuilds the renderer list after clothing or LOD hierarchy changes.
    /// </summary>
    [ContextMenu("Refresh Renderer List")]
    public void RefreshRendererList()
    {
        RestoreOriginalAppearance();
        rendererBackups.Clear();
        ApplySilhouette();
    }

    private void CaptureRendererState()
    {
        rendererBackups.Clear();

        Renderer[] childRenderers =
            GetComponentsInChildren<Renderer>(includeInactiveRenderers);
        for (int i = 0; i < childRenderers.Length; i++)
        {
            Renderer childRenderer = childRenderers[i];
            rendererBackups.Add(new RendererBackup
            {
                renderer = childRenderer,
                sharedMaterials = childRenderer.sharedMaterials,
                shadowCastingMode = childRenderer.shadowCastingMode,
                receiveShadows = childRenderer.receiveShadows,
                motionVectorGenerationMode =
                    childRenderer.motionVectorGenerationMode
            });
        }
    }
}
