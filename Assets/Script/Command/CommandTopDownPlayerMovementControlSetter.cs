using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Command/Top Down Player Movement Control Setter")]
public class CommandTopDownPlayerMovementControlSetter : CommandTopDownPlayerMovementControlBehaviour
{
    [Header("Control")]
    [Tooltip("调用 ApplyConfiguredState 时设置的移动控制状态。关闭即屏蔽控制。")]
    public bool movementControlEnabled;

    public override void Execute(RoomInteractionContext context)
    {
        ApplyMovementControlState(movementControlEnabled, context);
    }

    public void ApplyConfiguredState()
    {
        ApplyMovementControlState(movementControlEnabled);
    }

    public void SetMovementControlEnabled(bool enabled)
    {
        ApplyMovementControlState(enabled);
    }

    public void EnableMovementControl()
    {
        ApplyMovementControlState(true);
    }

    public void DisableMovementControl()
    {
        ApplyMovementControlState(false);
    }

    public void ToggleMovementControl()
    {
        RoomTopDownPlayerMovement movement = ResolveTargetMovement(null);
        if (movement == null)
        {
            WarnMissingTarget();
            return;
        }

        ApplyMovementControlState(!movement.MovementControlEnabled);
    }
}
