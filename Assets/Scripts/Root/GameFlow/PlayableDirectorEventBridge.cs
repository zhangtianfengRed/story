using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>
/// 通用 Timeline 回调桥。
/// 挂到任意带 PlayableDirector 的物体上，就能在播放开始/结束时触发 UnityEvent，
/// 也可以直接复用 GameFlowManager 的跳步能力。
/// </summary>
[DisallowMultipleComponent]
public class PlayableDirectorEventBridge : MonoBehaviour
{
    [SerializeField] private PlayableDirector director;
    [SerializeField] private bool autoPlayOnEnable;
    [SerializeField] private bool restartFromBeginningWhenAutoPlay = true;
    [SerializeField] private bool onlyInvokeStoppedAfterPlay = true;

    [Header("Callbacks")]
    [SerializeField] private UnityEvent onPlayed;
    [SerializeField] private UnityEvent onStopped;

    private bool hasPlayedSinceEnable;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void Awake()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }
    }

    private void OnEnable()
    {
        SubscribeDirector();

        if (autoPlayOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        UnsubscribeDirector();
        hasPlayedSinceEnable = false;
    }

    public void Play()
    {
        if (director == null)
        {
            Debug.LogWarning($"[{nameof(PlayableDirectorEventBridge)}] Missing PlayableDirector on {name}.", this);
            return;
        }

        if (restartFromBeginningWhenAutoPlay)
        {
            director.time = 0d;
            director.Evaluate();
        }

        director.Play();
    }

    public void Stop()
    {
        if (director == null)
        {
            return;
        }

        director.Stop();
    }

    public void CompleteCurrentStep()
    {
        GameFlowManager.Instance.CompleteCurrentStep();
    }

    public void LoadNextStep()
    {
        GameFlowManager.Instance.LoadNextStep();
    }

    public void CompleteCurrentStepAndLoadNext()
    {
        GameFlowManager.Instance.CompleteCurrentStepAndLoadNext();
    }

    public void JumpToStep(string stepId)
    {
        GameFlowManager.Instance.JumpToStep(stepId);
    }

    private void SubscribeDirector()
    {
        if (director == null)
        {
            return;
        }

        director.played -= HandleDirectorPlayed;
        director.stopped -= HandleDirectorStopped;
        director.played += HandleDirectorPlayed;
        director.stopped += HandleDirectorStopped;
    }

    private void UnsubscribeDirector()
    {
        if (director == null)
        {
            return;
        }

        director.played -= HandleDirectorPlayed;
        director.stopped -= HandleDirectorStopped;
    }

    private void HandleDirectorPlayed(PlayableDirector playedDirector)
    {
        if (playedDirector != director)
        {
            return;
        }

        hasPlayedSinceEnable = true;
        onPlayed?.Invoke();
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (stoppedDirector != director)
        {
            return;
        }

        if (onlyInvokeStoppedAfterPlay && !hasPlayedSinceEnable)
        {
            return;
        }

        hasPlayedSinceEnable = false;
        onStopped?.Invoke();
    }
}
