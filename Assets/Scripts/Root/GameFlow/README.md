# Game Flow 与房间互动进度配置教程

新版推荐思路：

- `GameFlowDefinition / GameFlowManager` 管章节步骤：当前是哪个 `stepId`，要加载哪个场景，下一个步骤是谁。
- `RoomInteractionProgressManager` 管当前步骤里的互动进度：某个 `progressId` 完成了几次、继续游戏坐标在哪。
- `RoomInteractable` 管具体互动：执行行为、记录进度、触发可选事件、推进剧情。
- `RoomStepEventTrigger` 管进入某个 `stepId` 时自动触发事件。
- `RoomInteractionProgressEventTrigger` 管某个 `progressId` 达成后触发事件。

`GameFlowSceneController` 以后只当旧方案/兼容组件使用。新场景如果不需要它，可以不用再挂。

## 1. 章节步骤：GameFlowDefinition

在 Unity 里创建流程资产：

`Create/Story/Game Flow Definition`

推荐路径：

`Assets/Resources/GameFlow/GameFlowDefinition.asset`

这样 `GameFlowManager` 可以自动加载。如果继续使用当前项目里的 `Assets/Resources/GameFlow/Game Flow Definition.asset`，就需要手动拖到 `PersistentGameRoot.gameFlowDefinition` 或 `MainMenuStartGameButton.definitionOverride`。

每个 `Step` 配这些字段：

- `stepId`：剧情步骤 ID，例如 `Intro_Mirror`、`Room_Search`、`Room_AfterTalk`。
- `sceneName`：要加载的场景名，例如 `Mirror`、`room`。
- `contentKey`：旧的 `GameFlowSceneController` 会用；新版可以当备注/阶段名使用。
- `nextStepId`：完成当前步骤后去哪里。
- `notes`：备注。

关键规则：

> 同一个场景可以有多个 `stepId`，每个 `stepId` 都会拥有独立的房间互动进度。

例如：

```text
stepId = Room_Search
sceneName = room
contentKey = RoomSearch
nextStepId = Room_AfterTalk

stepId = Room_AfterTalk
sceneName = room
contentKey = RoomAfterTalk
nextStepId = Talk_First
```

这两个步骤都在 `room` 场景，但互动进度分别存在：

```text
step:Room_Search
step:Room_AfterTalk
```

## 2. 互动进度：RoomInteractionProgressManager

`RoomInteractionProgressManager` 默认按当前 `GameFlowManager.CurrentStepId` 作为作用域：

```text
currentStepId = Room_Search
scope = step:Room_Search
```

它保存：

- 每个 `progressId` 的两种次数：
  - `Open`：玩家按 E 打开/触发互动的次数。
  - `Completion`：玩法真正通过后手动记录的完成次数。
- 当前步骤的继续坐标 `RoomInteractionResumeState`。

保存键：

`LocalSaveStore.Keys.RoomInteractionProgress`

如果单独运行场景、没有有效 `stepId`，它会退回到：

```text
scene:当前场景名
```

这个只是调试兜底。正式流程默认按 `step:<stepId>` 走。

## 3. 在互动上记录进度

在物体上挂 `RoomInteractable`。

`RoomInteractable` 现在有 CustomEditor。顶部先选 `配置模式`，常用模式包括：

- `Simple`：只配置提示、检测范围和主互动。
- `RecordProgress`：互动后记录打开次数或玩法完成进度。
- `Conditional`：满足条件才执行主互动，未满足时不可互动。
- `ConditionalWithDefault`：满足条件执行主互动，未满足时执行默认反馈。
- `RecordAndAdvance`：记录进度后，在进度写入事件里绑定跳流程事件。
- `FullCustom`：显示所有字段，适合旧场景和复杂互动。

切换模式后可以点 `应用模式默认值`，它会帮你打开/关闭 `useConditionalInteraction` 这类基础开关。

主互动里常用字段：

- `interactionBehaviours`
- `interactionActions`
- `onInteract`
- `进度 ID`
- `打开时增加次数`
- `完成时增加次数`
- `primaryResumeOverride`

如果只是查看/打开一次就算进度：

```text
进度 ID = Drawer
打开时增加次数 = 1
```

玩家按 E 后会写入：

```text
step:Room_Search / Drawer.Open = 1
```

如果按 E 只是打开一个玩法，玩法通过后才算完成：

```text
进度 ID = NotebookPuzzle
完成时增加次数 = 1
```

然后在玩法成功事件里绑定当前 `RoomInteractable.RecordCompletionProgress()`。

