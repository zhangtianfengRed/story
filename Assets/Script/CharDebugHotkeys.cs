using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

public class CharDebugHotkeys : MonoBehaviour
{
    public BodyClipPlayer body;
    public FaceClipPlayer face;
    public TalkController talker;

    [Header("Body pools")]
    public List<AnimationClip> idle;
    public List<AnimationClip> laugh;
    public List<AnimationClip> talk;

    [Header("Face clips")]
    public TimelineAsset faceHappyA;
    public TimelineAsset faceHappyB;
    public TimelineAsset faceAngryLoop;
    public TimelineAsset faceGiggle;

    [Header("Talk test")]
    public float talkSeconds = 1.5f;

    void Update()
    {
        // 1, 2, 4 是全状态切换（动作+表情）
        if (Input.GetKeyDown(KeyCode.Alpha1)) PlayState("1-Idle", idle, faceHappyA, false);
        if (Input.GetKeyDown(KeyCode.Alpha2)) PlayState("2-Laugh", laugh, faceGiggle, false);

        // 3 是叠加指令：只触发说话，不重置当前的身体动作和脸部 Timeline
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("[Hotkey] 按下 3-Talk (叠加模式: 保持当前动作/表情，仅开口说话)");
            if (talker) talker.SpeakFor(talkSeconds);
        }

        if (Input.GetKeyDown(KeyCode.Alpha4)) PlayState("4-Angry", idle, faceAngryLoop, false);

        // R 键重置所有存档（调试用）
        if (Input.GetKeyDown(KeyCode.R))
        {
            GameProgressManager.Instance.ClearProgress();
        }
    }

    void PlayState(string debugLabel, List<AnimationClip> bodyClips, TimelineAsset faceTimeline, bool shouldTalk)
    {
        Debug.Log($"[Hotkey] 切换状态: {debugLabel}");

        // 仅在列表非空时切换身体动作
        if (body != null && bodyClips != null && bodyClips.Count > 0)
        {
            body.PlayRandom(bodyClips, 0.3f);
        }

        // 仅在资源存在时切换表情 Timeline
        if (face != null && faceTimeline != null)
        {
            face.Play(faceTimeline);
        }

        // 说话逻辑
        if (talker)
        {
            if (shouldTalk) talker.SpeakFor(talkSeconds);
            else talker.StopSpeaking();
        }
    }
}