using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局设置管理器。
/// 负责读取、保存并应用显示、画质、音频和常见玩法设置。
/// </summary>
public class GameSettingsManager : MonoBehaviour
{
    private static GameSettingsManager _instance;

    [Serializable]
    public class GameSettingsData
    {
        public int resolutionWidth = 1920;
        public int resolutionHeight = 1080;
        public int fullScreenMode = (int)FullScreenMode.FullScreenWindow;
        public int qualityLevel = 0;
        public bool vSyncEnabled = true;
        public int targetFrameRate = 60;
        public int antiAliasing = 0;

        public float masterVolume = 1f;
        public bool musicEnabled = true;
        public float musicVolume = 0.8f;
        public bool sfxEnabled = true;
        public float sfxVolume = 0.8f;
        public float uiVolume = 0.8f;
        public float voiceVolume = 0.8f;
        public bool muteWhenUnfocused = false;

        public bool subtitlesEnabled = true;
        public float dialogueSpeed = 1f;
        public float mouseSensitivity = 1f;
        public bool invertYAxis = false;
        public bool cameraShakeEnabled = true;

        public GameSettingsData Clone()
        {
            return new GameSettingsData
            {
                resolutionWidth = resolutionWidth,
                resolutionHeight = resolutionHeight,
                fullScreenMode = fullScreenMode,
                qualityLevel = qualityLevel,
                vSyncEnabled = vSyncEnabled,
                targetFrameRate = targetFrameRate,
                antiAliasing = antiAliasing,
                masterVolume = masterVolume,
                musicEnabled = musicEnabled,
                musicVolume = musicVolume,
                sfxEnabled = sfxEnabled,
                sfxVolume = sfxVolume,
                uiVolume = uiVolume,
                voiceVolume = voiceVolume,
                muteWhenUnfocused = muteWhenUnfocused,
                subtitlesEnabled = subtitlesEnabled,
                dialogueSpeed = dialogueSpeed,
                mouseSensitivity = mouseSensitivity,
                invertYAxis = invertYAxis,
                cameraShakeEnabled = cameraShakeEnabled
            };
        }
    }

    [Serializable]
    private class ResolutionSaveData
    {
        public int width;
        public int height;
    }

