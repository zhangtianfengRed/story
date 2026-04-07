using Febucci.UI.Core;
using UnityEngine;

/// <summary>
/// 将全局对话速度设置同步到 Text Animator 的 Typewriter。
/// </summary>
[RequireComponent(typeof(TypewriterCore))]
public class TypewriterSettingsBridge : MonoBehaviour
{
    private TypewriterCore _typewriter;

    private void Awake()
    {
        _typewriter = GetComponent<TypewriterCore>();
    }

    private void OnEnable()
    {
        GameSettingsManager.Instance.SettingsApplied += HandleSettingsApplied;
        HandleSettingsApplied(GameSettingsManager.Instance.CurrentSettings);
    }

    private void OnDisable()
    {
        if (GameSettingsManager.HasInstance)
        {
            GameSettingsManager.Instance.SettingsApplied -= HandleSettingsApplied;
        }
    }

    private void HandleSettingsApplied(GameSettingsManager.GameSettingsData settings)
    {
        if (_typewriter == null || settings == null)
        {
            return;
        }

        _typewriter.SetTypewriterSpeed(settings.dialogueSpeed);
    }
}
