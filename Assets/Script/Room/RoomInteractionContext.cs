using UnityEngine;

public sealed class RoomInteractionContext
{
    public RoomInteractionContext(GameObject player, RoomInteractable interactable, float distance)
    {
        Player = player;
        Interactable = interactable;
        Distance = distance;
    }

    public GameObject Player { get; private set; }
    public RoomInteractable Interactable { get; private set; }
    public float Distance { get; private set; }

    public Transform PlayerTransform
    {
        get { return Player != null ? Player.transform : null; }
    }

    public Transform InteractableTransform
    {
        get { return Interactable != null ? Interactable.transform : null; }
    }
}
