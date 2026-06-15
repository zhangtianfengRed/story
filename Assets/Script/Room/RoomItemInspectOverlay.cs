using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
[AddComponentMenu("Command/Room Item Inspect Overlay")]
public class RoomItemInspectOverlay : MonoBehaviour
{
    public static RoomItemInspectOverlay Instance { get; private set; }

    [Header("Camera")]
    public Camera targetCamera;
    public bool useMainCameraWhenMissing = true;
    [Min(0.31f)]
    public float canvasPlaneDistance = 1f;

    [Header("UI")]
    public bool createUiOnAwake;
    public bool hideOnAwake = true;
    [Tooltip("开启后，检视 UI 会在主摄像机后处理之后绘制，避免物品预览被磨砂模糊一起糊掉。")]
    public bool renderUiAfterCameraEffects = true;
    public int canvasSortingOrder = 5000;
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public Color overlayColor = new Color(0.02f, 0.025f, 0.03f, 0.58f);
    public Color glassColor = new Color(1f, 1f, 1f, 0f);
    public Vector2 previewRectSize = new Vector2(960f, 820f);
    public Vector2 previewRectOffset = new Vector2(220f, 0f);

    [Header("Info Text")]
    public Vector2 infoPanelSize = new Vector2(420f, 540f);
    public Vector2 infoPanelOffset = new Vector2(96f, 0f);
    [Min(1f)]
    public float titleFontSize = 42f;
    [Min(1f)]
    public float descriptionFontSize = 22f;
    public Color titleColor = new Color(1f, 1f, 1f, 0.96f);
    public Color descriptionColor = new Color(1f, 1f, 1f, 0.82f);

    [Header("Blur")]
    public bool useBackgroundBlur = true;
    [Range(0, 4)]
    public int blurDownsample = 2;
    [Range(0, 6)]
    public int blurIterations = 2;
    [Range(0f, 6f)]
    public float blurSize = 1.5f;

    [Header("Preview")]
    [Range(0, 31)]
    public int previewLayerIndex = 30;
    [Min(128)]
    public int previewTextureSize = 1024;
    [Range(10f, 80f)]
    public float previewFieldOfView = 28f;
    [Min(0.2f)]
    public float previewDistance = 3f;
    [Tooltip("统一放大检视相机距离。用于让所有物品的默认浏览空间更宽松。")]
    [Min(0.1f)]
    public float previewDistanceMultiplier = 1.35f;
    [Tooltip("预览自动适配时保留的边距。数值越大，物品在预览里越小，可避免旋转后被裁剪。")]
    [Min(1f)]
    public float previewFitPadding = 1.7f;
    public Color previewBackgroundColor = new Color(0f, 0f, 0f, 0f);

    [Header("Input")]
    public bool closeOnEscape = true;
    public bool closeOnBackgroundClick;
    public bool setCursorVisibleWhileOpen = true;
    public bool lockPlayerControls = true;
    public bool disableInteractorWhileOpen = true;
    [Min(0f)]
    public float dragRotationSpeed = 0.35f;
    [Min(0f)]
    public float zoomSpeed = 0.18f;
    [Min(0.01f)]
    public float minZoom = 0.55f;
    [Min(0.01f)]
    public float maxZoom = 2f;

    private Canvas canvas;
    private CanvasGroup canvasGroup;
    private RectTransform infoPanelRect;
    private RectTransform previewHitRect;
    private RawImage previewImage;
    private TMP_Text titleText;
    private TMP_Text descriptionText;
    private Button closeButton;
    private EventSystem generatedEventSystem;

    private Transform previewStage;
    private Transform previewPivot;
    private Camera previewCamera;
    private Light previewKeyLight;
    private Light previewFillLight;
    private RenderTexture previewTexture;
    private GameObject currentPreview;

