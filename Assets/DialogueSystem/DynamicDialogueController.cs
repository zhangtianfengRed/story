using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Febucci.UI.Core;
using TMPro;
using UnityEngine.Events;

/// <summary>
/// 动态对话总控制器。通过资源文件（DialogueData）驱动整个流程。
/// 能够自动生成变长的选项按钮列表，并绑定对应的念波效果。
/// </summary>
public class DynamicDialogueController : MonoBehaviour
{
    [Header("配置")]
    public TypewriterCore npcTypewriter;    // NPC 的打字机组件
    public RectTransform choiceArea;        // 包含两个按钮的容器
    public ChoiceButton choiceButtonA;      // 场景中预设的第一个按钮
    public ChoiceButton choiceButtonB;      // 场景中预设的第二个按钮
    public WorryTextGroup worryGroupA;       // 对应第一个按钮的显示区域 (例如左侧)
    public WorryTextGroup worryGroupB;       // 对应第二个按钮的显示区域 (例如右侧)
    public TalkController talkController;   // 嘴巴动画控制器

    [Header("UI 渐显动画")]
    public CanvasGroup choiceGroupCG;
    public float fadeDuration = 0.5f;

    [Header("结局黑屏效果")]
    public CanvasGroup blackScreenGroup;    // 黑屏遮罩
    public TextMeshProUGUI blackScreenText; // 黑屏上显示的文字
    public TypewriterCore blackScreenTypewriter; // 黑屏文字的打字机 (如果是 Febucci UI)
    public float blackFadeDuration = 1.5f;
    public float endingWaitBeforeEvent = 3.0f; // 文字打完后的停留时间
    public UnityEvent OnEndingSequenceComplete; // 结局流程全部跑完后的事件回调

    [Header("当前对话内容")]
    public DialogueData currentDialogue;    // 拖入初始对话资源
    private int currentLineIndex = 0;       // 当前正在说的第几句
    private bool isTextFullyDisplayed = false; // 当前这一句是否打完

    [Header("流程设置")]
    public float autoAdvanceDelay = 1.0f;   // 自动切换下一句的延迟时间
    public float startDelay = 1.5f;         // 第一句话开始前的延迟时间

    [Header("心情表现")]
    public float joyLevelPerChoice = 0.15f; // 每次做出选择后增加的开心程度

    [Header("重复进入设置")]
    [Tooltip("针对不同通关次数的初始对话。索引 0 为通关 1 次后的对话，索引 1 为通关 2 次后的对话，以此类推。")]
    public List<DialogueData> repeatInitialDialogues = new List<DialogueData>();

    private void Start()
    {
        // 核心修复：初始状态下必须彻底关闭结局黑屏，防止挡住正常 UI
        if (blackScreenGroup != null)
        {
            blackScreenGroup.alpha = 0;
            blackScreenGroup.gameObject.SetActive(false);
        }

        if (choiceGroupCG != null) choiceGroupCG.alpha = 0;
        if (choiceArea != null) choiceArea.gameObject.SetActive(false);

        // 绑定打字机完结事件
        if (npcTypewriter != null)
        {
            npcTypewriter.onTextShowed.AddListener(HandleTextShowed);
        }

        // 延迟开始第一场对话
        if (currentDialogue != null)
        {
            Invoke(nameof(StartFirstDialogue), startDelay);
        }

        // 记录进入日志
        int count = GameProgressManager.Instance.GetCurrentSceneCompletionCount();
        if (count > 0)
        {
            Debug.Log($"<color=cyan>[Scene]</color> 玩家第 {count + 1} 次进入此场景。");
        }
    }

