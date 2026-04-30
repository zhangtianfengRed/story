using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 全局主流程管理器。
/// 负责保存当前步骤、按步骤加载场景，以及通知场景内控制器切换内容。
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    [Serializable]
    private class SaveData
    {
        public string currentStepId;
        public List<string> completedStepIds = new List<string>();
    }

    private const string DefaultResourcesPath = "GameFlow/GameFlowDefinition";

    private static GameFlowManager _instance;

    private readonly HashSet<string> completedStepIds = new HashSet<string>();
    private GameFlowDefinition definition;
    private string currentStepId;

    public static GameFlowManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("GameFlowManager");
                _instance = go.AddComponent<GameFlowManager>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    public GameFlowDefinition Definition => definition;
    public string CurrentStepId => currentStepId;
    public string CurrentSceneName => TryGetCurrentStep(out GameFlowDefinition.Step step) ? step.sceneName : string.Empty;
    public string CurrentContentKey => TryGetCurrentStep(out GameFlowDefinition.Step step) ? step.contentKey : string.Empty;

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
        TryAutoLoadDefinition();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void SetDefinition(GameFlowDefinition flowDefinition, bool resetToInitialStepIfInvalid = true)
    {
        definition = flowDefinition;

        if (definition == null)
        {
            Debug.LogWarning($"[{nameof(GameFlowManager)}] GameFlowDefinition is null.");
            return;
        }

        EnsureCurrentStepIsValid(resetToInitialStepIfInvalid);
    }

    public bool TryAutoLoadDefinition()
    {
        if (definition != null)
        {
            return true;
        }

        GameFlowDefinition loadedDefinition = Resources.Load<GameFlowDefinition>(DefaultResourcesPath);
        if (loadedDefinition == null)
        {
            return false;
        }

        SetDefinition(loadedDefinition);
        return true;
    }

    public bool TryGetCurrentStep(out GameFlowDefinition.Step step)
    {
        step = null;

        if (!EnsureCurrentStepIsValid(true))
        {
            return false;
        }

        return definition != null && definition.TryGetStep(currentStepId, out step);
    }

    public bool IsStepCompleted(string stepId)
    {
        return !string.IsNullOrWhiteSpace(stepId) && completedStepIds.Contains(stepId);
    }

    public void StartGame(bool forceReloadCurrentScene = true)
    {
        LoadCurrentStep(forceReloadCurrentScene);
    }

    public void LoadCurrentStep(bool forceReloadCurrentScene = false)
    {
        if (!TryGetCurrentStep(out GameFlowDefinition.Step step))
        {
            Debug.LogWarning($"[{nameof(GameFlowManager)}] Cannot load current step because no valid step is configured.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!forceReloadCurrentScene && activeScene.IsValid() && activeScene.name == step.sceneName)
        {
            ApplyCurrentStepToScene(activeScene);
            return;
        }

        RequestSceneLoad(step.sceneName);
    }

    public void JumpToStep(string stepId, bool loadScene = true, bool forceReloadCurrentScene = false)
    {
        if (definition == null)
        {
            if (!TryAutoLoadDefinition())
            {
                Debug.LogWarning($"[{nameof(GameFlowManager)}] Cannot jump to step '{stepId}' because no GameFlowDefinition is configured.");
                return;
            }
        }

        if (!definition.TryGetStep(stepId, out _))
        {
            Debug.LogWarning($"[{nameof(GameFlowManager)}] Step '{stepId}' does not exist in the current GameFlowDefinition.");
            return;
        }

        currentStepId = stepId;
        SaveProgress();

        if (loadScene)
        {
            LoadCurrentStep(forceReloadCurrentScene);
        }
    }

    public void CompleteCurrentStep()
    {
        if (!string.IsNullOrWhiteSpace(currentStepId))
        {
            completedStepIds.Add(currentStepId);
            SaveProgress();
        }
    }

    public void CompleteCurrentStepAndLoadNext(bool forceReloadCurrentScene = false)
    {
        if (!TryGetCurrentStep(out GameFlowDefinition.Step step))
        {
            return;
        }

        CompleteCurrentStep();

        if (string.IsNullOrWhiteSpace(step.nextStepId))
        {
            Debug.Log($"[{nameof(GameFlowManager)}] Step '{step.stepId}' has no next step. Flow stays on the current step.");
            return;
        }

        JumpToStep(step.nextStepId, true, forceReloadCurrentScene);
    }

    public void LoadNextStep(bool forceReloadCurrentScene = false)
    {
        if (!TryGetCurrentStep(out GameFlowDefinition.Step step))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(step.nextStepId))
        {
            Debug.Log($"[{nameof(GameFlowManager)}] Step '{step.stepId}' has no next step.");
            return;
        }

        JumpToStep(step.nextStepId, true, forceReloadCurrentScene);
    }

    public void ResetFlowToInitialStep(bool loadScene = false, bool clearCompletedSteps = true)
    {
        if (definition == null)
        {
            if (!TryAutoLoadDefinition())
            {
                Debug.LogWarning($"[{nameof(GameFlowManager)}] Cannot reset flow because no GameFlowDefinition is configured.");
                return;
            }
        }

        if (!definition.TryGetInitialStep(out GameFlowDefinition.Step step))
        {
            Debug.LogWarning($"[{nameof(GameFlowManager)}] Cannot reset flow because the GameFlowDefinition has no valid initial step.");
            return;
        }

        currentStepId = step.stepId;
        if (clearCompletedSteps)
        {
            completedStepIds.Clear();
        }

        SaveProgress();

        if (loadScene)
        {
            LoadCurrentStep(true);
        }
    }

    public void LoadSceneByName(string sceneName)
    {
        RequestSceneLoad(sceneName);
    }

    private bool EnsureCurrentStepIsValid(bool resetToInitialStepIfInvalid)
    {
        if (definition == null && !TryAutoLoadDefinition())
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(currentStepId) && definition.TryGetStep(currentStepId, out _))
        {
            return true;
        }

        if (!resetToInitialStepIfInvalid || !definition.TryGetInitialStep(out GameFlowDefinition.Step initialStep))
        {
            return false;
        }

        currentStepId = initialStep.stepId;
        SaveProgress();
        return true;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyCurrentStepToScene(scene);
    }

    private void RequestSceneLoad(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"[{nameof(GameFlowManager)}] Scene name is empty.");
            return;
        }

        SceneTransitionController.Instance.LoadScene(sceneName);
    }

    private void ApplyCurrentStepToScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return;
        }

        GameFlowSceneController[] controllers = FindObjectsOfType<GameFlowSceneController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            GameFlowSceneController controller = controllers[i];
            if (controller == null || controller.gameObject.scene != scene)
            {
                continue;
            }

            controller.ApplyCurrentStep();
        }
    }

    private void SaveProgress()
    {
        SaveData data = new SaveData
        {
            currentStepId = currentStepId,
            completedStepIds = new List<string>(completedStepIds)
        };

        LocalSaveStore.SaveJson(LocalSaveStore.Keys.GameFlowProgress, data);
    }

    private void LoadProgress()
    {
        completedStepIds.Clear();

        if (!LocalSaveStore.TryLoadJson(LocalSaveStore.Keys.GameFlowProgress, out SaveData data) || data == null)
        {
            return;
        }

        currentStepId = data.currentStepId;
        if (data.completedStepIds == null)
        {
            return;
        }

        for (int i = 0; i < data.completedStepIds.Count; i++)
        {
            string stepId = data.completedStepIds[i];
            if (!string.IsNullOrWhiteSpace(stepId))
            {
                completedStepIds.Add(stepId);
            }
        }
    }
}
