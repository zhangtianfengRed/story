using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class DialogueSubtitlePlaybackOptions
{
    public bool interruptCurrentPlayback = true;
    public bool respectGlobalSubtitleSetting = true;
    public bool autoDistributeUntimedCues = true;
    public bool hideWhenFinished = true;
    public bool clearTextWhenHidden = true;
    public float baseVoiceVolume = 1f;
    public float fallbackSubtitleDuration = 3f;
}

[DisallowMultipleComponent]
[AddComponentMenu("Dialogue/对白字幕显示层")]
public class DialogueSubtitleOverlay : MonoBehaviour
{
    private sealed class RuntimeCue
    {
        public float startTime;
        public float endTime;
        public string text;
    }

    public static DialogueSubtitleOverlay Instance { get; private set; }

    [Header("生命周期")]
    [InspectorName("跨场景保留")]
    [Tooltip("开启后，这个字幕显示层会 DontDestroyOnLoad，适合做全局通用字幕 UI。")]
    public bool persistAcrossScenes = true;

    [InspectorName("启动时创建 UI")]
    [Tooltip("没有手动绑定字幕 UI 时，自动生成一套默认字幕 UI。")]
    public bool createUiOnAwake = true;

    [InspectorName("启动时隐藏")]
    [Tooltip("启动时先隐藏字幕 UI。通常保持开启。")]
    public bool hideOnAwake = true;

    [Header("音频")]
    [InspectorName("语音音源")]
    [Tooltip("播放对白语音用的 AudioSource。不填会自动添加。")]
    public AudioSource voiceAudioSource;

    [InspectorName("默认语音音量")]
    [Tooltip("没有单条对白音量设置时使用的默认音量。")]
    [Range(0f, 1f)]
    public float defaultVoiceVolume = 1f;

    [Header("界面")]
    [InspectorName("目标 Canvas")]
    [Tooltip("手动绑定字幕 UI 时可指定 Canvas。不填则自动生成。")]
    public Canvas targetCanvas;

    [InspectorName("字幕 CanvasGroup")]
    [Tooltip("控制字幕淡入淡出的 CanvasGroup。自动生成 UI 时会自动创建。")]
    public CanvasGroup subtitleCanvasGroup;

    [InspectorName("字幕背景")]
    [Tooltip("字幕背景图。自动生成 UI 时会自动创建。")]
    public Image subtitleBackground;

    [InspectorName("字幕文本")]
    [Tooltip("实际显示字幕的 TMP 文本。手动 UI 必须绑定这个字段。")]
    public TMP_Text subtitleText;

    [InspectorName("Canvas 排序层级")]
    [Tooltip("自动生成 Canvas 时使用的 sorting order。数值越大越靠上。")]
    public int canvasSortingOrder = 6000;

    [InspectorName("参考分辨率")]
    [Tooltip("自动生成 UI 使用的 CanvasScaler 参考分辨率。")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Header("布局")]
    [InspectorName("字幕面板尺寸")]
    [Tooltip("自动生成字幕背景的宽高。")]
    public Vector2 panelSize = new Vector2(1400f, 176f);

    [InspectorName("底部偏移")]
    [Tooltip("字幕面板离屏幕底部的偏移。")]
    public Vector2 panelBottomOffset = new Vector2(0f, 92f);

    [InspectorName("文本内边距")]
    [Tooltip("字幕文本距离背景边缘的内边距。")]
    public Vector2 textPadding = new Vector2(40f, 24f);

    [InspectorName("字体大小")]
    [Min(1f)]
    public float fontSize = 34f;

