using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 监听 PlayableDirector 当前播放到的 Timeline Audio Clip，并在屏幕上显示对应字幕。
/// 建议挂在带有 PlayableDirector 的 TimeLine 对象上。
/// </summary>
[DisallowMultipleComponent]
public class TimelineAudioSubtitleController : MonoBehaviour
{
    [Serializable]
    public class SubtitleEntry
    {
        [Tooltip("优先匹配 Timeline 里 Audio Clip 的显示名，例如 monologue。")]
        public string timelineClipName;

        [Tooltip("可选：直接匹配 Timeline Audio Clip 使用的 AudioClip。")]
        public AudioClip audioClip;

        [TextArea(2, 5)]
        [Tooltip("需要显示在屏幕上的字幕文本。")]
        public string subtitleText;

        [Min(0f)]
        [Tooltip("字幕在当前音频片段内的开始时间，单位秒。")]
        public float clipLocalStartTime;

        [Tooltip("字幕在当前音频片段内的结束时间，单位秒。小于 0 表示自动持续到下一条字幕或片段结束。")]
        public float clipLocalEndTime = -1f;
    }

    private sealed class RuntimeAudioClipInfo
    {
        public AudioTrack track;
        public TimelineClip clip;
        public AudioClip audioClip;
        public AudioSource boundAudioSource;
    }

    [Header("Timeline")]
    [Tooltip("默认会自动抓取同物体上的 PlayableDirector。")]
    public PlayableDirector director;

    [Tooltip("建议指定为 Timeline Audio Track 绑定的 AudioSource，用来过滤掉别的音轨。")]
    public AudioSource targetAudioSource;

    [Tooltip("可选：进一步限制只读取某个 Audio Track 的字幕。留空表示不过滤。")]
    public string audioTrackName;

    [Header("Subtitle UI")]
    [Tooltip("屏幕上实际显示字幕的 TMP 文本。")]
    public TMP_Text subtitleText;

    [Tooltip("可选：如果字幕 UI 根节点上有 CanvasGroup，可以用它控制显隐。")]
    public CanvasGroup subtitleCanvasGroup;

    [Tooltip("隐藏字幕时是否顺手清空文本。")]
    public bool clearTextWhenHidden = true;

    [Header("Behavior")]
    [Tooltip("如果全局设置里关闭字幕，则这里也自动隐藏。")]
    public bool respectGlobalSubtitleSetting = true;

    [Tooltip("Director 不在播放时自动隐藏字幕。")]
    public bool hideWhenDirectorStops = true;

    [Header("Subtitles")]
    [Tooltip("按 Timeline 片段名或 AudioClip 配置字幕。")]
    public List<SubtitleEntry> subtitles = new List<SubtitleEntry>();

    private readonly List<RuntimeAudioClipInfo> cachedAudioClips = new List<RuntimeAudioClipInfo>();

    private TimelineAsset cachedTimelineAsset;
    private TimelineClip activeTimelineClip;
    private string activeSubtitleText;
    private bool warnedMissingDirector;
    private bool warnedMissingSubtitleText;
    private bool warnedMissingAudioTracks;

    private void Reset()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void Awake()
    {
        EnsureReferences();
        RebuildTimelineCache();
        ApplySubtitleVisibility(false);
    }

    private void OnEnable()
    {
        EnsureReferences();
        RebuildTimelineCache();
        RefreshSubtitle(true);
    }

    private void OnDisable()
    {
        HideSubtitle();
    }

    private void Update()
    {
        RefreshSubtitle(false);
    }

    private void EnsureReferences()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void RefreshSubtitle(bool force)
    {
        if (!HasUsableSetup())
        {
            HideSubtitle();
            return;
        }

        TimelineAsset currentTimelineAsset = director.playableAsset as TimelineAsset;
        if (currentTimelineAsset != cachedTimelineAsset)
        {
            RebuildTimelineCache();
        }

        if (respectGlobalSubtitleSetting && !GameSettingsManager.GetSubtitlesEnabled())
        {
            HideSubtitle();
            return;
        }

        if (hideWhenDirectorStops && director.state != PlayState.Playing)
        {
            HideSubtitle();
            return;
        }

        RuntimeAudioClipInfo runtimeClip = GetActiveRuntimeClip(director.time);
        if (runtimeClip == null)
        {
            HideSubtitle();
            return;
        }

        double localTime = director.time - runtimeClip.clip.start;
        SubtitleEntry subtitleEntry = FindSubtitleEntry(runtimeClip, localTime);
        if (subtitleEntry == null || string.IsNullOrWhiteSpace(subtitleEntry.subtitleText))
        {
            HideSubtitle();
            return;
        }

        ShowSubtitle(runtimeClip.clip, subtitleEntry.subtitleText, force);
    }