    public static GameSettingsManager Instance
    {
        get
        {
            EnsureInstance();
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    public GameSettingsData CurrentSettings { get; private set; }

    public IReadOnlyList<Resolution> AvailableResolutions => _availableResolutions;

    public event Action<GameSettingsData> SettingsApplied;

    private readonly List<Resolution> _availableResolutions = new List<Resolution>();
    private bool _suppressFocusMute;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
        {
            return;
        }

        _instance = FindObjectOfType<GameSettingsManager>();
        if (_instance != null)
        {
            return;
        }

        GameObject go = new GameObject(nameof(GameSettingsManager));
        _instance = go.AddComponent<GameSettingsManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        CacheResolutions();

        CurrentSettings = LoadSettings();
        ApplySettings(CurrentSettings, false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (_suppressFocusMute)
        {
            return;
        }

        if (CurrentSettings == null || !CurrentSettings.muteWhenUnfocused)
        {
            AudioListener.pause = false;
            return;
        }

        AudioListener.pause = !hasFocus;
    }

    public GameSettingsData GetSettingsCopy()
    {
        return CurrentSettings.Clone();
    }

    public GameSettingsData CreateDefaultSettings()
    {
        CacheResolutions();

        Resolution currentResolution = Screen.currentResolution;
        int currentQuality = Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, Mathf.Max(0, QualitySettings.names.Length - 1));

        return new GameSettingsData
        {
            resolutionWidth = currentResolution.width > 0 ? currentResolution.width : 1920,
            resolutionHeight = currentResolution.height > 0 ? currentResolution.height : 1080,
            fullScreenMode = (int)Screen.fullScreenMode,
            qualityLevel = currentQuality,
            vSyncEnabled = QualitySettings.vSyncCount > 0,
            targetFrameRate = Application.targetFrameRate > 0 ? Application.targetFrameRate : 60,
            antiAliasing = NormalizeAntiAliasing(QualitySettings.antiAliasing),
            masterVolume = 1f,
            musicEnabled = true,
            musicVolume = 0.8f,
            sfxEnabled = true,
            sfxVolume = 0.8f,
            uiVolume = 0.8f,
            voiceVolume = 0.8f,
            muteWhenUnfocused = false,
            subtitlesEnabled = true,
            dialogueSpeed = 1f,
            mouseSensitivity = 1f,
            invertYAxis = false,
            cameraShakeEnabled = true
        };
    }

    public void ApplySettings(GameSettingsData settings, bool saveAfterApply = true)
    {
        if (settings == null)
        {
            return;
        }

        CacheResolutions();
        ClampSettings(settings);

        CurrentSettings = settings.Clone();

        Resolution matchedResolution = FindBestResolution(CurrentSettings.resolutionWidth, CurrentSettings.resolutionHeight);
        Screen.SetResolution(
            matchedResolution.width,
            matchedResolution.height,
            (FullScreenMode)CurrentSettings.fullScreenMode);

        QualitySettings.SetQualityLevel(CurrentSettings.qualityLevel, true);
        QualitySettings.vSyncCount = CurrentSettings.vSyncEnabled ? 1 : 0;
        QualitySettings.antiAliasing = CurrentSettings.antiAliasing;
        Application.targetFrameRate = CurrentSettings.vSyncEnabled ? -1 : CurrentSettings.targetFrameRate;

        AudioListener.volume = Mathf.Clamp01(CurrentSettings.masterVolume);
        ApplyFocusMuteImmediate();
        SettingsAudioSource.ApplyAll(CurrentSettings);

        SettingsApplied?.Invoke(CurrentSettings.Clone());

        if (saveAfterApply)
        {
            SaveSettings(CurrentSettings);
        }
    }

    public void ResetToDefaults(bool saveAfterApply = true)
    {
        ApplySettings(CreateDefaultSettings(), saveAfterApply);
    }

    public int FindResolutionIndex(int width, int height)
    {
        CacheResolutions();

        for (int i = 0; i < _availableResolutions.Count; i++)
        {
            Resolution resolution = _availableResolutions[i];
            if (resolution.width == width && resolution.height == height)
            {
                return i;
            }
        }

        return Mathf.Max(0, _availableResolutions.Count - 1);
    }

    public string GetResolutionLabel(int index)
    {
        if (index < 0 || index >= _availableResolutions.Count)
        {
            return "Unknown";
        }

        Resolution resolution = _availableResolutions[index];
        return $"{resolution.width} x {resolution.height}";
    }

    public static float GetChannelVolume(SettingsAudioChannel channel)
    {
        if (!HasInstance || Instance.CurrentSettings == null)
        {
            return 1f;
        }

        switch (channel)
        {
            case SettingsAudioChannel.Music:
                return Instance.CurrentSettings.musicEnabled ? Instance.CurrentSettings.musicVolume : 0f;
            case SettingsAudioChannel.Sfx:
                return Instance.CurrentSettings.sfxEnabled ? Instance.CurrentSettings.sfxVolume : 0f;
            case SettingsAudioChannel.UI:
                return Instance.CurrentSettings.uiVolume;
            case SettingsAudioChannel.Voice:
                return Instance.CurrentSettings.voiceVolume;
            default:
                return 1f;
        }
    }

    public static bool GetMusicEnabled()
    {
        return !HasInstance || Instance.CurrentSettings == null || Instance.CurrentSettings.musicEnabled;
    }

    public static bool GetSfxEnabled()
    {
        return !HasInstance || Instance.CurrentSettings == null || Instance.CurrentSettings.sfxEnabled;
    }

    public static float GetMouseSensitivity()
    {
        return HasInstance && Instance.CurrentSettings != null ? Instance.CurrentSettings.mouseSensitivity : 1f;
    }

    public static float GetDialogueSpeed()
    {
        return HasInstance && Instance.CurrentSettings != null ? Instance.CurrentSettings.dialogueSpeed : 1f;
    }

    public static bool GetSubtitlesEnabled()
    {
        return !HasInstance || Instance.CurrentSettings == null || Instance.CurrentSettings.subtitlesEnabled;
    }

    public static bool GetInvertYAxis()
    {
        return HasInstance && Instance.CurrentSettings != null && Instance.CurrentSettings.invertYAxis;
    }

    public static bool GetCameraShakeEnabled()
    {
        return !HasInstance || Instance.CurrentSettings == null || Instance.CurrentSettings.cameraShakeEnabled;
    }

    private void ApplyFocusMuteImmediate()
    {
        _suppressFocusMute = true;
        AudioListener.pause = CurrentSettings.muteWhenUnfocused && !Application.isFocused;
        _suppressFocusMute = false;
    }

    private void CacheResolutions()
    {
        _availableResolutions.Clear();

        Resolution[] rawResolutions = Screen.resolutions;
        HashSet<string> uniqueKeys = new HashSet<string>();

        for (int i = rawResolutions.Length - 1; i >= 0; i--)
        {
            Resolution resolution = rawResolutions[i];
            string key = $"{resolution.width}x{resolution.height}";

            if (uniqueKeys.Add(key))
            {
                _availableResolutions.Insert(0, resolution);
            }
        }

        if (_availableResolutions.Count == 0)
        {
            Resolution fallback = Screen.currentResolution;
            if (fallback.width <= 0 || fallback.height <= 0)
            {
                fallback.width = 1920;
                fallback.height = 1080;
            }

            _availableResolutions.Add(fallback);
        }
    }

    private GameSettingsData LoadSettings()
    {
        if (!LocalSaveStore.TryLoadJson(LocalSaveStore.Keys.GameSettings, out GameSettingsData loaded))
        {
            return CreateDefaultSettings();
        }

        ClampSettings(loaded);
        return loaded;
    }

    private void SaveSettings(GameSettingsData settings)
    {
        LocalSaveStore.SaveJson(LocalSaveStore.Keys.GameSettings, settings);
    }

    private Resolution FindBestResolution(int width, int height)
    {
        int index = FindResolutionIndex(width, height);
        return _availableResolutions[index];
    }

    private void ClampSettings(GameSettingsData settings)
    {
        settings.qualityLevel = Mathf.Clamp(settings.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        settings.fullScreenMode = Mathf.Clamp(settings.fullScreenMode, 0, Enum.GetValues(typeof(FullScreenMode)).Length - 1);
        settings.masterVolume = Mathf.Clamp01(settings.masterVolume);
        settings.musicVolume = Mathf.Clamp01(settings.musicVolume);
        settings.sfxVolume = Mathf.Clamp01(settings.sfxVolume);
        settings.uiVolume = Mathf.Clamp01(settings.uiVolume);
        settings.voiceVolume = Mathf.Clamp01(settings.voiceVolume);
        settings.dialogueSpeed = Mathf.Clamp(settings.dialogueSpeed, 0.25f, 3f);
        settings.mouseSensitivity = Mathf.Clamp(settings.mouseSensitivity, 0.1f, 3f);
        settings.targetFrameRate = NormalizeFrameRate(settings.targetFrameRate);
        settings.antiAliasing = NormalizeAntiAliasing(settings.antiAliasing);

        Resolution bestResolution = FindBestResolution(settings.resolutionWidth, settings.resolutionHeight);
        settings.resolutionWidth = bestResolution.width;
        settings.resolutionHeight = bestResolution.height;
    }

    private static int NormalizeFrameRate(int frameRate)
    {
        if (frameRate <= 0)
        {
            return -1;
        }

        return Mathf.Clamp(frameRate, 30, 240);
    }

    private static int NormalizeAntiAliasing(int antiAliasing)
    {
        switch (antiAliasing)
        {
            case 0:
            case 2:
            case 4:
            case 8:
                return antiAliasing;
            default:
                return 0;
        }
    }
}
