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

[System.Serializable]
public class RoomInteractionResumeOverride
{
    [Tooltip("互动执行后，将当前步骤的继续位置写为这个坐标物体。")]
    public bool saveResumeTransformOnInteract;

    [Tooltip("继续游戏时玩家将被放到这个坐标。建议拖一个空物体作为锚点。")]
    public Transform resumeTransform;

    [Tooltip("是否同时保存这个坐标物体的朝向。")]
    public bool saveRotation = true;

    public void Apply(RoomInteractionContext context)
    {
        if (!saveResumeTransformOnInteract)
        {
            return;
        }

        Object logContext = context != null && context.Interactable != null
            ? context.Interactable
            : null;

        if (resumeTransform == null)
        {
            Debug.LogWarning("[RoomInteraction] Resume transform is enabled but no transform was assigned.", logContext);
            return;
        }

        RoomInteractionProgressManager.Instance.SetResumeStateFromTransform(resumeTransform, saveRotation);
    }
}

[System.Serializable]
public class RoomInteractionVariant
{
    [Tooltip("留空则沿用 RoomInteractable 上的 promptText。")]
    public string promptTextOverride;

    [Tooltip("互动执行后记录的互动进度 ID。留空则不记录。")]
    public string completionProgressId;

    [Min(1)]
    [Tooltip("每次互动后给该进度增加多少次。")]
    public int completionProgressIncrement = 1;

    [Header("Resume State")]
    public RoomInteractionResumeOverride resumeOverride = new RoomInteractionResumeOverride();

    public RoomInteractionBehaviour[] interactionBehaviours;
    public RoomInteractionAction[] interactionActions;
    public UnityEvent onInteract = new UnityEvent();
    public RoomGameObjectEvent onInteractWithPlayer = new RoomGameObjectEvent();
    public RoomInteractableEvent onInteractWithTarget = new RoomInteractableEvent();

    public string ResolvePromptText(string fallbackPromptText)
    {
        return string.IsNullOrWhiteSpace(promptTextOverride) ? fallbackPromptText : promptTextOverride;
    }

    public bool HasConfiguredInteraction()
    {
        return HasAny(interactionBehaviours) ||
               HasAny(interactionActions) ||
               GetPersistentEventCount() > 0;
    }

    public void Execute(RoomInteractionContext context)
    {
        ExecuteInteractionBehaviours(context);
        ExecuteInteractionActions(context);

        onInteract.Invoke();

        GameObject player = context != null ? context.Player : null;
        RoomInteractable interactable = context != null ? context.Interactable : null;

        onInteractWithPlayer.Invoke(player);
        onInteractWithTarget.Invoke(interactable);

        if (!string.IsNullOrWhiteSpace(completionProgressId))
        {
            RoomInteractionProgressManager.Instance.MarkCompleted(
                completionProgressId,
                Mathf.Max(1, completionProgressIncrement));
        }

        if (resumeOverride != null)
        {
            resumeOverride.Apply(context);
        }
    }

