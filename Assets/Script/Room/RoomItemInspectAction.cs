using UnityEngine;

[CreateAssetMenu(fileName = "RoomItemInspectAction", menuName = "Room/Interaction/Item Inspect Action")]
public class RoomItemInspectAction : RoomInteractionAction
{
    [Header("Overlay")]
    public bool autoFindOverlay = true;

    [Header("Preview Source")]
    public GameObject previewPrefab;
    public bool useInteractableObjectWhenPrefabMissing = true;

    [Header("Text")]
    [Tooltip("支持 TextMeshPro 富文本，例如 <color=#ff6666>标题</color>、<size=120%>大字</size>、<b>加粗</b>。")]
    [TextArea(1, 3)]
    public string displayName;
    [Tooltip("支持 TextMeshPro 富文本，例如 <color=#cccccc>说明</color>、<size=80%>小字</size>、<b>加粗</b>。")]
    [TextArea(2, 4)]
    public string description;
    public bool useInteractableNameWhenDisplayNameEmpty = true;

    [Header("View")]
    public Vector3 initialEulerAngles = new Vector3(15f, -25f, 0f);
    public Vector3 localOffset = Vector3.zero;
    [Min(0.01f)]
    public float scale = 1f;
    [Min(0.2f)]
    public float cameraDistance = 3f;
    [Range(10f, 80f)]
    public float fieldOfView = 28f;

    [Header("Room Flow")]
    [InspectorName("检视期间锁定移动")]
    [Tooltip("打开物品检视界面期间是否关闭玩家移动。默认开启，避免玩家边检视边走动。")]
    public bool lockPlayerMovementWhileInspecting = true;

    public override void Execute(RoomInteractionContext context)
    {
        RoomItemInspectOverlay overlay = ResolveOverlay();
        if (overlay == null)
        {
            Debug.LogWarning("[RoomItemInspectAction] No RoomItemInspectOverlay found.");
            return;
        }

        GameObject previewObject = ResolvePreviewObject(context);
        if (previewObject == null)
        {
            Debug.LogWarning("[RoomItemInspectAction] No preview object found.", this);
            return;
        }

        overlay.Open(
            previewObject,
            context,
            ResolveDisplayName(context),
            description,
            initialEulerAngles,
            localOffset,
            scale,
            cameraDistance,
            fieldOfView,
            lockPlayerMovementWhileInspecting);
    }

    private RoomItemInspectOverlay ResolveOverlay()
    {
        if (RoomItemInspectOverlay.Instance != null)
        {
            return RoomItemInspectOverlay.Instance;
        }

        return autoFindOverlay ? FindObjectOfType<RoomItemInspectOverlay>(true) : null;
    }

    private GameObject ResolvePreviewObject(RoomInteractionContext context)
    {
        if (previewPrefab != null)
        {
            return previewPrefab;
        }

        if (!useInteractableObjectWhenPrefabMissing || context == null || context.Interactable == null)
        {
            return null;
        }

        return context.Interactable.gameObject;
    }

    private string ResolveDisplayName(RoomInteractionContext context)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        if (!useInteractableNameWhenDisplayNameEmpty || context == null || context.Interactable == null)
        {
            return string.Empty;
        }

        return context.Interactable.name;
    }
}
