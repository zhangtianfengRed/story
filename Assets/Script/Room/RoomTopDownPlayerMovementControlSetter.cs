using UnityEngine;

public class RoomTopDownPlayerMovementControlSetter : RoomInteractionBehaviour
{
    [Header("Target")]
    [SerializeField] private RoomTopDownPlayerMovement targetMovement;

    [Tooltip("没有手动指定目标时，从互动上下文里的玩家对象上查找移动组件。")]
    [SerializeField] private bool useContextPlayerIfTargetMissing = true;

    [Tooltip("从玩家对象查找时，是否同时查找子对象。")]
    [SerializeField] private bool searchPlayerChildren = true;

    [Tooltip("目标为空时是否打印警告，方便检查场景配置。")]
    [SerializeField] private bool warnIfTargetMissing = true;

    [Header("Control")]
    [Tooltip("执行互动后是否允许玩家通过 RoomTopDownPlayerMovement 控制移动。关闭即屏蔽移动控制。")]
    [SerializeField] private bool movementControlEnabled;

    public override void Execute(RoomInteractionContext context)
    {
        RoomTopDownPlayerMovement movement = ResolveTargetMovement(context);
        if (movement == null)
        {
            if (warnIfTargetMissing)
            {
                Debug.LogWarning("[RoomTopDownPlayerMovementControlSetter] Target movement is not assigned or found.", this);
            }

            return;
        }

        movement.SetMovementControlEnabled(movementControlEnabled);
    }

    private RoomTopDownPlayerMovement ResolveTargetMovement(RoomInteractionContext context)
    {
        if (targetMovement != null)
        {
            return targetMovement;
        }

        if (!useContextPlayerIfTargetMissing || context == null || context.Player == null)
        {
            return null;
        }

        return searchPlayerChildren
            ? context.Player.GetComponentInChildren<RoomTopDownPlayerMovement>(true)
            : context.Player.GetComponent<RoomTopDownPlayerMovement>();
    }
}
