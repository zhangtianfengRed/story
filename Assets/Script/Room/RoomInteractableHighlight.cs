using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class RoomInteractableHighlight : MonoBehaviour
{
    [Header("Highlight")]
    public GameObject[] highlightedObjects;
    public Behaviour[] highlightedBehaviours;
    public bool deactivateHighlightedObjectsWhenInactive = false;

    [Header("Highlight Materials")]
    [FormerlySerializedAs("outlineRenderers")]
    public Renderer[] highlightRenderers;
    [FormerlySerializedAs("autoFindOutlineRenderers")]
    public bool autoFindHighlightRenderers = true;
    [FormerlySerializedAs("highlightedOutlineMaterial")]
    public Material highlightedOverlayMaterial;

    [Header("Current Target Extra")]
    public GameObject[] currentTargetObjects;
    public Behaviour[] currentTargetBehaviours;
    public bool deactivateCurrentTargetObjectsWhenInactive = false;
    [FormerlySerializedAs("currentTargetOutlineMaterial")]
    public Material currentTargetOverlayMaterial;

    [Header("Initial State")]
    public bool hideOnAwake = true;

    public bool IsHighlighted { get; private set; }
    public bool IsCurrentTarget { get; private set; }

    // Caches each renderer's original materials so outline materials can be appended and restored cleanly.
    private readonly Dictionary<Renderer, Material[]> originalMaterialsByRenderer = new Dictionary<Renderer, Material[]>();
    private readonly List<Material> activeOutlineMaterials = new List<Material>(2);

    private void Awake()
    {
        EnsureHighlightRenderers();

        if (hideOnAwake)
        {
            SetHighlighted(false, false);
        }
    }

    private void OnDisable()
    {
        RestoreAllOutlineMaterials();
    }

    private void OnDestroy()
    {
        RestoreAllOutlineMaterials();
    }

    public void SetHighlighted(bool highlighted, bool currentTarget)
    {
        IsHighlighted = highlighted;
        IsCurrentTarget = highlighted && currentTarget;

        SetGameObjects(highlightedObjects, IsHighlighted, deactivateHighlightedObjectsWhenInactive);
        SetBehaviours(highlightedBehaviours, IsHighlighted);

        SetGameObjects(currentTargetObjects, IsCurrentTarget, deactivateCurrentTargetObjectsWhenInactive);
        SetBehaviours(currentTargetBehaviours, IsCurrentTarget);

        ApplyHighlightMaterialState();
    }

    private void SetGameObjects(GameObject[] objects, bool active, bool allowDeactivation)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] == null)
            {
                continue;
            }

            if (!active && !allowDeactivation)
            {
                continue;
            }

            if (objects[i].activeSelf != active)
            {
                objects[i].SetActive(active);
            }
        }
    }

    private void SetBehaviours(Behaviour[] behaviours, bool enabled)
    {
        if (behaviours == null)
        {
            return;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i] != this && behaviours[i].enabled != enabled)
            {
                behaviours[i].enabled = enabled;
            }
        }
    }

    private void ApplyHighlightMaterialState()
    {
        EnsureHighlightRenderers();
        BuildActiveOutlineMaterialList();

        if (activeOutlineMaterials.Count == 0)
        {
            RestoreAllOutlineMaterials();
            return;
        }

        if (highlightRenderers == null || highlightRenderers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < highlightRenderers.Length; i++)
        {
            Renderer renderer = highlightRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            ApplyOutlineMaterials(renderer);
        }

        RestoreMaterialsOnRemovedRenderers();
    }

    private void BuildActiveOutlineMaterialList()
    {
        activeOutlineMaterials.Clear();

        if (IsHighlighted)
        {
            AddOutlineMaterialIfValid(highlightedOverlayMaterial);
        }

        if (IsCurrentTarget)
        {
            AddOutlineMaterialIfValid(currentTargetOverlayMaterial);
        }
    }

    private void AddOutlineMaterialIfValid(Material material)
    {
        if (material == null || ContainsMaterial(activeOutlineMaterials, material))
        {
            return;
        }

        activeOutlineMaterials.Add(material);
    }

    private void ApplyOutlineMaterials(Renderer renderer)
    {
        if (!originalMaterialsByRenderer.TryGetValue(renderer, out Material[] originalMaterials))
        {
            originalMaterials = renderer.materials;
            originalMaterialsByRenderer.Add(renderer, originalMaterials);
        }

        int extraCount = 0;
        for (int i = 0; i < activeOutlineMaterials.Count; i++)
        {
            if (!ContainsMaterial(originalMaterials, activeOutlineMaterials[i]))
            {
                extraCount++;
            }
        }

        if (extraCount == 0)
        {
            return;
        }

        Material[] combinedMaterials = new Material[originalMaterials.Length + extraCount];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            combinedMaterials[i] = originalMaterials[i];
        }

        int writeIndex = originalMaterials.Length;
        for (int i = 0; i < activeOutlineMaterials.Count; i++)
        {
            Material material = activeOutlineMaterials[i];
            if (ContainsMaterial(originalMaterials, material))
            {
                continue;
            }

            combinedMaterials[writeIndex] = material;
            writeIndex++;
        }

        renderer.materials = combinedMaterials;
    }

    private void RestoreMaterialsOnRemovedRenderers()
    {
        if (originalMaterialsByRenderer.Count == 0)
        {
            return;
        }

        var renderersToRestore = new List<Renderer>();

        foreach (KeyValuePair<Renderer, Material[]> pair in originalMaterialsByRenderer)
        {
            Renderer renderer = pair.Key;
            if (renderer == null || !ContainsRenderer(highlightRenderers, renderer))
            {
                renderersToRestore.Add(renderer);
            }
        }

        for (int i = 0; i < renderersToRestore.Count; i++)
        {
            RestoreRendererMaterials(renderersToRestore[i]);
        }
    }

    private void RestoreAllOutlineMaterials()
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

    private void RestoreRendererMaterials(Renderer renderer)
    {
        if (renderer != null && originalMaterialsByRenderer.TryGetValue(renderer, out Material[] originalMaterials))
        {
            renderer.materials = originalMaterials;
        }

        originalMaterialsByRenderer.Remove(renderer);
    }

    private void EnsureHighlightRenderers()
    {
        if ((highlightRenderers == null || highlightRenderers.Length == 0) && autoFindHighlightRenderers)
        {
            highlightRenderers = GetComponentsInChildren<Renderer>(true);
        }
    }

    private static bool ContainsRenderer(Renderer[] renderers, Renderer target)
    {
        if (renderers == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsMaterial(IList<Material> materials, Material target)
    {
        if (materials == null || target == null)
        {
            return false;
        }

        for (int i = 0; i < materials.Count; i++)
        {
            if (materials[i] == target)
            {
                return true;
            }
        }

        return false;
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
}
