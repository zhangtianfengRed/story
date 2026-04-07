using System.Collections.Generic;
using UnityEngine;

public class BodyClipPlayer : MonoBehaviour
{
    public Animator animator;

    [Header("AOC 槽位 (需对应 Animator 中两个状态的原始 Clip)")]
    public AnimationClip keyA;
    public AnimationClip keyB;

    [Header("状态名 (对应 Animator 窗口中的方块名)")]
    public string stateA = "Motion1";
    public string stateB = "Motion2";

    AnimatorOverrideController aoc;
    AnimationClip _lastClip;
    bool _isUsingStateB = false;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (animator && animator.runtimeAnimatorController != null)
        {
            // 创建覆盖控制器
            if (animator.runtimeAnimatorController is AnimatorOverrideController existingAoc)
                aoc = existingAoc;
            else
                aoc = new AnimatorOverrideController(animator.runtimeAnimatorController);

            animator.runtimeAnimatorController = aoc;
        }
    }

    public void Play(AnimationClip clip, float fade = 1f, float normalizedTime = 0f)
    {
        if (!clip || !aoc) return;
        if (!keyA || !keyB)
        {
            Debug.LogError("[Body] 无法混合！请在面板上分配 Key A 和 Key B 两个原始动作资源。");
            return;
        }

        // 核心：在 A 和 B 两个状态间切换以实现交叉混合
        _isUsingStateB = !_isUsingStateB;
        string targetState = _isUsingStateB ? stateB : stateA;
        AnimationClip slotKey = _isUsingStateB ? keyB : keyA;

        // 替换目标槽位的动作
        aoc[slotKey] = clip;

        if (animator)
        {
            // 执行交叉混合
            animator.CrossFadeInFixedTime(targetState, fade, 0);
            Debug.Log($"[Body] 正在混合切换至: <color=cyan>{targetState}</color> (动作: {clip.name})");
        }
        _lastClip = clip;
    }

    public void PlayRandom(IList<AnimationClip> clips, float fade = 0.3f, bool avoidRepeat = true)
    {
        if (clips == null || clips.Count == 0) return;
        int idx = Random.Range(0, clips.Count);
        if (avoidRepeat && clips.Count > 1 && clips[idx] == _lastClip)
            idx = (idx + 1) % clips.Count;

        Play(clips[idx], fade);
    }
}