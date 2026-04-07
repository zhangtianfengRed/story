using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class DialogueChoice
{
    public string choiceText;            // 选项按钮上显示的文字
    [TextArea(2, 4)]
    public List<string> worryThoughts;   // 悬停时出现的“小蚊子”杂念
    public DialogueData nextDialogue;    // 点击后进入的下一个对话（可选，用于扩展剧情树）
}

/// <summary>
/// 对话片段资源。
/// 你可以在 Unity 中右键创建很多个这种资源，每个代表一轮对话。
/// </summary>
[CreateAssetMenu(fileName = "New Dialogue", menuName = "Dialogue/Dialogue Data")]
public class DialogueData : ScriptableObject
{
    [Header("NPC 内容")]
    [TextArea(3, 5)]
    public List<string> npcLines = new List<string>();

    [Header("玩家回复选项")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    [Header("结局配置")]
    public bool isEndingScene = false;   // 若勾选，则此对话结束后标记该 Unity 场景为“已通过”一次
}
