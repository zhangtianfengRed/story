using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 记录房间互动玩法的完成次数，供其它互动作为前置条件使用。
/// 默认按当前 GameFlow stepId 作为作用域；如果当前没有有效 stepId，则退回到当前场景名。
/// </summary>
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
    [Serializable]
    private class ScopeSaveData
    {
        public string scopeId;
        public List<string> progressIds = new List<string>();
        public List<int> completionCounts = new List<int>();
        public RoomInteractionResumeState resumeState;
    }

    [Serializable]
    private class SaveData
    {
        public List<ScopeSaveData> scopes = new List<ScopeSaveData>();
    }

    private sealed class ScopeRuntimeData
    {
        public readonly Dictionary<string, int> completionCounts = new Dictionary<string, int>(StringComparer.Ordinal);
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
        return GetCompletionCountForScope(ResolveCurrentScopeId(), progressId);
    }

    public int GetCompletionCountForStep(string stepId, string progressId)
    {
        return GetCompletionCountForScope(BuildStepScopeId(stepId), progressId);
    }

    public int GetCompletionCountForScope(string scopeId, string progressId)
    {
        if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(progressId))
        {
            return 0;
        }

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, false);
        if (runtimeData == null)
        {
            return 0;
        }

        return runtimeData.completionCounts.TryGetValue(progressId, out int count) ? count : 0;
    }

    public bool IsCompleted(string progressId, int minimumCompletionCount = 1)
    {
        return GetCompletionCount(progressId) >= Mathf.Max(1, minimumCompletionCount);
    }

    public bool IsCompletedForStep(string stepId, string progressId, int minimumCompletionCount = 1)
    {
        return GetCompletionCountForStep(stepId, progressId) >= Mathf.Max(1, minimumCompletionCount);
    }

    public void MarkCompleted(string progressId, int increment = 1)
    {
        MarkCompletedForScope(ResolveCurrentScopeId(), progressId, increment);
    }

    public void MarkCompletedForStep(string stepId, string progressId, int increment = 1)
    {
        MarkCompletedForScope(BuildStepScopeId(stepId), progressId, increment);
    }

    public void MarkCompletedForScope(string scopeId, string progressId, int increment = 1)
    {
        if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(progressId))
        {
            return;
        }

        increment = Mathf.Max(1, increment);

        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, true);
        runtimeData.completionCounts[progressId] = GetCompletionCountForScope(scopeId, progressId) + increment;

        Debug.Log($"<color=green>[RoomInteractionProgress]</color> Scope={DescribeScope(scopeId)}, {progressId} -> {runtimeData.completionCounts[progressId]}");
        SaveProgress();
    }

    public void SetCompletionCount(string progressId, int count)
    {
        SetCompletionCountForScope(ResolveCurrentScopeId(), progressId, count);
    }

    public void SetCompletionCountForStep(string stepId, string progressId, int count)
    {
        SetCompletionCountForScope(BuildStepScopeId(stepId), progressId, count);
    }

    public void SetCompletionCountForScope(string scopeId, string progressId, int count)
    {
        if (string.IsNullOrWhiteSpace(scopeId) || string.IsNullOrWhiteSpace(progressId))
        {
            return;
        }

        count = Mathf.Max(0, count);
        ScopeRuntimeData runtimeData = GetScopeRuntimeData(scopeId, count > 0);

        if (runtimeData == null)
        {
            return;
        }

        if (count == 0)
        {
            runtimeData.completionCounts.Remove(progressId);
            CleanupEmptyScope(scopeId, runtimeData);
        }
        else
        {
            runtimeData.completionCounts[progressId] = count;
        }

        SaveProgress();
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
        if (runtimeData == null || !runtimeData.completionCounts.Remove(progressId))
        {
            return;
        }

        CleanupEmptyScope(scopeId, runtimeData);
        SaveProgress();
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
            bool hasProgress = runtimeData.completionCounts.Count > 0;
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

            foreach (KeyValuePair<string, int> progressPair in runtimeData.completionCounts)
            {
                scopeData.progressIds.Add(progressPair.Key);
                scopeData.completionCounts.Add(progressPair.Value);
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

                int count = j < scopeData.completionCounts.Count ? Mathf.Max(0, scopeData.completionCounts[j]) : 0;
                if (count > 0)
                {
                    runtimeData.completionCounts[progressId] = count;
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

    private void CleanupEmptyScope(string scopeId, ScopeRuntimeData runtimeData)
    {
        if (string.IsNullOrWhiteSpace(scopeId))
        {
            return;
        }

        bool hasAnyData =
            runtimeData != null &&
            (runtimeData.completionCounts.Count > 0 || runtimeData.resumeState.HasAnyData());

        if (!hasAnyData)
        {
            scopedData.Remove(scopeId);
        }
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
