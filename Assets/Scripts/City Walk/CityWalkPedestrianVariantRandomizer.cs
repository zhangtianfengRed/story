using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CityWalkSilhouetteAppearance))]
[AddComponentMenu("Story/City Walk/Pedestrian Variant Randomizer")]
public sealed class CityWalkPedestrianVariantRandomizer : MonoBehaviour
{
    [Header("Character Variants")]
    [Tooltip("The prefab shown while editing the scene. It is excluded from the first random choice when alternatives exist.")]
    [SerializeField] private GameObject editorPreviewPrefab;
    [Tooltip("Civil character prefabs that may be spawned at runtime.")]
    [SerializeField] private GameObject[] variantPrefabs = Array.Empty<GameObject>();
    [Tooltip("Optional parent for the runtime character. Leave empty to use this object.")]
    [SerializeField] private Transform visualParent;

    [Header("Randomization Timing")]
    [SerializeField] private bool randomizeOnAwake = true;
    [SerializeField] private bool randomizeAtRouteBoundaries = true;
    [SerializeField] private bool avoidImmediateRepeat = true;
    [SerializeField] private bool hideEditorPreviewAtRuntime = true;

    [Header("Spawn Alignment")]
    [SerializeField] private Vector3 localPositionOffset;
    [SerializeField] private Vector3 localEulerOffset;
    [Min(0.01f)]
    [SerializeField] private float localScaleMultiplier = 1f;

    private CityWalkPedestrianPatrol patrol;
    private CityWalkSilhouetteAppearance silhouetteAppearance;
    private RendererState[] previewRendererStates = Array.Empty<RendererState>();
    private AnimatorState[] previewAnimatorStates = Array.Empty<AnimatorState>();
    private GameObject activeVariantInstance;
    private GameObject activeVariantPrefab;
    private bool previewStateCaptured;
    private bool boundaryEventSubscribed;

    private struct RendererState
    {
        public Renderer Renderer;
        public bool Enabled;
    }

    private struct AnimatorState
    {
        public Animator Animator;
        public bool Enabled;
    }

    private void Awake()
    {
        ResolveCompanions();
        CaptureEditorPreviewState();

        if (randomizeOnAwake)
        {
            RandomizeVariant();
        }
    }

    private void OnEnable()
    {
        ResolveCompanions();
        SubscribeToRouteBoundary();
    }

    private void OnDisable()
    {
        UnsubscribeFromRouteBoundary();

        if (!Application.isPlaying)
        {
            return;
        }

        DestroyActiveVariant();
        RestoreEditorPreviewState();
    }

    private void OnDestroy()
    {
        UnsubscribeFromRouteBoundary();
    }

    [ContextMenu("Randomize Runtime Character Now")]
    public void RandomizeVariant()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveCompanions();
        if (silhouetteAppearance == null ||
            silhouetteAppearance.SilhouetteMaterial == null)
        {
            RestoreEditorPreviewState();
            return;
        }

        GameObject nextPrefab = ChooseNextPrefab();
        if (nextPrefab == null)
        {
            RestoreEditorPreviewState();
            return;
        }

        if (!previewStateCaptured)
        {
            CaptureEditorPreviewState();
        }

        if (hideEditorPreviewAtRuntime)
        {
            SetEditorPreviewEnabled(false);
        }

        DestroyActiveVariant();

        Transform parent = visualParent != null ? visualParent : transform;
        GameObject instance = Instantiate(nextPrefab, parent, false);
        instance.name = nextPrefab.name + " (Runtime Variant)";
        instance.transform.localPosition += localPositionOffset;
        instance.transform.localRotation = Quaternion.Euler(localEulerOffset) * instance.transform.localRotation;
        instance.transform.localScale *= localScaleMultiplier;

        activeVariantInstance = instance;
        activeVariantPrefab = nextPrefab;

        // The model changes, but every material slot is immediately replaced
        // with the same shared silhouette material before this frame renders.
        silhouetteAppearance.RefreshRendererList();

        if (patrol != null)
        {
            patrol.RefreshAnimators(instance.transform);
        }
    }

    public void Configure(GameObject previewPrefab, GameObject[] prefabs)
    {
        editorPreviewPrefab = previewPrefab;
        variantPrefabs = prefabs ?? Array.Empty<GameObject>();
    }

    private void ResolveCompanions()
    {
        if (patrol == null)
        {
            patrol = GetComponent<CityWalkPedestrianPatrol>();
        }

        if (silhouetteAppearance == null)
        {
            silhouetteAppearance = GetComponent<CityWalkSilhouetteAppearance>();
        }
    }

    private void SubscribeToRouteBoundary()
    {
        if (!randomizeAtRouteBoundaries || patrol == null || boundaryEventSubscribed)
        {
            return;
        }

        patrol.RouteBoundaryReached += HandleRouteBoundaryReached;
        boundaryEventSubscribed = true;
    }

    private void UnsubscribeFromRouteBoundary()
    {
        if (!boundaryEventSubscribed)
        {
            return;
        }

        if (patrol != null)
        {
            patrol.RouteBoundaryReached -= HandleRouteBoundaryReached;
        }

        boundaryEventSubscribed = false;
    }

    private void HandleRouteBoundaryReached()
    {
        RandomizeVariant();
    }

    private GameObject ChooseNextPrefab()
    {
        if (variantPrefabs == null || variantPrefabs.Length == 0)
        {
            return null;
        }

        List<GameObject> candidates = new List<GameObject>(variantPrefabs.Length);
        for (int i = 0; i < variantPrefabs.Length; i++)
        {
            GameObject candidate = variantPrefabs[i];
            if (candidate != null && !candidates.Contains(candidate))
            {
                candidates.Add(candidate);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        if (candidates.Count > 1 && activeVariantPrefab == null && editorPreviewPrefab != null)
        {
            candidates.Remove(editorPreviewPrefab);
        }

        if (avoidImmediateRepeat && candidates.Count > 1 && activeVariantPrefab != null)
        {
            candidates.Remove(activeVariantPrefab);
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private void CaptureEditorPreviewState()
    {
        if (previewStateCaptured)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        previewRendererStates = new RendererState[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            previewRendererStates[i] = new RendererState
            {
                Renderer = renderers[i],
                Enabled = renderers[i].enabled
            };
        }

        Animator[] animators = GetComponentsInChildren<Animator>(true);
        previewAnimatorStates = new AnimatorState[animators.Length];
        for (int i = 0; i < animators.Length; i++)
        {
            previewAnimatorStates[i] = new AnimatorState
            {
                Animator = animators[i],
                Enabled = animators[i].enabled
            };
        }

        previewStateCaptured = true;
    }

    private void SetEditorPreviewEnabled(bool enabled)
    {
        for (int i = 0; i < previewRendererStates.Length; i++)
        {
            Renderer renderer = previewRendererStates[i].Renderer;
            if (renderer != null)
            {
                renderer.enabled = enabled && previewRendererStates[i].Enabled;
            }
        }

        for (int i = 0; i < previewAnimatorStates.Length; i++)
        {
            Animator animator = previewAnimatorStates[i].Animator;
            if (animator != null)
            {
                animator.enabled = enabled && previewAnimatorStates[i].Enabled;
            }
        }
    }

    private void RestoreEditorPreviewState()
    {
        SetEditorPreviewEnabled(true);
    }

    private void DestroyActiveVariant()
    {
        if (activeVariantInstance == null)
        {
            return;
        }

        activeVariantInstance.SetActive(false);
        Destroy(activeVariantInstance);
        activeVariantInstance = null;
    }
}