    private bool HasUsableSetup()
    {
        if (director == null)
        {
            if (!warnedMissingDirector)
            {
                Debug.LogWarning($"[{nameof(TimelineAudioSubtitleController)}] Missing PlayableDirector on {name}.", this);
                warnedMissingDirector = true;
            }

            return false;
        }

        if (subtitleText == null)
        {
            if (!warnedMissingSubtitleText)
            {
                Debug.LogWarning($"[{nameof(TimelineAudioSubtitleController)}] Missing subtitle TMP_Text reference on {name}.", this);
                warnedMissingSubtitleText = true;
            }

            return false;
        }

        return true;
    }

    private void RebuildTimelineCache()
    {
        cachedAudioClips.Clear();
        warnedMissingAudioTracks = false;

        if (director == null)
        {
            cachedTimelineAsset = null;
            return;
        }

        cachedTimelineAsset = director.playableAsset as TimelineAsset;
        if (cachedTimelineAsset == null)
        {
            return;
        }

        foreach (TrackAsset trackAsset in cachedTimelineAsset.GetOutputTracks())
        {
            AudioTrack audioTrack = trackAsset as AudioTrack;
            if (audioTrack == null || audioTrack.muted)
            {
                continue;
            }

            AudioSource boundAudioSource = director.GetGenericBinding(audioTrack) as AudioSource;
            if (!MatchesTrackFilter(audioTrack, boundAudioSource))
            {
                continue;
            }

            foreach (TimelineClip timelineClip in audioTrack.GetClips())
            {
                AudioPlayableAsset audioPlayableAsset = timelineClip.asset as AudioPlayableAsset;
                if (audioPlayableAsset == null || audioPlayableAsset.clip == null)
                {
                    continue;
                }

                cachedAudioClips.Add(new RuntimeAudioClipInfo
                {
                    track = audioTrack,
                    clip = timelineClip,
                    audioClip = audioPlayableAsset.clip,
                    boundAudioSource = boundAudioSource
                });
            }
        }

        cachedAudioClips.Sort((left, right) => left.clip.start.CompareTo(right.clip.start));

        if (cachedAudioClips.Count == 0 && !warnedMissingAudioTracks)
        {
            Debug.LogWarning(
                $"[{nameof(TimelineAudioSubtitleController)}] No matching Timeline audio clips found on {name}.",
                this
            );
            warnedMissingAudioTracks = true;
        }
    }

    private bool MatchesTrackFilter(AudioTrack audioTrack, AudioSource boundAudioSource)
    {
        bool matchesAudioSource = targetAudioSource == null || boundAudioSource == targetAudioSource;
        bool matchesTrackName = string.IsNullOrEmpty(audioTrackName)
            || string.Equals(audioTrack.name, audioTrackName, StringComparison.OrdinalIgnoreCase);
        return matchesAudioSource && matchesTrackName;
    }

    private RuntimeAudioClipInfo GetActiveRuntimeClip(double directorTime)
    {
        RuntimeAudioClipInfo activeClip = null;

        for (int i = 0; i < cachedAudioClips.Count; i++)
        {
            RuntimeAudioClipInfo candidate = cachedAudioClips[i];
            double clipStart = candidate.clip.start;
            double clipEnd = candidate.clip.end;

            if (directorTime < clipStart || directorTime >= clipEnd)
            {
                continue;
            }

            if (activeClip == null || candidate.clip.start >= activeClip.clip.start)
            {
                activeClip = candidate;
            }
        }

        return activeClip;
    }

