using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 当前 GameFlow stepId 匹配时触发场景事件。
/// 用于替代 GameFlowSceneController 中“进入某个步骤自动触发 Timeline/显隐/初始化”的轻量场景入口。
/// </summary>
[DisallowMultipleComponent]
public class RoomStepEventTrigger : MonoBehaviour
{
    [Serializable]
    public class StepEventEntry
    {
        [Tooltip("要匹配的 GameFlow stepId。留空表示匹配任何有效 step。")]
        public string stepId;

        [Tooltip("同一次场景加载/启用周期内只触发一次。")]
        public bool invokeOnceWhileEnabled = true;

        [Tooltip("当前 GameFlow stepId 匹配时触发。可用于进入阶段时播放 Timeline、显隐物体、打开 UI 或初始化场景。")]
        public UnityEvent onStepActive = new UnityEvent();

        [NonSerialized] public bool hasInvoked;
    }

    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool reapplyWhenSceneLoaded = true;
    [SerializeField] private StepEventEntry[] entries;

    private void OnEnable()
    {
        if (reapplyWhenSceneLoaded)
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }
    }

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyCurrentStep();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        ResetInvocationState();
    }

    [ContextMenu("Apply Current Step")]
    public void ApplyCurrentStep()
    {
        if (entries == null)
        {
            return;
        }

        string currentStepId = GameFlowManager.Instance.CurrentStepId;
        if (string.IsNullOrWhiteSpace(currentStepId) &&
            GameFlowManager.Instance.TryGetCurrentStep(out GameFlowDefinition.Step step) &&
            step != null)
        {
            currentStepId = step.stepId;
        }

        if (string.IsNullOrWhiteSpace(currentStepId))
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            StepEventEntry entry = entries[i];
            if (!CanInvoke(entry, currentStepId))
            {
                continue;
            }

            entry.hasInvoked = true;
            entry.onStepActive?.Invoke();
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (gameObject.scene == scene)
        {
            ApplyCurrentStep();
        }
    }

    private static bool CanInvoke(StepEventEntry entry, string currentStepId)
    {
        if (entry == null)
        {
            return false;
        }

        if (entry.invokeOnceWhileEnabled && entry.hasInvoked)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(entry.stepId) ||
               string.Equals(entry.stepId, currentStepId, StringComparison.Ordinal);
    }

    private void ResetInvocationState()
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            if (entries[i] != null)
            {
                entries[i].hasInvoked = false;
            }
        }
    }
}
