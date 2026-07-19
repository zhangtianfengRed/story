using TMPro;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[AddComponentMenu("Dialogue/字幕背景自适应")]
public sealed class DialogueSubtitleBackgroundFitter : MonoBehaviour
{
    [Header("引用")]
    [InspectorName("字幕文本")]
    public TMP_Text targetText;

    [Header("尺寸")]
    [InspectorName("自适应宽度")]
    public bool fitWidth = true;

    [InspectorName("自适应高度")]
    public bool fitHeight = true;

    [InspectorName("单侧文本内边距")]
    [Tooltip("X 是左右单侧内边距，Y 是上下单侧内边距。")]
    public Vector2 textPadding = new Vector2(40f, 24f);

    [InspectorName("最小背景尺寸")]
    public Vector2 minimumSize = new Vector2(320f, 88f);

    [InspectorName("最大背景尺寸")]
    public Vector2 maximumSize = new Vector2(1400f, 320f);

    [InspectorName("同步文本内边距")]
    [Tooltip("开启后会让字幕文本始终铺满背景，并应用上面的文本内边距。")]
    public bool applyPaddingToTextRect = true;

    private RectTransform backgroundRect;
    private string lastText;
    private TMP_FontAsset lastFontAsset;
    private float lastFontSize = -1f;
    private Vector2 lastPadding;
    private Vector2 lastMinimumSize;
    private Vector2 lastMaximumSize;
    private bool lastFitWidth;
    private bool lastFitHeight;
    private bool refreshRequested = true;

    private void OnEnable()
    {
        ResolveReferences();
        refreshRequested = true;
        RefreshNow();
    }

    private void OnValidate()
    {
        minimumSize.x = Mathf.Max(0f, minimumSize.x);
        minimumSize.y = Mathf.Max(0f, minimumSize.y);
        maximumSize.x = Mathf.Max(minimumSize.x, maximumSize.x);
        maximumSize.y = Mathf.Max(minimumSize.y, maximumSize.y);
        textPadding.x = Mathf.Max(0f, textPadding.x);
        textPadding.y = Mathf.Max(0f, textPadding.y);

        refreshRequested = true;
        RefreshNow();
    }

    private void LateUpdate()
    {
        if (NeedsRefresh())
        {
            RefreshNow();
        }
    }

    [ContextMenu("立即刷新背景尺寸")]
    public void RefreshNow()
    {
        if (!ResolveReferences())
        {
            return;
        }

        Vector2 resolvedMinimum = new Vector2(
            Mathf.Max(0f, minimumSize.x),
            Mathf.Max(0f, minimumSize.y));
        Vector2 resolvedMaximum = new Vector2(
            Mathf.Max(resolvedMinimum.x, maximumSize.x),
            Mathf.Max(resolvedMinimum.y, maximumSize.y));
        Vector2 resolvedPadding = new Vector2(
            Mathf.Max(0f, textPadding.x),
            Mathf.Max(0f, textPadding.y));

        RectTransform textRect = targetText.rectTransform;
        if (applyPaddingToTextRect && textRect != null)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = resolvedPadding;
            textRect.offsetMax = -resolvedPadding;
        }

        float horizontalPadding = resolvedPadding.x * 2f;
        float verticalPadding = resolvedPadding.y * 2f;
        float maximumTextWidth = Mathf.Max(1f, resolvedMaximum.x - horizontalPadding);
        Vector2 preferredTextSize = targetText.GetPreferredValues(
            targetText.text ?? string.Empty,
            maximumTextWidth,
            0f);

        Vector2 nextSize = backgroundRect.sizeDelta;
        if (fitWidth)
        {
            nextSize.x = Mathf.Clamp(
                preferredTextSize.x + horizontalPadding,
                resolvedMinimum.x,
                resolvedMaximum.x);
        }

        if (fitHeight)
        {
            nextSize.y = Mathf.Clamp(
                preferredTextSize.y + verticalPadding,
                resolvedMinimum.y,
                resolvedMaximum.y);
        }

        backgroundRect.sizeDelta = nextSize;

        lastText = targetText.text;
        lastFontAsset = targetText.font;
        lastFontSize = targetText.fontSize;
        lastPadding = textPadding;
        lastMinimumSize = minimumSize;
        lastMaximumSize = maximumSize;
        lastFitWidth = fitWidth;
        lastFitHeight = fitHeight;
        refreshRequested = false;
    }

    private bool NeedsRefresh()
    {
        if (!ResolveReferences())
        {
            return false;
        }

        return refreshRequested ||
               lastText != targetText.text ||
               lastFontAsset != targetText.font ||
               !Mathf.Approximately(lastFontSize, targetText.fontSize) ||
               lastPadding != textPadding ||
               lastMinimumSize != minimumSize ||
               lastMaximumSize != maximumSize ||
               lastFitWidth != fitWidth ||
               lastFitHeight != fitHeight;
    }

    private bool ResolveReferences()
    {
        if (backgroundRect == null)
        {
            backgroundRect = GetComponent<RectTransform>();
        }

        if (targetText == null)
        {
            targetText = GetComponentInChildren<TMP_Text>(true);
        }

        return backgroundRect != null && targetText != null;
    }
}
