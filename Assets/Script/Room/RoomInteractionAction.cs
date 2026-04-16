using UnityEngine;

public abstract class RoomInteractionAction : ScriptableObject
{
    public abstract void Execute(RoomInteractionContext context);
}
