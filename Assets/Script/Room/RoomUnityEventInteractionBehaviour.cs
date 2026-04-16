using UnityEngine;
using UnityEngine.Events;

public class RoomUnityEventInteractionBehaviour : RoomInteractionBehaviour
{
    [Header("Events")]
    public UnityEvent onInteract = new UnityEvent();
    public RoomGameObjectEvent onInteractWithPlayer = new RoomGameObjectEvent();
    public RoomInteractableEvent onInteractWithTarget = new RoomInteractableEvent();

    public override void Execute(RoomInteractionContext context)
    {
        onInteract.Invoke();

        if (context == null)
        {
            onInteractWithPlayer.Invoke(null);
            onInteractWithTarget.Invoke(null);
            return;
        }

        onInteractWithPlayer.Invoke(context.Player);
        onInteractWithTarget.Invoke(context.Interactable);
    }
}
