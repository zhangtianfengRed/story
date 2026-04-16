using UnityEngine;

public abstract class RoomInteractionBehaviour : MonoBehaviour
{
    public abstract void Execute(RoomInteractionContext context);
}
