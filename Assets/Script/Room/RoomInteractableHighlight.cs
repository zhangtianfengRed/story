using UnityEngine;

public class RoomInteractableHighlight : MonoBehaviour
{
    [Header("Highlight")]
    public GameObject[] highlightedObjects;
    public Behaviour[] highlightedBehaviours;

    [Header("Current Target Extra")]
    public GameObject[] currentTargetObjects;
    public Behaviour[] currentTargetBehaviours;

    [Header("Initial State")]
    public bool hideOnAwake = true;

    public bool IsHighlighted { get; private set; }
    public bool IsCurrentTarget { get; private set; }

    private void Awake()
    {
        if (hideOnAwake)
        {
            SetHighlighted(false, false);
        }
    }

    public void SetHighlighted(bool highlighted, bool currentTarget)
    {
        IsHighlighted = highlighted;
        IsCurrentTarget = highlighted && currentTarget;

        SetGameObjects(highlightedObjects, IsHighlighted);
        SetBehaviours(highlightedBehaviours, IsHighlighted);

        SetGameObjects(currentTargetObjects, IsCurrentTarget);
        SetBehaviours(currentTargetBehaviours, IsCurrentTarget);
    }

    private void SetGameObjects(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null && objects[i].activeSelf != active)
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
}