这样玩家只是打开玩法不会记录完成，只有真正通过后才会写入：

```text
step:Room_Search / NotebookPuzzle.Completion = 1
```

如果一个互动既要统计打开次数，又要记录玩法完成，也只填同一个 ID：

```text
进度 ID = NotebookPuzzle
打开时增加次数 = 1
完成时增加次数 = 1
```

其它物体要依赖这个玩法结果时，在 `unlockConditions.requiredInteractionProgresses` 里填：

```text
progressId = NotebookPuzzle
countType = Completion
minimumCompletionCount = 1
```

如果只是要求玩家打开过一次，就把 `countType` 改成 `Open`。

进度写入后的事件：

- `打开进度写入后触发`：按 E 记录打开次数后触发。
- `完成进度写入后触发`：玩法成功调用 `RecordCompletionProgress()` 后触发。

这些事件可以绑定：

- 播放 Timeline
- 显示/隐藏对象
- 打开 UI
- 调用 `RoomInteractionProgressManager.CompleteCurrentStepAndLoadNext()`
- 调用 `RoomInteractionProgressManager.JumpToStep(string stepId)`

## 4. 条件互动和默认互动

如果 B 需要 A 完成后才能互动：

在 A 上：

```text
进度 ID = Drawer
打开时增加次数 = 1
```

在 B 上：

```text
useConditionalInteraction = true
unlockConditions.requiredInteractionProgresses:
  progressId = Drawer
  countType = Open
  minimumCompletionCount = 1
```

结果：

- `Drawer.Open >= 1` 时，B 执行主互动。
- 不满足条件但配置了 `defaultInteraction`，B 执行默认互动。
- 不满足条件且没有默认互动，B 会被视为当前不可互动。

注意：只填 `defaultInteraction.promptTextOverride` 不算配置了默认互动。默认互动至少要有一个实际内容：

- `interactionBehaviours`
- `interactionActions`
- UnityEvent

## 5. 互动后推进剧情

互动进度只记录状态，不会自动推进主流程。

如果互动后要切步骤，在 `打开进度写入后触发`、`完成进度写入后触发` 或其它互动事件里绑定：

- `RoomInteractionProgressManager.CompleteCurrentStep()`
- `RoomInteractionProgressManager.LoadNextStep()`
- `RoomInteractionProgressManager.CompleteCurrentStepAndLoadNext()`
- `RoomInteractionProgressManager.JumpToStep(string stepId)`
- `RoomInteractionProgressManager.LoadSceneByName(string sceneName)`

这样以后不用为了跳流程专门依赖 `GameFlowSceneController`。

## 6. 按 progressId 触发事件

如果事件不想挂在具体 `RoomInteractable` 上，可以在场景里放一个物体挂：

`RoomInteractionProgressEventTrigger`

配置 `entries`：

- `progressId`：要监听的互动进度 ID。
- `countType`：监听 `Open` 打开次数，还是 `Completion` 完成次数。
- `minimumCompletionCount`：达到多少次触发。
- `invokeIfAlreadySatisfiedOnEnable`：启用时如果已经满足，是否立刻补触发。
- `invokeOnceWhileEnabled`：同一次启用周期内只触发一次。
- `onSatisfied`：满足条件后触发的事件。

适合这些场景：

- 打开抽屉后，另一个远处对象变化。
- 收集到某个状态后，自动播一段 Timeline。
- 某个进度达成后，自动解锁 UI 或推进主流程。

## 7. 进入 step 时自动触发事件

如果一进入某个剧情步骤就要自动播 Timeline 或初始化场景，不用 `GameFlowSceneController`，改用：

`RoomStepEventTrigger`

配置 `entries`：

- `stepId`：要匹配的 `GameFlow stepId`，留空表示任何有效 step。
- `invokeOnceWhileEnabled`：同一次启用周期内只触发一次。
- `onStepActive`：匹配当前 step 后触发。

常见用法：

- 进入 `Intro_Mirror` 自动播放开场 Timeline。
- 进入 `Room_AfterTalk` 自动打开某组对象。
- 进入某个步骤时自动调用对话、镜头或 UI 初始化。

Timeline 播完后要推进流程，可以在 Timeline 的 stopped 事件上绑定：

- `RoomInteractionProgressManager.CompleteCurrentStepAndLoadNext()`

或者继续使用已有的 `PlayableDirectorEventBridge`。

## 8. 继续坐标

互动完成后可以保存继续游戏位置。

主互动配置：

