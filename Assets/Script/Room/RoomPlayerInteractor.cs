using UnityEngine;

[DisallowMultipleComponent]
public class RoomPlayerInteractor : MonoBehaviour
{
    [Header("Detection")]
    public Transform detectionOrigin;
    public KeyCode interactionKey = KeyCode.E;

    [Header("UI")]
    public RoomInteractionPromptUI promptUI;
    public bool autoFindPromptUI = true;

    [Header("Debug")]
    public bool logCurrentTarget;

    public RoomInteractable CurrentTarget { get; private set; }

    private void Awake()
    {
        if (detectionOrigin == null)
        {
            detectionOrigin = transform;
        }

        if (promptUI == null && autoFindPromptUI)
        {
            promptUI = FindObjectOfType<RoomInteractionPromptUI>(true);
        }
    }

    private void Update()
    {
        RefreshTargets();

        if (CurrentTarget != null && Input.GetKeyDown(interactionKey))
        {
            CurrentTarget.Interact(gameObject);
            RefreshTargets();
        }
    }

    private void OnDisable()
    {
        ClearAllHighlights();
        SetCurrentTarget(null);
    }

    public void RefreshTargets()
    {
        Vector3 origin = GetDetectionPosition();
        RoomInteractable nearest = FindNearestInteractable(origin);

        ApplyHighlightStates(origin, nearest);
        SetCurrentTarget(nearest);
    }

    private RoomInteractable FindNearestInteractable(Vector3 origin)
    {
        RoomInteractable nearest = null;
        float nearestSqrDistance = float.MaxValue;
        var interactables = RoomInteractable.ActiveInteractables;

        for (int i = interactables.Count - 1; i >= 0; i--)
        {
            RoomInteractable interactable = interactables[i];
            if (interactable == null)
            {
                interactables.RemoveAt(i);
                continue;
            }

            if (!interactable.IsInRange(origin))
            {
                continue;
            }

            float sqrDistance = interactable.GetSqrDistanceTo(origin);
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = interactable;
            }
        }

        return nearest;
    }

    private void ApplyHighlightStates(Vector3 origin, RoomInteractable nearest)
    {
        var interactables = RoomInteractable.ActiveInteractables;

        for (int i = interactables.Count - 1; i >= 0; i--)
        {
            RoomInteractable interactable = interactables[i];
            if (interactable == null)
            {
                interactables.RemoveAt(i);
                continue;
            }

            bool inRange = interactable.IsInRange(origin);
            interactable.SetHighlightState(inRange, inRange && interactable == nearest);
        }
    }

    private void SetCurrentTarget(RoomInteractable nextTarget)
    {
        CurrentTarget = nextTarget;

        if (logCurrentTarget)
        {
            Debug.Log(CurrentTarget != null
                ? $"[RoomPlayerInteractor] Current target: {CurrentTarget.name}, promptUI={(promptUI != null ? promptUI.name : "null")}"
                : $"[RoomPlayerInteractor] Current target: none, promptUI={(promptUI != null ? promptUI.name : "null")}", this);
        }

        if (promptUI == null)
        {
            return;
        }

        if (CurrentTarget != null)
        {
            promptUI.Show(CurrentTarget, interactionKey);
        }
        else
        {
            promptUI.Hide();
        }
    }

    private void ClearAllHighlights()
    {
        var interactables = RoomInteractable.ActiveInteractables;

        for (int i = interactables.Count - 1; i >= 0; i--)
        {
            if (interactables[i] != null)
            {
                interactables[i].SetHighlightState(false, false);
            }
        }
    }

    private Vector3 GetDetectionPosition()
    {
        return detectionOrigin != null ? detectionOrigin.position : transform.position;
    }
}
