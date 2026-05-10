using System;
using UnityEngine;

[Serializable]
public class RoomInteractionProgressRequirement
{
    [Tooltip("需要检查的互动进度 ID。默认在当前 GameFlow Step 作用域内检查。")]
    public string progressId;

    [Tooltip("判断这个 ID 下的哪一种次数。Open 是按 E 打开次数，Completion 是玩法完成次数。")]
    public RoomInteractionProgressCountType countType = RoomInteractionProgressCountType.Completion;

    [Min(1)]
    [Tooltip("该次数至少达到多少才算满足。")]
    public int minimumCompletionCount = 1;

    public bool IsSatisfied()
    {
        if (string.IsNullOrWhiteSpace(progressId))
        {
            return true;
        }

        return RoomInteractionProgressManager.Instance.GetProgressCount(progressId, countType) >= Mathf.Max(1, minimumCompletionCount);
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(progressId);
    }
}

[Serializable]
public class RoomSceneCompletionRequirement
{
    [Tooltip("需要检查的场景名。")]
    public string sceneName;

    [Min(1)]
    [Tooltip("该场景至少通关多少次才算满足。")]
    public int minimumCompletionCount = 1;

    public bool IsSatisfied()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return true;
        }

        return GameProgressManager.Instance.GetSceneCompletionCount(sceneName) >= Mathf.Max(1, minimumCompletionCount);
    }

    public bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(sceneName);
    }
}

[Serializable]
public class RoomInteractionUnlockConditions
{
    [Header("互动完成条件")]
    [Tooltip("这些互动进度都满足后，主互动才会解锁。默认都在当前 GameFlow Step 作用域内检查。")]
    public RoomInteractionProgressRequirement[] requiredInteractionProgresses;

    [Header("场景通关条件")]
    [Min(0)]
    [Tooltip("当前场景至少通关多少次。0 表示不检查。")]
    public int minimumCurrentSceneCompletionCount;

    [Tooltip("指定场景的通关次数条件，全部满足后才算通过。")]
    public RoomSceneCompletionRequirement[] requiredSceneCompletions;

    public bool AreSatisfied()
    {
        if (minimumCurrentSceneCompletionCount > 0 &&
            GameProgressManager.Instance.GetCurrentSceneCompletionCount() < minimumCurrentSceneCompletionCount)
        {
            return false;
        }

        if (!AreProgressRequirementsSatisfied(requiredInteractionProgresses))
        {
            return false;
        }

        if (!AreSceneRequirementsSatisfied(requiredSceneCompletions))
        {
            return false;
        }

        return true;
    }

    private static bool AreProgressRequirementsSatisfied(RoomInteractionProgressRequirement[] requirements)
    {
        if (requirements == null)
        {
            return true;
        }

        for (int i = 0; i < requirements.Length; i++)
        {
            RoomInteractionProgressRequirement requirement = requirements[i];
            if (requirement != null && !requirement.IsSatisfied())
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreSceneRequirementsSatisfied(RoomSceneCompletionRequirement[] requirements)
    {
        if (requirements == null)
        {
            return true;
        }

        for (int i = 0; i < requirements.Length; i++)
        {
            RoomSceneCompletionRequirement requirement = requirements[i];
            if (requirement != null && !requirement.IsSatisfied())
            {
                return false;
            }
        }

        return true;
    }
}
