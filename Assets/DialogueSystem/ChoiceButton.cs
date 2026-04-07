using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

/// <summary>
/// 动态生成的按钮组件。负责处理自身的显示、点击以及悬停逻辑。
/// </summary>
public class ChoiceButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public TMP_Text buttonText;
    [Header("Visual Settings")]
    public Color normalColor = Color.white;
    public Color hoverColor = Color.cyan;
    public float hoverScale = 1.1f;

    private DialogueChoice data;
    private Action<DialogueChoice> onSelected;
    private WorryTextGroup worryGroup;

    public void Setup(DialogueChoice choiceData, WorryTextGroup targetWorryGroup, Action<DialogueChoice> onClickCallback)
    {
        data = choiceData;
        buttonText.text = data.choiceText;
        worryGroup = targetWorryGroup;
        onSelected = onClickCallback;

        // 强制 UI 立即重新计算布局（解决按钮扩展延迟问题）
        Canvas.ForceUpdateCanvases();
        var rect = GetComponent<RectTransform>();
        if (rect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        // 初始视觉状态
        ResetVisuals();

        // 绑定点击事件
        var btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => onSelected?.Invoke(data));
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 1. 变色与缩放
        buttonText.color = hoverColor;
        buttonText.transform.localScale = new Vector3(hoverScale, hoverScale, 1f);

        // 2. 显示杂念
        if (worryGroup != null && data != null && data.worryThoughts != null && data.worryThoughts.Count > 0)
        {
            worryGroup.Show(data.worryThoughts);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ResetVisuals();

        // 隐藏杂念
        if (worryGroup != null)
        {
            worryGroup.Hide();
        }
    }

    private void ResetVisuals()
    {
        if (buttonText == null) return;
        buttonText.color = normalColor;
        buttonText.transform.localScale = Vector3.one;
        if (data != null) buttonText.text = data.choiceText;
    }
}
