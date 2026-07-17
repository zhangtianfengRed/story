using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CityWalkPedestrianVariantRandomizer))]
public sealed class CityWalkPedestrianVariantRandomizerEditor : Editor
{
    private const string SilhouetteMaterialPath =
        "Assets/Material/CityWalkBackgroundSilhouette.mat";

    private static readonly string[] CivilPrefabFolders =
    {
        "Assets/CivilCharactersPack/Prefabs/Male",
        "Assets/CivilCharactersPack/Prefabs/Female"
    };

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();

        if (GUILayout.Button("Fill Civil Character Variants"))
        {
            Configure((CityWalkPedestrianVariantRandomizer)target);
        }

        EditorGUILayout.HelpBox(
            "Only the Civil character model changes. Every runtime variant keeps the same shared black silhouette material.",
            MessageType.Info);
    }

    [MenuItem("GameObject/Story/City Walk/Add Random Civilian Variants", false, 23)]
    private static void AddToSelectedObjects()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        foreach (GameObject selectedObject in selectedObjects)
        {
            if (EditorUtility.IsPersistent(selectedObject))
            {
                Debug.LogWarning("Select a character instance in the scene, not a prefab asset.", selectedObject);
                continue;
            }

            CityWalkPedestrianVariantRandomizer randomizer =
                selectedObject.GetComponent<CityWalkPedestrianVariantRandomizer>();

            if (randomizer == null)
            {
                randomizer = Undo.AddComponent<CityWalkPedestrianVariantRandomizer>(selectedObject);
            }

            Configure(randomizer);
        }
    }

    [MenuItem("GameObject/Story/City Walk/Add Random Civilian Variants", true)]
    private static bool ValidateAddToSelectedObjects()
    {
        return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }

    private static void Configure(CityWalkPedestrianVariantRandomizer randomizer)
    {
        EnsureSilhouetteAppearance(randomizer.gameObject);

        GameObject previewPrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(randomizer.gameObject);
        GameObject[] variants = FindCivilCharacterPrefabs();

        Undo.RecordObject(randomizer, "Configure Random Civilian Variants");
        randomizer.Configure(previewPrefab, variants);
        EditorUtility.SetDirty(randomizer);

        Debug.Log(
            $"Configured {variants.Length} Civil character variants. The current scene model remains the editor preview.",
            randomizer);
    }

    private static void EnsureSilhouetteAppearance(GameObject targetObject)
    {
        CityWalkSilhouetteAppearance appearance =
            targetObject.GetComponent<CityWalkSilhouetteAppearance>();
        if (appearance == null)
        {
            appearance = Undo.AddComponent<CityWalkSilhouetteAppearance>(targetObject);
        }

        if (appearance.SilhouetteMaterial != null)
        {
            return;
        }

        Material silhouetteMaterial =
            AssetDatabase.LoadAssetAtPath<Material>(SilhouetteMaterialPath);
        if (silhouetteMaterial == null)
        {
            Debug.LogWarning(
                "The shared City Walk silhouette material could not be found.",
                targetObject);
            return;
        }

        Undo.RecordObject(appearance, "Assign Shared Silhouette Material");
        appearance.SetSilhouetteMaterial(silhouetteMaterial);
        EditorUtility.SetDirty(appearance);
    }

    private static GameObject[] FindCivilCharacterPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", CivilPrefabFolders);
        List<string> paths = new List<string>(prefabGuids.Length);

        foreach (string prefabGuid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuid).Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (path.IndexOf("/WithLod/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("custom", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            bool isStandardMale = fileName.StartsWith("male_", StringComparison.OrdinalIgnoreCase);
            bool isStandardFemale = fileName.StartsWith("female_suite", StringComparison.OrdinalIgnoreCase);
            if (isStandardMale || isStandardFemale)
            {
                paths.Add(path);
            }
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);

        List<GameObject> prefabs = new List<GameObject>(paths.Count);
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
            {
                prefabs.Add(prefab);
            }
        }

        return prefabs.ToArray();
    }
}
