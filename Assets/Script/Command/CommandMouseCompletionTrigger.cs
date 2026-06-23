using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[AddComponentMenu("Command/Mouse Completion Trigger")]
public class CommandMouseCompletionTrigger : MonoBehaviour
{
    [Header("Click Source")]
    [Tooltip("触发点击的 CommandMouseInteractable。通常挂在当前触发物体上。")]
    public CommandMouseInteractable clickSource;
    [Tooltip("点击检测使用的摄像机。配置后会同步到 clickSource.targetCamera，并关闭 clickSource.autoUseMainCamera。")]
    public Camera targetCamera;
    [Tooltip("启用后，把 targetCamera 应用到 clickSource。")]
    public bool applyTargetCameraToClickSource = true;
    [Tooltip("启用后自动监听 clickSource.onClickCollider。")]
    public bool autoBindClickSource = true;
    [Tooltip("触发一次后忽略后续点击。")]
    public bool triggerOnce = true;
    [Tooltip("完成后是否把 clickSource 自己也标记为完成，避免重复点击。")]
    public bool completeClickSourceOnTriggered = true;

    [Header("Direct Mouse Input")]
    [Tooltip("启用后，本脚本自己使用 targetCamera 做射线检测。适合 clickSource 不是当前可点击物体，或玩法里使用独立摄像机的情况。")]
    public bool enableDirectMouseInput;
    [Tooltip("Direct Mouse Input 的命中根节点。留空时使用当前物体。")]
    public Transform directHitRoot;
    public LayerMask directRaycastLayers = ~0;
    [Min(0f)]
    public float directRaycastDistance = 1000f;
    public QueryTriggerInteraction directTriggerInteraction = QueryTriggerInteraction.Collide;
    [Tooltip("开启后，Direct Mouse Input 会尊重前方遮挡；射线最近命中不是当前触发物时不会触发。")]
    public bool directRespectOcclusion = true;
    [Tooltip("开启后，鼠标在 UI 上时不处理 direct input 点击。")]
    public bool directIgnoreWhenPointerOverUI;

    [Header("Completion Targets")]
    [Tooltip("可选：通用鼠标互动完成目标。适合只需要调用 CommandMouseInteractable.CompleteInteraction 的玩法。")]
    public CommandMouseInteractable completionInteractable;
    [Tooltip("可选：多个玩法共同完成一个物体时，手动拖入对应的 CommandMouseInteractionCompletionGroup。不会自动查找，避免多个 Group 时误连。")]
    public CommandMouseInteractionCompletionGroup completionGroup;
    [Tooltip("当前触发器在 completionGroup 中对应的完成条目。留空时优先使用 completionInteractable，其次使用 clickSource。")]
    public CommandMouseInteractable completionGroupItem;
    [Tooltip("触发时是否先调用 completionGroupItem.CompleteInteraction()。Group 已订阅该条目时会自动收到完成。")]
    public bool completeCompletionGroupItem = true;
    [Tooltip("触发时是否直接通知 completionGroup.MarkItemCompleted，避免订阅时序或禁用状态导致漏记。")]
    public bool markCompletionGroupItem = true;

    [Header("Events")]
    [Tooltip("点击源触发后立即调用。适合播放音效、显隐对象等不依赖完成结果的逻辑。")]
    public UnityEvent onClick = new UnityEvent();
    [Tooltip("点击源触发后立即调用，并传入本次点击命中的 Collider。")]
    public CommandColliderEvent onClickCollider = new CommandColliderEvent();
    [Tooltip("完成触发成功后调用。点击触发和脚本主动调用 CompleteFromScript()/TriggerCompletion() 都会触发这里。")]
    public UnityEvent onTriggered = new UnityEvent();

