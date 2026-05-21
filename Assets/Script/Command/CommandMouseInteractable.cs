using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

[Serializable]
public class CommandGameObjectEvent : UnityEvent<GameObject>
{
}

[Serializable]
public class CommandColliderEvent : UnityEvent<Collider>
{
}

[DisallowMultipleComponent]
[AddComponentMenu("Command/Mouse Interactable")]
public class CommandMouseInteractable : MonoBehaviour
{
    private const string DefaultHoverGlowMaterialPath = "Assets/Material/CommandHoverPulseGlow.mat";

    [Header("Detection")]
    [Tooltip("不指定时默认使用 Main Camera。")]
    public Camera targetCamera;
    public bool autoUseMainCamera = true;
    public LayerMask raycastLayers = ~0;
    [Min(0f)]
    public float maxRaycastDistance = 1000f;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    public bool ignoreWhenPointerOverUI = true;

    [Header("Highlight")]
    [Tooltip("不手动指定时，会自动收集当前物体及子物体上的 Renderer。")]
    public Renderer[] targetRenderers;
    public bool autoFindRenderers = true;
    [FormerlySerializedAs("edgeGlowMaterial")]
    public Material hoverGlowMaterial;

    [Header("Events")]
    public UnityEvent onHoverEnter = new UnityEvent();
    public UnityEvent onHoverExit = new UnityEvent();
    public UnityEvent onClick = new UnityEvent();
    public CommandGameObjectEvent onClickObject = new CommandGameObjectEvent();
    public CommandColliderEvent onClickCollider = new CommandColliderEvent();

    private readonly Dictionary<Renderer, Material[]> originalMaterialsByRenderer =
        new Dictionary<Renderer, Material[]>();

    private bool isHovered;
    private Collider currentHitCollider;

    public bool IsHovered
    {
        get { return isHovered; }
    }

    public Collider CurrentHitCollider
    {
        get { return currentHitCollider; }
    }

    private void Reset()
    {
        EnsureTargetRenderers();
        AssignDefaultHoverGlowMaterialIfNeeded();
    }

    private void Awake()
    {
        EnsureTargetRenderers();
    }

    private void Update()
    {
        bool pointerOverTarget = TryGetPointerTarget(out Collider hitCollider);
        SetHovered(pointerOverTarget, hitCollider);

        if (isHovered && Input.GetMouseButtonDown(0))
        {
            InvokeClick(currentHitCollider);
        }
    }

    private void OnDisable()
    {
        SetHovered(false, null);
        RestoreAllMaterials();
    }

    private void OnDestroy()
    {
        RestoreAllMaterials();
    }

    private void OnValidate()
    {
        maxRaycastDistance = Mathf.Max(0f, maxRaycastDistance);
        EnsureTargetRenderers();
        AssignDefaultHoverGlowMaterialIfNeeded();
    }

    public void SetHovered(bool hovered)
    {
        SetHovered(hovered, currentHitCollider);
    }

    public void InvokeClick()
    {
        InvokeClick(currentHitCollider);
    }

    private void SetHovered(bool hovered, Collider hitCollider)
    {
        if (isHovered == hovered)
        {
            currentHitCollider = hovered ? hitCollider : null;
            return;
        }

        isHovered = hovered;
        currentHitCollider = hovered ? hitCollider : null;

        if (isHovered)
        {
            ApplyGlowMaterials();
            onHoverEnter.Invoke();
            OnPointerEntered(currentHitCollider);
        }
        else
        {
            RestoreAllMaterials();
            onHoverExit.Invoke();
            OnPointerExited();
        }
    }

    private void InvokeClick(Collider hitCollider)
    {
        onClick.Invoke();
        onClickObject.Invoke(gameObject);
        onClickCollider.Invoke(hitCollider);
        OnClicked(hitCollider);
    }

    protected virtual void OnPointerEntered(Collider hitCollider)
    {
    }

    protected virtual void OnPointerExited()
    {
    }

    protected virtual void OnClicked(Collider hitCollider)
    {
    }

    private bool TryGetPointerTarget(out Collider hitCollider)
    {
        hitCollider = null;

        if (ignoreWhenPointerOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        Camera rayCamera = ResolveCamera();
        if (rayCamera == null)
        {
            return false;
        }

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, raycastLayers, triggerInteraction))
        {
            return false;
        }

        if (!IsTargetCollider(hit.collider))
        {
            return false;
        }

        hitCollider = hit.collider;
        return true;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        return autoUseMainCamera ? Camera.main : null;
    }

    private bool IsTargetCollider(Collider hitCollider)
    {
        if (hitCollider == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        return hitTransform == transform || hitTransform.IsChildOf(transform);
    }

    private void ApplyGlowMaterials()
    {
        if (hoverGlowMaterial == null)
        {
            return;
        }

        EnsureTargetRenderers();
        if (targetRenderers == null)
        {
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer targetRenderer = targetRenderers[i];
            if (targetRenderer != null)
            {
                ApplyGlowMaterial(targetRenderer);
            }
        }
    }

    private void ApplyGlowMaterial(Renderer targetRenderer)
    {
        if (!originalMaterialsByRenderer.TryGetValue(targetRenderer, out Material[] originalMaterials))
        {
            originalMaterials = targetRenderer.materials;
            originalMaterialsByRenderer.Add(targetRenderer, originalMaterials);
        }

        if (ContainsMaterial(originalMaterials, hoverGlowMaterial))
        {
            return;
        }

        Material[] combinedMaterials = new Material[originalMaterials.Length + 1];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            combinedMaterials[i] = originalMaterials[i];
        }

        combinedMaterials[combinedMaterials.Length - 1] = hoverGlowMaterial;
        targetRenderer.materials = combinedMaterials;
    }

    private void RestoreAllMaterials()
    {
        if (originalMaterialsByRenderer.Count == 0)
        {
            return;
        }

        var renderersToRestore = new List<Renderer>(originalMaterialsByRenderer.Keys);
        for (int i = 0; i < renderersToRestore.Count; i++)
        {
            RestoreRendererMaterials(renderersToRestore[i]);
        }
    }

    private void RestoreRendererMaterials(Renderer targetRenderer)
    {
        if (targetRenderer != null &&
            originalMaterialsByRenderer.TryGetValue(targetRenderer, out Material[] originalMaterials))
        {
            targetRenderer.materials = originalMaterials;
        }

        originalMaterialsByRenderer.Remove(targetRenderer);
    }

    private void EnsureTargetRenderers()
    {
        if (!autoFindRenderers)
        {
            return;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private static bool ContainsMaterial(Material[] materials, Material target)
    {
        if (materials == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private void AssignDefaultHoverGlowMaterialIfNeeded()
    {
#if UNITY_EDITOR
        if (hoverGlowMaterial != null)
        {
            return;
        }

        hoverGlowMaterial =
            UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultHoverGlowMaterialPath);
#endif
    }
}
