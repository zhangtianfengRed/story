using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class FaceClipPlayer : MonoBehaviour
{
    public PlayableDirector director;

    void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    public void Play(TimelineAsset clip, bool restart = true)
    {
        if (!director || !clip) return;

        if (restart) director.Stop();                 // 防止叠加抢控制
        director.playableAsset = clip;                // 换片段
        director.time = 0;
        director.Evaluate();                          // 立即刷新一帧（可选但很实用）
        director.Play();
    }
}