```text
primaryResumeOverride.saveResumeTransformOnInteract = true
primaryResumeOverride.resumeTransform = DoorResumeAnchor
primaryResumeOverride.saveRotation = true/false
```

默认互动配置：

```text
defaultInteraction.resumeOverride.saveResumeTransformOnInteract = true
defaultInteraction.resumeOverride.resumeTransform = DoorResumeAnchor
defaultInteraction.resumeOverride.saveRotation = true/false
```

`DoorResumeAnchor` 建议是场景里的空物体，放在希望继续游戏时玩家出现的位置。

玩家身上的 `RoomTopDownPlayerMovement.restoreSavedResumeStateOnStart` 勾选后，进入场景时会读取当前 `stepId` 作用域下的继续坐标并恢复。

## 9. 场景通关次数条件

`RoomInteractionUnlockConditions` 里还有场景通关次数条件：

- `minimumCurrentSceneCompletionCount`
- `requiredSceneCompletions`

它们读的是 `GameProgressManager`，不是 `RoomInteractionProgressManager`。

也就是说：

- `requiredInteractionProgresses` 判断当前步骤里做过哪些互动。
- `minimumCurrentSceneCompletionCount / requiredSceneCompletions` 判断场景通关次数。

这些条件是并且关系，全部满足才解锁主互动。

## 10. Main 场景入口

Main 场景建议放 `PersistentGameRoot`：

- `ensureGameFlowManager` 勾选。
- `ensureProgressManager` 勾选。
- 需要转场时 `ensureSceneTransitionController` 勾选。
- `gameFlowDefinition` 拖入流程资产。

开始按钮挂 `MainMenuStartGameButton`：

- `StartGame()`：继续当前流程。
- `StartFromBeginning()`：重置 `GameFlow` 到初始步骤并开始。

注意：`StartFromBeginning()` 只重置 `GameFlowManager` 的步骤，不会自动清空 `RoomInteractionProgressManager` 的互动进度。完全新游戏时需要额外调用：

```text
RoomInteractionProgressManager.Instance.ClearAllProgress()
```

## 11. 新增一个房间阶段的推荐步骤

1. 在 `GameFlowDefinition.Steps` 新增步骤，例如 `Room_Search`。
2. `sceneName` 填 `room`。
3. 上一个步骤的 `nextStepId` 指向 `Room_Search`。
4. 如果进场要自动播 Timeline，场景里挂 `RoomStepEventTrigger`，配置 `stepId = Room_Search`。
5. 给可互动对象挂 `RoomInteractable`。
6. 需要记录状态时，填 `进度 ID`，再按需要设置打开次数/完成次数增量。
7. 需要前置条件时，配置 `useConditionalInteraction` 和 `unlockConditions.requiredInteractionProgresses`。
8. 需要互动后触发事件时，绑定 `打开进度写入后触发` 或 `完成进度写入后触发`。
9. 需要全局监听某个进度时，挂 `RoomInteractionProgressEventTrigger`。
10. 需要推进剧情时，绑定 `RoomInteractionProgressManager` 的跳转代理方法。

## 12. GameFlowSceneController 的新定位

`GameFlowSceneController` 现在不是必需组件。

旧场景如果已经靠它按 `contentKey` 批量显隐对象、播 Timeline，可以先保留，避免一次性改坏。

新场景推荐不用它，改成：

- 进 step 自动事件：`RoomStepEventTrigger`
- 互动完成事件：`打开进度写入后触发` / `完成进度写入后触发`
- 进度达成事件：`RoomInteractionProgressEventTrigger`
- 流程跳转：`RoomInteractionProgressManager` 代理方法

这样 Timeline 和场景表现逻辑回到具体场景对象上，不再需要一个单独的 `GameFlowSceneController` 集中管理。

## 13. 常见排错

- 条件互动不解锁：确认写入和读取是否在同一个 `step:<stepId>` 作用域。
- 直接运行场景和正式流程表现不同：直接运行可能用的是 `scene:<sceneName>` fallback。
- 互动后事件没触发：确认该互动配置了打开或完成进度 ID，并且对应进度确实被写入。
- 默认互动没出现：确认默认互动里有实际行为、Action 或 UnityEvent。
- Timeline 播完不推进：给 Timeline stopped 事件绑定 `RoomInteractionProgressManager.CompleteCurrentStepAndLoadNext()`。
- 继续坐标没恢复：确认保存了 `resumeTransform`，玩家身上 `restoreSavedResumeStateOnStart` 已勾选。
