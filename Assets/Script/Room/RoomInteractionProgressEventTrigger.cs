using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 监听当前互动进度作用域里的某个 progressId，达成指定次数后触发事件。
/// 适合把 Timeline、显隐物体、流程跳转等表现逻辑挂回具体场景对象上。
/// </summary>
[DisallowMultipleComponent]
public class RoomInteractionProgressEventTrigger : MonoBehaviour
{
    [Serializable]
    public class ProgressEventEntry
    {
        [Tooltip("要监听的互动进度 ID。")]
        public string progressId;

        [Tooltip("监听这个 ID 下的哪一种次数。Open 是按 E 打开次数，Completion 是玩法完成次数。")]
        public RoomInteractionProgressCountType countType = RoomInteractionProgressCountType.Completion;

        [Min(1)]
        [Tooltip("达到多少次后触发。")]
        public int minimumCompletionCount = 1;

        [Tooltip("启用组件时，如果进度已经满足，是否立刻触发一次。")]
        public bool invokeIfAlreadySatisfiedOnEnable;

        [Tooltip("同一次启用周期内只触发一次。")]
        public bool invokeOnceWhileEnabled = true;

        [Tooltip("当前作用域内 progressId 达到指定次数后触发。可用于播放 Timeline、显隐物体、打开 UI 或推进剧情。")]
        public UnityEvent onSatisfied = new UnityEvent();

        [NonSerialized] public bool hasInvoked;
    }

    [SerializeField] private ProgressEventEntry[] entries;

    private void OnEnable()
    {
        RoomInteractionProgressManager.Instance.ProgressCountChanged += HandleProgressChanged;
        InvokeAlreadySatisfiedEntries();
    }

    private void OnDisable()
    {
        if (RoomInteractionProgressManager.Current != null)
        {
            RoomInteractionProgressManager.Current.ProgressCountChanged -= HandleProgressChanged;
        }

        ResetInvocationState();
    }

    private void HandleProgressChanged(
        string scopeId,
        string progressId,
        RoomInteractionProgressCountType countType,
        int previousCount,
        int nextCount)
    {
        if (string.IsNullOrWhiteSpace(progressId) || entries == null)
        {
            return;
        }

        string currentScopeId = RoomInteractionProgressManager.Instance.CurrentScopeId;
        if (!string.Equals(scopeId, currentScopeId, StringComparison.Ordinal))
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            ProgressEventEntry entry = entries[i];
            if (!CanInvoke(entry, progressId, countType, nextCount))
            {
                continue;
            }

            InvokeEntry(entry);
        }
    }

    private void InvokeAlreadySatisfiedEntries()
    {
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            ProgressEventEntry entry = entries[i];
            if (entry == null || !entry.invokeIfAlreadySatisfiedOnEnable || string.IsNullOrWhiteSpace(entry.progressId))
            {
                continue;
            }

            int count = RoomInteractionProgressManager.Instance.GetProgressCount(entry.progressId, entry.countType);
            if (CanInvoke(entry, entry.progressId, entry.countType, count))
            {
                InvokeEntry(entry);
            }
        }
    }

    private static bool CanInvoke(
        ProgressEventEntry entry,
        string progressId,
        RoomInteractionProgressCountType countType,
        int count)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.progressId))
        {
            return false;
        }

        if (entry.invokeOnceWhileEnabled && entry.hasInvoked)
        {
            return false;
        }

        return entry.countType == countType &&
               string.Equals(entry.progressId, progressId, StringComparison.Ordinal) &&
               count >= Mathf.Max(1, entry.minimumCompletionCount);
    }

    private static void InvokeEntry(ProgressEventEntry entry)
    {
        entry.hasInvoked = true;
        entry.onSatisfied?.Invoke();
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
