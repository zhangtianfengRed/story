using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// Switches a PlayableDirector between isolated Timeline shots while preserving
/// common track bindings such as Animation/Audio/Signal tracks.
/// </summary>
[DisallowMultipleComponent]
public class TimelineShotPlayer : MonoBehaviour
{
    [Serializable]
    public class ShotEntry
    {
        public string shotName;
        public TimelineAsset timeline;
        [Min(0)] public double startTime;
    }

    [Serializable]
    public class TrackBindingOverride
    {
        public string trackName;
        public UnityEngine.Object bindingTarget;
    }

    [Header("Director")]
    public PlayableDirector director;
    public TimelineCameraPoseSetter cameraPoseSetter;

    [Header("Playback")]
    public bool stopDirectorBeforeSwitch = true;
    public bool inheritBindingsFromCurrentTimeline = true;
    public bool enableCameraAnimatorBeforePlay = true;
    public bool evaluateBeforePlay = true;
    public bool holdCameraPoseWhenDirectorStops = true;

    [Header("Bindings")]
    public TrackBindingOverride[] bindingOverrides;

    [Header("Shots")]
    public ShotEntry[] shots;

    private readonly Dictionary<string, UnityEngine.Object> runtimeBindings = new Dictionary<string, UnityEngine.Object>();
    private PlayableDirector subscribedDirector;

