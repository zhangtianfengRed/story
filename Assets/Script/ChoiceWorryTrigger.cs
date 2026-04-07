using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 挂载在对话按钮上，当鼠标悬停时会触发关联的 WorryTextGroup。
/// </summary>
public class ChoiceWorryTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("担忧显示设置")]
    public WorryTextGroup targetWorryGroup; // 指向屏幕侧边的“蚊子点”容器
    
    [Header("专属担忧内容")]
    [Tooltip("针对这一项选择，男主脑子里的杂念")]
    [TextArea(2, 4)] public List<string> customWorryText = new List<string>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (targetWorryGroup != null)
        {
            // 如果 customWorryText 为空，它会使用预设在 WorryTextGroup 里的默认句子
            targetWorryGroup.Show(customWorryText);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (targetWorryGroup != null)
        {
            targetWorryGroup.Hide();
        }
    }
}
