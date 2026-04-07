using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 记忆闪回走马灯效果：
///   - 快速轮播一组照片，模拟进入回忆状态
///   - 每张照片带有随机轻微旋转/缩放，营造"老照片"质感
///   - 支持整体淡入 → 快速闪图 → 整体淡出
///
/// 使用方法：
///   1. 创建 Canvas → Panel（设为黑色半透明背景）→ Image（显示照片）→ 再套一层 CanvasGroup
///   2. 将此脚本挂载到 Canvas 或 Panel 上
///   3. 赋值 displayImage、canvasGroup、photos 列表
///   4. 初始状态将 GameObject 设为不激活
///   5. 由 FailTrigger 调用 StartFlashback() 触发
/// </summary>
public class MemoryFlashback : MonoBehaviour
{
    [Header("照片列表")]
    [Tooltip("依次闪过的照片 Sprite（建议 5~12 张）")]
    public List<Sprite> photos = new List<Sprite>();

    [Header("UI 引用")]
    [Tooltip("显示照片的 UI Image 组件")]
    public Image displayImage;
    [Tooltip("挂在根节点上的 CanvasGroup，用于整体淡入淡出")]
    public CanvasGroup canvasGroup;
    [Tooltip("可选：背景遮罩 CanvasGroup（黑色半透明面板）")]
    public CanvasGroup backgroundGroup;
    [Tooltip("纯黑遮罩的 CanvasGroup（Image 颜色设为纯黑，alpha 初始为 0）")]
    public CanvasGroup blackoutGroup;

    [Header("时序设置")]
    [Tooltip("Barrier 激活后多久开始闪回")]
    public float delayBeforeStart = 0.3f;
    [Tooltip("整体淡入时长")]
    public float globalFadeInDuration = 0.3f;
    [Tooltip("每张照片显示的基础时长（秒）")]
    public float photoDisplayTime = 0.12f;
    [Tooltip("每张照片的淡入淡出时长")]
    public float photoFadeDuration = 0.04f;
    [Tooltip("整体淡出时长")]
    public float globalFadeOutDuration = 0.6f;
    [Tooltip("闪回结束后保留最后一张停顿时长")]
    public float holdLastPhotoDuration = 0.5f;
    [Tooltip("照片全部播完后，黑屏淡入的时长")]
    public float fadeToBlackDuration = 0.8f;

    [Header("随机抖动（营造老照片质感）")]
    [Tooltip("每张照片随机旋转范围（度）")]
    public float randomRotationRange = 5f;
    [Tooltip("每张照片随机缩放浮动范围")]
    public float randomScaleRange = 0.05f;
    [Tooltip("照片基础缩放")]
    public float baseScale = 1f;

    // 外部调用入口
    public void StartFlashback()
    {
        if (photos == null || photos.Count == 0)
        {
            Debug.LogWarning("[MemoryFlashback] photos 列表为空，跳过闪回。");
            return;
        }

        // 必须先激活 GameObject，再启动 Coroutine（非激活状态无法运行协程）
        gameObject.SetActive(true);
        StartCoroutine(FlashbackSequence());
    }

    private IEnumerator FlashbackSequence()
    {
        // 激活后稍作延迟再开始，避免场景切换瞬间跳帧


        // 重置透明度
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (backgroundGroup != null) backgroundGroup.alpha = 0f;
        if (delayBeforeStart > 0f)
            yield return new WaitForSeconds(delayBeforeStart);
        // ── 整体淡入（背景 + 照片层一起淡入）──
        // 先设好第一张，淡入时直接显示照片，不会白一下
        displayImage.sprite = photos[0];
        ApplyRandomTransform();

        yield return StartCoroutine(FadeGroup(backgroundGroup, 0f, 0.85f, globalFadeInDuration));
        yield return StartCoroutine(FadeGroup(canvasGroup, 0f, 1f, globalFadeInDuration));

        // ── 逐张硬切（不淡出到 0，直接换图，避免白帧）──
        for (int i = 0; i < photos.Count; i++)
        {
            if (photos[i] == null) continue;

            // 直接换 Sprite，canvasGroup 始终保持 alpha = 1
            displayImage.sprite = photos[i];
            ApplyRandomTransform();

            // 最后一张多停一会儿
            float hold = (i == photos.Count - 1) ? photoDisplayTime + holdLastPhotoDuration : photoDisplayTime;
            yield return new WaitForSeconds(hold);
        }

        // ── 照片播完：直接淡入黑屏盖上去（不能先把 canvasGroup 淡出，否则父级 alpha=0 会把 blackout 一起压掉）──
        if (blackoutGroup != null)
        {
            blackoutGroup.alpha = 0f;
            yield return StartCoroutine(FadeGroup(blackoutGroup, 0f, 1f, fadeToBlackDuration));
        }
        else
        {
            // 没有黑屏层时，退回：淡出照片 + 淡出背景
            yield return StartCoroutine(FadeGroup(canvasGroup, 1f, 0f, globalFadeOutDuration));
            yield return StartCoroutine(FadeGroup(backgroundGroup, 0.85f, 0f, globalFadeOutDuration));
            gameObject.SetActive(false);
        }
        // 黑屏保持，由外部决定何时结束
    }

    /// <summary>对照片施加随机轻微旋转和缩放，增加"老照片"质感</summary>
    private void ApplyRandomTransform()
    {
        if (displayImage == null) return;
        RectTransform rt = displayImage.rectTransform;

        float rot = Random.Range(-randomRotationRange, randomRotationRange);
        float scaleDelta = Random.Range(-randomScaleRange, randomScaleRange);
        float s = baseScale + scaleDelta;

        rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        rt.localScale = new Vector3(s, s, 1f);
    }

    /// <summary>将 CanvasGroup 的 alpha 在 duration 内从 from 渐变到 to</summary>
    private IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        group.alpha = from;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = to;
    }
}
