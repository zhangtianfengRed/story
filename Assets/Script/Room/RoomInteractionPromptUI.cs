using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomInteractionPromptUI : MonoBehaviour
{
    [Header("References")]
    public CanvasGroup canvasGroup;
    public TMP_Text promptText;
    public Text legacyPromptText;

    [Header("Initial State")]
    public bool hideOnAwake = true;

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

        if (hideOnAwake)
        {
            Hide();
        }
    }

    public void Show(RoomInteractable interactable, KeyCode key)
    {
        if (interactable == null)
        {
            Hide();
            return;
        }

        SetText(interactable.GetPromptText(key));
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
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
