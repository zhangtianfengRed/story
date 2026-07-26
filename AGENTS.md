# Project Instructions

## Generic World Interactions, Gameplay Progress, and Saving

`Room` is a historical prefix. The `RoomInteractable` family is the project's generic world-interaction, progress, unlock-condition, and resume-state system, not a system limited to the Room scene.

For any request that involves `RoomInteractable`, `RoomInteractionBehaviour`, `Command` gameplay interactions, gameplay objectives, `RoomInteractionProgressManager`, local saving/loading, `GameFlow`, scene progression, or the Office computer gameplay:

1. Read `Docs/InteractionSystemGuide.md` before analysing, editing, or proposing an implementation.
2. For a feature with multiple internal objectives or persistent state, also read the section `玩法进度与存档：后续功能只读这一节` in that document.
3. Reuse `RoomInteractionProgressManager` and its existing `LocalSaveStore` path for interaction objective persistence. Do not create ad-hoc `PlayerPrefs` keys.
4. Keep the outer `RoomInteractable` entry progress separate from each internal gameplay objective, and restore UI state from saved progress when the feature is opened.
5. Use `GameFlowManager` for story-step scene changes so the existing transition flow remains intact.
6. `Docs/InteractionSystemGuide.md` is a guide. If a required runtime detail is uncertain or an existing feature is being changed, use its `文档与源码的优先级` table to inspect the named source and Scene/Prefab/ScriptableObject configuration before editing.

The project owner normally compiles in the Unity Editor. Do not run Unity compilation or `dotnet build` unless explicitly asked.
