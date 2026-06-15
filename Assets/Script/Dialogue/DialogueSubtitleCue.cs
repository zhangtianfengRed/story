using System;
using UnityEngine;

[Serializable]
public class DialogueSubtitleCue
{
    [InspectorName("开始时间")]
    [Min(0f)]
    [Tooltip("字幕在当前语音内开始显示的时间，单位秒。多段长语音建议手动填写这个值来对齐。")]
    public float startTime;

    [InspectorName("结束时间")]
    [Tooltip("字幕结束时间，单位秒。小于 0 表示自动持续到下一段字幕开始，最后一段会持续到语音结束。")]
    public float endTime = -1f;

    [InspectorName("显示字幕")]
    [TextArea(2, 4)]
    [Tooltip("支持 TextMeshPro 富文本。")]
    public string text;

    [InspectorName("节奏参考文本（可选）")]
    [TextArea(2, 4)]
    [Tooltip("可选：只用于估算时间，不会显示。英文语音配中文字幕时，可以填英文原文来提升自动分配准确度。")]
    public string timingText;

    public bool HasText
    {
        get { return !string.IsNullOrWhiteSpace(text); }
    }

    public string ResolveTimingText()
    {
        return string.IsNullOrWhiteSpace(timingText) ? text : timingText;
    }
}
