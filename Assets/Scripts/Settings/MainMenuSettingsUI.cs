using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主页设置面板控制器。
/// 负责把 UI 控件和全局设置管理器连起来。
/// </summary>
public class MainMenuSettingsUI : MonoBehaviour
{
    private enum SettingsTab
    {
        Display,
        Graphics,
        Audio,
        Gameplay
    }

    [Header("面板")]
    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private CanvasGroup settingsCanvasGroup;
    [SerializeField] private Button applyButton;
    [SerializeField] private bool allowEscapeToClose = true;
    [SerializeField] private bool autoBindControlEvents = true;

    [Header("按钮")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button displayTabButton;
    [SerializeField] private Button graphicsTabButton;
    [SerializeField] private Button audioTabButton;
    [SerializeField] private Button gameplayTabButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button resetButton;

    [Header("Tab 文字")]
    [SerializeField] private TMP_Text displayTabText;
    [SerializeField] private TMP_Text graphicsTabText;
    [SerializeField] private TMP_Text audioTabText;
    [SerializeField] private TMP_Text gameplayTabText;
    [SerializeField] private Color activeTabTextColor = Color.white;
    [SerializeField] private Color inactiveTabTextColor = new Color(0.7f, 0.7f, 0.7f, 1f);

    [Header("分页")]
    [SerializeField] private GameObject displayPage;
    [SerializeField] private GameObject graphicsPage;
    [SerializeField] private GameObject audioPage;
    [SerializeField] private GameObject gameplayPage;
    [SerializeField] private bool openDisplayTabByDefault = true;

    [Header("显示")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown displayModeDropdown;
    [SerializeField] private TMP_Dropdown frameRateDropdown;
    [SerializeField] private Toggle vSyncToggle;

    [Header("画质")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown antiAliasingDropdown;

    [Header("音频")]
    [SerializeField] private Toggle musicEnabledToggle;
    [SerializeField] private Toggle sfxEnabledToggle;

    [Header("游戏性")]
    [SerializeField] private Slider dialogueSpeedSlider;
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private Toggle invertYAxisToggle;

    [Header("可选文本")]
    [SerializeField] private TMP_Text dialogueSpeedValueText;
    [SerializeField] private TMP_Text mouseSensitivityValueText;

    private static readonly int[] FrameRateOptions = { 30, 60, 120, 144, 240, -1 };
    private static readonly string[] FrameRateLabels = { "30 FPS", "60 FPS", "120 FPS", "144 FPS", "240 FPS", "Unlimited" };
    private static readonly int[] AntiAliasingOptions = { 0, 2, 4, 8 };
    private static readonly string[] AntiAliasingLabels = { "Off", "2x MSAA", "4x MSAA", "8x MSAA" };

    private GameSettingsManager.GameSettingsData _editingSettings;
    private GameSettingsManager.GameSettingsData _savedSettingsSnapshot;
    private bool _isRefreshingUI;

    private void Awake()
    {
        if (autoBindControlEvents)
        {
            BindUiEvents();
        }
    }

    private void Start()
    {
        PopulateStaticDropdowns();
        RefreshFromSavedSettings();
        SetPanelVisible(settingsRoot != null && settingsRoot.activeSelf);
    }

    private void Update()
    {
        if (allowEscapeToClose && settingsRoot != null && settingsRoot.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            CancelChanges();
        }
    }

    public void OpenSettings()
    {
        RefreshFromSavedSettings();
        OpenDefaultTab();
        SetPanelVisible(true);
    }

    public void CloseSettings()
    {
        SetPanelVisible(false);
    }

    public void ApplyChanges()
    {
        SyncFromUI();
        GameSettingsManager.Instance.ApplySettings(_editingSettings, true);
        RefreshFromSavedSettings();
    }

    public void ApplyAndClose()
    {
        ApplyChanges();
        CloseSettings();
    }

    public void CancelChanges()
    {
        RefreshFromSavedSettings();
        CloseSettings();
    }

    public void ResetToDefaults()
    {
        _editingSettings = GameSettingsManager.Instance.CreateDefaultSettings();
        RefreshUIFromEditingSettings();
        UpdateApplyButtonVisibility();
    }

    public void RefreshFromSavedSettings()
    {
        _editingSettings = GameSettingsManager.Instance.GetSettingsCopy();
        _savedSettingsSnapshot = _editingSettings.Clone();
        PopulateResolutionDropdown();
        RefreshUIFromEditingSettings();
        UpdateApplyButtonVisibility();
    }

    public void OpenDisplayTab()
    {
        SetActiveTab(SettingsTab.Display);
    }

    public void OpenGraphicsTab()
    {
        SetActiveTab(SettingsTab.Graphics);
    }

    public void OpenAudioTab()
    {
        SetActiveTab(SettingsTab.Audio);
    }

    public void OpenGameplayTab()
    {
        SetActiveTab(SettingsTab.Gameplay);
    }

    public void OnMusicEnabledToggled(bool value)
    {
        if (_editingSettings == null) return;
        _editingSettings.musicEnabled = value;
        NotifyValueChanged();
    }

    public void OnSfxEnabledToggled(bool value)
    {
        if (_editingSettings == null) return;
        _editingSettings.sfxEnabled = value;
        NotifyValueChanged();
    }

    public void OnDialogueSpeedChanged(float value)
    {
        if (_editingSettings == null) return;
        _editingSettings.dialogueSpeed = value;
        UpdateFloatLabel(dialogueSpeedValueText, value, "0.00x");
        NotifyValueChanged();
    }

    public void OnMouseSensitivityChanged(float value)
    {
        if (_editingSettings == null) return;
        _editingSettings.mouseSensitivity = value;
        UpdateFloatLabel(mouseSensitivityValueText, value, "0.00x");
        NotifyValueChanged();
    }

    public void OnInvertYAxisToggled(bool value)
    {
        if (_editingSettings != null) _editingSettings.invertYAxis = value;
        NotifyValueChanged();
    }

    public void OnVSyncToggled(bool value)
    {
        if (_editingSettings != null) _editingSettings.vSyncEnabled = value;
        NotifyValueChanged();
    }

    public void OnResolutionChanged(int index)
    {
        if (_editingSettings == null) return;

        IReadOnlyList<Resolution> resolutions = GameSettingsManager.Instance.AvailableResolutions;
        if (index < 0 || index >= resolutions.Count)
        {
            return;
        }

        Resolution resolution = resolutions[index];
        _editingSettings.resolutionWidth = resolution.width;
        _editingSettings.resolutionHeight = resolution.height;
        NotifyValueChanged();
    }

    public void OnDisplayModeChanged(int index)
    {
        if (_editingSettings != null)
        {
            _editingSettings.fullScreenMode = index;
            NotifyValueChanged();
        }
    }

    public void OnQualityChanged(int index)
    {
        if (_editingSettings != null)
        {
            _editingSettings.qualityLevel = index;
            NotifyValueChanged();
        }
    }

    public void OnAntiAliasingChanged(int index)
    {
        if (_editingSettings == null || index < 0 || index >= AntiAliasingOptions.Length)
        {
            return;
        }

        _editingSettings.antiAliasing = AntiAliasingOptions[index];
        NotifyValueChanged();
    }

    public void OnFrameRateChanged(int index)
    {
        if (_editingSettings == null || index < 0 || index >= FrameRateOptions.Length)
        {
            return;
        }

        _editingSettings.targetFrameRate = FrameRateOptions[index];
        NotifyValueChanged();
    }

    private void PopulateStaticDropdowns()
    {
        if (displayModeDropdown != null)
        {
            displayModeDropdown.ClearOptions();
            displayModeDropdown.AddOptions(new List<string>
            {
                "Exclusive Fullscreen",
                "Fullscreen Window",
                "Maximized Window",
                "Windowed"
            });
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        }

        if (frameRateDropdown != null)
        {
            frameRateDropdown.ClearOptions();
            frameRateDropdown.AddOptions(new List<string>(FrameRateLabels));
        }

        if (antiAliasingDropdown != null)
        {
            antiAliasingDropdown.ClearOptions();
            antiAliasingDropdown.AddOptions(new List<string>(AntiAliasingLabels));
        }
    }

    private void PopulateResolutionDropdown()
    {
        if (resolutionDropdown == null)
        {
            return;
        }

        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();

        IReadOnlyList<Resolution> resolutions = GameSettingsManager.Instance.AvailableResolutions;
        for (int i = 0; i < resolutions.Count; i++)
        {
            options.Add(GameSettingsManager.Instance.GetResolutionLabel(i));
        }

        resolutionDropdown.AddOptions(options);
    }

    private void RefreshUIFromEditingSettings()
    {
        if (_editingSettings == null)
        {
            return;
        }

        _isRefreshingUI = true;

        if (resolutionDropdown != null)
        {
            resolutionDropdown.value = GameSettingsManager.Instance.FindResolutionIndex(_editingSettings.resolutionWidth, _editingSettings.resolutionHeight);
            resolutionDropdown.RefreshShownValue();
        }

        SetDropdownValue(displayModeDropdown, _editingSettings.fullScreenMode);
        SetDropdownValue(qualityDropdown, _editingSettings.qualityLevel);
        SetDropdownValue(frameRateDropdown, FindFrameRateIndex(_editingSettings.targetFrameRate));
        SetDropdownValue(antiAliasingDropdown, FindAntiAliasingIndex(_editingSettings.antiAliasing));

        SetToggleValue(vSyncToggle, _editingSettings.vSyncEnabled);
        SetToggleValue(musicEnabledToggle, _editingSettings.musicEnabled);
        SetToggleValue(sfxEnabledToggle, _editingSettings.sfxEnabled);
        SetToggleValue(invertYAxisToggle, _editingSettings.invertYAxis);

        SetSliderValue(dialogueSpeedSlider, _editingSettings.dialogueSpeed, OnDialogueSpeedChanged);
        SetSliderValue(mouseSensitivitySlider, _editingSettings.mouseSensitivity, OnMouseSensitivityChanged);

        _isRefreshingUI = false;
    }

    private void SyncFromUI()
    {
        if (_editingSettings == null || _isRefreshingUI)
        {
            return;
        }

        if (resolutionDropdown != null)
        {
            OnResolutionChanged(resolutionDropdown.value);
        }

        if (displayModeDropdown != null)
        {
            _editingSettings.fullScreenMode = displayModeDropdown.value;
        }

        if (qualityDropdown != null)
        {
            _editingSettings.qualityLevel = qualityDropdown.value;
        }

        if (frameRateDropdown != null)
        {
            _editingSettings.targetFrameRate = FrameRateOptions[Mathf.Clamp(frameRateDropdown.value, 0, FrameRateOptions.Length - 1)];
        }

        if (antiAliasingDropdown != null)
        {
            _editingSettings.antiAliasing = AntiAliasingOptions[Mathf.Clamp(antiAliasingDropdown.value, 0, AntiAliasingOptions.Length - 1)];
        }

        if (vSyncToggle != null)
        {
            _editingSettings.vSyncEnabled = vSyncToggle.isOn;
        }

        if (invertYAxisToggle != null)
        {
            _editingSettings.invertYAxis = invertYAxisToggle.isOn;
        }

        if (musicEnabledToggle != null)
        {
            _editingSettings.musicEnabled = musicEnabledToggle.isOn;
        }

        if (sfxEnabledToggle != null)
        {
            _editingSettings.sfxEnabled = sfxEnabledToggle.isOn;
        }

        if (dialogueSpeedSlider != null)
        {
            _editingSettings.dialogueSpeed = dialogueSpeedSlider.value;
        }

        if (mouseSensitivitySlider != null)
        {
            _editingSettings.mouseSensitivity = mouseSensitivitySlider.value;
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (settingsRoot != null)
        {
            settingsRoot.SetActive(visible);
        }

        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = visible ? 1f : 0f;
            settingsCanvasGroup.interactable = visible;
            settingsCanvasGroup.blocksRaycasts = visible;
        }
    }

    private void BindUiEvents()
    {
        BindButton(settingsButton, OpenSettings);
        BindButton(displayTabButton, OpenDisplayTab);
        BindButton(graphicsTabButton, OpenGraphicsTab);
        BindButton(audioTabButton, OpenAudioTab);
        BindButton(gameplayTabButton, OpenGameplayTab);
        BindButton(applyButton, ApplyChanges);
        BindButton(cancelButton, CancelChanges);
        BindButton(resetButton, ResetToDefaults);

        BindDropdown(resolutionDropdown, OnResolutionChanged);
        BindDropdown(displayModeDropdown, OnDisplayModeChanged);
        BindDropdown(frameRateDropdown, OnFrameRateChanged);
        BindDropdown(qualityDropdown, OnQualityChanged);
        BindDropdown(antiAliasingDropdown, OnAntiAliasingChanged);

        BindToggle(vSyncToggle, OnVSyncToggled);
        BindToggle(musicEnabledToggle, OnMusicEnabledToggled);
        BindToggle(sfxEnabledToggle, OnSfxEnabledToggled);
        BindToggle(invertYAxisToggle, OnInvertYAxisToggled);

        BindSlider(dialogueSpeedSlider, OnDialogueSpeedChanged);
        BindSlider(mouseSensitivitySlider, OnMouseSensitivityChanged);
    }

    private void OpenDefaultTab()
    {
        if (openDisplayTabByDefault)
        {
            OpenDisplayTab();
            return;
        }

        if (graphicsPage != null)
        {
            OpenGraphicsTab();
            return;
        }

        if (audioPage != null)
        {
            OpenAudioTab();
            return;
        }

        OpenGameplayTab();
    }

    private void SetActiveTab(SettingsTab tab)
    {
        SetPageActive(displayPage, tab == SettingsTab.Display);
        SetPageActive(graphicsPage, tab == SettingsTab.Graphics);
        SetPageActive(audioPage, tab == SettingsTab.Audio);
        SetPageActive(gameplayPage, tab == SettingsTab.Gameplay);

        SetTabTextColor(displayTabText, tab == SettingsTab.Display);
        SetTabTextColor(graphicsTabText, tab == SettingsTab.Graphics);
        SetTabTextColor(audioTabText, tab == SettingsTab.Audio);
        SetTabTextColor(gameplayTabText, tab == SettingsTab.Gameplay);
    }

    private static void SetPageActive(GameObject page, bool isActive)
    {
        if (page != null)
        {
            page.SetActive(isActive);
        }
    }

    private void SetTabTextColor(TMP_Text label, bool isActive)
    {
        if (label != null)
        {
            label.color = isActive ? activeTabTextColor : inactiveTabTextColor;
        }
    }

    private void NotifyValueChanged()
    {
        if (_isRefreshingUI)
        {
            return;
        }

        UpdateApplyButtonVisibility();
    }

    private void UpdateApplyButtonVisibility()
    {
        bool isDirty = IsDirty();

        if (applyButton != null)
        {
            applyButton.gameObject.SetActive(isDirty);
        }

        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(isDirty);
        }
    }

    private bool IsDirty()
    {
        if (_editingSettings == null || _savedSettingsSnapshot == null)
        {
            return false;
        }

        return _editingSettings.resolutionWidth != _savedSettingsSnapshot.resolutionWidth
            || _editingSettings.resolutionHeight != _savedSettingsSnapshot.resolutionHeight
            || _editingSettings.fullScreenMode != _savedSettingsSnapshot.fullScreenMode
            || _editingSettings.qualityLevel != _savedSettingsSnapshot.qualityLevel
            || _editingSettings.vSyncEnabled != _savedSettingsSnapshot.vSyncEnabled
            || _editingSettings.targetFrameRate != _savedSettingsSnapshot.targetFrameRate
            || _editingSettings.antiAliasing != _savedSettingsSnapshot.antiAliasing
            || _editingSettings.musicEnabled != _savedSettingsSnapshot.musicEnabled
            || _editingSettings.sfxEnabled != _savedSettingsSnapshot.sfxEnabled
            || !Mathf.Approximately(_editingSettings.dialogueSpeed, _savedSettingsSnapshot.dialogueSpeed)
            || !Mathf.Approximately(_editingSettings.mouseSensitivity, _savedSettingsSnapshot.mouseSensitivity)
            || _editingSettings.invertYAxis != _savedSettingsSnapshot.invertYAxis;
    }

    private static int FindFrameRateIndex(int targetFrameRate)
    {
        for (int i = 0; i < FrameRateOptions.Length; i++)
        {
            if (FrameRateOptions[i] == targetFrameRate)
            {
                return i;
            }
        }

        return FrameRateOptions.Length - 1;
    }

    private static int FindAntiAliasingIndex(int antiAliasing)
    {
        for (int i = 0; i < AntiAliasingOptions.Length; i++)
        {
            if (AntiAliasingOptions[i] == antiAliasing)
            {
                return i;
            }
        }

        return 0;
    }

    private void SetSliderValue(Slider slider, float value, System.Action<float> onChanged)
    {
        if (slider == null)
        {
            return;
        }

        slider.value = value;
        onChanged?.Invoke(value);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void BindDropdown(TMP_Dropdown dropdown, UnityEngine.Events.UnityAction<int> action)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.onValueChanged.RemoveListener(action);
        dropdown.onValueChanged.AddListener(action);
    }

    private static void BindToggle(Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveListener(action);
        toggle.onValueChanged.AddListener(action);
    }

    private static void BindSlider(Slider slider, UnityEngine.Events.UnityAction<float> action)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(action);
        slider.onValueChanged.AddListener(action);
    }

    private static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle != null)
        {
            toggle.isOn = value;
        }
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.value = Mathf.Clamp(value, 0, dropdown.options.Count - 1);
        dropdown.RefreshShownValue();
    }

    private static void UpdateFloatLabel(TMP_Text label, float value, string format)
    {
        if (label != null)
        {
            label.text = value.ToString(format);
        }
    }
}
