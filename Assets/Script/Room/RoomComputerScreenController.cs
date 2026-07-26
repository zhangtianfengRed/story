using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Room/Computer Screen Controller")]
public class RoomComputerScreenController : RoomInteractionBehaviour
{
    [Header("Screen Objects")]
    [SerializeField] private GameObject screenRoot;
    [SerializeField] private CanvasGroup screenCanvasGroup;

    [Header("Desktop Windows")]
    [SerializeField] private GameObject windowFrame;
    [SerializeField] private GameObject inboxWindow;
    [SerializeField] private GameObject tasksWindow;
    [SerializeField] private GameObject documentsWindow;

    [Header("Office Workday Gameplay")]
    [SerializeField] private OfficeComputerWorkdayDefinition workdayDefinition;
    [SerializeField] private Button mailDesktopButton;
    [SerializeField] private Button tasksDesktopButton;
    [SerializeField] private Button documentsDesktopButton;
    [SerializeField] private TMP_Text inboxTitleText;
    [SerializeField] private TMP_Text inboxBodyText;
    [SerializeField] private TMP_Text tasksTitleText;
    [SerializeField] private TMP_Text tasksBodyText;
    [SerializeField] private TMP_Text taskFeedbackText;
    [SerializeField] private TMP_Text documentsTitleText;
    [SerializeField] private TMP_Text documentsBodyText;
    [SerializeField] private Button[] decisionButtons;
    [SerializeField] private TMP_Text[] decisionButtonTexts;

