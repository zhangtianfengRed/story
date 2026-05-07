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
    public bool faceCamera = true;
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
        UpdateWorldTransform();
    }

    public void Show(RoomInteractable interactable, KeyCode key)
    {
        if (interactable == null)
        {
            Hide();
            return;
        }

        currentInteractable = interactable;
        SetText(interactable.GetPromptText(key));
        UpdateWorldTransform();
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
        if (directionToCamera.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(directionToCamera, Vector3.up);
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
}