    private SubtitleEntry FindSubtitleEntry(RuntimeAudioClipInfo runtimeClip, double localTime)
    {
        SubtitleEntry activeEntry = null;
        float activeEntryStartTime = float.MinValue;

        for (int i = 0; i < subtitles.Count; i++)
        {
            SubtitleEntry entry = subtitles[i];
            if (entry == null)
            {
                continue;
            }

            if (!MatchesEntryToClip(entry, runtimeClip))
            {
                continue;
            }

            float startTime = Mathf.Max(0f, entry.clipLocalStartTime);
            float endTime = GetResolvedEntryEndTime(runtimeClip, entry);
            bool isInsideWindow = localTime >= startTime && localTime < endTime;

            if (!isInsideWindow)
            {
                continue;
            }

            if (activeEntry == null || startTime >= activeEntryStartTime)
            {
                activeEntry = entry;
                activeEntryStartTime = startTime;
            }
        }

        return activeEntry;
    }

    private void ShowSubtitle(TimelineClip clip, string subtitle, bool force)
    {
        if (!force && activeTimelineClip == clip && activeSubtitleText == subtitle)
        {
            return;
        }

        activeTimelineClip = clip;
        activeSubtitleText = subtitle;

        if (subtitleText != null)
        {
            subtitleText.text = subtitle;
        }

        ApplySubtitleVisibility(true);
    }

    private void HideSubtitle()
    {
        activeTimelineClip = null;
        activeSubtitleText = null;

        if (subtitleText != null && clearTextWhenHidden)
        {
            subtitleText.text = string.Empty;
        }

        ApplySubtitleVisibility(false);
    }

    private void ApplySubtitleVisibility(bool visible)
    {
        if (subtitleCanvasGroup != null)
        {
            subtitleCanvasGroup.alpha = visible ? 1f : 0f;
            subtitleCanvasGroup.interactable = false;
            subtitleCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (subtitleText != null)
        {
            subtitleText.enabled = visible;
        }
    }

    [ContextMenu("Sync Subtitle Entries From Timeline")]
    private void SyncSubtitleEntriesFromTimeline()
    {
        EnsureReferences();
        RebuildTimelineCache();

        if (cachedAudioClips.Count == 0)
        {
            return;
        }

#if UNITY_EDITOR
        Undo.RecordObject(this, "Sync Timeline Subtitle Entries");
#endif

        for (int i = 0; i < cachedAudioClips.Count; i++)
        {
            RuntimeAudioClipInfo runtimeClip = cachedAudioClips[i];
            if (HasMatchingEntry(runtimeClip))
            {
                continue;
            }

            subtitles.Add(new SubtitleEntry
            {
                timelineClipName = runtimeClip.clip.displayName,
                audioClip = runtimeClip.audioClip,
                subtitleText = string.Empty,
                clipLocalStartTime = 0f,
                clipLocalEndTime = -1f
            });
        }

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    private bool HasMatchingEntry(RuntimeAudioClipInfo runtimeClip)
    {
        for (int i = 0; i < subtitles.Count; i++)
        {
            SubtitleEntry entry = subtitles[i];
            if (entry == null)
            {
                continue;
            }

            if (MatchesEntryToClip(entry, runtimeClip))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesEntryToClip(SubtitleEntry entry, RuntimeAudioClipInfo runtimeClip)
    {
        bool sameClipName = !string.IsNullOrEmpty(entry.timelineClipName)
            && string.Equals(entry.timelineClipName, runtimeClip.clip.displayName, StringComparison.OrdinalIgnoreCase);

        bool sameAudioClip = entry.audioClip != null && entry.audioClip == runtimeClip.audioClip;

        return sameClipName || sameAudioClip;
    }

    private float GetResolvedEntryEndTime(RuntimeAudioClipInfo runtimeClip, SubtitleEntry entry)
    {
        float clipDuration = (float)runtimeClip.clip.duration;
        float startTime = Mathf.Max(0f, entry.clipLocalStartTime);

        if (entry.clipLocalEndTime >= 0f)
        {
            return Mathf.Clamp(entry.clipLocalEndTime, startTime, clipDuration);
        }

        float nextEntryStartTime = clipDuration;

        for (int i = 0; i < subtitles.Count; i++)
        {
            SubtitleEntry candidate = subtitles[i];
            if (candidate == null || ReferenceEquals(candidate, entry) || !MatchesEntryToClip(candidate, runtimeClip))
            {
                continue;
            }

            float candidateStartTime = Mathf.Max(0f, candidate.clipLocalStartTime);
            if (candidateStartTime > startTime && candidateStartTime < nextEntryStartTime)
            {
                nextEntryStartTime = candidateStartTime;
            }
        }

        return Mathf.Clamp(nextEntryStartTime, startTime, clipDuration);
    }
}
