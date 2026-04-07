using System.Collections;
using UnityEngine;

/// <summary>
/// 控制 CanvasGroup 的渐入（Fade In）和淡出（Fade Out）。
/// 提供两个开关方法：FadeIn() 和 FadeOut()，可在 Inspector 或其他脚本中调用。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CanvasGroupFader : MonoBehaviour
{
    [Header("渐变设置")]
    [Tooltip("渐入持续时间（秒）")]
    [SerializeField] private float fadeInDuration = 0.5f;

    [Tooltip("淡出持续时间（秒）")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("初始状态")]
    [Tooltip("游戏开始时是否可见（与 Fade In On Start 互斥，后者优先）")]
    [SerializeField] private bool startVisible = false;

    [Tooltip("开场黑屏淡入：游戏启动时保持全黑，延迟后自动淡出，适合放在全屏遮罩上")]
    [SerializeField] private bool fadeInOnStart = false;

    [Tooltip("fadeInOnStart 模式下，等待多少秒后开始淡出")]
    [SerializeField] private float startDelay = 0f;

    [Header("交互控制")]
    [Tooltip("渐入后是否允许交互（Interactable & BlocksRaycasts）")]
    [SerializeField] private bool interactableWhenVisible = true;

    // ── 内部引用 ──────────────────────────────────────────
    private CanvasGroup _canvasGroup;
    private Coroutine _currentCoroutine;

    // ─────────────────────────────────────────────────────
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();

        if (fadeInOnStart)
        {
            // 黑屏开场：强制不透明，屏蔽所有交互
            _canvasGroup.alpha = 1f;
            SetInteraction(false);
        }
        else
        {
            // 普通初始状态
            float initialAlpha = startVisible ? 1f : 0f;
            _canvasGroup.alpha = initialAlpha;
            SetInteraction(startVisible);
        }
    }

    private void Start()
    {
        if (fadeInOnStart)
        {
            // 延迟后执行淡出，露出场景
            StartCoroutine(StartFadeInRoutine());
        }
    }

    private IEnumerator StartFadeInRoutine()
    {
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        FadeOut();
    }

    // ══════════════════════════════════════════════════════
    //  公开开关 ── 直接在 Inspector Button 或其他脚本调用
    // ══════════════════════════════════════════════════════

    /// <summary>开关一：渐入（透明 → 不透明）</summary>
    public void FadeIn()
    {
        StopCurrentCoroutine();
        _currentCoroutine = StartCoroutine(FadeRoutine(0f, 1f, fadeInDuration, true));
    }

    /// <summary>开关二：淡出（不透明 → 透明）</summary>
    public void FadeOut()
    {
        StopCurrentCoroutine();
        _currentCoroutine = StartCoroutine(FadeRoutine(1f, 0f, fadeOutDuration, false));
    }

    // ══════════════════════════════════════════════════════
    //  可选扩展方法
    // ══════════════════════════════════════════════════════

    /// <summary>立即切换为可见（无动画）</summary>
    public void ShowImmediate()
    {
        StopCurrentCoroutine();
        _canvasGroup.alpha = 1f;
        SetInteraction(true);
    }

    /// <summary>立即切换为隐藏（无动画）</summary>
    public void HideImmediate()
    {
        StopCurrentCoroutine();
        _canvasGroup.alpha = 0f;
        SetInteraction(false);
    }

    /// <summary>根据当前 alpha 值自动切换（如果可见则淡出，反之渐入）</summary>
    public void Toggle()
    {
        if (_canvasGroup.alpha > 0.5f)
            FadeOut();
        else
            FadeIn();
    }

    // ══════════════════════════════════════════════════════
    //  属性查询
    // ══════════════════════════════════════════════════════

    /// <summary>当前 alpha 值</summary>
    public float Alpha => _canvasGroup.alpha;

    /// <summary>是否正在做渐变动画</summary>
    public bool IsFading => _currentCoroutine != null;

    /// <summary>是否处于完全可见状态</summary>
    public bool IsVisible => Mathf.Approximately(_canvasGroup.alpha, 1f);

    // ══════════════════════════════════════════════════════
    //  内部实现
    // ══════════════════════════════════════════════════════

    private IEnumerator FadeRoutine(float fromAlpha, float toAlpha, float duration, bool visibleOnEnd)
    {
        // 如果当前 alpha 和目标起始值不同，先同步（支持中途打断再反向）
        float startAlpha = _canvasGroup.alpha;

        // 根据实际起始 alpha 计算实际剩余时间（避免打断时跳变）
        float adjustedDuration = Mathf.Abs(toAlpha - startAlpha) * duration;

        if (adjustedDuration <= 0f)
        {
            _canvasGroup.alpha = toAlpha;
            SetInteraction(visibleOnEnd);
            _currentCoroutine = null;
            yield break;
        }

        float elapsed = 0f;

        // 渐变开始时若是渐入，提前开启 BlocksRaycasts 避免穿透问题
        if (visibleOnEnd)
            _canvasGroup.blocksRaycasts = true;

        while (elapsed < adjustedDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / adjustedDuration);
            // 使用 SmoothStep 曲线让渐变更自然
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, toAlpha, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        _canvasGroup.alpha = toAlpha;
        SetInteraction(visibleOnEnd);
        _currentCoroutine = null;
    }

    private void SetInteraction(bool active)
    {
        if (interactableWhenVisible)
        {
            _canvasGroup.interactable = active;
            _canvasGroup.blocksRaycasts = active;
        }
    }

    private void StopCurrentCoroutine()
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
            _currentCoroutine = null;
        }
    }
}
