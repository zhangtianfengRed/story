using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 同一个互动进度 ID 下可分别记录打开次数和完成次数。
/// 默认按当前 GameFlow stepId 作为作用域；如果当前没有有效 stepId，则退回到当前场景名。
/// </summary>
public enum RoomInteractionProgressCountType
{
    Completion = 0,
    Open = 1
}

[Serializable]
public struct RoomInteractionResumeState
{
    public bool hasPosition;
    public Vector3 position;
    public bool hasRotation;
    public Vector3 eulerAngles;

    public bool HasAnyData()
    {
        return hasPosition || hasRotation;
    }
}

public class RoomInteractionProgressManager : MonoBehaviour
{
    public event Action<string, string, RoomInteractionProgressCountType, int, int> ProgressCountChanged;

    [Serializable]
    private class ScopeSaveData
    {
        public string scopeId;
        public List<string> progressIds = new List<string>();
        public List<int> completionCounts = new List<int>();
        public List<int> openCounts = new List<int>();
        public RoomInteractionResumeState resumeState;
    }

    [Serializable]
    private class SaveData
    {
        public List<ScopeSaveData> scopes = new List<ScopeSaveData>();
    }

    private sealed class ProgressRuntimeData
    {
        public int completionCount;
        public int openCount;

        public bool HasAnyData()
        {
            return completionCount > 0 || openCount > 0;
        }
    }

    private sealed class ScopeRuntimeData
    {
        public readonly Dictionary<string, ProgressRuntimeData> progressData =
            new Dictionary<string, ProgressRuntimeData>(StringComparer.Ordinal);

        public RoomInteractionResumeState resumeState;
    }

    private static RoomInteractionProgressManager _instance;

    private readonly Dictionary<string, ScopeRuntimeData> scopedData =
        new Dictionary<string, ScopeRuntimeData>(StringComparer.Ordinal);

    public static RoomInteractionProgressManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("RoomInteractionProgressManager");
                _instance = go.AddComponent<RoomInteractionProgressManager>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public static RoomInteractionProgressManager Current => _instance;

