using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景内内容切换控制器。
/// 根据当前流程步骤的 contentKey，决定激活哪组对象、是否播放 Timeline、以及是否自动推进。
/// </summary>
[DisallowMultipleComponent]
public class GameFlowSceneController : MonoBehaviour
{
    [Serializable]
    public class ContentEntry
    {
        [Tooltip("和 GameFlowDefinition.Step.contentKey 对应。")]
        public string contentKey = "Default";

        [Tooltip("进入该内容时会被激活的对象。")]
        public GameObject[] activeRoots;

        [Tooltip("进入该内容时会被额外关闭的对象。")]
        public GameObject[] inactiveRoots;

        [Tooltip("进入该内容时触发，可用于显示 UI、启用组件、播放额外逻辑。")]
        public UnityEvent onEnter;

        [Tooltip("Timeline 播放结束时的回调。可以在这里直接绑定加载下一个场景、激活事件或自定义逻辑。")]
        public UnityEvent onDirectorStopped;

        [Header("Timeline")]
        public PlayableDirector director;
        public bool playDirectorOnEnter;
        public bool completeCurrentStepWhenDirectorStops;

        [Tooltip("如果想跳转到非默认 nextStepId，可以在这里覆盖。")]
        public string nextStepIdOverride;
    }

    [Header("匹配设置")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool onlyApplyWhenCurrentSceneMatchesStepScene = true;
    [SerializeField] private bool useFallbackContentKeyWhenFlowUnavailable = true;
    [SerializeField] private string fallbackContentKey = "Default";

    [Header("内容表")]
    [SerializeField] private List<ContentEntry> contents = new List<ContentEntry>();

    private ContentEntry activeContent;
    private PlayableDirector subscribedDirector;
    private bool pendingDirectorAutoAdvance;
    private string lastAppliedContentKey;

    private void Start()
    {
        if (applyOnStart)
        {
            ApplyCurrentStep();
        }
    }

    private void OnDisable()
    {
        UnsubscribeDirector();
    }

    [ContextMenu("Apply Current Step")]
    public void ApplyCurrentStep()
    {
        string contentKey = ResolveContentKey();
        if (string.IsNullOrWhiteSpace(contentKey))
        {
            return;
        }

        if (activeContent != null && string.Equals(lastAppliedContentKey, contentKey, StringComparison.Ordinal))
        {
            return;
        }

        ContentEntry entry = FindContent(contentKey);
        if (entry == null)
        {
            Debug.LogWarning($"[{nameof(GameFlowSceneController)}] Content key '{contentKey}' was not found on {name}.", this);
            return;
        }

        ApplyContent(entry);
    }

    [ContextMenu("Reapply Current Step")]
    public void ReapplyCurrentStep()
    {
        lastAppliedContentKey = null;
        ApplyCurrentStep();
    }

    public void CompleteCurrentStepAndLoadNext()
    {
        GameFlowManager.Instance.CompleteCurrentStepAndLoadNext();
    }

    public void CompleteCurrentStep()
    {
        GameFlowManager.Instance.CompleteCurrentStep();
    }

    public void LoadNextStep()
    {
        GameFlowManager.Instance.LoadNextStep();
    }

    public void JumpToStep(string stepId)
    {
        GameFlowManager.Instance.JumpToStep(stepId);
    }

    private string ResolveContentKey()
    {
        if (GameFlowManager.Instance.TryGetCurrentStep(out GameFlowDefinition.Step step))
        {
            Scene activeScene = gameObject.scene;
            if (!onlyApplyWhenCurrentSceneMatchesStepScene || string.Equals(activeScene.name, step.sceneName, StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(step.contentKey) ? fallbackContentKey : step.contentKey;
            }

            return null;
        }

        return useFallbackContentKeyWhenFlowUnavailable ? fallbackContentKey : null;
    }

    private ContentEntry FindContent(string contentKey)
    {
        for (int i = 0; i < contents.Count; i++)
        {
            ContentEntry entry = contents[i];
            if (entry == null)
            {
                continue;
            }

            if (string.Equals(entry.contentKey, contentKey, StringComparison.Ordinal))
            {
                return entry;
            }
        }

        return null;
    }

    private void ApplyContent(ContentEntry entry)
    {
        activeContent = entry;
        lastAppliedContentKey = entry.contentKey;
        pendingDirectorAutoAdvance = entry.completeCurrentStepWhenDirectorStops;

        UnsubscribeDirector();
        DeactivateManagedRoots();

        SetRootsActive(entry.activeRoots, true);
        SetRootsActive(entry.inactiveRoots, false);

        entry.onEnter?.Invoke();

        if (entry.director != null)
        {
            subscribedDirector = entry.director;
            subscribedDirector.stopped += HandleDirectorStopped;
        }

        if (entry.director != null && entry.playDirectorOnEnter)
        {
            entry.director.time = 0d;
            entry.director.Evaluate();
            entry.director.Play();
        }
    }

    private void HandleDirectorStopped(PlayableDirector stoppedDirector)
    {
        if (activeContent == null || stoppedDirector == null || stoppedDirector != subscribedDirector)
        {
            return;
        }

        if (!pendingDirectorAutoAdvance || !activeContent.completeCurrentStepWhenDirectorStops)
        {
            activeContent.onDirectorStopped?.Invoke();
            return;
        }

        pendingDirectorAutoAdvance = false;
        activeContent.onDirectorStopped?.Invoke();

        if (!activeContent.completeCurrentStepWhenDirectorStops)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(activeContent.nextStepIdOverride))
        {
            GameFlowManager.Instance.CompleteCurrentStep();
            GameFlowManager.Instance.JumpToStep(activeContent.nextStepIdOverride);
            return;
        }

        GameFlowManager.Instance.CompleteCurrentStepAndLoadNext();
    }

    private void UnsubscribeDirector()
    {
        if (subscribedDirector != null)
        {
            subscribedDirector.stopped -= HandleDirectorStopped;
            subscribedDirector = null;
        }
    }

    private void DeactivateManagedRoots()
    {
        HashSet<GameObject> uniqueRoots = new HashSet<GameObject>();

        for (int i = 0; i < contents.Count; i++)
        {
            ContentEntry entry = contents[i];
            if (entry == null)
            {
                continue;
            }

            CollectRoots(uniqueRoots, entry.activeRoots);
            CollectRoots(uniqueRoots, entry.inactiveRoots);
        }

        foreach (GameObject root in uniqueRoots)
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }
    }

    private static void CollectRoots(HashSet<GameObject> collector, GameObject[] roots)
    {
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
            {
                collector.Add(roots[i]);
            }
        }
    }

    private static void SetRootsActive(GameObject[] roots, bool value)
    {
        if (roots == null)
        {
            return;
        }

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null)
            {
                roots[i].SetActive(value);
            }
        }
    }
}
