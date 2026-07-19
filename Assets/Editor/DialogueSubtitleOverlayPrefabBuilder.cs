using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class DialogueSubtitleOverlayPrefabBuilder
{
    private const string ResourcesFolder = "Assets/Resources";
    private const string UiFolder = ResourcesFolder + "/UI";
    private const string PrefabPath = UiFolder + "/DialogueSubtitleOverlay.prefab";
    private const string FontAssetPath = "Assets/Font/OPPOSans-Regular SDF.asset";

    [InitializeOnLoadMethod]
    private static void CreatePrefabWhenMissingAfterScriptReload()
    {
        EditorApplication.delayCall += TryCreateMissingPrefab;
    }

    private static void TryCreateMissingPrefab()
    {
        if (File.Exists(PrefabPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryCreateMissingPrefab;
            return;
        }

        BuildFromCommandLine();
    }

    [MenuItem("Tools/Dialogue/Create Default Subtitle Overlay Prefab")]
    private static void CreateFromMenu()
    {
        if (File.Exists(PrefabPath) &&
            !EditorUtility.DisplayDialog(
                "Replace subtitle overlay prefab?",
                "This recreates the default subtitle overlay prefab and replaces its current layout.",
                "Replace",
                "Cancel"))
        {
            return;
        }

        BuildFromCommandLine();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }

    public static void BuildFromCommandLine()
    {
        EnsureFolder(ResourcesFolder);
        EnsureFolder(UiFolder);

        GameObject root = new GameObject(
            "DialogueSubtitleOverlay",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(CanvasGroup),
            typeof(AudioSource),
            typeof(DialogueSubtitleOverlay));

        try
        {
            root.layer = LayerMask.NameToLayer("UI");

            RectTransform rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 6000;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            // Keep it visible while editing the prefab. DialogueSubtitleOverlay.Awake
            // applies hideOnAwake at runtime, so the preview never flashes in-game.
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            AudioSource audioSource = root.GetComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            Image background = CreateBackground(rootRect);
            TextMeshProUGUI subtitleText = CreateSubtitleText(background.rectTransform);
            DialogueSubtitleBackgroundFitter backgroundFitter =
                background.gameObject.AddComponent<DialogueSubtitleBackgroundFitter>();
            backgroundFitter.targetText = subtitleText;
            backgroundFitter.fitWidth = true;
            backgroundFitter.fitHeight = true;
            backgroundFitter.textPadding = new Vector2(40f, 24f);
            backgroundFitter.minimumSize = new Vector2(320f, 88f);
            backgroundFitter.maximumSize = new Vector2(1400f, 320f);
            backgroundFitter.applyPaddingToTextRect = true;
            backgroundFitter.RefreshNow();

            DialogueSubtitleOverlay overlay = root.GetComponent<DialogueSubtitleOverlay>();
            overlay.persistAcrossScenes = true;
            overlay.createUiOnAwake = false;
            overlay.hideOnAwake = true;
            overlay.voiceAudioSource = audioSource;
            overlay.defaultVoiceVolume = 1f;
            overlay.targetCanvas = canvas;
            overlay.subtitleCanvasGroup = canvasGroup;
            overlay.subtitleBackground = background;
            overlay.subtitleText = subtitleText;
            overlay.canvasSortingOrder = 6000;
            overlay.referenceResolution = new Vector2(1920f, 1080f);
            overlay.panelSize = new Vector2(1400f, 176f);
            overlay.panelBottomOffset = new Vector2(0f, 92f);
            overlay.textPadding = new Vector2(40f, 24f);
            overlay.fontSize = 34f;
            overlay.backgroundColor = new Color(0f, 0f, 0f, 0.58f);
            overlay.textColor = new Color(1f, 1f, 1f, 0.96f);
            overlay.defaultAutoDistributeUntimedCues = true;
            overlay.defaultFallbackSubtitleDuration = 3f;
            overlay.fadeInDuration = 0.12f;
            overlay.fadeOutDuration = 0.16f;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[DialogueSubtitleOverlayPrefabBuilder] Created {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Image CreateBackground(Transform parent)
    {
        GameObject backgroundObject = new GameObject(
            "SubtitleBackground",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        backgroundObject.layer = LayerMask.NameToLayer("UI");
        backgroundObject.transform.SetParent(parent, false);

        RectTransform rect = backgroundObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 92f);
        rect.sizeDelta = new Vector2(1400f, 176f);

        Image image = backgroundObject.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.58f);
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateSubtitleText(Transform parent)
    {
        GameObject textObject = new GameObject(
            "SubtitleText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = LayerMask.NameToLayer("UI");
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(40f, 24f);
        rect.offsetMax = new Vector2(-40f, -24f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font != null)
        {
            text.font = font;
        }
        else
        {
            Debug.LogWarning($"[DialogueSubtitleOverlayPrefabBuilder] Font not found: {FontAssetPath}");
        }

        text.text = "字幕预览 Subtitle Preview";
        text.fontSize = 34f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 1f, 1f, 0.96f);
        text.richText = true;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        string folderName = Path.GetFileName(folderPath);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