    [InspectorName("背景颜色")]
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.58f);

    [InspectorName("文字颜色")]
    public Color textColor = new Color(1f, 1f, 1f, 0.96f);

    [Header("时间")]
    [InspectorName("默认自动分配未定时字幕")]
    [Tooltip("当多段字幕都没有填写开始/结束时间时，按语音长度和文本长度自动分配时间。")]
    public bool defaultAutoDistributeUntimedCues = true;

    [InspectorName("默认无语音时长")]
    [Tooltip("没有语音片段时，每段字幕默认显示多少秒。")]
    [Min(0.1f)]
    public float defaultFallbackSubtitleDuration = 3f;

    [Header("淡入淡出")]
    [InspectorName("淡入时间")]
    [Min(0f)]
    public float fadeInDuration = 0.12f;

    [InspectorName("淡出时间")]
    [Min(0f)]
    public float fadeOutDuration = 0.16f;

    private readonly List<RuntimeCue> runtimeCues = new List<RuntimeCue>();

    private Coroutine fadeCoroutine;
    private DialogueSubtitlePlaybackOptions activeOptions;
    private Action activeFinishedCallback;
    private AudioClip activeVoiceClip;
    private float playbackDuration;
    private float playbackStartedAt;
    private int activeCueIndex = -1;
    private bool lastSubtitleAllowed = true;
    private bool generatedCanvas;
    private bool isPlaying;

    public bool IsPlaying
    {
        get { return isPlaying; }
    }

    public static DialogueSubtitleOverlay GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        DialogueSubtitleOverlay existing = FindObjectOfType<DialogueSubtitleOverlay>(true);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject overlayObject = new GameObject(nameof(DialogueSubtitleOverlay));
        return overlayObject.AddComponent<DialogueSubtitleOverlay>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[DialogueSubtitleOverlay] Multiple overlays exist. The newest instance will be used.", this);
        }

        Instance = this;

        if (persistAcrossScenes && transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }

        EnsureAudioSource();

        if (createUiOnAwake)
        {
            EnsureUi();
        }

        if (hideOnAwake)
        {
            SetSubtitleVisibleImmediate(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        UpdateAudioVolume();

        float time = GetPlaybackTime();
        RefreshSubtitle(time, false);

        if (HasPlaybackFinished(time))
        {
            StopPlayback(true);
        }
    }

    public bool Play(
        AudioClip voiceClip,
        IList<DialogueSubtitleCue> subtitleCues,
        DialogueSubtitlePlaybackOptions options = null,
        Action onFinished = null)
    {
        DialogueSubtitlePlaybackOptions resolvedOptions = options ?? CreateDefaultPlaybackOptions();

        if (isPlaying)
        {
            if (!resolvedOptions.interruptCurrentPlayback)
            {
                return false;
            }

            StopPlayback(true);
        }

        EnsureAudioSource();
        EnsureUi();

        playbackDuration = ResolvePlaybackDuration(voiceClip, subtitleCues, resolvedOptions);
        BuildRuntimeCues(subtitleCues, playbackDuration, resolvedOptions);

        if (voiceClip == null && runtimeCues.Count == 0)
        {
            Debug.LogWarning("[DialogueSubtitleOverlay] Play ignored because no voice clip or subtitle text was supplied.", this);
            return false;
        }

        activeOptions = resolvedOptions;
        activeFinishedCallback = onFinished;
        activeVoiceClip = voiceClip;
        activeCueIndex = -1;
        lastSubtitleAllowed = true;
        playbackStartedAt = Time.unscaledTime;
        isPlaying = true;

        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
            voiceAudioSource.clip = activeVoiceClip;
            voiceAudioSource.loop = false;
            voiceAudioSource.playOnAwake = false;
            UpdateAudioVolume();

            if (activeVoiceClip != null)
            {
                voiceAudioSource.Play();
            }
        }

        RefreshSubtitle(0f, true);
        return true;
    }

    public void Stop()
    {
        StopPlayback(true);
    }

    private DialogueSubtitlePlaybackOptions CreateDefaultPlaybackOptions()
    {
        return new DialogueSubtitlePlaybackOptions
        {
            interruptCurrentPlayback = true,
            respectGlobalSubtitleSetting = true,
            autoDistributeUntimedCues = defaultAutoDistributeUntimedCues,
            hideWhenFinished = true,
            clearTextWhenHidden = true,
            baseVoiceVolume = defaultVoiceVolume,
            fallbackSubtitleDuration = defaultFallbackSubtitleDuration
        };
    }

    private void StopPlayback(bool invokeFinishedCallback)
    {
        if (!isPlaying && activeFinishedCallback == null)
        {
            return;
        }

        isPlaying = false;
        activeVoiceClip = null;
        activeCueIndex = -1;
        lastSubtitleAllowed = true;

        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();
            voiceAudioSource.clip = null;
        }

        bool shouldHide = activeOptions == null || activeOptions.hideWhenFinished;
        bool shouldClear = activeOptions == null || activeOptions.clearTextWhenHidden;

        if (shouldHide)
        {
            if (shouldClear && subtitleText != null)
            {
                subtitleText.text = string.Empty;
            }

            SetSubtitleVisible(false);
        }

        Action finishedCallback = activeFinishedCallback;
        activeFinishedCallback = null;
        activeOptions = null;

        if (invokeFinishedCallback)
        {
            finishedCallback?.Invoke();
        }
    }

    private float GetPlaybackTime()
    {
        if (activeVoiceClip != null && voiceAudioSource != null && voiceAudioSource.clip == activeVoiceClip)
        {
            return Mathf.Clamp(voiceAudioSource.time, 0f, playbackDuration);
        }

        return Mathf.Max(0f, Time.unscaledTime - playbackStartedAt);
    }

    private bool HasPlaybackFinished(float playbackTime)
    {
        if (activeVoiceClip != null && voiceAudioSource != null)
        {
            float elapsedSinceStart = Time.unscaledTime - playbackStartedAt;
            return elapsedSinceStart > 0.1f && !voiceAudioSource.isPlaying;
        }

        return playbackDuration > 0f && playbackTime >= playbackDuration;
    }

    private void RefreshSubtitle(float playbackTime, bool force)
    {
        int nextCueIndex = FindActiveCueIndex(playbackTime);
        bool subtitleAllowed = AreSubtitlesAllowed();

        if (!force && activeCueIndex == nextCueIndex && lastSubtitleAllowed == subtitleAllowed)
        {
            return;
        }

        activeCueIndex = nextCueIndex;
        lastSubtitleAllowed = subtitleAllowed;

        if (activeCueIndex < 0 || activeCueIndex >= runtimeCues.Count)
        {
            if (activeOptions == null || activeOptions.clearTextWhenHidden)
            {
                SetSubtitleText(string.Empty);
            }

            SetSubtitleVisible(false);
            return;
        }

        RuntimeCue cue = runtimeCues[activeCueIndex];
        SetSubtitleText(cue.text);
        SetSubtitleVisible(ShouldShowSubtitles(cue.text, subtitleAllowed));
    }

    private int FindActiveCueIndex(float playbackTime)
    {
        for (int i = 0; i < runtimeCues.Count; i++)
        {
            RuntimeCue cue = runtimeCues[i];
            if (playbackTime >= cue.startTime && playbackTime < cue.endTime)
            {
                return i;
            }
        }

        return -1;
    }

    private bool AreSubtitlesAllowed()
    {
        return activeOptions == null ||
               !activeOptions.respectGlobalSubtitleSetting ||
               GameSettingsManager.GetSubtitlesEnabled();
    }

    private bool ShouldShowSubtitles(string text, bool subtitleAllowed)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return subtitleAllowed;
    }

    private void SetSubtitleText(string text)
    {
        if (subtitleText != null)
        {
            subtitleText.text = text ?? string.Empty;
        }
    }

    private void SetSubtitleVisible(bool visible)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        float duration = visible ? fadeInDuration : fadeOutDuration;
        fadeCoroutine = StartCoroutine(FadeSubtitle(visible, duration));
    }

    private IEnumerator FadeSubtitle(bool visible, float duration)
    {
        float startAlpha = GetSubtitleAlpha();
        float targetAlpha = visible ? 1f : 0f;

        if (duration <= 0f)
        {
            SetSubtitleAlpha(targetAlpha);
            fadeCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetSubtitleAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            yield return null;
        }

        SetSubtitleAlpha(targetAlpha);
        fadeCoroutine = null;
    }

    private void SetSubtitleVisibleImmediate(bool visible)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        SetSubtitleAlpha(visible ? 1f : 0f);
    }

    private float GetSubtitleAlpha()
    {
        if (subtitleCanvasGroup != null)
        {
            return subtitleCanvasGroup.alpha;
        }

        return subtitleText != null && subtitleText.enabled ? 1f : 0f;
    }

    private void SetSubtitleAlpha(float alpha)
    {
        bool visible = alpha > 0.001f;

        if (subtitleCanvasGroup != null)
        {
            subtitleCanvasGroup.alpha = alpha;
            subtitleCanvasGroup.interactable = false;
            subtitleCanvasGroup.blocksRaycasts = false;
        }

        if (subtitleText != null && subtitleCanvasGroup == null)
        {
            subtitleText.enabled = visible;
        }

        if (subtitleBackground != null && subtitleCanvasGroup == null)
        {
            subtitleBackground.enabled = visible;
        }

        if (targetCanvas != null && generatedCanvas)
        {
            targetCanvas.enabled = visible;
        }
    }

    private void UpdateAudioVolume()
    {
        if (voiceAudioSource == null)
        {
            return;
        }

        float baseVolume = activeOptions != null
            ? activeOptions.baseVoiceVolume
            : defaultVoiceVolume;

        voiceAudioSource.volume = Mathf.Clamp01(baseVolume * GameSettingsManager.GetChannelVolume(SettingsAudioChannel.Voice));
    }

    private void EnsureAudioSource()
    {
        if (voiceAudioSource == null)
        {
            voiceAudioSource = GetComponent<AudioSource>();
        }

        if (voiceAudioSource == null)
        {
            voiceAudioSource = gameObject.AddComponent<AudioSource>();
        }

        voiceAudioSource.playOnAwake = false;
        voiceAudioSource.loop = false;
    }

    private void EnsureUi()
    {
        if (subtitleText != null)
        {
            if (targetCanvas == null)
            {
                targetCanvas = subtitleText.GetComponentInParent<Canvas>(true);
            }

            if (subtitleCanvasGroup == null)
            {
                subtitleCanvasGroup = subtitleText.GetComponent<CanvasGroup>();
            }

            return;
        }

        GameObject canvasObject = new GameObject("DialogueSubtitleCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.layer = LayerMask.NameToLayer("UI");

        targetCanvas = canvasObject.AddComponent<Canvas>();
        targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        targetCanvas.overrideSorting = true;
        targetCanvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        subtitleCanvasGroup = canvasObject.AddComponent<CanvasGroup>();
        subtitleCanvasGroup.interactable = false;
        subtitleCanvasGroup.blocksRaycasts = false;
        generatedCanvas = true;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        Stretch(canvasRect);

        subtitleBackground = CreateImage("SubtitleBackground", canvasRect, backgroundColor);
        subtitleBackground.raycastTarget = false;

        RectTransform panelRect = subtitleBackground.rectTransform;
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = panelBottomOffset;
        panelRect.sizeDelta = panelSize;

        subtitleText = CreateText("SubtitleText", panelRect);
        RectTransform textRect = subtitleText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textPadding;
        textRect.offsetMax = -textPadding;
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

    private TMP_Text CreateText(string objectName, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        textObject.layer = LayerMask.NameToLayer("UI");

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = textColor;
        text.richText = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private float ResolvePlaybackDuration(
        AudioClip voiceClip,
        IList<DialogueSubtitleCue> subtitleCues,
        DialogueSubtitlePlaybackOptions options)
    {
        if (voiceClip != null)
        {
            return Mathf.Max(0.01f, voiceClip.length);
        }

        if (subtitleCues != null && options.autoDistributeUntimedCues)
        {
            int untimedCueCount = 0;
            bool allTextCuesAreUntimed = true;

            for (int i = 0; i < subtitleCues.Count; i++)
            {
                DialogueSubtitleCue cue = subtitleCues[i];
                if (cue == null || !cue.HasText)
                {
                    continue;
                }

                untimedCueCount++;
                if (cue.startTime > 0.001f || cue.endTime >= 0f)
                {
                    allTextCuesAreUntimed = false;
                }
            }

            if (untimedCueCount > 1 && allTextCuesAreUntimed)
            {
                return Mathf.Max(0.1f, options.fallbackSubtitleDuration) * untimedCueCount;
            }
        }

        float duration = 0f;
        if (subtitleCues != null)
        {
            for (int i = 0; i < subtitleCues.Count; i++)
            {
                DialogueSubtitleCue cue = subtitleCues[i];
                if (cue == null || !cue.HasText)
                {
                    continue;
                }

                float startTime = Mathf.Max(0f, cue.startTime);
                float endTime = cue.endTime >= 0f
                    ? Mathf.Max(startTime, cue.endTime)
                    : startTime + Mathf.Max(0.1f, options.fallbackSubtitleDuration);

                duration = Mathf.Max(duration, endTime);
            }
        }

        return duration;
    }

    private void BuildRuntimeCues(
        IList<DialogueSubtitleCue> subtitleCues,
        float duration,
        DialogueSubtitlePlaybackOptions options)
    {
        runtimeCues.Clear();

        if (subtitleCues == null)
        {
            return;
        }

        List<DialogueSubtitleCue> validCues = new List<DialogueSubtitleCue>();
        for (int i = 0; i < subtitleCues.Count; i++)
        {
            DialogueSubtitleCue cue = subtitleCues[i];
            if (cue != null && cue.HasText)
            {
                validCues.Add(cue);
            }
        }

        if (validCues.Count == 0)
        {
            return;
        }

        bool distribute = options.autoDistributeUntimedCues &&
                          duration > 0f &&
                          validCues.Count > 1 &&
                          AllCuesAreUntimed(validCues);

        if (distribute)
        {
            BuildDistributedRuntimeCues(validCues, duration);
            return;
        }

        validCues.Sort(CompareCueStartTimes);

        for (int i = 0; i < validCues.Count; i++)
        {
            DialogueSubtitleCue source = validCues[i];
            float startTime = Mathf.Max(0f, source.startTime);
            float nextStartTime = i < validCues.Count - 1
                ? Mathf.Max(startTime, validCues[i + 1].startTime)
                : duration;
            float endTime = source.endTime >= 0f
                ? source.endTime
                : nextStartTime;

            if (duration > 0f)
            {
                startTime = Mathf.Clamp(startTime, 0f, duration);
                endTime = Mathf.Clamp(endTime, startTime, duration);
            }

            if (endTime <= startTime)
            {
                endTime = duration > startTime
                    ? duration
                    : startTime + Mathf.Max(0.1f, options.fallbackSubtitleDuration);
            }

            runtimeCues.Add(new RuntimeCue
            {
                startTime = startTime,
                endTime = endTime,
                text = source.text
            });
        }
    }

    private void BuildDistributedRuntimeCues(IList<DialogueSubtitleCue> cues, float duration)
    {
        int totalWeight = 0;
        int[] weights = new int[cues.Count];

        for (int i = 0; i < cues.Count; i++)
        {
            weights[i] = Mathf.Max(1, CountVisibleCharacters(cues[i].ResolveTimingText()));
            totalWeight += weights[i];
        }

        float cursor = 0f;
        for (int i = 0; i < cues.Count; i++)
        {
            float segmentDuration = i == cues.Count - 1
                ? duration - cursor
                : duration * weights[i] / Mathf.Max(1f, totalWeight);
            float endTime = i == cues.Count - 1
                ? duration
                : Mathf.Min(duration, cursor + segmentDuration);

            runtimeCues.Add(new RuntimeCue
            {
                startTime = cursor,
                endTime = Mathf.Max(cursor, endTime),
                text = cues[i].text
            });

            cursor = endTime;
        }
    }

    private static int CompareCueStartTimes(DialogueSubtitleCue left, DialogueSubtitleCue right)
    {
        float leftTime = left != null ? left.startTime : 0f;
        float rightTime = right != null ? right.startTime : 0f;
        return leftTime.CompareTo(rightTime);
    }

    private static bool AllCuesAreUntimed(IList<DialogueSubtitleCue> cues)
    {
        for (int i = 0; i < cues.Count; i++)
        {
            DialogueSubtitleCue cue = cues[i];
            if (cue == null)
            {
                continue;
            }

            if (cue.startTime > 0.001f || cue.endTime >= 0f)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountVisibleCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 1;
        }

        int count = 0;
        bool insideTag = false;

        for (int i = 0; i < text.Length; i++)
        {
            char character = text[i];
            if (character == '<')
            {
                insideTag = true;
                continue;
            }

            if (character == '>')
            {
                insideTag = false;
                continue;
            }

            if (!insideTag && !char.IsWhiteSpace(character))
            {
                count++;
            }
        }

        return Mathf.Max(1, count);
    }
}
