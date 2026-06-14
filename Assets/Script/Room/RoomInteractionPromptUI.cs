using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomInteractionPromptUI : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup canvasGroup;
    public TMP_Text promptText;
    public Text legacyPromptText;

    [Header("World Follow")]
    public bool followInteractableInWorld = true;
    [Tooltip("开启后显示期间每帧更新位置和朝向。关闭时只在切换目标或首次显示时放到交互物上方。")]
    public bool updateWorldTransformWhileVisible;
    public bool faceCamera = true;
    [Tooltip("面向摄像机时保持 UI 竖直，只绕世界 Y 轴转向摄像机。")]
    public bool keepUprightWhenFacingCamera = true;
    [Tooltip("面向摄像机后额外叠加的本地旋转。World Space UI 需要朝向 Top 时通常使用 X=90。")]
    public Vector3 promptRotationOffset = new Vector3(90f, 0f, 0f);
    public Camera targetCamera;
    public bool autoFindCamera = true;

    [Header("Initial State")]
    public bool hideOnAwake = true;

    private RoomInteractable currentInteractable;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (promptText == null)
        {
            promptText = GetComponentInChildren<TMP_Text>(true);
        }

        if (legacyPromptText == null)
        {
            legacyPromptText = GetComponentInChildren<Text>(true);
        }

        EnsureCamera();

        if (hideOnAwake)
        {
            Hide();
        }
    }

    private void LateUpdate()
    {
        if (updateWorldTransformWhileVisible)
        {
            UpdateWorldTransform();
        }
    }

    public void Show(RoomInteractable interactable, KeyCode key)
    {
        if (interactable == null)
        {
            Hide();
            return;
        }

        bool targetChanged = currentInteractable != interactable;
        bool wasVisible = IsVisible();

        currentInteractable = interactable;
        SetText(interactable.GetPromptText(key));

        if (targetChanged || !wasVisible || updateWorldTransformWhileVisible)
        {
            UpdateWorldTransform();
        }

        SetVisible(true);
    }

    public void Hide()
    {
        currentInteractable = null;
        SetVisible(false);
    }

    private void UpdateWorldTransform()
    {
        if (!followInteractableInWorld || currentInteractable == null)
        {
            return;
        }

        transform.position = currentInteractable.PromptWorldPosition;

        if (!faceCamera)
        {
            return;
        }

        EnsureCamera();

        if (targetCamera == null)
        {
            return;
        }

        Vector3 directionToCamera = transform.position - targetCamera.transform.position;
        if (keepUprightWhenFacingCamera)
        {
            Vector3 flatDirectionToCamera = Vector3.ProjectOnPlane(directionToCamera, Vector3.up);
            if (flatDirectionToCamera.sqrMagnitude > 0.0001f)
            {
                directionToCamera = flatDirectionToCamera;
            }
        }

        if (directionToCamera.sqrMagnitude > 0.0001f)
        {
            transform.rotation =
                Quaternion.LookRotation(directionToCamera, Vector3.up) *
                Quaternion.Euler(promptRotationOffset);
        }
    }

    private void EnsureCamera()
    {
        if (targetCamera == null && autoFindCamera)
        {
            targetCamera = Camera.main;
        }
    }

    private void SetText(string text)
    {
        if (promptText != null)
        {
            promptText.text = text;
        }

        if (legacyPromptText != null)
        {
            legacyPromptText.text = text;
        }
    }

    private void SetVisible(bool visible)
    {
        if (visible && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return;
        }

        if (gameObject.activeSelf != visible)
        {
            gameObject.SetActive(visible);
        }
    }

    private bool IsVisible()
    {
        if (canvasGroup != null)
        {
            return canvasGroup.alpha > 0.001f;
        }

        return gameObject.activeSelf;
    }
}