    private CommandMouseInteractable subscribedClickSource;
    private bool hasTriggered;

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeClickSource();
    }

    private void OnDisable()
    {
        UnsubscribeClickSource();
    }

    private void OnValidate()
    {
        directRaycastDistance = Mathf.Max(0f, directRaycastDistance);
        ResolveReferences();
    }

    private void Update()
    {
        if (enableDirectMouseInput && Input.GetMouseButtonDown(0))
        {
            HandleDirectMouseDown();
        }
    }

    public void Trigger()
    {
        TryTriggerInternal(null, true);
    }

    public void Trigger(Collider hitCollider)
    {
        TryTriggerInternal(hitCollider, true);
    }

    public bool TryTrigger()
    {
        return TryTriggerInternal(null, true);
    }

    public bool TryTrigger(Collider hitCollider)
    {
        return TryTriggerInternal(hitCollider, true);
    }

    /// <summary>
    /// 脚本主动触发完成，不调用 onClick/onClickCollider，只执行完成目标和 onTriggered。
    /// 适合由 Timeline、动画事件、其它玩法脚本或 UnityEvent 主动调用。
    /// </summary>
    public void TriggerCompletion()
    {
        TryTriggerCompletion();
    }

    /// <summary>
    /// 脚本主动尝试触发完成，不调用点击事件；返回本次是否成功完成。
    /// </summary>
    public bool TryTriggerCompletion()
    {
        return TryTriggerInternal(null, false);
    }

    /// <summary>
    /// TriggerCompletion 的语义化别名。推荐在 UnityEvent 里选择这个方法，表示“由脚本主动完成”。
    /// </summary>
    public void CompleteFromScript()
    {
        TryTriggerCompletion();
    }

    /// <summary>
    /// TryTriggerCompletion 的语义化别名；返回本次是否成功完成。
    /// </summary>
    public bool TryCompleteFromScript()
    {
        return TryTriggerCompletion();
    }

    // invokeClickEvents 为 false 时用于脚本主动完成，避免误触发点击表现逻辑。
    private bool TryTriggerInternal(Collider hitCollider, bool invokeClickEvents)
    {
        ResolveReferences();

        if (triggerOnce && hasTriggered)
        {
            return false;
        }

        if (invokeClickEvents)
        {
            InvokeClickEvents(hitCollider);
        }

        bool hasCompletionTarget = false;
        bool completedAnyTarget = false;

        if (completionInteractable != null)
        {
            hasCompletionTarget = true;
            if (!completionInteractable.IsCompleted)
            {
                completionInteractable.CompleteInteraction();
                completedAnyTarget = true;
            }
        }

        if (completionGroup != null)
        {
            hasCompletionTarget = true;
            CommandMouseInteractable groupItem = ResolveCompletionGroupItem();

            if (groupItem == null)
            {
                return false;
            }

            if (completeCompletionGroupItem && !groupItem.IsCompleted)
            {
                groupItem.CompleteInteraction();
                completedAnyTarget = true;
            }

            if (markCompletionGroupItem)
            {
                completedAnyTarget |= completionGroup.TryMarkItemCompleted(groupItem);
            }

            if (groupItem.IsCompleted && completionGroup.IsRequiredInteractable(groupItem))
            {
                completionGroup.EvaluateCompletion();
                completedAnyTarget = true;
            }
        }

        completedAnyTarget |= CompleteClickSourceIfNeeded();

        if (hasCompletionTarget && !completedAnyTarget)
        {
            return false;
        }

        hasTriggered = true;
        onTriggered.Invoke();

        return true;
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
    }

    private void InvokeClickEvents(Collider hitCollider)
    {
        onClick.Invoke();
        onClickCollider.Invoke(hitCollider);
    }

    private void HandleDirectMouseDown()
    {
        if (directIgnoreWhenPointerOverUI &&
            EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (TryGetDirectHitCollider(out Collider hitCollider))
        {
            Trigger(hitCollider);
        }
    }

    private bool TryGetDirectHitCollider(out Collider hitCollider)
    {
        hitCollider = null;

        Camera rayCamera = ResolveTargetCamera();
        if (rayCamera == null)
        {
            return false;
        }

        Ray ray = rayCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, directRaycastDistance, directRaycastLayers, directTriggerInteraction);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        Transform hitRoot = directHitRoot != null ? directHitRoot : transform;
        RaycastHit? nearestHit = null;
        RaycastHit? nearestRootHit = null;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!nearestHit.HasValue || hit.distance < nearestHit.Value.distance)
            {
                nearestHit = hit;
            }

            if (!IsColliderUnderRoot(hit.collider, hitRoot))
            {
                continue;
            }

            if (!nearestRootHit.HasValue || hit.distance < nearestRootHit.Value.distance)
            {
                nearestRootHit = hit;
            }
        }

        if (directRespectOcclusion &&
            nearestHit.HasValue &&
            !IsColliderUnderRoot(nearestHit.Value.collider, hitRoot))
        {
            return false;
        }

        if (!nearestRootHit.HasValue)
        {
            return false;
        }

        hitCollider = nearestRootHit.Value.collider;
        return true;
    }

    private void ResolveReferences()
    {
        if (clickSource == null)
        {
            clickSource = GetComponent<CommandMouseInteractable>();
        }

        ApplyTargetCameraToClickSource();
    }

    private void ApplyTargetCameraToClickSource()
    {
        if (!applyTargetCameraToClickSource ||
            clickSource == null ||
            targetCamera == null)
        {
            return;
        }

        clickSource.targetCamera = targetCamera;
        clickSource.autoUseMainCamera = false;
    }

    private Camera ResolveTargetCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        if (clickSource != null && clickSource.targetCamera != null)
        {
            return clickSource.targetCamera;
        }

        return Camera.main;
    }

    private CommandMouseInteractable ResolveCompletionGroupItem()
    {
        if (completionGroupItem != null)
        {
            return completionGroupItem;
        }

        if (completionInteractable != null)
        {
            return completionInteractable;
        }

        return clickSource;
    }

    private bool CompleteClickSourceIfNeeded()
    {
        if (!completeClickSourceOnTriggered ||
            clickSource == null ||
            clickSource == completionInteractable ||
            clickSource.IsCompleted)
        {
            return false;
        }

        clickSource.CompleteInteraction();
        return true;
    }

    private void SubscribeClickSource()
    {
        if (!autoBindClickSource || clickSource == null)
        {
            return;
        }

        if (subscribedClickSource == clickSource)
        {
            return;
        }

        UnsubscribeClickSource();
        clickSource.onClickCollider.RemoveListener(Trigger);
        clickSource.onClickCollider.AddListener(Trigger);
        subscribedClickSource = clickSource;
    }

    private void UnsubscribeClickSource()
    {
        if (subscribedClickSource != null)
        {
            subscribedClickSource.onClickCollider.RemoveListener(Trigger);
            subscribedClickSource = null;
        }
    }

    private static bool IsColliderUnderRoot(Collider hitCollider, Transform root)
    {
        if (hitCollider == null || root == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        return hitTransform == root || hitTransform.IsChildOf(root);
    }
}