    [Header("Behaviour")]
    [SerializeField] private bool hideScreenOnAwake = true;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField, Min(0f)] private float fadeDuration = 0.18f;
    [SerializeField] private bool useUnscaledTime = true;

    public bool IsOpen { get; private set; }

    private Coroutine fadeCoroutine;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool cursorStateStored;
    private RoomFirstPersonCameraInteractor blockedFirstPersonInteractor;
    private RoomInteractable activeEntryInteractable;
    private OfficeComputerCaseDefinition activeCase;
    private string taskFeedback;

    private void Awake()
    {
        if (hideScreenOnAwake && screenRoot != null)
        {
            screenRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(closeKey))
        {
            Close();
        }
    }

    private void OnDisable()
    {
        CloseImmediate();
    }

    public override void Execute(RoomInteractionContext context)
    {
        if (context != null && context.Interactable != null)
        {
            activeEntryInteractable = context.Interactable;
        }

        Open(context);
    }

    public void Open()
    {
        Open(null);
    }

    private void Open(RoomInteractionContext context)
    {
        if (screenRoot == null)
        {
            Debug.LogWarning(
                $"[{nameof(RoomComputerScreenController)}] Screen root is not assigned on {name}.",
                this);
            return;
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        BlockFirstPersonInput(context);
        StoreCursorState();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        screenRoot.SetActive(true);
        activeCase = FindFirstIncompleteCase();
        taskFeedback = string.Empty;
        RefreshDesktopButtons();
        ShowDesktop();

        if (screenCanvasGroup != null)
        {
            screenCanvasGroup.interactable = true;
            screenCanvasGroup.blocksRaycasts = true;
            StartFade(1f, false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;
        RestoreCursorState();
        ReleaseFirstPersonInput();

        if (screenCanvasGroup == null || fadeDuration <= 0f)
        {
            HideScreen();
            return;
        }

        screenCanvasGroup.interactable = false;
        screenCanvasGroup.blocksRaycasts = false;
        StartFade(0f, true);
    }

    public void ShowInbox()
    {
        if (!EnsureActiveCase())
        {
            ShowCompletedWorkday();
            return;
        }

        MarkCompletedIfNeeded(activeCase.MailProgressId);
        SetText(inboxTitleText, activeCase.MailTitle);
        SetText(inboxBodyText, activeCase.MailBody);
        RefreshDesktopButtons();
        ShowWindow(inboxWindow);
    }

    public void ShowTasks()
    {
        if (!EnsureActiveCase())
        {
            ShowCompletedWorkday();
            return;
        }

        SetText(tasksTitleText, activeCase.TaskTitle);
        SetText(tasksBodyText, activeCase.TaskBody);

        if (!AreTaskPrerequisitesComplete(activeCase))
        {
            taskFeedback = activeCase.TaskLockedMessage;
        }

        RefreshDecisionButtons();
        ShowWindow(tasksWindow);
    }

    public void ShowDocuments()
    {
        if (!EnsureActiveCase())
        {
            ShowCompletedWorkday();
            return;
        }

        MarkCompletedIfNeeded(activeCase.DocumentsProgressId);
        SetText(documentsTitleText, activeCase.DocumentsTitle);
        SetText(documentsBodyText, activeCase.DocumentsBody);
        RefreshDesktopButtons();
        ShowWindow(documentsWindow);
    }

    public void ShowDesktop()
    {
        ShowWindow(null);
    }

    public void SelectDecision(int decisionIndex)
    {
        if (!EnsureActiveCase())
        {
            ShowCompletedWorkday();
            return;
        }

        if (!AreTaskPrerequisitesComplete(activeCase))
        {
            taskFeedback = activeCase.TaskLockedMessage;
            RefreshDecisionButtons();
            return;
        }

        if (IsCompleted(activeCase.TaskProgressId))
        {
            RefreshDecisionButtons();
            return;
        }

        if (activeCase.Decisions == null ||
            decisionIndex < 0 ||
            decisionIndex >= activeCase.Decisions.Count)
        {
            Debug.LogWarning(
                $"[{nameof(RoomComputerScreenController)}] Decision index {decisionIndex} is not configured for '{activeCase.CaseId}'.",
                this);
            return;
        }

        OfficeComputerDecisionDefinition decision = activeCase.Decisions[decisionIndex];
        if (decision == null)
        {
            return;
        }

        taskFeedback = decision.Feedback;
        if (!decision.IsCorrect)
        {
            MarkOpenedIfConfigured(activeCase.IncorrectDecisionProgressId);
            RefreshDecisionButtons();
            return;
        }

        MarkCompletedIfNeeded(activeCase.TaskProgressId);
        CompleteEntryIfWorkdayFinished();
        RefreshDesktopButtons();
        RefreshDecisionButtons();
    }

    private void ShowWindow(GameObject selectedWindow)
    {
        SetWindowVisible(windowFrame, selectedWindow != null);
        SetWindowVisible(inboxWindow, inboxWindow == selectedWindow);
        SetWindowVisible(tasksWindow, tasksWindow == selectedWindow);
        SetWindowVisible(documentsWindow, documentsWindow == selectedWindow);
    }

    private static void SetWindowVisible(GameObject window, bool visible)
    {
        if (window != null)
        {
            window.SetActive(visible);
        }
    }

    private bool EnsureActiveCase()
    {
        if (activeCase != null && !IsCaseCompleted(activeCase))
        {
            return true;
        }

        activeCase = FindFirstIncompleteCase();
        return activeCase != null;
    }

    private OfficeComputerCaseDefinition FindFirstIncompleteCase()
    {
        if (workdayDefinition == null || workdayDefinition.Cases == null)
        {
            return null;
        }

        for (int i = 0; i < workdayDefinition.Cases.Count; i++)
        {
            OfficeComputerCaseDefinition candidate = workdayDefinition.Cases[i];
            if (candidate != null && !IsCaseCompleted(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private bool IsCaseCompleted(OfficeComputerCaseDefinition caseDefinition)
    {
        return caseDefinition != null && IsCompleted(caseDefinition.TaskProgressId);
    }

    private bool AreTaskPrerequisitesComplete(OfficeComputerCaseDefinition caseDefinition)
    {
        return caseDefinition != null &&
               IsCompleted(caseDefinition.MailProgressId) &&
               IsCompleted(caseDefinition.DocumentsProgressId);
    }

    private bool IsCompleted(string progressId)
    {
        return !string.IsNullOrWhiteSpace(progressId) &&
               RoomInteractionProgressManager.Instance.IsCompleted(progressId);
    }

    private void MarkCompletedIfNeeded(string progressId)
    {
        if (string.IsNullOrWhiteSpace(progressId) || IsCompleted(progressId))
        {
            return;
        }

        RoomInteractionProgressManager.Instance.MarkCompleted(progressId);
    }

    private void MarkOpenedIfConfigured(string progressId)
    {
        if (!string.IsNullOrWhiteSpace(progressId))
        {
            RoomInteractionProgressManager.Instance.MarkOpened(progressId);
        }
    }

    private void RefreshDesktopButtons()
    {
        OfficeComputerCaseDefinition nextCase = FindFirstIncompleteCase();
        bool hasPendingCase = nextCase != null;

        SetButtonInteractable(mailDesktopButton, hasPendingCase);
        SetButtonInteractable(
            documentsDesktopButton,
            hasPendingCase && IsCompleted(nextCase.MailProgressId));
        SetButtonInteractable(
            tasksDesktopButton,
            !hasPendingCase ||
            (IsCompleted(nextCase.MailProgressId) && IsCompleted(nextCase.DocumentsProgressId)));
    }

    private void RefreshDecisionButtons()
    {
        bool canSubmit = activeCase != null &&
                         AreTaskPrerequisitesComplete(activeCase) &&
                         !IsCaseCompleted(activeCase);

        if (decisionButtons == null)
        {
            SetText(taskFeedbackText, taskFeedback);
            return;
        }

        int decisionCount = activeCase != null && activeCase.Decisions != null
            ? activeCase.Decisions.Count
            : 0;

        for (int i = 0; i < decisionButtons.Length; i++)
        {
            Button button = decisionButtons[i];
            if (button == null)
            {
                continue;
            }

            bool hasDecision = i < decisionCount &&
                               activeCase.Decisions[i] != null;
            button.gameObject.SetActive(hasDecision);
            button.interactable = canSubmit;

            if (hasDecision && decisionButtonTexts != null && i < decisionButtonTexts.Length)
            {
                SetText(decisionButtonTexts[i], activeCase.Decisions[i].Label);
            }
        }

        SetText(taskFeedbackText, taskFeedback);
    }

    private void ShowCompletedWorkday()
    {
        if (workdayDefinition != null)
        {
            SetText(tasksTitleText, workdayDefinition.CompletedTitle);
            SetText(tasksBodyText, workdayDefinition.CompletedBody);
        }

        taskFeedback = string.Empty;
        activeCase = null;
        RefreshDecisionButtons();
        RefreshDesktopButtons();
        ShowWindow(tasksWindow);
    }

    private void CompleteEntryIfWorkdayFinished()
    {
        if (FindFirstIncompleteCase() != null ||
            activeEntryInteractable == null ||
            string.IsNullOrWhiteSpace(activeEntryInteractable.progressId) ||
            IsCompleted(activeEntryInteractable.progressId))
        {
            return;
        }

        activeEntryInteractable.CompleteFromScript();
    }

    private static void SetButtonInteractable(Button button, bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private void StartFade(float targetAlpha, bool hideAfterFade)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        if (!gameObject.activeInHierarchy)
        {
            screenCanvasGroup.alpha = targetAlpha;
            fadeCoroutine = null;

            if (hideAfterFade)
            {
                HideScreen();
            }

            return;
        }

        fadeCoroutine = StartCoroutine(FadeRoutine(targetAlpha, hideAfterFade));
    }

    private IEnumerator FadeRoutine(float targetAlpha, bool hideAfterFade)
    {
        float startAlpha = screenCanvasGroup.alpha;
        float safeDuration = Mathf.Max(0.0001f, fadeDuration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            screenCanvasGroup.alpha =
                Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        screenCanvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;

        if (hideAfterFade)
        {
            HideScreen();
        }
    }

    private void HideScreen()
    {
        if (screenRoot != null)
        {
            screenRoot.SetActive(false);
        }
    }

    private void CloseImmediate()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        IsOpen = false;
        RestoreCursorState();
        ReleaseFirstPersonInput();

        if (screenCanvasGroup != null)
        {
            screenCanvasGroup.alpha = 0f;
            screenCanvasGroup.interactable = false;
            screenCanvasGroup.blocksRaycasts = false;
        }

        HideScreen();
    }

    private void StoreCursorState()
    {
        if (cursorStateStored)
        {
            return;
        }

        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorStateStored = true;
    }

    private void RestoreCursorState()
    {
        if (!cursorStateStored)
        {
            return;
        }

        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        cursorStateStored = false;
    }

    private void BlockFirstPersonInput(RoomInteractionContext context)
    {
        if (context != null && context.Player != null)
        {
            blockedFirstPersonInteractor =
                context.Player.GetComponentInChildren<RoomFirstPersonCameraInteractor>(true);
        }

        if (blockedFirstPersonInteractor == null)
        {
            blockedFirstPersonInteractor =
                FindObjectOfType<RoomFirstPersonCameraInteractor>(true);
        }

        if (blockedFirstPersonInteractor != null)
        {
            blockedFirstPersonInteractor.SetInterfaceInputBlocked(true);
        }
    }

    private void ReleaseFirstPersonInput()
    {
        if (blockedFirstPersonInteractor == null)
        {
            return;
        }

        blockedFirstPersonInteractor.SetInterfaceInputBlocked(false);
        blockedFirstPersonInteractor = null;
    }
}
