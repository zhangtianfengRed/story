using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Command/Disable Top Down Player Movement Control")]
public class CommandDisableTopDownPlayerMovementControl : CommandTopDownPlayerMovementControlBehaviour
{
    public override void Execute(RoomInteractionContext context)
    {
        ApplyMovementControlState(false, context);
    }

    public void Apply()
    {
        DisableMovementControl();
    }

    public void DisableMovementControl()
    {
        ApplyMovementControlState(false);
    }
}