    public bool IsPlaying => director != null && director.state == PlayState.Playing;

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
        if (cameraPoseSetter == null)
        {
            cameraPoseSetter = FindObjectOfType<TimelineCameraPoseSetter>();
        }
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
        SubscribeDirectorEvents();
    }

    private void OnDisable()
    {
        UnsubscribeDirectorEvents();
    }

    public void PlayShotByIndex(int shotIndex)
    {
        if (shots == null || shotIndex < 0 || shotIndex >= shots.Length)
        {
            Debug.LogWarning($"[{nameof(TimelineShotPlayer)}] Shot index {shotIndex} is not valid on {name}.", this);
            return;
        }

        PlayShot(shots[shotIndex]);
    }

    public void PlayShotByName(string shotName)
    {
        if (shots == null)
        {
            Debug.LogWarning($"[{nameof(TimelineShotPlayer)}] No shots are configured on {name}.", this);
            return;
        }

        for (int i = 0; i < shots.Length; i++)
        {
            ShotEntry shot = shots[i];
            if (shot != null && shot.timeline != null && shot.shotName == shotName)
            {
                PlayShot(shot);
                return;
            }
        }

        Debug.LogWarning($"[{nameof(TimelineShotPlayer)}] Shot named '{shotName}' was not found on {name}.", this);
    }

    public void PlayTimeline(TimelineAsset timeline)
    {
        PlayTimeline(timeline, 0d);
    }

    public void PlayTimeline(TimelineAsset timeline, double startTime)
    {
        EnsureReferences();
        SubscribeDirectorEvents();

        if (director == null)
        {
            Debug.LogWarning($"[{nameof(TimelineShotPlayer)}] Missing PlayableDirector on {name}.", this);
            return;
        }

        if (timeline == null)
        {
            Debug.LogWarning($"[{nameof(TimelineShotPlayer)}] Missing TimelineAsset on {name}.", this);
            return;
        }

        runtimeBindings.Clear();
        if (inheritBindingsFromCurrentTimeline)
        {
            CacheBindings(director.playableAsset as TimelineAsset);
        }

        if (stopDirectorBeforeSwitch)
        {
            director.Stop();
        }

        if (enableCameraAnimatorBeforePlay && cameraPoseSetter != null)
        {
            cameraPoseSetter.EnableAnimator();
        }
        else if (cameraPoseSetter != null)
        {
            cameraPoseSetter.CleanupAnimationState();
        }

        director.playableAsset = timeline;
        ApplyBindings(timeline);
        director.time = Math.Max(0d, startTime);

        if (evaluateBeforePlay)
        {
            director.Evaluate();
        }

        director.Play();
    }

    public void StopActiveShot()
    {
        EnsureReferences();
        if (director != null)
        {
            director.Stop();
        }
    }

    public void RefreshBindingsForCurrentTimeline()
    {
        EnsureReferences();
        if (director == null)
        {
            Debug.LogWarning($"[{nameof(TimelineShotPlayer)}] Missing PlayableDirector on {name}.", this);
            return;
        }

        ApplyBindings(director.playableAsset as TimelineAsset);
    }

    private void PlayShot(ShotEntry shot)
    {
        if (shot == null || shot.timeline == null)
        {
            Debug.LogWarning($"[{nameof(TimelineShotPlayer)}] Shot entry is missing a TimelineAsset on {name}.", this);
            return;
        }

        PlayTimeline(shot.timeline, shot.startTime);
    }

    private void EnsureReferences()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }

        if (cameraPoseSetter == null)
        {
            cameraPoseSetter = FindObjectOfType<TimelineCameraPoseSetter>();
        }
    }

    private void SubscribeDirectorEvents()
    {
        if (subscribedDirector == director)
        {
            return;
        }

        UnsubscribeDirectorEvents();
        if (director != null)
        {
            subscribedDirector = director;
            subscribedDirector.stopped += HandleDirectorStopped;
        }
    }

    private void UnsubscribeDirectorEvents()
    {
        if (subscribedDirector != null)
        {
            subscribedDirector.stopped -= HandleDirectorStopped;
            subscribedDirector = null;
        }
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (!holdCameraPoseWhenDirectorStops || cameraPoseSetter == null)
        {
            return;
        }

        cameraPoseSetter.HoldCurrentPoseAndDisableAnimator();
    }

    private void CacheBindings(TimelineAsset timeline)
    {
        if (timeline == null || director == null)
        {
            return;
        }

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track == null)
            {
                continue;
            }

            UnityEngine.Object binding = director.GetGenericBinding(track);
            if (binding == null)
            {
                continue;
            }

            runtimeBindings[BuildBindingKey(track.name, track.GetType())] = binding;
            runtimeBindings[BuildBindingKey(track.name, null)] = binding;
        }
    }

    private void ApplyBindings(TimelineAsset timeline)
    {
        if (timeline == null || director == null)
        {
            return;
        }

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track == null)
            {
                continue;
            }

            UnityEngine.Object binding = ResolveBinding(track);
            if (binding != null)
            {
                director.SetGenericBinding(track, binding);
            }
        }
    }

    private UnityEngine.Object ResolveBinding(TrackAsset track)
    {
        if (bindingOverrides != null)
        {
            for (int i = 0; i < bindingOverrides.Length; i++)
            {
                TrackBindingOverride bindingOverride = bindingOverrides[i];
                if (bindingOverride == null || string.IsNullOrEmpty(bindingOverride.trackName))
                {
                    continue;
                }

                if (bindingOverride.trackName == track.name && bindingOverride.bindingTarget != null)
                {
                    return bindingOverride.bindingTarget;
                }
            }
        }

        if (runtimeBindings.TryGetValue(BuildBindingKey(track.name, track.GetType()), out UnityEngine.Object binding) && binding != null)
        {
            return binding;
        }

        if (runtimeBindings.TryGetValue(BuildBindingKey(track.name, null), out binding) && binding != null)
        {
            return binding;
        }

        if (track is SignalTrack)
        {
            return GetComponent<SignalReceiver>();
        }

        if (track is AudioTrack)
        {
            TimelineAudioSubtitleController subtitleController = GetComponent<TimelineAudioSubtitleController>();
            if (subtitleController != null && subtitleController.targetAudioSource != null)
            {
                return subtitleController.targetAudioSource;
            }

            return GetComponent<AudioSource>();
        }

        if (track is AnimationTrack && cameraPoseSetter != null)
        {
            if (cameraPoseSetter.animatorToDisable != null)
            {
                return cameraPoseSetter.animatorToDisable;
            }

            if (cameraPoseSetter.targetCamera != null)
            {
                return cameraPoseSetter.targetCamera.GetComponent<Animator>();
            }
        }

        return null;
    }

    private static string BuildBindingKey(string trackName, Type trackType)
    {
        string typeName = trackType == null ? string.Empty : trackType.FullName;
        return $"{typeName}|{trackName}";
    }
}
