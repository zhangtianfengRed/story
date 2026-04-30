using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main 场景开始按钮入口。
/// 默认继续当前流程；如果没有存档，会从初始步骤开始。
/// </summary>
[DisallowMultipleComponent]
public class MainMenuStartGameButton : MonoBehaviour
{
    [SerializeField] private Button targetButton;
    [SerializeField] private GameFlowDefinition definitionOverride;
    [SerializeField] private bool bindOnAwake = true;
    [SerializeField] private bool forceReloadTargetScene = true;

    private void Awake()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        if (bindOnAwake && targetButton != null)
        {
            targetButton.onClick.RemoveListener(StartGame);
            targetButton.onClick.AddListener(StartGame);
        }
    }

    public void StartGame()
    {
        ConfigureFlowIfNeeded();
        GameFlowManager.Instance.StartGame(forceReloadTargetScene);
    }

    public void StartFromBeginning()
    {
        ConfigureFlowIfNeeded();
        GameFlowManager.Instance.ResetFlowToInitialStep(false, true);
        GameFlowManager.Instance.StartGame(forceReloadTargetScene);
    }

    private void ConfigureFlowIfNeeded()
    {
        if (definitionOverride != null)
        {
            GameFlowManager.Instance.SetDefinition(definitionOverride);
            return;
        }

        GameFlowManager.Instance.TryAutoLoadDefinition();
    }
}
