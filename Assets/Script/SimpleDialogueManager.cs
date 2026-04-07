using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Febucci.UI.Core;
using TMPro;

/// <summary>
/// 对话流程管理器。
/// 处理：NPC 说话 -> 说话结束 -> 显示选项 -> 悬停选项出现杂念。
/// </summary>
public class SimpleDialogueManager : MonoBehaviour
{
    [Header("UI 引用")]
    public TypewriterCore npcTypewriter; // NPC 的打字机组件
    public GameObject choicesContainer; // 包含选项按钮的父物体
    public Button choiceButtonA;
    public Button choiceButtonB;
    
    [Header("侧边杂念引用")]
    public WorryTextGroup worryGroup;

    [Header("测试数据 (可选)")]
    [TextArea] public string npcOpeningLine = "你真的决定要这么做吗？";
    public ChoiceData choiceA;
    public ChoiceData choiceB;

    [System.Serializable]
    public struct ChoiceData
    {
        public string buttonText;
        [TextArea(2, 4)] public List<string> worries;
    }

    private void Start()
    {
        // 初始状态
        if (choicesContainer != null) choicesContainer.SetActive(false);
        
        // 绑定打字机结束事件
        if (npcTypewriter != null)
        {
            npcTypewriter.SetTypewriterSpeed(GameSettingsManager.GetDialogueSpeed());
            npcTypewriter.onTextShowed.AddListener(OnNPCDone);
            
            // 开始测试对话
            StartCoroutine(StartDialogueRoutine());
        }
    }

    private System.Collections.IEnumerator StartDialogueRoutine()
    {
        yield return new WaitForSeconds(1f);
        npcTypewriter.ShowText(npcOpeningLine);
    }

    private void OnNPCDone()
    {
        // NPC 说完后，显示选项
        if (choicesContainer != null)
        {
            choicesContainer.SetActive(true);
            SetupButton(choiceButtonA, choiceA);
            SetupButton(choiceButtonB, choiceB);
            
            // 可以加个淡入效果
            var cg = choicesContainer.GetComponent<CanvasGroup>();
            if (cg != null) StartCoroutine(FadeInChoices(cg));
        }
    }

    private void SetupButton(Button btn, ChoiceData data)
    {
        if (btn == null) return;
        
        // 设置按钮文字
        var tmp = btn.GetComponentInChildren<TMP_Text>();
        if (tmp != null) tmp.text = data.buttonText;

        // 设置悬停触发的文字
        var trigger = btn.GetComponent<ChoiceWorryTrigger>();
        if (trigger == null) trigger = btn.gameObject.AddComponent<ChoiceWorryTrigger>();
        
        trigger.targetWorryGroup = worryGroup;
        trigger.customWorryText = data.worries;
    }

    private System.Collections.IEnumerator FadeInChoices(CanvasGroup cg)
    {
        cg.alpha = 0;
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.deltaTime;
            cg.alpha = elapsed / 0.5f;
            yield return null;
        }
        cg.alpha = 1;
    }
}