    private static bool HasAny<T>(T[] items) where T : class
    {
        if (items == null)
        {
            return false;
        }

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private int GetPersistentEventCount()
    {
        int count = 0;

        if (onInteract != null)
        {
            count += onInteract.GetPersistentEventCount();
        }

        if (onInteractWithPlayer != null)
        {
            count += onInteractWithPlayer.GetPersistentEventCount();
        }

        if (onInteractWithTarget != null)
        {
            count += onInteractWithTarget.GetPersistentEventCount();
        }

        return count;
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
}

[DisallowMultipleComponent]
public class RoomInteractable : MonoBehaviour
{
    private static readonly List<RoomInteractable> activeInteractables = new List<RoomInteractable>();

    private enum ActiveInteractionMode
    {
        None,
        Primary,
        Default
    }

    [Header("Detection")]
    public bool isInteractable = true;
    public Transform detectionCenter;

    [Min(0f)]
    public float interactionRange = 2f;

    [Header("Prompt")]
    public string promptText = "按下 {key} 进行交互";
    public Transform promptAnchor;
    public Vector3 promptWorldOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Highlight")]
    public RoomInteractableHighlight highlightController;
    public bool autoFindHighlightController = true;

    [Header("Interaction Logic")]
    public RoomInteractionBehaviour[] interactionBehaviours;
    public RoomInteractionAction[] interactionActions;
    [Tooltip("主互动执行后记录的互动进度 ID。留空则不记录。")]
    public string completionProgressId;

    [Min(1)]
    [Tooltip("每次主互动后给该进度增加多少次。")]
    public int completionProgressIncrement = 1;

    [Header("Resume State")]
    public RoomInteractionResumeOverride primaryResumeOverride = new RoomInteractionResumeOverride();

    public UnityEvent onInteract = new UnityEvent();
    public RoomGameObjectEvent onInteractWithPlayer = new RoomGameObjectEvent();
    public RoomInteractableEvent onInteractWithTarget = new RoomInteractableEvent();

    [Header("Conditional Interaction")]
    [Tooltip("开启后：满足条件执行上面的主互动；不满足时执行下面的默认互动。")]
    public bool useConditionalInteraction;
    public RoomInteractionUnlockConditions unlockConditions = new RoomInteractionUnlockConditions();
    public RoomInteractionVariant defaultInteraction = new RoomInteractionVariant();

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
        get
        {
            return isActiveAndEnabled &&
                   isInteractable &&
                   ResolveActiveInteractionMode() != ActiveInteractionMode.None;
        }
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

    public Vector3 PromptWorldPosition
    {
        get
        {
            Transform anchor = promptAnchor != null ? promptAnchor : transform;
            return anchor.position + promptWorldOffset;
        }
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
        string resolvedPromptText = promptText;
        if (ResolveActiveInteractionMode() == ActiveInteractionMode.Default && defaultInteraction != null)
        {
            resolvedPromptText = defaultInteraction.ResolvePromptText(promptText);
        }

        string text = string.IsNullOrEmpty(resolvedPromptText) ? "按下 {key} 进行交互" : resolvedPromptText;
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
        ActiveInteractionMode activeMode = ResolveActiveInteractionMode();
        if (!CanBeDetected || activeMode == ActiveInteractionMode.None)
        {
            return;
        }

        float distance = 0f;
        if (player != null)
        {
            distance = Vector3.Distance(DetectionCenterPosition, player.transform.position);
        }

        RoomInteractionContext context = new RoomInteractionContext(player, this, distance);

        if (activeMode == ActiveInteractionMode.Default)
        {
            defaultInteraction.Execute(context);
            return;
        }

        ExecutePrimaryInteraction(context, player);
    }

    public bool IsPrimaryInteractionUnlocked()
    {
        return ResolveActiveInteractionMode() == ActiveInteractionMode.Primary;
    }

    private ActiveInteractionMode ResolveActiveInteractionMode()
    {
        if (!useConditionalInteraction)
        {
            return ActiveInteractionMode.Primary;
        }

        if (unlockConditions == null || unlockConditions.AreSatisfied())
        {
            return ActiveInteractionMode.Primary;
        }

        return defaultInteraction != null && defaultInteraction.HasConfiguredInteraction()
            ? ActiveInteractionMode.Default
            : ActiveInteractionMode.None;
    }

    private void ExecutePrimaryInteraction(RoomInteractionContext context, GameObject player)
    {
        ExecuteInteractionBehaviours(context);
        ExecuteInteractionActions(context);

        onInteract.Invoke();
        onInteractWithPlayer.Invoke(player);
        onInteractWithTarget.Invoke(this);

        if (!string.IsNullOrWhiteSpace(completionProgressId))
        {
            RoomInteractionProgressManager.Instance.MarkCompleted(
                completionProgressId,
                Mathf.Max(1, completionProgressIncrement));
        }

        if (primaryResumeOverride != null)
        {
            primaryResumeOverride.Apply(context);
        }
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