    private void Update()
    {
        // 如果正在对话中，监听鼠标左键或空格键，仅用于跳过打字动画
        if (currentDialogue != null && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)))
        {
            // 如果选项区已经显示了，就不处理点击
            if (choiceArea.gameObject.activeSelf) return;

            if (!isTextFullyDisplayed)
            {
                // 如果还在打字，点击则立即显示全句
                npcTypewriter.SkipTypewriter();
            }
        }
    }

    private void StartFirstDialogue()
    {
        int completionCount = GameProgressManager.Instance.GetCurrentSceneCompletionCount();

        // 如果有针对重复进入的特殊对话配置
        if (completionCount > 0 && repeatInitialDialogues != null && repeatInitialDialogues.Count > 0)
        {
            // 通过 Mathf.Min 确保如果超出数组长度，则循环使用最后一个故事
            int index = Mathf.Min(completionCount - 1, repeatInitialDialogues.Count - 1);
            if (repeatInitialDialogues[index] != null)
            {
                Debug.Log($"<color=green>[Progress]</color> 已通关 {completionCount} 次，触发特殊对话片段 (索引: {index})");
                StartDialogue(repeatInitialDialogues[index]);
                return;
            }
        }

        if (currentDialogue != null)
        {
            StartDialogue(currentDialogue);
        }
    }

    /// <summary>
    /// 开始一段新的对话片段。
    /// </summary>
    public void StartDialogue(DialogueData data)
    {
        if (data == null) return;

        currentDialogue = data;
        currentLineIndex = 0; // 重置行索引

        Debug.Log($"<color=white>[Dialogue Flow]</color> 开始新的对话片段: {data.name}");

        // 1. 强制清理黑屏和旧 UI
        if (blackScreenGroup != null)
        {
            blackScreenGroup.alpha = 0;
            blackScreenGroup.blocksRaycasts = false;
            blackScreenGroup.gameObject.SetActive(false);
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        if (choiceGroupCG != null)
        {
            choiceGroupCG.alpha = 0;
            choiceGroupCG.blocksRaycasts = false;
        }
        if (choiceArea != null) choiceArea.gameObject.SetActive(false);

        // 2. NPC 开始说话
        ShowCurrentLine();
    }

    private void ShowCurrentLine()
    {
        if (currentDialogue == null || currentDialogue.npcLines.Count == 0 || currentLineIndex >= currentDialogue.npcLines.Count) return;

        isTextFullyDisplayed = false;

        // 核心修复：确保打字机及其容器已显示
        if (npcTypewriter != null)
        {
            if (!npcTypewriter.gameObject.activeSelf)
                npcTypewriter.gameObject.SetActive(true);

            if (npcTypewriter.transform.parent != null && !npcTypewriter.transform.parent.gameObject.activeSelf)
                npcTypewriter.transform.parent.gameObject.SetActive(true);

            Debug.Log($"<color=cyan>[Dialogue]</color> 渲染文本: {currentDialogue.npcLines[currentLineIndex]}");
            npcTypewriter.ShowText(currentDialogue.npcLines[currentLineIndex]);
        }

        if (talkController != null)
        {
            talkController.StartSpeaking();
        }
    }

    private Coroutine fadeCoroutine;

    private void HandleTextShowed()
    {
        isTextFullyDisplayed = true;

        if (talkController != null)
        {
            Debug.Log("<color=yellow>[Dialogue]</color> 台词播完，停止口型动画");
            talkController.StopSpeaking();
        }

        // 还原：直接读取 npcLines 数量
        int totalLines = (currentDialogue != null) ? currentDialogue.npcLines.Count : 0;

        // 如果不是最后一句，则自动在延迟后切换
        if (currentLineIndex < totalLines - 1)
        {
            CancelInvoke(nameof(NextLine)); // 防止重复调用
            Invoke(nameof(NextLine), autoAdvanceDelay);
        }
        else
        {
            // 结局判定加固：
            // 1. 资源明确勾选了结局
            // 2. 或是正处于“重复进入”状态（通关次数 > 0）且对话已经完结
            int completionCount = GameProgressManager.Instance.GetCurrentSceneCompletionCount();
            bool isRepeatSession = completionCount > 0;
            bool isEndingConclusion = currentDialogue != null && (currentDialogue.isEndingScene || isRepeatSession);

            if (isEndingConclusion && (currentDialogue.choices == null || currentDialogue.choices.Count == 0))
            {
                Debug.Log($"<color=green>[Progress]</color> 达成结局（无选项），触发序列。(重复探索模式: {isRepeatSession})");
                GameProgressManager.Instance.MarkCurrentSceneCompleted();
                StartCoroutine(ShowEndingSequence());
                return;
            }

            // 显现选项按钮
            ShowChoices();
        }
    }

    private void NextLine()
    {
        // 只有在还没出现选项的情况下才切下一句
        if (!choiceArea.gameObject.activeSelf)
        {
            currentLineIndex++;
            ShowCurrentLine();
        }
    }

    private void ShowChoices()
    {
        if (currentDialogue == null) return;

        int count = currentDialogue.choices != null ? currentDialogue.choices.Count : 0;
        Debug.Log($"<color=cyan>[UI]</color> 尝试唤醒选项区。资源: {currentDialogue.name}, 选项数: {count}");

        if (count == 0)
        {
            Debug.LogWarning("<color=orange>[Dialogue]</color> 该对话资源没有配置选项，忽略显示逻辑。");
            choiceArea.gameObject.SetActive(false);
            return;
        }

        // 核心修复：确保物体的同时，清理黑屏遮挡
        if (blackScreenGroup != null)
        {
            blackScreenGroup.alpha = 0;
            blackScreenGroup.blocksRaycasts = false;
            blackScreenGroup.gameObject.SetActive(false);
        }

        choiceArea.gameObject.SetActive(true);
        if (choiceGroupCG != null)
        {
            choiceGroupCG.alpha = 0;
            choiceGroupCG.interactable = true;
            choiceGroupCG.blocksRaycasts = true;
        }

        // 设置第一个按钮
        if (count >= 1 && choiceButtonA != null)
        {
            choiceButtonA.gameObject.SetActive(true);
            choiceButtonA.Setup(currentDialogue.choices[0], worryGroupA, OnChoiceSelected);
        }
        else if (choiceButtonA != null) choiceButtonA.gameObject.SetActive(false);

        // 设置第二个按钮
        if (count >= 2 && choiceButtonB != null)
        {
            choiceButtonB.gameObject.SetActive(true);
            choiceButtonB.Setup(currentDialogue.choices[1], worryGroupB, OnChoiceSelected);
        }
        else if (choiceButtonB != null) choiceButtonB.gameObject.SetActive(false);

        // 2. 渐显动画
        if (choiceGroupCG != null)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeInChoices());
        }
    }

    private void OnChoiceSelected(DialogueChoice choice)
    {
        if (worryGroupA != null) worryGroupA.Hide();
        if (worryGroupB != null) worryGroupB.Hide();

        Debug.Log($"<color=cyan>[Choice]</color> 玩家选择了：{choice.choiceText}");

        // --- 心情表现 ---
        if (talkController != null && talkController.face != null)
        {
            var f = talkController.face;
            f.mouthJoy = Mathf.Clamp(f.mouthJoy + joyLevelPerChoice, 0, 0.5f);
            f.mouthFun = Mathf.Clamp(f.mouthFun + joyLevelPerChoice * 0.5f, 0, 0.4f);
            f.eyeJoy = Mathf.Clamp01(f.eyeJoy + joyLevelPerChoice);
            f.browJoy = Mathf.Clamp01(f.browJoy + joyLevelPerChoice);
        }

        // 重要：即便资源没勾选，只要是“重复进入”且此路已断，也视为结局
        int completionCount = GameProgressManager.Instance.GetCurrentSceneCompletionCount();
        bool isRepeatSession = completionCount > 0;
        bool thisWasAnEnding = (currentDialogue != null && (currentDialogue.isEndingScene || isRepeatSession));
        bool hasNextContent = (choice.nextDialogue != null);

        Debug.Log($"<color=white>[Debug]</color> 结局判定: 资源标记={currentDialogue?.isEndingScene}, 重复探索={isRepeatSession}, 后继={hasNextContent}");

        if (hasNextContent)
        {
            StartDialogue(choice.nextDialogue);
        }
        else
        {
            // 剧情已到终点
            Debug.Log("<color=yellow>[Dialogue]</color> 对话已结束，清理 UI。");

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            if (choiceGroupCG != null) choiceGroupCG.alpha = 0;
            if (choiceArea != null) choiceArea.gameObject.SetActive(false);

            if (thisWasAnEnding)
            {
                Debug.Log("<color=green>[Progress]</color> 触发结局黑屏序列！");
                GameProgressManager.Instance.MarkCurrentSceneCompleted();
                StartCoroutine(ShowEndingSequence());
            }
            else
            {
                Debug.Log("<color=orange>[Warning]</color> 对话结束且非结局标记，仅清场。");
                if (talkController != null) talkController.StopSpeaking();
                if (npcTypewriter != null)
                {
                    if (npcTypewriter.transform.parent != null) npcTypewriter.transform.parent.gameObject.SetActive(false);
                    npcTypewriter.gameObject.SetActive(false);
                }
            }
        }
    }

    private System.Collections.IEnumerator ShowEndingSequence()
    {
        Debug.Log("<color=red>[Ending]</color> 触发最终结局黑屏。");

        // 1. 彻底清场
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        if (choiceGroupCG != null) choiceGroupCG.alpha = 0;
        if (choiceArea != null) choiceArea.gameObject.SetActive(false);
        if (talkController != null) talkController.StopSpeaking();

        // 强制隐藏打字机及其可能的父面板 (NPC 对话框)
        if (npcTypewriter != null)
        {
            // 尝试关闭打字机所在的父物体，确保背景框也消失
            if (npcTypewriter.transform.parent != null)
            {
                npcTypewriter.transform.parent.gameObject.SetActive(false);
            }
            npcTypewriter.gameObject.SetActive(false);
        }

        // 2. 黑屏淡入
        if (blackScreenGroup != null)
        {
            blackScreenGroup.gameObject.SetActive(true);
            blackScreenGroup.alpha = 0;

            // 2.1 先执行黑屏本身的淡入
            float fadeElapsed = 0f;
            while (fadeElapsed < blackFadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                blackScreenGroup.alpha = Mathf.Lerp(0, 1, fadeElapsed / blackFadeDuration);
                yield return null;
            }
            blackScreenGroup.alpha = 1;

            // 2.2 准备结局文字
            int count = GameProgressManager.Instance.GetCurrentSceneCompletionCount();
            string msg = $"你与她的故事，已经在这一刻凝固。";

            // 2.3 开始打字效果
            if (blackScreenTypewriter != null)
            {
                blackScreenTypewriter.ShowText(msg);

                // 等待显示完成
                bool isDone = false;
                UnityEngine.Events.UnityAction onDoneAction = null;
                onDoneAction = () => { isDone = true; };

                blackScreenTypewriter.onTextShowed.AddListener(onDoneAction);
                yield return new WaitUntil(() => isDone);
                blackScreenTypewriter.onTextShowed.RemoveListener(onDoneAction);
            }
            else if (blackScreenText != null)
            {
                blackScreenText.text = msg;
            }
        }
        else
        {
            Debug.LogError("<color=red>[Ending Error]</color> BlackScreenGroup 未分配！请检查脚本槽位。");
        }

        // 3. 停顿几秒
        yield return new WaitForSeconds(endingWaitBeforeEvent);

        // 4. 触发最终结局完成事件
        Debug.Log("<color=red>[Ending]</color> 结局演示完毕，触发 OnEndingSequenceComplete。");
        OnEndingSequenceComplete?.Invoke();
    }

    private System.Collections.IEnumerator FadeInChoices()
    {
        float start = choiceGroupCG.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            choiceGroupCG.alpha = Mathf.Lerp(start, 1f, elapsed / fadeDuration);
            yield return null;
        }
        choiceGroupCG.alpha = 1f;
    }
}
