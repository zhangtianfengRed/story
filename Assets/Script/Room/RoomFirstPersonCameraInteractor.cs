using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Room/First Person Camera Interactor")]
public class RoomFirstPersonCameraInteractor : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private Transform yawTransform;
    [SerializeField] private Transform pitchTransform;
    [SerializeField] private Camera interactionCamera;
    [SerializeField, Min(0f)] private float mouseSensitivity = 2f;
    [SerializeField, Range(0f, 180f)] private float horizontalAngleLimit = 60f;
    [SerializeField] private Vector2 verticalAngleLimits = new Vector2(-35f, 35f);
    [SerializeField] private bool invertVerticalLook;

    [Header("Interaction")]
    [SerializeField, Min(0f)] private float interactionDistance = 3f;
    [SerializeField] private LayerMask interactionLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private RoomInteractionPromptUI promptUI;
    [SerializeField] private bool autoFindPromptUI = true;

    [Header("Center Reticle")]
    [SerializeField] private Image reticleImage;
    [SerializeField] private bool createReticleAutomatically = true;
    [SerializeField] private Vector2 reticleSize = new Vector2(6f, 6f);
    [SerializeField] private Color idleReticleColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color targetReticleColor = new Color(0.25f, 1f, 0.65f, 1f);
    [SerializeField] private int reticleSortingOrder = 1000;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnEnable = true;
    [SerializeField] private bool clickGameViewToRelock = true;
    [SerializeField] private KeyCode unlockCursorKey = KeyCode.Escape;
    [SerializeField] private bool restoreCursorStateOnDisable = true;

    public RoomInteractable CurrentTarget { get; private set; }

    private Quaternion initialYawRotation;
    private Quaternion initialPitchRotation;
    private float yawOffset;
    private float pitchOffset;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool cursorStateStored;
    private bool viewInitialized;

    private void Awake()
    {
        ResolveReferences();
        CaptureInitialView();
        EnsureReticle();
    }

    private void OnEnable()
    {
        if (!viewInitialized)
        {
            ResolveReferences();
            CaptureInitialView();
        }

        EnsureReticle();
        StoreCursorState();

        if (lockCursorOnEnable)
        {
            LockCursor();
        }
    }

    private void Update()
    {
        UpdateCursorState();

        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
        if (cursorLocked)
        {
            UpdateView();
        }

        RefreshTarget(cursorLocked);

        if (cursorLocked && CurrentTarget != null && Input.GetKeyDown(interactionKey))
        {
            CurrentTarget.Interact(gameObject);
            RefreshTarget(true);
        }

        UpdateReticleVisibility(cursorLocked);
    }

    private void OnDisable()
    {
        SetCurrentTarget(null);
        UpdateReticleVisibility(false);
        RestoreCursorState();
    }

    private void OnValidate()
    {
        mouseSensitivity = Mathf.Max(0f, mouseSensitivity);
        horizontalAngleLimit = Mathf.Clamp(horizontalAngleLimit, 0f, 180f);
        interactionDistance = Mathf.Max(0f, interactionDistance);
        reticleSize.x = Mathf.Max(1f, reticleSize.x);
        reticleSize.y = Mathf.Max(1f, reticleSize.y);
    }

    public void ResetView()
    {
        yawOffset = 0f;
        pitchOffset = 0f;
        ApplyViewRotation();
    }

    public void RefreshTarget()
    {
        RefreshTarget(Cursor.lockState == CursorLockMode.Locked);
    }

    private void ResolveReferences()
    {
        if (yawTransform == null)
        {
            yawTransform = transform;
        }

        if (interactionCamera == null)
        {
            interactionCamera = GetComponentInChildren<Camera>(true);
        }

        if (pitchTransform == null && interactionCamera != null)
        {
            pitchTransform = interactionCamera.transform;
        }

        if (promptUI == null && autoFindPromptUI)
        {
            promptUI = FindObjectOfType<RoomInteractionPromptUI>(true);
        }
    }

    private void CaptureInitialView()
    {
        if (yawTransform == null || pitchTransform == null || interactionCamera == null)
        {
            Debug.LogWarning(
                $"[{nameof(RoomFirstPersonCameraInteractor)}] Camera references are incomplete on {name}.",
                this);
            viewInitialized = false;
            return;
        }

        initialYawRotation = yawTransform.localRotation;
        initialPitchRotation = pitchTransform.localRotation;
        yawOffset = 0f;
        pitchOffset = 0f;
        viewInitialized = true;
    }

    private void UpdateView()
    {
        if (!viewInitialized)
        {
            return;
        }

        float horizontalInput = Input.GetAxisRaw("Mouse X");
        float verticalInput = Input.GetAxisRaw("Mouse Y");
        float verticalDirection = invertVerticalLook ? 1f : -1f;

        yawOffset = Mathf.Clamp(
            yawOffset + horizontalInput * mouseSensitivity,
            -horizontalAngleLimit,
            horizontalAngleLimit);

        float minPitch = Mathf.Min(verticalAngleLimits.x, verticalAngleLimits.y);
        float maxPitch = Mathf.Max(verticalAngleLimits.x, verticalAngleLimits.y);
        pitchOffset = Mathf.Clamp(
            pitchOffset + verticalInput * mouseSensitivity * verticalDirection,
            minPitch,
            maxPitch);

        ApplyViewRotation();
    }

    private void ApplyViewRotation()
    {
        if (!viewInitialized)
        {
            return;
        }

        if (yawTransform == pitchTransform)
        {
            yawTransform.localRotation =
                initialYawRotation * Quaternion.Euler(pitchOffset, yawOffset, 0f);
            return;
        }

        yawTransform.localRotation =
            initialYawRotation * Quaternion.Euler(0f, yawOffset, 0f);
        pitchTransform.localRotation =
            initialPitchRotation * Quaternion.Euler(pitchOffset, 0f, 0f);
    }

    private void RefreshTarget(bool canTarget)
    {
        RoomInteractable nextTarget = canTarget ? FindTargetAtScreenCenter() : null;
        SetCurrentTarget(nextTarget);
    }

    private RoomInteractable FindTargetAtScreenCenter()
    {
        if (interactionCamera == null || interactionDistance <= 0f)
        {
            return null;
        }

        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                interactionDistance,
                interactionLayers,
                triggerInteraction))
        {
            return null;
        }

        RoomInteractable target = hit.collider.GetComponentInParent<RoomInteractable>();
        return target != null && target.IsInRange(ray.origin) ? target : null;
    }

    private void SetCurrentTarget(RoomInteractable nextTarget)
    {
        if (CurrentTarget != nextTarget)
        {
            if (CurrentTarget != null)
            {
                CurrentTarget.SetHighlightState(false, false);
            }

            CurrentTarget = nextTarget;

            if (CurrentTarget != null)
            {
                CurrentTarget.SetHighlightState(true, true);
            }
        }

        if (promptUI != null)
        {
            if (CurrentTarget != null)
            {
                promptUI.Show(CurrentTarget, interactionKey);
            }
            else
            {
                promptUI.Hide();
            }
        }

        if (reticleImage != null)
        {
            reticleImage.color =
                CurrentTarget != null ? targetReticleColor : idleReticleColor;
        }
    }

    private void EnsureReticle()
    {
        if (reticleImage != null || !createReticleAutomatically)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "First Person Reticle Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = reticleSortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject reticleObject = new GameObject(
            "Center Dot",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        reticleObject.transform.SetParent(canvasObject.transform, false);

        RectTransform reticleRect = reticleObject.GetComponent<RectTransform>();
        reticleRect.anchorMin = new Vector2(0.5f, 0.5f);
        reticleRect.anchorMax = new Vector2(0.5f, 0.5f);
        reticleRect.pivot = new Vector2(0.5f, 0.5f);
        reticleRect.anchoredPosition = Vector2.zero;
        reticleRect.sizeDelta = reticleSize;

        reticleImage = reticleObject.GetComponent<Image>();
        reticleImage.color = idleReticleColor;
        reticleImage.raycastTarget = false;
    }

    private void UpdateReticleVisibility(bool visible)
    {
        if (reticleImage != null)
        {
            reticleImage.gameObject.SetActive(visible);
        }
    }

    private void UpdateCursorState()
    {
        if (Input.GetKeyDown(unlockCursorKey))
        {
            UnlockCursor();
            return;
        }

        if (!clickGameViewToRelock ||
            Cursor.lockState == CursorLockMode.Locked ||
            !Input.GetMouseButtonDown(0))
        {
            return;
        }

        if (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())
        {
            LockCursor();
        }
    }

    private void StoreCursorState()
    {
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cursorStateStored = true;
    }

    private void RestoreCursorState()
    {
        if (!restoreCursorStateOnDisable || !cursorStateStored)
        {
            return;
        }

        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
        cursorStateStored = false;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
