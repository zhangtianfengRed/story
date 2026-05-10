using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RoomInteractionProgressRequirement))]
public class RoomInteractionProgressRequirementDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            DrawLine(ref line, property.FindPropertyRelative("progressId"), "进度 ID");
            DrawLine(ref line, property.FindPropertyRelative("countType"), "判断次数类型");
            DrawLine(ref line, property.FindPropertyRelative("minimumCompletionCount"), "至少达到次数");
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lineCount = property.isExpanded ? 4 : 1;
        return lineCount * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
    }

    private static void DrawLine(ref Rect line, SerializedProperty property, string label)
    {
        if (property == null)
        {
            return;
        }

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property, new GUIContent(label));
    }
}

[CustomPropertyDrawer(typeof(RoomSceneCompletionRequirement))]
public class RoomSceneCompletionRequirementDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            DrawLine(ref line, property.FindPropertyRelative("sceneName"), "场景名");
            DrawLine(ref line, property.FindPropertyRelative("minimumCompletionCount"), "至少通关次数");
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lineCount = property.isExpanded ? 3 : 1;
        return lineCount * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
    }

    private static void DrawLine(ref Rect line, SerializedProperty property, string label)
    {
        if (property == null)
        {
            return;
        }

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property, new GUIContent(label));
    }
}