    public string CurrentScopeId => ResolveCurrentScopeId();
    public string CurrentScopeDescription => DescribeScope(ResolveCurrentScopeId());

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        LoadProgress();
    }

    public int GetCompletionCount(string progressId)
    {
        return GetProgressCount(progressId, RoomInteractionProgressCountType.Completion);
    }

    public int GetOpenCount(string progressId)
    {
        return GetProgressCount(progressId, RoomInteractionProgressCountType.Open);
    }

    public int GetProgressCount(string progressId, RoomInteractionProgressCountType countType)
    {
        return GetProgressCountForScope(ResolveCurrentScopeId(), progressId, countType);
    }

    public int GetCompletionCountForStep(string stepId, string progressId)
    {
        return GetProgressCountForScope(BuildStepScopeId(stepId), progressId, RoomInteractionProgressCountType.Completion);
    }

    public int GetOpenCountForStep(string stepId, string progressId)
    {
        return GetProgressCountForScope(BuildStepScopeId(stepId), progressId, RoomInteractionProgressCountType.Open);
    }

    public int GetCompletionCountForScope(string scopeId, string progressId)
    {
        return GetProgressCountForScope(scopeId, progressId, RoomInteractionProgressCountType.Completion);
    }

    public int GetOpenCountForScope(string scopeId, string progressId)
    {
        return GetProgressCountForScope(scopeId, progressId, RoomInteractionProgressCountType.Open);
    }

    public int GetProgressCountForScope(string scopeId, string progressId, RoomInteractionProgressCountType countType)
    {
        if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(progressId))
        {
            return 0;
        }

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, false);
        if (runtimeData == null || !runtimeData.progressData.TryGetValue(progressId, out ProgressRuntimeData progressData))
        {
            return 0;
        }

        return GetCount(progressData, countType);
    }

    public bool IsCompleted(string progressId, int minimumCompletionCount = 1)
    {
        return GetCompletionCount(progressId) >= Mathf.Max(1, minimumCompletionCount);
    }

    public bool IsCompletedForStep(string stepId, string progressId, int minimumCompletionCount = 1)
    {
        return GetCompletionCountForStep(stepId, progressId) >= Mathf.Max(1, minimumCompletionCount);
    }

    public void MarkOpened(string progressId, int increment = 1)
    {
        MarkProgress(progressId, RoomInteractionProgressCountType.Open, increment);
    }

    public void MarkCompleted(string progressId, int increment = 1)
    {
        MarkProgress(progressId, RoomInteractionProgressCountType.Completion, increment);
    }

    public void MarkProgress(string progressId, RoomInteractionProgressCountType countType, int increment = 1)
    {
        MarkProgressForScope(ResolveCurrentScopeId(), progressId, countType, increment);
    }

    public void MarkCompletedForStep(string stepId, string progressId, int increment = 1)
    {
        MarkProgressForScope(BuildStepScopeId(stepId), progressId, RoomInteractionProgressCountType.Completion, increment);
    }

    public void MarkOpenedForStep(string stepId, string progressId, int increment = 1)
    {
        MarkProgressForScope(BuildStepScopeId(stepId), progressId, RoomInteractionProgressCountType.Open, increment);
    }

    public void MarkCompletedForScope(string scopeId, string progressId, int increment = 1)
    {
        MarkProgressForScope(scopeId, progressId, RoomInteractionProgressCountType.Completion, increment);
    }

    public void MarkOpenedForScope(string scopeId, string progressId, int increment = 1)
    {
        MarkProgressForScope(scopeId, progressId, RoomInteractionProgressCountType.Open, increment);
    }

    public void MarkProgressForScope(string scopeId, string progressId, RoomInteractionProgressCountType countType, int increment = 1)
    {
        if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(progressId))
        {
            return;
        }

        increment = Mathf.Max(1, increment);

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, true);
        ProgressRuntimeData progressData = GetProgressRuntimeData(runtimeData, progressId, true);
        int previousCount = GetCount(progressData, countType);
        int nextCount = previousCount + increment;
        SetCount(progressData, countType, nextCount);

        Debug.Log($"<color=green>[RoomInteractionProgress]</color> Scope={DescribeScope(scopeId)}, {progressId}.{DescribeCountType(countType)} -> {nextCount}");
        SaveProgress();
        NotifyProgressChanged(scopeId, progressId, countType, previousCount, nextCount);
    }

    public void SetCompletionCount(string progressId, int count)
    {
        SetProgressCount(progressId, RoomInteractionProgressCountType.Completion, count);
    }

    public void SetOpenCount(string progressId, int count)
    {
        SetProgressCount(progressId, RoomInteractionProgressCountType.Open, count);
    }

    public void SetProgressCount(string progressId, RoomInteractionProgressCountType countType, int count)
    {
        SetProgressCountForScope(ResolveCurrentScopeId(), progressId, countType, count);
    }

    public void SetCompletionCountForStep(string stepId, string progressId, int count)
    {
        SetProgressCountForScope(BuildStepScopeId(stepId), progressId, RoomInteractionProgressCountType.Completion, count);
    }

    public void SetCompletionCountForScope(string scopeId, string progressId, int count)
    {
        SetProgressCountForScope(scopeId, progressId, RoomInteractionProgressCountType.Completion, count);
    }

    public void SetProgressCountForScope(string scopeId, string progressId, RoomInteractionProgressCountType countType, int count)
    {
        if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(progressId))
        {
            return;
        }

        count = Mathf.Max(0, count);
        int previousCount = GetProgressCountForScope(scopeId, progressId, countType);
        if (previousCount == count)
        {
            return;
        }

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, count > 0);
        if (runtimeData == null)
        {
            return;
        }

        ProgressRuntimeData progressData = GetProgressRuntimeData(runtimeData, progressId, count > 0);
        if (progressData == null)
        {
            return;
        }

        SetCount(progressData, countType, count);
        if (!progressData.HasAnyData())
        {
            runtimeData.progressData.Remove(progressId);
            CleanupEmptyScope(scopeId, runtimeData);
        }

        SaveProgress();
        NotifyProgressChanged(scopeId, progressId, countType, previousCount, count);
    }

    public void ClearProgress(string progressId)
    {
        ClearProgressForScope(ResolveCurrentScopeId(), progressId);
    }

    public void ClearProgressForStep(string stepId, string progressId)
    {
        ClearProgressForScope(BuildStepScopeId(stepId), progressId);
    }

    public void ClearProgressForScope(string scopeId, string progressId)
    {
        if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(progressId))
        {
            return;
        }

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, false);
        if (runtimeData == null || !runtimeData.progressData.TryGetValue(progressId, out ProgressRuntimeData progressData))
        {
            return;
        }

        int previousCompletionCount = progressData.completionCount;
        int previousOpenCount = progressData.openCount;

        runtimeData.progressData.Remove(progressId);
        CleanupEmptyScope(scopeId, runtimeData);
        SaveProgress();

        if (previousCompletionCount > 0)
        {
            NotifyProgressChanged(scopeId, progressId, RoomInteractionProgressCountType.Completion, previousCompletionCount, 0);
        }

        if (previousOpenCount > 0)
        {
            NotifyProgressChanged(scopeId, progressId, RoomInteractionProgressCountType.Open, previousOpenCount, 0);
        }
    }

    public bool TryGetResumeState(out RoomInteractionResumeState resumeState)
    {
        return TryGetResumeStateForScope(ResolveCurrentScopeId(), out resumeState);
    }

    public bool TryGetResumeStateForStep(string stepId, out RoomInteractionResumeState resumeState)
    {
        return TryGetResumeStateForScope(BuildStepScopeId(stepId), out resumeState);
    }

    public bool TryGetResumeStateForScope(string scopeId, out RoomInteractionResumeState resumeState)
    {
        resumeState = default;

        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return false;
        }

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, false);
        if (runtimeData == null || !runtimeData.resumeState.HasAnyData())
        {
            return false;
        }

        resumeState = runtimeData.resumeState;
        return true;
    }

    public void SetResumeState(RoomInteractionResumeState resumeState)
    {
        SetResumeStateForScope(ResolveCurrentScopeId(), resumeState);
    }

    public void SetResumeStateForStep(string stepId, RoomInteractionResumeState resumeState)
    {
        SetResumeStateForScope(BuildStepScopeId(stepId), resumeState);
    }

    public void SetResumeStateForScope(string scopeId, RoomInteractionResumeState resumeState)
    {
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return;
        }

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, true);
        runtimeData.resumeState = resumeState;
        CleanupEmptyScope(scopeId, runtimeData);
        SaveProgress();
    }

    public void SetResumeStateFromTransform(Transform sourceTransform, bool saveRotation = true)
    {
        if (sourceTransform == null)
        {
            return;
        }

        RoomInteractionResumeState resumeState = new RoomInteractionResumeState
        {
            hasPosition = true,
            position = sourceTransform.position,
            hasRotation = saveRotation,
            eulerAngles = sourceTransform.eulerAngles
        };

        SetResumeState(resumeState);
    }

    public void ClearResumeState()
    {
        ClearResumeStateForScope(ResolveCurrentScopeId());
    }

    public void ClearResumeStateForStep(string stepId)
    {
        ClearResumeStateForScope(BuildStepScopeId(stepId));
    }

    public void ClearResumeStateForScope(string scopeId)
    {
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return;
        }

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, false);
        if (runtimeData == null || !runtimeData.resumeState.HasAnyData())
        {
            return;
        }

        runtimeData.resumeState = default;
        CleanupEmptyScope(scopeId, runtimeData);
        SaveProgress();
    }

    [ContextMenu("Clear Current Interaction Progress")]
    public void ClearCurrentProgress()
    {
        string scopeId = ResolveCurrentScopeId();
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return;
        }

        if (!scopedData.Remove(scopeId))
        {
            return;
        }

        SaveProgress();
        Debug.Log($"<color=red>[RoomInteractionProgress]</color> 当前互动进度已清空: {DescribeScope(scopeId)}");
    }

    public void ClearCurrentSceneProgress()
    {
        ClearCurrentProgress();
    }

    [ContextMenu("Clear All Interaction Progress")]
    public void ClearAllProgress()
    {
        scopedData.Clear();
        LocalSaveStore.DeleteKey(LocalSaveStore.Keys.RoomInteractionProgress);
        Debug.Log("<color=red>[RoomInteractionProgress]</color> 所有步骤作用域的房间互动进度已清空");
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

    public void LoadSceneByName(string sceneName)
    {
        GameFlowManager.Instance.LoadSceneByName(sceneName);
    }

    private void SaveProgress()
    {
        if (scopedData.Count == 0)
        {
            LocalSaveStore.DeleteKey(LocalSaveStore.Keys.RoomInteractionProgress);
            return;
        }

        SaveData data = new SaveData();

        foreach (KeyValuePair<string, ScopeRuntimeData> scopePair in scopedData)
        {
            if (string.IsNullOrWhiteSpace(scopePair.Key) || scopePair.Value == null)
            {
                continue;
            }

            ScopeRuntimeData runtimeData = scopePair.Value;
            bool hasProgress = runtimeData.progressData.Count > 0;
            bool hasResumeState = runtimeData.resumeState.HasAnyData();
            if (!hasProgress && !hasResumeState)
            {
                continue;
            }

            ScopeSaveData scopeData = new ScopeSaveData
            {
                scopeId = scopePair.Key,
                resumeState = runtimeData.resumeState
            };

            foreach (KeyValuePair<string, ProgressRuntimeData> progressPair in runtimeData.progressData)
            {
                if (progressPair.Value == null || !progressPair.Value.HasAnyData())
                {
                    continue;
                }

                scopeData.progressIds.Add(progressPair.Key);
                scopeData.completionCounts.Add(Mathf.Max(0, progressPair.Value.completionCount));
                scopeData.openCounts.Add(Mathf.Max(0, progressPair.Value.openCount));
            }

            data.scopes.Add(scopeData);
        }

        if (data.scopes.Count == 0)
        {
            LocalSaveStore.DeleteKey(LocalSaveStore.Keys.RoomInteractionProgress);
            return;
        }

        LocalSaveStore.SaveJson(LocalSaveStore.Keys.RoomInteractionProgress, data);
    }

    private void NotifyProgressChanged(
        string scopeId,
        string progressId,
        RoomInteractionProgressCountType countType,
        int previousCount,
        int nextCount)
    {
        ProgressCountChanged?.Invoke(scopeId, progressId, countType, previousCount, nextCount);
    }

    private void LoadProgress()
    {
        scopedData.Clear();

        if (!LocalSaveStore.TryLoadJson(LocalSaveStore.Keys.RoomInteractionProgress, out SaveData data) || data == null || data.scopes == null)
        {
            return;
        }

        for (int i = 0; i < data.scopes.Count; i++)
        {
            ScopeSaveData scopeData = data.scopes[i];
            if (scopeData == null || string.IsNullOrWhiteSpace(scopeData.scopeId))
            {
                continue;
            }

            ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeData.scopeId, true);
            for (int j = 0; j < scopeData.progressIds.Count; j++)
            {
                string progressId = scopeData.progressIds[j];
                if (string.IsNullOrWhiteSpace(progressId))
                {
                    continue;
                }

                int completionCount = j < scopeData.completionCounts.Count ? Mathf.Max(0, scopeData.completionCounts[j]) : 0;
                int openCount = j < scopeData.openCounts.Count ? Mathf.Max(0, scopeData.openCounts[j]) : 0;

                if (completionCount > 0 || openCount > 0)
                {
                    ProgressRuntimeData progressData = GetProgressRuntimeData(runtimeData, progressId, true);
                    progressData.completionCount = completionCount;
                    progressData.openCount = openCount;
                }
            }

            runtimeData.resumeState = scopeData.resumeState;
            CleanupEmptyScope(scopeData.scopeId, runtimeData);
        }
    }

    private ScopeRuntimeData GetScopeRuntimeData(string scopeId, bool createIfMissing)
    {
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return null;
        }

        if (scopedData.TryGetValue(scopeId, out ScopeRuntimeData runtimeData))
        {
            return runtimeData;
        }

        if (!createIfMissing)
        {
            return null;
        }

        runtimeData = new ScopeRuntimeData();
        scopedData[scopeId] = runtimeData;
        return runtimeData;
    }

    private static ProgressRuntimeData GetProgressRuntimeData(ScopeRuntimeData runtimeData, string progressId, bool createIfMissing)
    {
        if (runtimeData == null || string.IsNullOrWhiteSpace(progressId))
        {
            return null;
        }

        if (runtimeData.progressData.TryGetValue(progressId, out ProgressRuntimeData progressData))
        {
            return progressData;
        }

        if (!createIfMissing)
        {
            return null;
        }

        progressData = new ProgressRuntimeData();
        runtimeData.progressData[progressId] = progressData;
        return progressData;
    }

    private void CleanupEmptyScope(string scopeId, ScopeRuntimeData runtimeData)
    {
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return;
        }

        bool hasAnyData =
            runtimeData != null &&
            (runtimeData.progressData.Count > 0 || runtimeData.resumeState.HasAnyData());

        if (!hasAnyData)
        {
            scopedData.Remove(scopeId);
        }
    }

    private static int GetCount(ProgressRuntimeData progressData, RoomInteractionProgressCountType countType)
    {
        if (progressData == null)
        {
            return 0;
        }

        return countType == RoomInteractionProgressCountType.Open
            ? progressData.openCount
            : progressData.completionCount;
    }

    private static void SetCount(ProgressRuntimeData progressData, RoomInteractionProgressCountType countType, int count)
    {
        if (progressData == null)
        {
            return;
        }

        if (countType == RoomInteractionProgressCountType.Open)
        {
            progressData.openCount = Mathf.Max(0, count);
        }
        else
        {
            progressData.completionCount = Mathf.Max(0, count);
        }
    }

    private static string DescribeCountType(RoomInteractionProgressCountType countType)
    {
        return countType == RoomInteractionProgressCountType.Open ? "OpenCount" : "CompletionCount";
    }

    private static string ResolveCurrentScopeId()
    {
        GameFlowManager flowManager = GameFlowManager.Instance;
        if (flowManager != null)
        {
            if (flowManager.TryGetCurrentStep(out GameFlowDefinition.Step step) &&
                step != null &&
                !string.IsNullOrWhiteSpace(step.stepId))
            {
                return BuildStepScopeId(step.stepId);
            }

            if (!string.IsNullOrWhiteSpace(flowManager.CurrentStepId))
            {
                return BuildStepScopeId(flowManager.CurrentStepId);
            }
        }

        return BuildSceneFallbackScopeId(GetCurrentSceneName());
    }

    private static string BuildStepScopeId(string stepId)
    {
        return string.IsNullOrWhiteSpace(stepId) ? string.Empty : $"step:{stepId}";
    }

    private static string BuildSceneFallbackScopeId(string sceneName)
    {
        return string.IsNullOrWhiteSpace(sceneName) ? string.Empty : $"scene:{sceneName}";
    }

    private static string DescribeScope(string scopeId)
    {
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return "(none)";
        }

        if (scopeId.StartsWith("step:", StringComparison.Ordinal))
        {
            return $"Step={scopeId.Substring("step:".Length)}";
        }

        if (scopeId.StartsWith("scene:", StringComparison.Ordinal))
        {
            return $"SceneFallback={scopeId.Substring("scene:".Length)}";
        }

        return scopeId;
    }

    private static string GetCurrentSceneName()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.IsValid() ? scene.name : string.Empty;
    }
}
