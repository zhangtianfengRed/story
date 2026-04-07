using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 游戏进度管理器，用于记录玩家在各个场景的通过次数。
/// 使用统一本地存储入口进行持久化。
/// </summary>
public class GameProgressManager : MonoBehaviour
{
    private static GameProgressManager _instance;
    public static GameProgressManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("GameProgressManager");
                _instance = go.AddComponent<GameProgressManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [System.Serializable]
    private class SceneProgressData
    {
        public List<string> sceneNames = new List<string>();
        public List<int> completionCounts = new List<int>();
    }

    private Dictionary<string, int> _sceneCompletionCounts = new Dictionary<string, int>();
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

    /// <summary>
    /// 获取当前场景的通关次数
    /// </summary>
    public int GetCurrentSceneCompletionCount()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return GetSceneCompletionCount(sceneName);
    }

    /// <summary>
    /// 获取指定场景的通关次数
    /// </summary>
    public int GetSceneCompletionCount(string sceneName)
    {
        if (_sceneCompletionCounts.TryGetValue(sceneName, out int count))
        {
            return count;
        }
        return 0;
    }

    /// <summary>
    /// 标记当前场景已通关，并增加计数
    /// </summary>
    public void MarkCurrentSceneCompleted()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        MarkSceneCompleted(sceneName);
    }

    /// <summary>
    /// 标记指定场景已通关，并增加计数
    /// </summary>
    public void MarkSceneCompleted(string sceneName)
    {
        if (_sceneCompletionCounts.ContainsKey(sceneName))
        {
            _sceneCompletionCounts[sceneName]++;
        }
        else
        {
            _sceneCompletionCounts[sceneName] = 1;
        }
        
        Debug.Log($"<color=green>[Progress]</color> 场景 {sceneName} 通关次数记录: {_sceneCompletionCounts[sceneName]}");
        SaveProgress();
    }

    /// <summary>
    /// 检查玩家是否是第一次进入该场景（针对特殊剧情展示）
    /// </summary>
    public bool IsFirstTimeInCurrentScene()
    {
        return GetCurrentSceneCompletionCount() == 0;
    }

    private void SaveProgress()
    {
        SceneProgressData data = new SceneProgressData();
        foreach (var kvp in _sceneCompletionCounts)
        {
            data.sceneNames.Add(kvp.Key);
            data.completionCounts.Add(kvp.Value);
        }

        LocalSaveStore.SaveJson(LocalSaveStore.Keys.SceneProgress, data);
    }

    private void LoadProgress()
    {
        if (LocalSaveStore.TryLoadJson(LocalSaveStore.Keys.SceneProgress, out SceneProgressData data))
        {
            _sceneCompletionCounts.Clear();
            for (int i = 0; i < data.sceneNames.Count; i++)
            {
                if (i < data.completionCounts.Count)
                {
                    _sceneCompletionCounts[data.sceneNames[i]] = data.completionCounts[i];
                }
            }
        }
    }

    [ContextMenu("Clear All Progress")]
    public void ClearProgress()
    {
        _sceneCompletionCounts.Clear();
        LocalSaveStore.DeleteKey(LocalSaveStore.Keys.SceneProgress);
        Debug.Log("<color=red>[Progress]</color> 所有通关数据已重置");
    }
}
