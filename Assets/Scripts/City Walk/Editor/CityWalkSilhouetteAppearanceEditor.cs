using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CityWalkSilhouetteAppearance))]
public sealed class CityWalkSilhouetteAppearanceEditor : Editor
{
    private const string DefaultMaterialPath =
        "Assets/Material/CityWalkBackgroundSilhouette.mat";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool valuesChanged = EditorGUI.EndChangeCheck();
        serializedObject.ApplyModifiedProperties();

        CityWalkSilhouetteAppearance appearance =
            (CityWalkSilhouetteAppearance)target;

        if (valuesChanged)
        {
            RecordHierarchy(appearance, "Update pedestrian silhouette");
            appearance.RefreshRendererList();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Apply / Refresh Silhouette"))
        {
            RecordHierarchy(appearance, "Apply pedestrian silhouette");
            appearance.RefreshRendererList();
            EditorUtility.SetDirty(appearance);
        }

        if (GUILayout.Button("Restore Original Materials"))
        {
            RecordHierarchy(appearance, "Restore pedestrian materials");
            appearance.RestoreOriginalAppearance();
            EditorUtility.SetDirty(appearance);
        }
    }

    [MenuItem(
        "GameObject/Story/City Walk/Apply Background Silhouette",
        false,
        20)]
    private static void ApplyToSelection()
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
        if (material == null)
        {
            Debug.LogError(
                $"Silhouette material was not found at {DefaultMaterialPath}.");
            return;
        }

        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            CityWalkSilhouetteAppearance appearance =
                selectedObject.GetComponent<CityWalkSilhouetteAppearance>();
            if (appearance == null)
            {
                appearance = Undo.AddComponent<CityWalkSilhouetteAppearance>(
                    selectedObject);
            }

            RecordHierarchy(appearance, "Apply pedestrian silhouette");
            appearance.SetSilhouetteMaterial(material);
            appearance.RefreshRendererList();
            EditorUtility.SetDirty(appearance);
        }
    }

    [MenuItem(
        "GameObject/Story/City Walk/Apply Background Silhouette",
        true)]
    private static bool ValidateApplyToSelection()
    {
        return Selection.gameObjects.Length > 0;
    }

    [MenuItem(
        "GameObject/Story/City Walk/Restore Background Silhouette",
        false,
        21)]
    private static void RestoreSelection()
    {
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            CityWalkSilhouetteAppearance appearance =
                selectedObject.GetComponent<CityWalkSilhouetteAppearance>();
            if (appearance == null)
            {
                continue;
            }

            RecordHierarchy(appearance, "Restore pedestrian materials");
            appearance.RestoreOriginalAppearance();
            EditorUtility.SetDirty(appearance);
        }
    }

    private static void RecordHierarchy(
        CityWalkSilhouetteAppearance appearance,
        string undoName)
    {
        Renderer[] renderers =
            appearance.GetComponentsInChildren<Renderer>(true);
        Object[] undoTargets = new Object[renderers.Length + 1];
        undoTargets[0] = appearance;
        for (int i = 0; i < renderers.Length; i++)
        {
            undoTargets[i + 1] = renderers[i];
        }

        Undo.RecordObjects(undoTargets, undoName);
    }
}
