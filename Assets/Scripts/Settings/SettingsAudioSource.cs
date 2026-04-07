using System.Collections.Generic;
using UnityEngine;

public enum SettingsAudioChannel
{
    Music,
    Sfx,
    UI,
    Voice
}

/// <summary>
/// 给音源挂上这个组件后，设置管理器就能按通道控制它的音量。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SettingsAudioSource : MonoBehaviour
{
    private static readonly HashSet<SettingsAudioSource> Instances = new HashSet<SettingsAudioSource>();

    [SerializeField] private SettingsAudioChannel channel = SettingsAudioChannel.Sfx;
    [SerializeField] [Range(0f, 1f)] private float baseVolume = 1f;
    [SerializeField] private bool captureSourceVolumeOnAwake = true;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();

        if (captureSourceVolumeOnAwake)
        {
            baseVolume = _audioSource.volume;
        }
    }

    private void OnEnable()
    {
        Instances.Add(this);
        ApplyCurrentSettings();
    }

    private void OnDisable()
    {
        Instances.Remove(this);
    }

    public void ApplyCurrentSettings()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        float channelVolume = GameSettingsManager.GetChannelVolume(channel);
        _audioSource.volume = Mathf.Clamp01(baseVolume * channelVolume);
    }

    public static void ApplyAll(GameSettingsManager.GameSettingsData settings)
    {
        foreach (SettingsAudioSource source in Instances)
        {
            if (source != null)
            {
                source.ApplyCurrentSettings();
            }
        }
    }
}
