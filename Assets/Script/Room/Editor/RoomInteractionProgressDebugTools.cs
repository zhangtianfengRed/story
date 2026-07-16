using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class RoomInteractionProgressDebugTools
{
    private const string ClearCurrentProgressMenuPath =
        "Tools/互动测试/清除当前 Scene 的互动进度";

    [MenuItem(ClearCurrentProgressMenuPath, false, 2100)]
    private static void ClearCurrentProgress()
    {
        RoomInteractionProgressManager progressManager = RoomInteractionProgressManager.Instance;
        Scene activeScene = SceneManager.GetActiveScene();
        string sceneName = activeScene.IsValid() ? activeScene.name : "(无有效 Scene)";
        string scopeDescription = progressManager.CurrentScopeDescription;
        List<string> progressIds = CollectCurrentSceneProgressIds(activeScene);

        if (progressIds.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "没有可清除的互动进度",
                $"当前 Scene“{sceneName}”中没有找到已配置进度 ID 的 RoomInteractable。",
                "确定");
            return;
        }

        string progressList = "• " + string.Join("\n• ", progressIds);

        bool confirmed = EditorUtility.DisplayDialog(
            "清除当前 Scene 的互动进度",
            $"当前 Scene：{sceneName}\n" +
            $"实际存档作用域：{scopeDescription}\n\n" +
            $"将只清除当前 Scene 中以下 {progressIds.Count} 个进度 ID 的打开和完成次数：\n" +
            $"{progressList}\n\n" +
            "此操作仅用于 Play Mode 流程测试。",
            "清除",
            "取消");

        if (!confirmed)
        {
            return;
        }

        for (int i = 0; i < progressIds.Count; i++)
        {
            progressManager.ClearProgress(progressIds[i]);
        }

        ResetCurrentSceneProgressEventTriggers(activeScene);

        RoomPlayerInteractor[] interactors = Object.FindObjectsOfType<RoomPlayerInteractor>(true);
        for (int i = 0; i < interactors.Length; i++)
        {
            if (interactors[i].gameObject.scene == activeScene)
            {
                interactors[i].RefreshTargets();
            }
        }

        Debug.Log(
            $"<color=orange>[RoomInteractionProgressDebug]</color> " +
            $"已清除当前 Scene 的 {progressIds.Count} 个互动进度 ID：" +
            $"Scene={sceneName}, Scope={scopeDescription}");
    }

    [MenuItem(ClearCurrentProgressMenuPath, true)]
    private static bool ValidateClearCurrentProgress()
    {
        return EditorApplication.isPlaying;
    }

    private static List<string> CollectCurrentSceneProgressIds(Scene activeScene)
    {
        HashSet<string> uniqueProgressIds = new HashSet<string>();
        RoomInteractable[] interactables = Object.FindObjectsOfType<RoomInteractable>(true);

        for (int i = 0; i < interactables.Length; i++)
        {
            RoomInteractable interactable = interactables[i];
            if (interactable == null || interactable.gameObject.scene != activeScene)
            {
                continue;
            }

            AddProgressId(uniqueProgressIds, interactable.progressId);

            if (interactable.enableUnlockSatisfiedEvent &&
                !string.IsNullOrWhiteSpace(interactable.unlockEventHandledProgressId))
            {
                AddProgressId(uniqueProgressIds, interactable.unlockEventHandledProgressId);
            }
            else if (interactable.enableUnlockSatisfiedEvent &&
                     !string.IsNullOrWhiteSpace(interactable.progressId))
            {
                AddProgressId(uniqueProgressIds, interactable.progressId + ".UnlockEventHandled");
            }
        }

        List<string> sortedProgressIds = new List<string>(uniqueProgressIds);
        sortedProgressIds.Sort(System.StringComparer.Ordinal);
        return sortedProgressIds;
    }

    private static void AddProgressId(HashSet<string> progressIds, string progressId)
    {
        if (!string.IsNullOrWhiteSpace(progressId))
        {
            progressIds.Add(progressId);
        }
    }

    private static void ResetCurrentSceneProgressEventTriggers(Scene activeScene)
    {
        RoomInteractionProgressEventTrigger[] triggers =
            Object.FindObjectsOfType<RoomInteractionProgressEventTrigger>(true);

        for (int i = 0; i < triggers.Length; i++)
        {
            RoomInteractionProgressEventTrigger trigger = triggers[i];
            if (trigger == null || trigger.gameObject.scene != activeScene || !trigger.enabled)
            {
                continue;
            }

            trigger.enabled = false;
            trigger.enabled = true;
        }
    }
}
