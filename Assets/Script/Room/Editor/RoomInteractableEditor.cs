using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RoomInteractable))]
[CanEditMultipleObjects]
public class RoomInteractableEditor : Editor
{
    private SerializedProperty inspectorMode;
    private SerializedProperty isInteractable;
    private SerializedProperty detectionCenter;
    private SerializedProperty interactionRange;
    private SerializedProperty ignoreVerticalDistance;
    private SerializedProperty promptText;
    private SerializedProperty promptAnchor;
    private SerializedProperty promptWorldOffset;
    private SerializedProperty highlightController;
    private SerializedProperty autoFindHighlightController;
    private SerializedProperty interactionBehaviours;
    private SerializedProperty interactionActions;
    private SerializedProperty primaryResumeOverride;
    private SerializedProperty onInteract;
    private SerializedProperty onInteractWithPlayer;
    private SerializedProperty onInteractWithTarget;
    private SerializedProperty progressId;
    private SerializedProperty openProgressIncrement;
    private SerializedProperty onOpenProgressRecorded;
    private SerializedProperty completionTaskProgressIncrement;
    private SerializedProperty onCompletionTaskProgressRecorded;
    private SerializedProperty useConditionalInteraction;
    private SerializedProperty unlockConditions;
    private SerializedProperty defaultInteraction;
    private SerializedProperty onHighlightChanged;
    private SerializedProperty onCurrentTargetChanged;

    private static bool showDetection = true;
    private static bool showMainLogic = true;
    private static bool showConditions = true;
    private static bool showDefaultInteraction = true;
    private static bool showAdvancedEvents;

    private void OnEnable()
    {
        inspectorMode = serializedObject.FindProperty("inspectorMode");
        isInteractable = serializedObject.FindProperty("isInteractable");
        detectionCenter = serializedObject.FindProperty("detectionCenter");
        interactionRange = serializedObject.FindProperty("interactionRange");
        ignoreVerticalDistance = serializedObject.FindProperty("ignoreVerticalDistance");
        promptText = serializedObject.FindProperty("promptText");
        promptAnchor = serializedObject.FindProperty("promptAnchor");
        promptWorldOffset = serializedObject.FindProperty("promptWorldOffset");
        highlightController = serializedObject.FindProperty("highlightController");
        autoFindHighlightController = serializedObject.FindProperty("autoFindHighlightController");
        interactionBehaviours = serializedObject.FindProperty("interactionBehaviours");
        interactionActions = serializedObject.FindProperty("interactionActions");
        primaryResumeOverride = serializedObject.FindProperty("primaryResumeOverride");
        onInteract = serializedObject.FindProperty("onInteract");
        onInteractWithPlayer = serializedObject.FindProperty("onInteractWithPlayer");
        onInteractWithTarget = serializedObject.FindProperty("onInteractWithTarget");
        progressId = serializedObject.FindProperty("progressId");
        openProgressIncrement = serializedObject.FindProperty("openProgressIncrement");
        onOpenProgressRecorded = serializedObject.FindProperty("onOpenProgressRecorded");
        completionTaskProgressIncrement = serializedObject.FindProperty("completionTaskProgressIncrement");
        onCompletionTaskProgressRecorded = serializedObject.FindProperty("onCompletionTaskProgressRecorded");
        useConditionalInteraction = serializedObject.FindProperty("useConditionalInteraction");
        unlockConditions = serializedObject.FindProperty("unlockConditions");
        defaultInteraction = serializedObject.FindProperty("defaultInteraction");
        onHighlightChanged = serializedObject.FindProperty("onHighlightChanged");
        onCurrentTargetChanged = serializedObject.FindProperty("onCurrentTargetChanged");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(inspectorMode, new GUIContent("配置模式"));
        if (EditorGUI.EndChangeCheck())
        {
            ApplyModeDefaults();
        }

        DrawModeHint();
        DrawRuntimeHint();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("应用模式默认值"))
            {
                ApplyModeDefaults();
            }

