using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class RoomBoolEvent : UnityEvent<bool>
{
}

[System.Serializable]
public class RoomGameObjectEvent : UnityEvent<GameObject>
{
}

[System.Serializable]
public class RoomInteractableEvent : UnityEvent<RoomInteractable>
{
}

[DisallowMultipleComponent]
public class RoomInteractable : MonoBehaviour
{
    private static readonly List<RoomInteractable> activeInteractables = new List<RoomInteractable>();

    [Header("Detection")]
    public bool isInteractable = true;
    public Transform detectionCenter;

    [Min(0f)]
    public float interactionRange = 2f;

    [Header("Prompt")]
    public string promptText = "按下 {key} 进行交互";

    [Header("Highlight")]
    public RoomInteractableHighlight highlightController;
    public bool autoFindHighlightController = true;

    [Header("Interaction Logic")]
    public RoomInteractionBehaviour[] interactionBehaviours;
    public RoomInteractionAction[] interactionActions;
    public UnityEvent onInteract = new UnityEvent();
    public RoomGameObjectEvent onInteractWithPlayer = new RoomGameObjectEvent();
    public RoomInteractableEvent onInteractWithTarget = new RoomInteractableEvent();

    [Header("State Events")]
    public RoomBoolEvent onHighlightChanged = new RoomBoolEvent();
    public RoomBoolEvent onCurrentTargetChanged = new RoomBoolEvent();

    private bool isHighlighted;
    private bool isCurrentTarget;

    public static List<RoomInteractable> ActiveInteractables
    {
        get { return activeInteractables; }
    }

    public bool CanBeDetected
    {
        get { return isActiveAndEnabled && isInteractable; }
    }

    public bool IsHighlighted
    {
        get { return isHighlighted; }
    }

    public bool IsCurrentTarget
    {
        get { return isCurrentTarget; }
    }

    public Vector3 DetectionCenterPosition
    {
        get { return detectionCenter != null ? detectionCenter.position : transform.position; }
    }

    private void Awake()
    {
        EnsureHighlightController();
    }

    private void OnEnable()
    {
        if (!activeInteractables.Contains(this))
        {
            activeInteractables.Add(this);
        }
    }

    private void OnDisable()
    {
        activeInteractables.Remove(this);
        SetHighlightState(false, false);
    }

    private void OnDestroy()
    {
        activeInteractables.Remove(this);
    }

    private void OnValidate()
    {
        if (interactionRange < 0f)
        {
            interactionRange = 0f;
        }

        EnsureHighlightController();
    }

    public float GetSqrDistanceTo(Vector3 worldPosition)
    {
        return (DetectionCenterPosition - worldPosition).sqrMagnitude;
    }

    public bool IsInRange(Vector3 worldPosition)
    {
        if (!CanBeDetected)
        {
            return false;
        }

        float range = Mathf.Max(0f, interactionRange);
        return GetSqrDistanceTo(worldPosition) <= range * range;
    }

    public string GetPromptText(KeyCode key)
    {
        string text = string.IsNullOrEmpty(promptText) ? "按下 {key} 进行交互" : promptText;
        return text.Replace("{key}", GetKeyDisplayName(key));
    }

    public void SetHighlightState(bool highlighted, bool currentTarget)
    {
        currentTarget = highlighted && currentTarget;

        bool highlightChanged = isHighlighted != highlighted;
        bool currentTargetChanged = isCurrentTarget != currentTarget;

        if (!highlightChanged && !currentTargetChanged)
        {
            return;
        }

        isHighlighted = highlighted;
        isCurrentTarget = currentTarget;

        EnsureHighlightController();

        if (highlightController != null)
        {
            highlightController.SetHighlighted(isHighlighted, isCurrentTarget);
        }

        if (highlightChanged)
        {
            onHighlightChanged.Invoke(isHighlighted);
        }

        if (currentTargetChanged)
        {
            onCurrentTargetChanged.Invoke(isCurrentTarget);
        }
    }

    public void Interact(GameObject player)
    {
        if (!CanBeDetected)
        {
            return;
        }

        float distance = 0f;
        if (player != null)
        {
            distance = Vector3.Distance(DetectionCenterPosition, player.transform.position);
        }

        RoomInteractionContext context = new RoomInteractionContext(player, this, distance);

        ExecuteInteractionBehaviours(context);
        ExecuteInteractionActions(context);

        onInteract.Invoke();
        onInteractWithPlayer.Invoke(player);
        onInteractWithTarget.Invoke(this);
    }

    private void ExecuteInteractionBehaviours(RoomInteractionContext context)
    {
        if (interactionBehaviours == null)
        {
            return;
        }

        for (int i = 0; i < interactionBehaviours.Length; i++)
        {
            if (interactionBehaviours[i] != null)
            {
                interactionBehaviours[i].Execute(context);
            }
        }
    }

    private void ExecuteInteractionActions(RoomInteractionContext context)
    {
        if (interactionActions == null)
        {
            return;
        }

        for (int i = 0; i < interactionActions.Length; i++)
        {
            if (interactionActions[i] != null)
            {
                interactionActions[i].Execute(context);
            }
        }
    }

    private void EnsureHighlightController()
    {
        if (highlightController == null && autoFindHighlightController)
        {
            highlightController = GetComponentInChildren<RoomInteractableHighlight>(true);
        }
    }

    private string GetKeyDisplayName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0:
                return "鼠标左键";
            case KeyCode.Mouse1:
                return "鼠标右键";
            case KeyCode.Mouse2:
                return "鼠标中键";
            case KeyCode.Space:
                return "空格";
            case KeyCode.Return:
                return "回车";
            case KeyCode.Escape:
                return "Esc";
            default:
                return key.ToString();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = isInteractable ? Color.cyan : Color.gray;
        Gizmos.DrawWireSphere(DetectionCenterPosition, Mathf.Max(0f, interactionRange));
    }
}