    private RoomFrostedScreenBlur blurEffect;
    private RoomTopDownPlayerMovement lockedMovement;
    private RoomPlayerInteractor lockedInteractor;
    private bool previousMovementEnabled;
    private bool previousInteractorEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private int originalTargetCameraCullingMask;
    private bool hasOriginalCullingMask;
    private bool isOpen;
    private bool isDragging;
    private Vector3 rotationEuler;
    private float zoom = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[RoomItemInspectOverlay] Multiple overlays exist. The newest instance will be used.", this);
        }

        Instance = this;
        ResolveTargetCamera();

        if (createUiOnAwake)
        {
            EnsureUi();
            EnsurePreviewStage();

            if (hideOnAwake)
            {
                SetVisible(false);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        ReleasePreviewTexture();
        RestoreTargetCameraCullingMask();
    }

    private void Update()
    {
        if (!isOpen)
        {
            return;
        }

        if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
            return;
        }

        UpdatePreviewInput();
    }

    public void Open(GameObject previewPrefab)
    {
        Open(
            previewPrefab,
            null,
            string.Empty,
            string.Empty,
            Vector3.zero,
            Vector3.zero,
            1f,
            previewDistance,
            previewFieldOfView,
            lockPlayerControls);
    }

    public void Open(
        GameObject previewPrefab,
        RoomInteractionContext context,
        string displayName,
        string description,
        Vector3 initialEulerAngles,
        Vector3 localOffset,
        float scale,
        float cameraDistance,
        float fieldOfView,
        bool lockPlayerMovementWhileOpen = true)
    {
        if (previewPrefab == null)
        {
            Debug.LogWarning("[RoomItemInspectOverlay] No preview prefab was supplied.", this);
            return;
        }

        EnsureUi();
        EnsurePreviewStage();
        EnsurePreviewTexture();
        EnsureEventSystem();
        ApplyTargetCameraCullingMask();
        ApplyPlayerLock(context, lockPlayerMovementWhileOpen);
        ConfigureBlur(true);

        DestroyCurrentPreview();

        currentPreview = Instantiate(previewPrefab, previewPivot);
        currentPreview.name = previewPrefab.name + "_InspectPreview";
        currentPreview.transform.localPosition = Vector3.zero;
        currentPreview.transform.localRotation = Quaternion.identity;
        currentPreview.transform.localScale = Vector3.one;

        PreparePreviewObject(currentPreview);
        rotationEuler = initialEulerAngles;
        zoom = 1f;
        previewPivot.localRotation = Quaternion.Euler(rotationEuler);
        previewPivot.localScale = Vector3.one;

        FitPreviewObject(currentPreview, Mathf.Max(0.01f, scale));
        currentPreview.transform.localPosition += localOffset;

        ConfigurePreviewCamera(Mathf.Max(0.2f, cameraDistance) * previewDistanceMultiplier, Mathf.Clamp(fieldOfView, 10f, 80f));
        SetTexts(displayName, description);
        SetVisible(true);
        StoreCursorState();
        ApplyCursorState(true);
        isOpen = true;
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        isDragging = false;
        DestroyCurrentPreview();
        SetVisible(false);
        ConfigureBlur(false);
        RestorePlayerLock();
        RestoreTargetCameraCullingMask();
        ApplyCursorState(false);
    }

    private void UpdatePreviewInput()
    {
        bool pointerOverPreview = previewHitRect != null &&
                                  RectTransformUtility.RectangleContainsScreenPoint(
                                      previewHitRect,
                                      Input.mousePosition,
                                      GetUiEventCamera());

        if (Input.GetMouseButtonDown(0))
        {
            if (pointerOverPreview)
            {
                isDragging = true;
            }
            else if (closeOnBackgroundClick)
            {
                Close();
                return;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging && currentPreview != null)
        {
            rotationEuler.y -= Input.GetAxis("Mouse X") * dragRotationSpeed * 100f;
            rotationEuler.x += Input.GetAxis("Mouse Y") * dragRotationSpeed * 100f;
            rotationEuler.x = Mathf.Clamp(rotationEuler.x, -80f, 80f);
            previewPivot.localRotation = Quaternion.Euler(rotationEuler);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            zoom = Mathf.Clamp(zoom + scroll * zoomSpeed, minZoom, maxZoom);
            previewPivot.localScale = Vector3.one * zoom;
        }
    }

    private void EnsureUi()
    {
        if (canvas != null)
        {
            ConfigureCanvasCamera();
            return;
        }

        ResolveTargetCamera();

        GameObject canvasObject = new GameObject("InspectOverlayCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = canvasSortingOrder;
        ConfigureCanvasCamera();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvas.enabled = false;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        Image overlay = CreateImage("Background", canvasRect, overlayColor);
        overlay.raycastTarget = true;
        Stretch(overlay.rectTransform);

        Image glass = CreateImage("Glass", canvasRect, glassColor);
        glass.raycastTarget = false;
        previewHitRect = glass.rectTransform;
        previewHitRect.anchorMin = new Vector2(0.5f, 0.5f);
        previewHitRect.anchorMax = new Vector2(0.5f, 0.5f);
        previewHitRect.pivot = new Vector2(0.5f, 0.5f);
        previewHitRect.anchoredPosition = previewRectOffset;
        previewHitRect.sizeDelta = previewRectSize;

        previewImage = CreateRawImage("PreviewImage", previewHitRect);
        Stretch(previewImage.rectTransform);
        previewImage.raycastTarget = false;
        previewImage.enabled = false;

        infoPanelRect = new GameObject("InfoPanel", typeof(RectTransform)).GetComponent<RectTransform>();
        infoPanelRect.SetParent(canvasRect, false);
        infoPanelRect.gameObject.layer = LayerMask.NameToLayer("UI");
        infoPanelRect.anchorMin = new Vector2(0f, 0.5f);
        infoPanelRect.anchorMax = new Vector2(0f, 0.5f);
        infoPanelRect.pivot = new Vector2(0f, 0.5f);
        infoPanelRect.anchoredPosition = infoPanelOffset;
        infoPanelRect.sizeDelta = infoPanelSize;

        titleText = CreateText("Title", infoPanelRect, titleFontSize, TextAlignmentOptions.TopLeft, titleColor);
        titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleText.rectTransform.pivot = new Vector2(0f, 1f);
        titleText.rectTransform.anchoredPosition = Vector2.zero;
        titleText.rectTransform.sizeDelta = new Vector2(0f, 108f);

        descriptionText = CreateText("Description", infoPanelRect, descriptionFontSize, TextAlignmentOptions.TopLeft, descriptionColor);
        descriptionText.rectTransform.anchorMin = Vector2.zero;
        descriptionText.rectTransform.anchorMax = Vector2.one;
        descriptionText.rectTransform.offsetMin = Vector2.zero;
        descriptionText.rectTransform.offsetMax = new Vector2(0f, -128f);

        closeButton = CreateCloseButton(canvasRect);
    }

    private void EnsurePreviewStage()
    {
        if (previewStage != null)
        {
            return;
        }

        previewStage = new GameObject("InspectPreviewStage").transform;
        previewStage.SetParent(transform, false);
        previewStage.localPosition = new Vector3(0f, -1000f, 0f);
        previewStage.localRotation = Quaternion.identity;
        previewStage.localScale = Vector3.one;

        previewPivot = new GameObject("PreviewPivot").transform;
        previewPivot.SetParent(previewStage, false);

        GameObject cameraObject = new GameObject("PreviewCamera");
        cameraObject.transform.SetParent(previewStage, false);
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = previewBackgroundColor;
        previewCamera.nearClipPlane = 0.01f;
        previewCamera.farClipPlane = 50f;
        previewCamera.allowHDR = false;
        previewCamera.allowMSAA = true;
        previewCamera.enabled = true;

        GameObject keyLightObject = new GameObject("PreviewKeyLight");
        keyLightObject.transform.SetParent(previewStage, false);
        keyLightObject.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);
        previewKeyLight = keyLightObject.AddComponent<Light>();
        previewKeyLight.type = LightType.Directional;
        previewKeyLight.intensity = 1.25f;

        GameObject fillLightObject = new GameObject("PreviewFillLight");
        fillLightObject.transform.SetParent(previewStage, false);
        fillLightObject.transform.localPosition = new Vector3(-2f, 1.5f, -2f);
        previewFillLight = fillLightObject.AddComponent<Light>();
        previewFillLight.type = LightType.Point;
        previewFillLight.range = 8f;
        previewFillLight.intensity = 0.7f;

        ApplyPreviewLayerToStage();
    }

    private void EnsurePreviewTexture()
    {
        Vector2Int textureSize = ResolvePreviewTextureSize();
        if (previewTexture != null && previewTexture.width == textureSize.x && previewTexture.height == textureSize.y)
        {
            return;
        }

        ReleasePreviewTexture();

        previewTexture = new RenderTexture(textureSize.x, textureSize.y, 16, RenderTextureFormat.ARGB32)
        {
            name = "RoomItemInspectPreviewTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        if (previewCamera != null)
        {
            previewCamera.targetTexture = previewTexture;
            previewCamera.enabled = false;
        }

        if (previewImage != null)
        {
            previewImage.texture = previewTexture;
            previewImage.enabled = previewTexture != null;
        }
    }

    private Vector2Int ResolvePreviewTextureSize()
    {
        int longEdge = Mathf.Max(128, previewTextureSize);
        float width = Mathf.Max(1f, previewRectSize.x);
        float height = Mathf.Max(1f, previewRectSize.y);
        float aspect = width / height;

        if (aspect >= 1f)
        {
            return new Vector2Int(longEdge, Mathf.Max(128, Mathf.RoundToInt(longEdge / aspect)));
        }

        return new Vector2Int(Mathf.Max(128, Mathf.RoundToInt(longEdge * aspect)), longEdge);
    }

    private Camera ResolveTargetCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        if (useMainCameraWhenMissing)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            targetCamera = FindObjectOfType<Camera>();
        }

        return targetCamera;
    }

    private void ConfigureCanvasCamera()
    {
        if (canvas == null)
        {
            return;
        }

        canvas.renderMode = renderUiAfterCameraEffects
            ? RenderMode.ScreenSpaceOverlay
            : RenderMode.ScreenSpaceCamera;

        canvas.worldCamera = renderUiAfterCameraEffects ? null : ResolveTargetCamera();
        canvas.planeDistance = canvasPlaneDistance;
        canvas.sortingOrder = canvasSortingOrder;
    }

    private Camera GetUiEventCamera()
    {
        return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : targetCamera;
    }

    private void ConfigurePreviewCamera(float cameraDistance, float fieldOfView)
    {
        if (previewCamera == null)
        {
            return;
        }

        previewCamera.transform.localPosition = new Vector3(0f, 0f, -cameraDistance);
        previewCamera.transform.localRotation = Quaternion.identity;
        previewCamera.fieldOfView = fieldOfView;
        previewCamera.cullingMask = 1 << previewLayerIndex;
        previewCamera.backgroundColor = previewBackgroundColor;

        if (previewKeyLight != null)
        {
            previewKeyLight.cullingMask = 1 << previewLayerIndex;
        }

        if (previewFillLight != null)
        {
            previewFillLight.cullingMask = 1 << previewLayerIndex;
        }
    }

    private void FitPreviewObject(GameObject previewObject, float scale)
    {
        if (!TryGetRenderBounds(previewObject, out Bounds bounds))
        {
            previewObject.transform.localScale = Vector3.one * ResolvePaddedScale(scale);
            return;
        }

        float largestSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (largestSize <= 0.0001f)
        {
            largestSize = 1f;
        }

        float normalizedScale = ResolvePaddedScale(scale) / largestSize;
        previewObject.transform.localScale = Vector3.one * normalizedScale;

        if (TryGetRenderBounds(previewObject, out bounds))
        {
            Vector3 localCenter = previewPivot.InverseTransformPoint(bounds.center);
            previewObject.transform.localPosition -= localCenter;
        }
    }

    private float ResolvePaddedScale(float scale)
    {
        return scale / Mathf.Max(1f, previewFitPadding);
    }

    private bool TryGetRenderBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = new Bounds(root.transform.position, Vector3.zero);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private void PreparePreviewObject(GameObject previewObject)
    {
        SetLayerRecursively(previewObject, previewLayerIndex);

        Collider[] colliders = previewObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody[] rigidbodies = previewObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
        }

        RoomInteractable[] interactables = previewObject.GetComponentsInChildren<RoomInteractable>(true);
        for (int i = 0; i < interactables.Length; i++)
        {
            interactables[i].enabled = false;
        }

        RoomInteractionBehaviour[] behaviours = previewObject.GetComponentsInChildren<RoomInteractionBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
        }

        AudioSource[] audioSources = previewObject.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            audioSources[i].Stop();
            audioSources[i].playOnAwake = false;
        }
    }

    private void ApplyPreviewLayerToStage()
    {
        if (previewStage == null)
        {
            return;
        }

        SetLayerRecursively(previewStage.gameObject, previewLayerIndex);
    }

    private void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;

        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }

    private void ApplyTargetCameraCullingMask()
    {
        Camera resolvedCamera = ResolveTargetCamera();
        if (resolvedCamera == null)
        {
            return;
        }

        if (!hasOriginalCullingMask)
        {
            originalTargetCameraCullingMask = resolvedCamera.cullingMask;
            hasOriginalCullingMask = true;
        }

        resolvedCamera.cullingMask = originalTargetCameraCullingMask & ~(1 << previewLayerIndex);
    }

    private void RestoreTargetCameraCullingMask()
    {
        Camera resolvedCamera = targetCamera;
        if (resolvedCamera == null || !hasOriginalCullingMask)
        {
            return;
        }

        resolvedCamera.cullingMask = originalTargetCameraCullingMask;
        hasOriginalCullingMask = false;
    }

    private void ConfigureBlur(bool enabled)
    {
        if (!useBackgroundBlur)
        {
            return;
        }

        Camera resolvedCamera = ResolveTargetCamera();
        if (resolvedCamera == null)
        {
            return;
        }

        if (blurEffect == null)
        {
            blurEffect = resolvedCamera.GetComponent<RoomFrostedScreenBlur>();
            if (blurEffect == null)
            {
                blurEffect = resolvedCamera.gameObject.AddComponent<RoomFrostedScreenBlur>();
            }
        }

        blurEffect.downsample = blurDownsample;
        blurEffect.iterations = blurIterations;
        blurEffect.blurSize = blurSize;
        blurEffect.enabled = enabled;
    }

    private void ApplyPlayerLock(RoomInteractionContext context, bool lockPlayerMovementWhileOpen)
    {
        if (context == null || context.Player == null)
        {
            return;
        }

        if (lockPlayerMovementWhileOpen)
        {
            lockedMovement = context.Player.GetComponentInChildren<RoomTopDownPlayerMovement>(true);
            if (lockedMovement != null)
            {
                previousMovementEnabled = lockedMovement.MovementControlEnabled;
                lockedMovement.SetMovementControlEnabled(false);
            }
        }

        if (!lockPlayerControls || !disableInteractorWhileOpen)
        {
            return;
        }

        lockedInteractor = context.Player.GetComponentInChildren<RoomPlayerInteractor>(true);
        if (lockedInteractor != null)
        {
            previousInteractorEnabled = lockedInteractor.enabled;
            lockedInteractor.enabled = false;
        }
    }

    private void RestorePlayerLock()
    {
        if (lockedMovement != null)
        {
            lockedMovement.SetMovementControlEnabled(previousMovementEnabled);
            lockedMovement = null;
        }

        if (lockedInteractor != null)
        {
            lockedInteractor.enabled = previousInteractorEnabled;
            lockedInteractor = null;
        }
    }

    private void StoreCursorState()
    {
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
    }

    private void ApplyCursorState(bool overlayOpen)
    {
        if (!setCursorVisibleWhileOpen)
        {
            return;
        }

        if (overlayOpen)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    private void DestroyCurrentPreview()
    {
        if (currentPreview == null)
        {
            return;
        }

        Destroy(currentPreview);
        currentPreview = null;
    }

    private void ReleasePreviewTexture()
    {
        if (previewCamera != null)
        {
            previewCamera.targetTexture = null;
        }

        if (previewImage != null)
        {
            previewImage.texture = null;
            previewImage.enabled = false;
        }

        if (previewTexture == null)
        {
            return;
        }

        previewTexture.Release();

        if (Application.isPlaying)
        {
            Destroy(previewTexture);
        }
        else
        {
            DestroyImmediate(previewTexture);
        }

        previewTexture = null;
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = visible;
        canvasGroup.blocksRaycasts = visible;

        if (canvas != null)
        {
            canvas.enabled = visible;
        }

        if (previewCamera != null)
        {
            previewCamera.enabled = visible;
        }

        if (previewImage != null)
        {
            previewImage.enabled = visible && previewImage.texture != null;
        }
    }

    private void SetTexts(string displayName, string description)
    {
        bool hasTitle = !string.IsNullOrWhiteSpace(displayName);
        bool hasDescription = !string.IsNullOrWhiteSpace(description);

        if (infoPanelRect != null)
        {
            infoPanelRect.gameObject.SetActive(hasTitle || hasDescription);
        }

        if (titleText != null)
        {
            titleText.text = hasTitle ? displayName : string.Empty;
            titleText.gameObject.SetActive(hasTitle);
        }

        if (descriptionText != null)
        {
            descriptionText.text = hasDescription ? description : string.Empty;
            descriptionText.gameObject.SetActive(hasDescription);
        }
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("InspectOverlayEventSystem");
        eventSystemObject.transform.SetParent(transform, false);
        generatedEventSystem = eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        imageObject.layer = LayerMask.NameToLayer("UI");

        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private RawImage CreateRawImage(string objectName, Transform parent)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        imageObject.layer = LayerMask.NameToLayer("UI");
        return imageObject.AddComponent<RawImage>();
    }

    private TMP_Text CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        textObject.layer = LayerMask.NameToLayer("UI");

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.richText = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateCloseButton(Transform parent)
    {
        Image image = CreateImage("CloseButton", parent, new Color(1f, 1f, 1f, 0.14f));
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-48f, -40f);
        rect.sizeDelta = new Vector2(56f, 56f);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Close);

        TMP_Text label = CreateText(
            "Label",
            rect,
            28f,
            TextAlignmentOptions.Center,
            new Color(1f, 1f, 1f, 0.92f));
        label.text = "X";
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.raycastTarget = false;
        return button;
    }

    private void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }
}
