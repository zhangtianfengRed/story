using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomDialogueVoiceSubtitleAction", menuName = "Room/Interaction/对白语音字幕 Action")]
public class RoomDialogueVoiceSubtitleAction : RoomInteractionAction
{
    [Header("显示层")]
    [InspectorName("自动查找显示层")]
    [Tooltip("开启后会优先查找场景里的 DialogueSubtitleOverlay。通常保持开启。")]
    public bool autoFindOverlay = true;

    [InspectorName("缺失时自动创建")]
    [Tooltip("场景里没有 DialogueSubtitleOverlay 时，运行时自动创建一个通用字幕显示层。通常保持开启。")]
    public bool createOverlayWhenMissing = true;

    [Header("语音")]
    [InspectorName("语音片段")]
    [Tooltip("要播放的对白语音。可以是英文语音，字幕文本可以单独配置成中文。")]
    public AudioClip voiceClip;

    [InspectorName("语音音量")]
    [Tooltip("这条对白自己的音量，会再乘以全局 Voice 音量设置。")]
    [Range(0f, 1f)]
    public float voiceVolume = 1f;

    [InspectorName("打断当前播放")]
    [Tooltip("开启后，触发新对白时会停止当前正在播放的对白。")]
    public bool interruptCurrentPlayback = true;

    [Header("字幕")]
    [InspectorName("跟随全局字幕开关")]
    [Tooltip("开启后，如果设置里关闭了字幕，这里也不会显示字幕，但语音仍会播放。")]
    public bool respectGlobalSubtitleSetting = true;

    [InspectorName("字幕输入模式")]
    [Tooltip("推荐使用“整段文本自动拆分”：只填整段字幕，系统会自动按标点拆段并分配时间。")]
    public DialogueSubtitleInputMode subtitleInputMode = DialogueSubtitleInputMode.FullTextAutoSplit;

    [InspectorName("整段字幕文本")]
    [TextArea(3, 8)]
    [Tooltip("推荐模式：直接粘整段字幕。系统会按标点自动拆成多段，并按语音长度自动分配显示时间。")]
    public string fullSubtitleText;

    [InspectorName("原文节奏参考（可选）")]
    [TextArea(3, 8)]
    [Tooltip("可选：只用于估算字幕节奏，不显示。英文语音配中文字幕时，可以填英文原文。")]
    public string fullTimingText;

    [InspectorName("每段最大字符数")]
    [Min(8)]
    [Tooltip("自动拆分时，每段字幕建议的最大可见字符数。")]
    public int preferredMaxCharactersPerCue = 42;

    [InspectorName("自动分配未定时字幕")]
    [Tooltip("自动拆分或手动字幕没有填写时间时，按语音长度和每段文本长度自动分配显示时间。")]
    public bool autoDistributeUntimedCues = true;

    [InspectorName("无语音默认时长")]
    [Min(0.1f)]
    [Tooltip("没有配置语音片段时，每段字幕默认显示多少秒。")]
    public float fallbackSubtitleDuration = 3f;

    [InspectorName("手动字幕分段")]
    [Tooltip("高级模式：需要精确时间轴时再切换到 Manual Cues 使用。")]
    public List<DialogueSubtitleCue> subtitles = new List<DialogueSubtitleCue>();

    [Header("房间互动流程")]
    [InspectorName("播放期间锁定移动")]
    [Tooltip("语音播放期间关闭玩家移动。")]
    public bool lockPlayerMovementWhilePlaying;

    [InspectorName("播放期间禁用交互")]
    [Tooltip("语音播放期间关闭 RoomPlayerInteractor，避免重复按 E 重放。")]
    public bool disableInteractorWhilePlaying;

