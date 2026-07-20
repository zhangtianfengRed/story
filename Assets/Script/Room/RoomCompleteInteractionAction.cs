using UnityEngine;

[CreateAssetMenu(
    fileName = "RoomCompleteInteractionAction",
    menuName = "Room/Interaction/Complete Interaction Action")]
public class RoomCompleteInteractionAction : RoomInteractionAction
{
    public override void Execute(RoomInteractionContext context)
    {
        if (context == null || context.Interactable == null)
        {
            Debug.LogWarning(
                "[RoomCompleteInteractionAction] Cannot complete the interaction because the RoomInteractable context is missing.",
                this);
            return;
        }

        context.Interactable.CompleteInteraction();
    }
}
