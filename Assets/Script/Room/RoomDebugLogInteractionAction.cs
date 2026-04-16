using UnityEngine;

[CreateAssetMenu(fileName = "RoomDebugLogInteractionAction", menuName = "Room/Interaction/Debug Log Action")]
public class RoomDebugLogInteractionAction : RoomInteractionAction
{
    [TextArea(2, 4)]
    public string message = "Room interaction triggered.";

    public override void Execute(RoomInteractionContext context)
    {
        Object logContext = context != null && context.Interactable != null
            ? context.Interactable
            : null;

        Debug.Log(message, logContext);
    }
}
