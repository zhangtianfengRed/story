using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomInteractionProgressEventTrigger))]
[CanEditMultipleObjects]
public class RoomInteractionProgressEventTriggerEditor : Editor
{
    private SerializedProperty entries;

    private void OnEnable()
    {
        entries = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "监听当前 step 作用域内的互动进度。进度达到指定次数后触发事件，可用于播 Timeline、显隐物体、打开 UI 或推进剧情。",
            MessageType.Info);

        DrawEntries(entries, "进度触发项", DrawProgressEntry);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawProgressEntry(SerializedProperty entry, int index)
    {
        EditorGUILayout.LabelField($"进度触发项 {index + 1}", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        DrawRelative(entry, "progressId", "监听的进度 ID");
        DrawRelative(entry, "countType", "监听次数类型");
        DrawRelative(entry, "minimumCompletionCount", "达到次数");
        DrawRelative(entry, "invokeIfAlreadySatisfiedOnEnable", "启用时已满足则触发");
        DrawRelative(entry, "invokeOnceWhileEnabled", "启用期间只触发一次");
        DrawRelative(entry, "onSatisfied", "满足进度后触发事件", true);
        EditorGUI.indentLevel--;
    }

    private static void DrawEntries(SerializedProperty array, string label, System.Action<SerializedProperty, int> drawEntry)
    {
        EditorGUILayout.PropertyField(array, new GUIContent(label), false);
        if (!array.isExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        array.arraySize = EditorGUILayout.IntField("数量", array.arraySize);
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            drawEntry(entry, i);
            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawRelative(SerializedProperty parent, string propertyName, string label, bool includeChildren = false)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }
    }
}

[CustomEditor(typeof(RoomStepEventTrigger))]
[CanEditMultipleObjects]
public class RoomStepEventTriggerEditor : Editor
{
    private SerializedProperty applyOnStart;
    private SerializedProperty reapplyWhenSceneLoaded;
    private SerializedProperty entries;

    private void OnEnable()
    {
        applyOnStart = serializedObject.FindProperty("applyOnStart");
        reapplyWhenSceneLoaded = serializedObject.FindProperty("reapplyWhenSceneLoaded");
        entries = serializedObject.FindProperty("entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.HelpBox(
            "当前 GameFlow stepId 匹配时触发事件。适合进入阶段时自动播 Timeline、显隐物体、打开 UI 或初始化场景。",
            MessageType.Info);

        EditorGUILayout.PropertyField(applyOnStart, new GUIContent("Start 时应用当前步骤"));
        EditorGUILayout.PropertyField(reapplyWhenSceneLoaded, new GUIContent("场景加载完成后重新应用"));
        DrawEntries(entries, "步骤触发项", DrawStepEntry);

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawStepEntry(SerializedProperty entry, int index)
    {
        EditorGUILayout.LabelField($"步骤触发项 {index + 1}", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        DrawRelative(entry, "stepId", "匹配的 stepId");
        DrawRelative(entry, "invokeOnceWhileEnabled", "启用期间只触发一次");
        DrawRelative(entry, "onStepActive", "进入步骤时触发事件", true);
        EditorGUI.indentLevel--;
    }

    private static void DrawEntries(SerializedProperty array, string label, System.Action<SerializedProperty, int> drawEntry)
    {
        EditorGUILayout.PropertyField(array, new GUIContent(label), false);
        if (!array.isExpanded)
        {
            return;
        }

        EditorGUI.indentLevel++;
        array.arraySize = EditorGUILayout.IntField("数量", array.arraySize);
        for (int i = 0; i < array.arraySize; i++)
        {
            SerializedProperty entry = array.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            drawEntry(entry, i);
            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }

    private static void DrawRelative(SerializedProperty parent, string propertyName, string label, bool includeChildren = false)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }
    }
}
