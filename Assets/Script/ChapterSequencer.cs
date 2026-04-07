using System.Collections;
using UnityEngine;

/// <summary>
/// 章节序幕管理器。用于在场景启动时，首先展示一个章节主题面板（带渐变效果），
/// 随后激活对话系统控制器。
/// </summary>
public class ChapterSequencer : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("章节主题面板的 CanvasGroup，用于实现淡入淡出效果")]
    public CanvasGroup introPanel;

    [Tooltip("包含 DynamicDialogueController 的游戏对象")]
    public GameObject dialogueControllerObject;

    [Tooltip("是否开启显示时的淡入效果（如果面板已经可见，可以关闭此项）")]
    public bool useFadeIn = false;

    [Tooltip("面板完全显示后的停留时间")]
    public float displayDuration = 2.0f;

    [Tooltip("淡入和淡出的持续时间")]
    public float fadeDuration = 1.0f;

    [Tooltip("在序列开始前的初始延迟")]
    public float initialDelay = 0.5f;

    private void Start()
    {
        // 确保初始状态正确
        if (dialogueControllerObject != null)
        {
            dialogueControllerObject.SetActive(false);
        }

        if (introPanel != null)
        {
            // 如果不需要淡入，初始 alpha 直接设为 1
            introPanel.alpha = useFadeIn ? 0f : 1f;
            introPanel.gameObject.SetActive(true);
        }

        StartCoroutine(RunSequence());
    }

    private IEnumerator RunSequence()
    {
        // 0. 初始等待
        yield return new WaitForSeconds(initialDelay);

        // 1. 淡入章节面板（如果开启了开关）
        if (introPanel != null && useFadeIn)
        {
            yield return StartCoroutine(Fade(introPanel, 0f, 1f, fadeDuration));
        }
        else if (introPanel != null)
        {
            introPanel.alpha = 1f;
        }

        // 2. 展示停留
        yield return new WaitForSeconds(displayDuration);

        // 3. 淡出章节面板（通常淡出还是需要的，视觉更丝滑）
        if (introPanel != null)
        {
            yield return StartCoroutine(Fade(introPanel, 1f, 0f, fadeDuration));
            introPanel.gameObject.SetActive(false);
        }

        // 4. 激活对话控制器
        if (dialogueControllerObject != null)
        {
            dialogueControllerObject.SetActive(true);
            Debug.Log("ChapterSequencer: 章节序幕完成，激活对话系统。");
        }
    }

    private IEnumerator Fade(CanvasGroup cg, float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            yield return null;
        }
        cg.alpha = endAlpha;
    }
}
