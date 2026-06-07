using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Command/Enable Top Down Player Movement Control")]
public class CommandEnableTopDownPlayerMovementControl : CommandTopDownPlayerMovementControlBehaviour
{
    public override void Execute(RoomInteractionContext context)
    {
        ApplyMovementControlState(true, context);
    }

    public void Apply()
    {
        EnableMovementControl();
    }

    public void EnableMovementControl()
    {
        ApplyMovementControlState(true);
    }
}
