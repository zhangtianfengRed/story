using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 全局场景切换控制器。
/// 常驻一个全屏黑幕 UI，在切换场景时统一执行淡入/加载/淡出。
/// </summary>
[DisallowMultipleComponent]
public class SceneTransitionController : MonoBehaviour
{
    private static SceneTransitionController _instance;

    [Header("Fade Settings")]
    [SerializeField] private Color fadeColor = Color.black;
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float holdDuration = 0.05f;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private int sortingOrder = 30000;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool blockInputWhileVisible = true;

    private Canvas overlayCanvas;
    private GraphicRaycaster graphicRaycaster;
    private CanvasGroup canvasGroup;
    private Image fadeImage;
    private Coroutine transitionCoroutine;

    public static SceneTransitionController Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("SceneTransitionController");
                _instance = go.AddComponent<SceneTransitionController>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public bool IsTransitioning => transitionCoroutine != null;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlay();
        HideImmediate();
    }

    public void Configure(Color color, float fadeOut, float hold, float fadeIn, int order, bool useUnscaled, bool blockInput)
    {
        fadeColor = color;
        fadeOutDuration = Mathf.Max(0f, fadeOut);
        holdDuration = Mathf.Max(0f, hold);
        fadeInDuration = Mathf.Max(0f, fadeIn);
        sortingOrder = order;
        useUnscaledTime = useUnscaled;
        blockInputWhileVisible = blockInput;

        EnsureOverlay();
        ApplyVisualSettings();
    }

    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"[{nameof(SceneTransitionController)}] Scene name is empty.");
            return;
        }

        if (IsTransitioning)
        {
            Debug.LogWarning($"[{nameof(SceneTransitionController)}] Transition is already in progress.");
            return;
        }

        Debug.Log($"[{nameof(SceneTransitionController)}] Start global transition to scene: {sceneName}");
        transitionCoroutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    public void ReloadCurrentScene()
    {
        LoadScene(SceneManager.GetActiveScene().name);
    }

    public void FadeToBlack()
    {
        if (IsTransitioning)
        {
            return;
        }

        transitionCoroutine = StartCoroutine(FadeOnlyRoutine(1f, fadeOutDuration));
    }

    public void FadeFromBlack()
    {
        if (IsTransitioning)
        {
            return;
        }

        transitionCoroutine = StartCoroutine(FadeOnlyRoutine(0f, fadeInDuration));
    }

    public void ShowImmediate()
    {
        EnsureOverlay();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = blockInputWhileVisible;
        canvasGroup.interactable = false;
    }

    public void HideImmediate()
    {
        EnsureOverlay();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        EnsureOverlay();

        yield return FadeRoutine(1f, fadeOutDuration);

        if (holdDuration > 0f)
        {
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(holdDuration);
            }
            else
            {
                yield return new WaitForSeconds(holdDuration);
            }
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null)
        {
            yield return FadeRoutine(0f, fadeInDuration);
            transitionCoroutine = null;
            yield break;
        }

        while (!operation.isDone)
        {
            yield return null;
        }

        Debug.Log($"[{nameof(SceneTransitionController)}] Scene loaded behind overlay: {sceneName}");

        if (TryHandleSceneAdapter(sceneName))
        {
            Debug.Log($"[{nameof(SceneTransitionController)}] Scene adapter took over fade in: {sceneName}");
            transitionCoroutine = null;
            yield break;
        }

        yield return null;
        Debug.Log($"[{nameof(SceneTransitionController)}] Fade in from global overlay: {sceneName}");
        yield return FadeRoutine(0f, fadeInDuration);
        Debug.Log($"[{nameof(SceneTransitionController)}] Fade in complete: {sceneName}");

        transitionCoroutine = null;
    }

    private IEnumerator FadeOnlyRoutine(float targetAlpha, float duration)
    {
        EnsureOverlay();
        yield return FadeRoutine(targetAlpha, duration);
        transitionCoroutine = null;
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        EnsureOverlay();

        float startAlpha = canvasGroup.alpha;
        float safeDuration = Mathf.Max(0f, duration);

        if (Mathf.Approximately(safeDuration, 0f))
        {
            canvasGroup.alpha = targetAlpha;
            canvasGroup.blocksRaycasts = blockInputWhileVisible && targetAlpha > 0f;
            yield break;
        }

        if (blockInputWhileVisible && targetAlpha > startAlpha)
        {
            canvasGroup.blocksRaycasts = true;
        }

        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
        canvasGroup.blocksRaycasts = blockInputWhileVisible && targetAlpha > 0f;
    }

    private void EnsureOverlay()
    {
        if (overlayCanvas == null)
        {
            overlayCanvas = GetComponent<Canvas>();
            if (overlayCanvas == null)
            {
                overlayCanvas = gameObject.AddComponent<Canvas>();
            }
        }

        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = sortingOrder;

        if (graphicRaycaster == null)
        {
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            if (graphicRaycaster == null)
            {
                graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (fadeImage == null)
        {
            Transform existingChild = transform.Find("FadeOverlay");
            GameObject overlayObject;

            if (existingChild != null)
            {
                overlayObject = existingChild.gameObject;
            }
            else
            {
                overlayObject = new GameObject("FadeOverlay", typeof(RectTransform), typeof(Image));
                overlayObject.transform.SetParent(transform, false);
            }

            RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            fadeImage = overlayObject.GetComponent<Image>();
        }

        ApplyVisualSettings();
    }

    private void ApplyVisualSettings()
    {
        if (overlayCanvas != null)
        {
            overlayCanvas.sortingOrder = sortingOrder;
        }

        if (fadeImage != null)
        {
            fadeImage.color = fadeColor;
            fadeImage.raycastTarget = blockInputWhileVisible;
        }
    }

    private bool TryHandleSceneAdapter(string sceneName)
    {
        SceneTransitionSceneAdapter[] adapters = FindObjectsOfType<SceneTransitionSceneAdapter>(true);
        for (int i = 0; i < adapters.Length; i++)
        {
            SceneTransitionSceneAdapter adapter = adapters[i];
            if (adapter == null || adapter.gameObject.scene.name != sceneName || !adapter.TakeOverGlobalFadeIn)
            {
                continue;
            }

            adapter.HandleSceneLoadedBehindBlack(this);
            return true;
        }

        return false;
    }
}
