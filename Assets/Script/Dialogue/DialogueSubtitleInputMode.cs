using UnityEngine;

public enum DialogueSubtitleInputMode
{
    [InspectorName("整段文本自动拆分")]
    FullTextAutoSplit = 0,

    [InspectorName("手动字幕分段")]
    ManualCues = 1
}
