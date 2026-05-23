using UnityEngine;

public class RoomObjectActivator : RoomInteractionBehaviour
{
    [Header("Target")]
    [SerializeField] private GameObject targetObject;

    [Tooltip("目标为空时是否打印警告，方便检查场景配置。")]
    [SerializeField] private bool warnIfTargetMissing = true;

    public override void Execute(RoomInteractionContext context)
    {
        if (targetObject == null)
        {
            if (warnIfTargetMissing)
            {
                Debug.LogWarning("[RoomObjectActivator] Target object is not assigned.", this);
            }

            return;
        }

        targetObject.SetActive(true);
    }
}
