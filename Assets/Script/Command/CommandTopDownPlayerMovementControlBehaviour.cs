using UnityEngine;
using UnityEngine.Events;

public abstract class CommandTopDownPlayerMovementControlBehaviour : RoomInteractionBehaviour
{
    [Header("Target")]
    [Tooltip("要切换控制状态的 RoomTopDownPlayerMovement。留空时可自动查找。")]
    public RoomTopDownPlayerMovement targetMovement;

    [Tooltip("目标为空时，优先从互动上下文里的玩家对象查找。")]
    public bool useContextPlayerIfTargetMissing = true;

    [Tooltip("从互动上下文里的玩家对象查找时，是否同时查找子对象。")]
    public bool searchContextPlayerChildren = true;

    [Tooltip("目标为空时，再从当前对象和子对象查找。")]
    public bool searchChildrenIfTargetMissing = true;

    [Tooltip("目标为空时，最后在场景里自动查找第一个 RoomTopDownPlayerMovement。")]
    public bool searchSceneIfTargetMissing = true;

    [Tooltip("自动查找时是否包含未激活对象。")]
    public bool includeInactiveTargets = true;

    [Tooltip("目标为空时是否打印警告，方便检查场景配置。")]
    public bool warnIfTargetMissing = true;

    [Header("Trigger")]
    [Tooltip("触发一次后禁用本组件，避免重复响应点击。")]
    public bool triggerOnce;

    [Header("Events")]
    public UnityEvent onControlChanged = new UnityEvent();

    private bool hasTriggered;

    protected virtual void Awake()
    {
        ResolveTargetMovement(null);
    }

    public void ResetTrigger()
    {
        hasTriggered = false;
        enabled = true;
    }

    protected void ApplyMovementControlState(bool movementControlEnabled)
    {
        ApplyMovementControlState(movementControlEnabled, null);
    }

    protected void ApplyMovementControlState(bool movementControlEnabled, RoomInteractionContext context)
    {
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        RoomTopDownPlayerMovement movement = ResolveTargetMovement(context);
        if (movement == null)
        {
            WarnMissingTarget();
            return;
        }

        hasTriggered = true;
        movement.SetMovementControlEnabled(movementControlEnabled);
        onControlChanged.Invoke();

        if (triggerOnce)
        {
            enabled = false;
        }
    }

    protected RoomTopDownPlayerMovement ResolveTargetMovement(RoomInteractionContext context)
    {
        if (targetMovement != null)
        {
            return targetMovement;
        }

        if (useContextPlayerIfTargetMissing && context != null && context.Player != null)
        {
            targetMovement = searchContextPlayerChildren
                ? context.Player.GetComponentInChildren<RoomTopDownPlayerMovement>(true)
                : context.Player.GetComponent<RoomTopDownPlayerMovement>();

            if (targetMovement != null)
            {
                return targetMovement;
            }
        }

        if (searchChildrenIfTargetMissing)
        {
            targetMovement = GetComponentInChildren<RoomTopDownPlayerMovement>(includeInactiveTargets);
            if (targetMovement != null)
            {
                return targetMovement;
            }
        }

        if (searchSceneIfTargetMissing)
        {
            targetMovement = FindObjectOfType<RoomTopDownPlayerMovement>(includeInactiveTargets);
        }

        return targetMovement;
    }

    protected void WarnMissingTarget()
    {
        if (warnIfTargetMissing)
        {
            Debug.LogWarning($"[{GetType().Name}] Target movement is not assigned or found.", this);
        }
    }
}
