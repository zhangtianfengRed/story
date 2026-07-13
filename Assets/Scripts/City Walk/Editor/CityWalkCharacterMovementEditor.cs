using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CityWalkCharacterMovement))]
public class CityWalkCharacterMovementEditor : Editor
{
    private const float HandleScale = 0.08f;

    private SerializedProperty movementBoundsMin;
    private SerializedProperty movementBoundsMax;

    private void OnEnable()
    {
        movementBoundsMin = serializedObject.FindProperty("movementBoundsMin");
        movementBoundsMax = serializedObject.FindProperty("movementBoundsMax");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Select this object in the Scene view, then drag the four green square handles to adjust the world X/Z movement bounds.",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        CityWalkCharacterMovement movement = (CityWalkCharacterMovement)target;

        serializedObject.Update();
        Vector2 serializedMin = movementBoundsMin.vector2Value;
        Vector2 serializedMax = movementBoundsMax.vector2Value;
        Vector2 boundsMin = Vector2.Min(serializedMin, serializedMax);
        Vector2 boundsMax = Vector2.Max(serializedMin, serializedMax);

        float y = movement.transform.position.y;
        float middleX = (boundsMin.x + boundsMax.x) * 0.5f;
        float middleZ = (boundsMin.y + boundsMax.y) * 0.5f;

        Vector3[] corners =
        {
            new Vector3(boundsMin.x, y, boundsMin.y),
            new Vector3(boundsMin.x, y, boundsMax.y),
            new Vector3(boundsMax.x, y, boundsMax.y),
            new Vector3(boundsMax.x, y, boundsMin.y)
        };

        Color borderColor = new Color(0.2f, 1f, 0.45f, 1f);
        Color fillColor = new Color(0.2f, 1f, 0.45f, 0.08f);
        Handles.DrawSolidRectangleWithOutline(corners, fillColor, borderColor);

        Vector3 leftHandle = new Vector3(boundsMin.x, y, middleZ);
        Vector3 rightHandle = new Vector3(boundsMax.x, y, middleZ);
        Vector3 backHandle = new Vector3(middleX, y, boundsMin.y);
        Vector3 frontHandle = new Vector3(middleX, y, boundsMax.y);

        Handles.color = borderColor;
        EditorGUI.BeginChangeCheck();

        leftHandle = Handles.Slider(
            leftHandle,
            Vector3.right,
            HandleUtility.GetHandleSize(leftHandle) * HandleScale,
            Handles.CubeHandleCap,
            0f);
        rightHandle = Handles.Slider(
            rightHandle,
            Vector3.right,
            HandleUtility.GetHandleSize(rightHandle) * HandleScale,
            Handles.CubeHandleCap,
            0f);
        backHandle = Handles.Slider(
            backHandle,
            Vector3.forward,
            HandleUtility.GetHandleSize(backHandle) * HandleScale,
            Handles.CubeHandleCap,
            0f);
        frontHandle = Handles.Slider(
            frontHandle,
            Vector3.forward,
            HandleUtility.GetHandleSize(frontHandle) * HandleScale,
            Handles.CubeHandleCap,
            0f);

        if (!EditorGUI.EndChangeCheck())
        {
            return;
        }

        movementBoundsMin.vector2Value = new Vector2(
            Mathf.Min(leftHandle.x, boundsMax.x),
            Mathf.Min(backHandle.z, boundsMax.y));
        movementBoundsMax.vector2Value = new Vector2(
            Mathf.Max(rightHandle.x, boundsMin.x),
            Mathf.Max(frontHandle.z, boundsMin.y));

        serializedObject.ApplyModifiedProperties();
    }
}
