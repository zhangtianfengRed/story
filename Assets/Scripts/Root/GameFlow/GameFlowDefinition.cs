using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局主流程配置。
/// 每一步定义“加载哪个场景”以及“该场景里激活哪个内容 key”。
/// </summary>
[CreateAssetMenu(fileName = "GameFlowDefinition", menuName = "Story/Game Flow Definition")]
public class GameFlowDefinition : ScriptableObject
{
    [Serializable]
    public class Step
    {
        [Tooltip("全局唯一的步骤 ID，例如 Intro_Mirror。")]
        public string stepId;

        [Tooltip("进入该步骤时要加载的场景名，必须和 Unity 场景名一致。")]
        public string sceneName;

        [Tooltip("场景内的内容 key。GameFlowSceneController 会用它决定该显示哪组内容。")]
        public string contentKey = "Default";

        [Tooltip("当前步骤完成后要前往的下一步 ID。留空表示流程停在这里，等待手动跳转。")]
        public string nextStepId;

        [TextArea]
        [Tooltip("仅用于备注，运行时不会使用。")]
        public string notes;
    }

    [Header("默认入口")]
    [SerializeField] private string initialStepId;

    [Header("流程步骤")]
    [SerializeField] private List<Step> steps = new List<Step>();

    public IReadOnlyList<Step> Steps => steps;

    public string InitialStepId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(initialStepId))
            {
                return initialStepId;
            }

            return steps.Count > 0 ? steps[0].stepId : string.Empty;
        }
    }

    public bool TryGetStep(string stepId, out Step step)
    {
        step = null;
        if (string.IsNullOrWhiteSpace(stepId))
        {
            return false;
        }

        for (int i = 0; i < steps.Count; i++)
        {
            Step candidate = steps[i];
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(candidate.stepId, stepId, StringComparison.Ordinal))
            {
                step = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetInitialStep(out Step step)
    {
        if (!string.IsNullOrWhiteSpace(InitialStepId) && TryGetStep(InitialStepId, out step))
        {
            return true;
        }

        step = null;
        return false;
    }
}