            if (GUILayout.Button("切到完整自定义"))
            {
                inspectorMode.enumValueIndex = (int)RoomInteractableInspectorMode.FullCustom;
            }
        }

        EditorGUILayout.Space(6);

        DrawDetectionSection();
        DrawModeBody((RoomInteractableInspectorMode)inspectorMode.enumValueIndex);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawModeBody(RoomInteractableInspectorMode mode)
    {
        switch (mode)
        {
            case RoomInteractableInspectorMode.Simple:
                DrawSimpleMode();
                break;
            case RoomInteractableInspectorMode.RecordProgress:
                DrawRecordProgressMode(false);
                break;
            case RoomInteractableInspectorMode.Conditional:
                DrawConditionalMode(false);
                break;
            case RoomInteractableInspectorMode.ConditionalWithDefault:
                DrawConditionalMode(true);
                break;
            case RoomInteractableInspectorMode.RecordAndAdvance:
                DrawRecordProgressMode(true);
                break;
            default:
                DrawFullCustomMode();
                break;
        }
    }

    private void DrawDetectionSection()
    {
        showDetection = EditorGUILayout.Foldout(showDetection, "检测与提示", true);
        if (showDetection)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(isInteractable);
            EditorGUILayout.PropertyField(promptText);
            EditorGUILayout.PropertyField(interactionRange);
            EditorGUILayout.PropertyField(ignoreVerticalDistance, new GUIContent(
                "忽略高度距离",
                "开启后只按地面 XZ 距离判断靠近，桌面物品建议开启。"));
            EditorGUILayout.PropertyField(detectionCenter);
            EditorGUILayout.PropertyField(promptAnchor);
            EditorGUILayout.PropertyField(promptWorldOffset);
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(autoFindHighlightController);
            if (!autoFindHighlightController.boolValue)
            {
                EditorGUILayout.PropertyField(highlightController);
            }
            EditorGUI.indentLevel--;
        }
    }

    private void DrawSimpleMode()
    {
        DrawMainLogicHeader("简单互动");
        if (!showMainLogic)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(interactionBehaviours, new GUIContent("主互动脚本 Behaviours"), true);
        EditorGUILayout.PropertyField(interactionActions, new GUIContent("主互动资产 Actions"), true);
        EditorGUILayout.PropertyField(onInteract, new GUIContent("主互动事件"));
        DrawProgressFields();
        DrawAdvancedEvents();
        EditorGUI.indentLevel--;
    }

    private void DrawRecordProgressMode(bool showAdvanceEvent)
    {
        DrawMainLogicHeader(showAdvanceEvent ? "记录进度并推进" : "记录进度");
        if (showMainLogic)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(interactionBehaviours, new GUIContent("主互动脚本 Behaviours"), true);
            EditorGUILayout.PropertyField(interactionActions, new GUIContent("主互动资产 Actions"), true);
            EditorGUILayout.PropertyField(onInteract, new GUIContent("主互动事件"));
            EditorGUILayout.Space(2);
            DrawProgressFields();
            EditorGUILayout.PropertyField(primaryResumeOverride, new GUIContent("主互动后保存继续坐标"), true);
            DrawAdvancedEvents();
            EditorGUI.indentLevel--;
        }
    }

    private void DrawConditionalMode(bool withDefault)
    {
        EditorGUILayout.PropertyField(useConditionalInteraction, new GUIContent("启用条件互动"));

        showConditions = EditorGUILayout.Foldout(showConditions, "解锁条件", true);
        if (showConditions)
        {
            EditorGUI.indentLevel++;
            DrawUnlockConditions();
            EditorGUI.indentLevel--;
        }

        DrawRecordProgressMode(false);

        if (withDefault)
        {
            DrawDefaultInteractionSection();
        }
    }

    private void DrawFullCustomMode()
    {
        DrawMainLogicHeader("主互动");
        if (showMainLogic)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(interactionBehaviours, new GUIContent("主互动脚本 Behaviours"), true);
            EditorGUILayout.PropertyField(interactionActions, new GUIContent("主互动资产 Actions"), true);
            DrawProgressFields();
            EditorGUILayout.PropertyField(primaryResumeOverride, new GUIContent("主互动后保存继续坐标"), true);
            EditorGUILayout.PropertyField(onInteract, new GUIContent("主互动事件"));
            EditorGUILayout.PropertyField(onInteractWithPlayer, new GUIContent("主互动事件（传入玩家）"));
            EditorGUILayout.PropertyField(onInteractWithTarget, new GUIContent("主互动事件（传入当前物体）"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(useConditionalInteraction);
        if (useConditionalInteraction.boolValue)
        {
            showConditions = EditorGUILayout.Foldout(showConditions, "解锁条件", true);
            if (showConditions)
            {
                EditorGUI.indentLevel++;
                DrawUnlockConditions();
                EditorGUI.indentLevel--;
            }

            DrawDefaultInteractionSection();
        }

        showAdvancedEvents = EditorGUILayout.Foldout(showAdvancedEvents, "高亮与目标状态事件", true);
        if (showAdvancedEvents)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(onHighlightChanged, new GUIContent("高亮状态变化事件"));
            EditorGUILayout.PropertyField(onCurrentTargetChanged, new GUIContent("成为/离开当前目标事件"));
            EditorGUI.indentLevel--;
        }
    }

    private void DrawDefaultInteractionSection()
    {
        showDefaultInteraction = EditorGUILayout.Foldout(showDefaultInteraction, "默认互动/未解锁反馈", true);
        if (showDefaultInteraction)
        {
            EditorGUI.indentLevel++;
            DrawNested(defaultInteraction, "promptTextOverride", "未解锁提示覆盖");
            DrawNested(defaultInteraction, "interactionBehaviours", "默认互动脚本 Behaviours", true);
            DrawNested(defaultInteraction, "interactionActions", "默认互动资产 Actions", true);
            DrawNested(defaultInteraction, "onInteract", "默认互动事件");
            DrawNested(defaultInteraction, "resumeOverride", "默认继续坐标", true);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawMainLogicHeader(string title)
    {
        showMainLogic = EditorGUILayout.Foldout(showMainLogic, title, true);
    }

    private void DrawUnlockConditions()
    {
        DrawNested(unlockConditions, "requiredInteractionProgresses", "互动完成条件", true);
        DrawNested(unlockConditions, "minimumCurrentSceneCompletionCount", "当前场景至少通关次数");
        DrawNested(unlockConditions, "requiredSceneCompletions", "指定场景通关条件", true);
    }

    private void DrawProgressFields()
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("互动进度", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("同一个进度 ID 下分别记录打开次数和完成次数。按 E 自动增加打开次数；玩法通过后调用 RecordCompletionProgress() 增加完成次数。", MessageType.None);
        EditorGUILayout.PropertyField(progressId, new GUIContent("进度 ID"));
        EditorGUILayout.PropertyField(openProgressIncrement, new GUIContent("打开时增加次数"));
        EditorGUILayout.PropertyField(onOpenProgressRecorded, new GUIContent("打开进度写入后触发"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("玩法完成进度", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("按 E 不会记录。玩法通过后，在成功事件里绑定 RoomInteractable.RecordCompletionProgress()。", MessageType.None);
        EditorGUILayout.PropertyField(completionTaskProgressIncrement, new GUIContent("完成时增加次数"));
        EditorGUILayout.PropertyField(onCompletionTaskProgressRecorded, new GUIContent("完成进度写入后触发"));

    }

    private void DrawAdvancedEvents()
    {
        showAdvancedEvents = EditorGUILayout.Foldout(showAdvancedEvents, "高级事件", true);
        if (!showAdvancedEvents)
        {
            return;
        }

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(onInteractWithPlayer, new GUIContent("互动事件（传入玩家）"));
        EditorGUILayout.PropertyField(onInteractWithTarget, new GUIContent("互动事件（传入当前物体）"));
        EditorGUILayout.PropertyField(onHighlightChanged, new GUIContent("高亮状态变化事件"));
        EditorGUILayout.PropertyField(onCurrentTargetChanged, new GUIContent("成为/离开当前目标事件"));
        EditorGUI.indentLevel--;
    }

    private static void DrawNested(SerializedProperty parent, string propertyName, string label, bool includeChildren = false)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            EditorGUILayout.PropertyField(property, new GUIContent(label), includeChildren);
        }
    }

    private void ApplyModeDefaults()
    {
        RoomInteractableInspectorMode mode = (RoomInteractableInspectorMode)inspectorMode.enumValueIndex;

        switch (mode)
        {
            case RoomInteractableInspectorMode.Conditional:
            case RoomInteractableInspectorMode.ConditionalWithDefault:
                useConditionalInteraction.boolValue = true;
                break;
            default:
                useConditionalInteraction.boolValue = false;
                break;
        }
    }

    private void DrawModeHint()
    {
        RoomInteractableInspectorMode mode = (RoomInteractableInspectorMode)inspectorMode.enumValueIndex;
        switch (mode)
        {
            case RoomInteractableInspectorMode.Simple:
                EditorGUILayout.HelpBox("只配置提示、范围和主互动事件。适合普通查看/播放一句话。", MessageType.Info);
                break;
            case RoomInteractableInspectorMode.RecordProgress:
                EditorGUILayout.HelpBox("配置一个进度 ID；按 E 增加打开次数，玩法通过后增加完成次数。", MessageType.Info);
                break;
            case RoomInteractableInspectorMode.Conditional:
                EditorGUILayout.HelpBox("满足解锁条件才执行主互动；未满足且无默认互动时不可交互。", MessageType.Info);
                break;
            case RoomInteractableInspectorMode.ConditionalWithDefault:
                EditorGUILayout.HelpBox("满足条件走主互动；未满足时走 defaultInteraction 做反馈。", MessageType.Info);
                break;
            case RoomInteractableInspectorMode.RecordAndAdvance:
                EditorGUILayout.HelpBox("用一个进度 ID 记录打开/完成次数，并在写入事件里绑定 RoomInteractionProgressManager 的跳转方法。", MessageType.Info);
                break;
            default:
                EditorGUILayout.HelpBox("显示所有字段。旧场景或复杂互动建议用这个模式。", MessageType.None);
                break;
        }
    }

    private void DrawRuntimeHint()
    {
        RoomInteractableInspectorMode mode = (RoomInteractableInspectorMode)inspectorMode.enumValueIndex;

        if (!isInteractable.boolValue)
        {
            EditorGUILayout.HelpBox("当前不会显示提示：Is Interactable 没有勾选。", MessageType.Warning);
            return;
        }

        if (interactionRange.floatValue <= 0f)
        {
            EditorGUILayout.HelpBox("当前很难显示提示：Interaction Range 小于等于 0。", MessageType.Warning);
            return;
        }

        if (mode != RoomInteractableInspectorMode.Conditional &&
            mode != RoomInteractableInspectorMode.ConditionalWithDefault &&
            useConditionalInteraction.boolValue)
        {
            EditorGUILayout.HelpBox(
                "注意：当前模式隐藏了条件字段，但 useConditionalInteraction 仍然开启。请点“应用模式默认值”，或切到完整自定义检查旧条件。",
                MessageType.Warning);
            return;
        }

        if (mode == RoomInteractableInspectorMode.Simple && !useConditionalInteraction.boolValue)
        {
            EditorGUILayout.HelpBox("当前无条件限制：玩家进入范围后应该显示主互动提示。", MessageType.None);
        }
    }
}