    public override void Execute(RoomInteractionContext context)
    {
        DialogueSubtitleOverlay overlay = ResolveOverlay();
        if (overlay == null)
        {
            Debug.LogWarning("[RoomDialogueVoiceSubtitleAction] No DialogueSubtitleOverlay found.", this);
            return;
        }

        DialogueSubtitlePlaybackOptions options = new DialogueSubtitlePlaybackOptions
        {
            interruptCurrentPlayback = interruptCurrentPlayback,
            respectGlobalSubtitleSetting = respectGlobalSubtitleSetting,
            autoDistributeUntimedCues = autoDistributeUntimedCues,
            hideWhenFinished = true,
            clearTextWhenHidden = true,
            baseVoiceVolume = voiceVolume,
            fallbackSubtitleDuration = fallbackSubtitleDuration
        };

        if (overlay.IsPlaying && interruptCurrentPlayback)
        {
            overlay.Stop();
        }

        RoomDialoguePlaybackLock playbackLock = overlay.IsPlaying
            ? null
            : RoomDialoguePlaybackLock.Acquire(
                context,
                lockPlayerMovementWhilePlaying,
                disableInteractorWhilePlaying);

        List<DialogueSubtitleCue> playbackCues = BuildPlaybackCues();
        bool started = overlay.Play(
            voiceClip,
            playbackCues,
            options,
            playbackLock != null ? playbackLock.Release : null);

        if (!started && playbackLock != null)
        {
            playbackLock.Release();
        }
    }

    private DialogueSubtitleOverlay ResolveOverlay()
    {
        if (DialogueSubtitleOverlay.Instance != null)
        {
            return DialogueSubtitleOverlay.Instance;
        }

        if (autoFindOverlay)
        {
            DialogueSubtitleOverlay overlay = FindObjectOfType<DialogueSubtitleOverlay>(true);
            if (overlay != null)
            {
                return overlay;
            }
        }

        return createOverlayWhenMissing ? DialogueSubtitleOverlay.GetOrCreate() : null;
    }

    private List<DialogueSubtitleCue> BuildPlaybackCues()
    {
        List<DialogueSubtitleCue> cues = DialogueSubtitleTextUtility.BuildCues(
            subtitleInputMode,
            fullSubtitleText,
            fullTimingText,
            subtitles,
            preferredMaxCharactersPerCue);

        if (cues.Count == 0 && subtitleInputMode == DialogueSubtitleInputMode.FullTextAutoSplit)
        {
            cues = DialogueSubtitleTextUtility.BuildCues(
                DialogueSubtitleInputMode.ManualCues,
                string.Empty,
                string.Empty,
                subtitles,
                preferredMaxCharactersPerCue);
        }

        return cues;
    }

    private sealed class RoomDialoguePlaybackLock
    {
        private RoomTopDownPlayerMovement movement;
        private RoomPlayerInteractor interactor;
        private bool previousMovementEnabled;
        private bool previousInteractorEnabled;
        private bool released;

        public static RoomDialoguePlaybackLock Acquire(
            RoomInteractionContext context,
            bool lockMovement,
            bool disableInteractor)
        {
            if ((!lockMovement && !disableInteractor) || context == null || context.Player == null)
            {
                return null;
            }

            RoomDialoguePlaybackLock playbackLock = new RoomDialoguePlaybackLock();

            if (lockMovement)
            {
                playbackLock.movement = context.Player.GetComponentInChildren<RoomTopDownPlayerMovement>(true);
                if (playbackLock.movement != null)
                {
                    playbackLock.previousMovementEnabled = playbackLock.movement.MovementControlEnabled;
                    playbackLock.movement.SetMovementControlEnabled(false);
                }
            }

            if (disableInteractor)
            {
                playbackLock.interactor = context.Player.GetComponentInChildren<RoomPlayerInteractor>(true);
                if (playbackLock.interactor != null)
                {
                    playbackLock.previousInteractorEnabled = playbackLock.interactor.enabled;
                    playbackLock.interactor.enabled = false;
                }
            }

            return playbackLock.HasAnyLock ? playbackLock : null;
        }

        private bool HasAnyLock
        {
            get { return movement != null || interactor != null; }
        }

        public void Release()
        {
            if (released)
            {
                return;
            }

            released = true;

            if (movement != null)
            {
                movement.SetMovementControlEnabled(previousMovementEnabled);
                movement = null;
            }

            if (interactor != null)
            {
                interactor.enabled = previousInteractorEnabled;
                interactor = null;
            }
        }
    }
}
