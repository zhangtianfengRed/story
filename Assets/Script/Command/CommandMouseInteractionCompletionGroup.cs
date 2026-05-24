using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[AddComponentMenu("Command/Mouse Interaction Completion Group")]
public class CommandMouseInteractionCompletionGroup : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("需要全部完成的鼠标互动道具。留空并开启自动查找时，会从子物体收集。")]
    public CommandMouseInteractable[] requiredInteractables;
    public bool autoFindInteractablesInChildren = true;
    public bool includeInactiveChildren = true;

    [Header("Completion")]
    [Tooltip("启用时如果某些道具已经完成，是否把它们计入当前组进度。")]
    public bool countAlreadyCompletedOnEnable = true;
    [Tooltip("所有道具完成后只触发一次。")]
    public bool invokeAllCompletedOnce = true;
    [Tooltip("没有配置任何道具时是否也触发完成。通常保持关闭，方便发现漏配。")]
    public bool invokeWhenEmpty;

    [Header("Events")]
    public CommandGameObjectEvent onItemCompleted = new CommandGameObjectEvent();
    public UnityEvent onAllCompleted = new UnityEvent();

    private readonly HashSet<CommandMouseInteractable> completedInteractables =
        new HashSet<CommandMouseInteractable>();

    private readonly List<CommandMouseInteractable> subscribedInteractables =
        new List<CommandMouseInteractable>();

    private bool hasInvokedAllCompleted;

    public int RequiredCount
    {
        get { return CountValidRequiredInteractables(); }
    }

    public int CompletedCount
    {
        get { return completedInteractables.Count; }
    }

    public bool IsAllCompleted
    {
        get
        {
            int requiredCount = RequiredCount;
            return requiredCount > 0 && completedInteractables.Count >= requiredCount;
        }
    }

    private void Reset()
    {
        RefreshInteractablesFromChildren();
    }

    private void OnEnable()
    {
        EnsureRequiredInteractables();
        SubscribeRequiredInteractables();
        RebuildCompletionState();
        CheckAllCompleted();
    }

    private void OnDisable()
    {
        UnsubscribeRequiredInteractables();
    }

    private void OnValidate()
    {
        EnsureRequiredInteractables();
    }

    [ContextMenu("Refresh Interactables From Children")]
    public void RefreshInteractablesFromChildren()
    {
        requiredInteractables = GetComponentsInChildren<CommandMouseInteractable>(includeInactiveChildren);
    }

    public void MarkItemCompleted(CommandMouseInteractable interactable)
    {
        if (interactable == null || !IsRequiredInteractable(interactable))
        {
            return;
        }

        if (!completedInteractables.Add(interactable))
        {
            return;
        }

        onItemCompleted.Invoke(interactable.gameObject);
        CheckAllCompleted();
    }

    public void ResetGroup()
    {
        completedInteractables.Clear();
        hasInvokedAllCompleted = false;
    }

    public void ResetGroupAndItems()
    {
        ResetGroup();

        if (requiredInteractables == null)
        {
            return;
        }

        for (int i = 0; i < requiredInteractables.Length; i++)
        {
            if (requiredInteractables[i] != null)
            {
                requiredInteractables[i].ResetCompletion();
            }
        }
    }

    private void EnsureRequiredInteractables()
    {
        if (!autoFindInteractablesInChildren)
        {
            return;
        }

        if (requiredInteractables == null || requiredInteractables.Length == 0)
        {
            RefreshInteractablesFromChildren();
        }
    }

    private void SubscribeRequiredInteractables()
    {
        UnsubscribeRequiredInteractables();

        if (requiredInteractables == null)
        {
            return;
        }

        for (int i = 0; i < requiredInteractables.Length; i++)
        {
            CommandMouseInteractable interactable = requiredInteractables[i];
            if (interactable == null || subscribedInteractables.Contains(interactable))
            {
                continue;
            }

            interactable.onCompletedObject.AddListener(HandleInteractableCompleted);
            subscribedInteractables.Add(interactable);
        }
    }

    private void UnsubscribeRequiredInteractables()
    {
        for (int i = 0; i < subscribedInteractables.Count; i++)
        {
            CommandMouseInteractable interactable = subscribedInteractables[i];
            if (interactable != null)
            {
                interactable.onCompletedObject.RemoveListener(HandleInteractableCompleted);
            }
        }

        subscribedInteractables.Clear();
    }

    private void RebuildCompletionState()
    {
        completedInteractables.Clear();

        if (!countAlreadyCompletedOnEnable || requiredInteractables == null)
        {
            return;
        }

        for (int i = 0; i < requiredInteractables.Length; i++)
        {
            CommandMouseInteractable interactable = requiredInteractables[i];
            if (interactable != null && interactable.IsCompleted)
            {
                completedInteractables.Add(interactable);
            }
        }
    }

    private void HandleInteractableCompleted(GameObject completedObject)
    {
        if (completedObject == null)
        {
            return;
        }

        CommandMouseInteractable interactable = completedObject.GetComponent<CommandMouseInteractable>();
        MarkItemCompleted(interactable);
    }

    private void CheckAllCompleted()
    {
        int requiredCount = RequiredCount;
        if (requiredCount == 0)
        {
            if (invokeWhenEmpty)
            {
                InvokeAllCompleted();
            }

            return;
        }

        if (completedInteractables.Count >= requiredCount)
        {
            InvokeAllCompleted();
        }
    }

    private void InvokeAllCompleted()
    {
        if (invokeAllCompletedOnce && hasInvokedAllCompleted)
        {
            return;
        }

        hasInvokedAllCompleted = true;
        onAllCompleted.Invoke();
    }

    private bool IsRequiredInteractable(CommandMouseInteractable target)
    {
        if (target == null || requiredInteractables == null)
        {
            return false;
        }

        for (int i = 0; i < requiredInteractables.Length; i++)
        {
            if (requiredInteractables[i] == target)
            {
                return true;
            }
        }

        return false;
    }

    private int CountValidRequiredInteractables()
    {
        if (requiredInteractables == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < requiredInteractables.Length; i++)
        {
            if (requiredInteractables[i] != null)
            {
                count++;
            }
        }

        return count;
    }
}
