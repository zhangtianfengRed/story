# Room 前置条件互动教程

这份文档只讲一件事：

- 某个道具要不要能互动，不再单独看“当前场景名”
- 而是看 `GameFlowManager.CurrentStepId`
- 这个 `stepId` 同时也是当前房间互动进度的作用域

如果你现在对“主流程”和“场景互动进度”的关系有点乱，先记住下面这句就够了：

> 一个 `stepId`，对应一套当前可用的房间互动状态。

例如：

- `Room_Search`
- `Room_AfterTalk`
- `Room_Ending`

它们就像 3 个不同的“房间互动阶段”。

即便这 3 个步骤都发生在同一个 `room.unity` 场景里，它们的互动进度也不会混在一起。

---

## 1. 现在这套系统的核心规则

现在房间互动的判断规则是：

1. `GameFlowManager` 负责全局主流程，记录当前 `currentStepId`
2. `RoomInteractionProgressManager` 负责互动进度，但它默认不是按场景名隔离，而是按当前 `stepId` 隔离
3. `RoomInteractable` 在玩家按互动键时，会判断：
   - 条件满足：执行主互动
   - 条件不满足，但配置了默认互动：执行默认互动
   - 条件不满足，也没配置默认互动：视为当前没有互动

所以你可以把它理解成：

- `GameFlowManager` 决定“我现在处于哪个剧情阶段”
- `RoomInteractionProgressManager` 决定“这个剧情阶段里，我已经做过哪些互动”

---

## 2. 三个最重要的概念

### 概念 A：`stepId`

这是主流程步骤 ID。

例如：

- `Room_Search`
- `Room_AfterTalk`
- `Office_Explore`

当前激活的 `stepId` 来自：

- `GameFlowManager.CurrentStepId`

它不仅代表剧情走到哪里，也代表当前房间互动应该读取哪一套互动进度。

### 概念 B：互动进度 ID

这是你自己给某个互动结果起的名字。

例如：

- `DrawerOpened`
- `PhotoChecked`
- `PasswordSolved`

当某个道具互动成功后，你可以把它记为一个完成状态。

例如：

- 玩家打开抽屉后，记录 `DrawerOpened`
- 玩家看完照片后，记录 `PhotoChecked`

后面的其它道具，就可以拿这些 ID 作为前置条件。

### 概念 C：主互动和默认互动

每个 `RoomInteractable` 现在可以有两套互动：

- 主互动：条件满足后执行
- 默认互动：条件不满足时执行

如果默认互动没配，则这个物体在锁定状态下会被视为“当前没有互动”。

也就是：

- 不会显示提示
- 不会被选中
- 按键不会触发

---

## 3. 一条完整的运行链路

假设当前主流程步骤是：

- `Room_Search`

此时系统会这样工作：

1. `GameFlowManager` 当前步骤是 `Room_Search`
2. `RoomInteractionProgressManager` 会把当前互动进度作用域切到 `step:Room_Search`
3. 你在这个步骤里触发的所有 `completionProgressId`
   都会记到 `step:Room_Search` 下面
4. 其它道具检查前置条件时，也默认从 `step:Room_Search` 下面读取
5. 当主流程切到 `Room_AfterTalk` 后
6. 当前互动作用域自动变成 `step:Room_AfterTalk`
7. 此时读取和写入的互动进度，都是另一套新的数据

所以：

- 同一个场景
- 不同的 `stepId`
- 可以拥有完全不同的互动状态

这正是你想要的“进入某个场景时，刚好衔接到该阶段的场景互动”。

---

## 4. Inspector 里你真正要配的字段

## 4.1 在“前置道具 A”上配置

如果道具 A 互动完成后，应该给后续道具解锁，就在 A 上配置：

- `completionProgressId`
- `completionProgressIncrement`

最常见的配置：

- `completionProgressId = DrawerOpened`
- `completionProgressIncrement = 1`

意思就是：

- 这个道具的主互动执行完一次后
- 当前 `stepId` 的互动进度里，`DrawerOpened` 加 1

如果你配置的是默认互动里的完成 ID，那么它会记到默认互动里。

## 4.2 在“受限制道具 B”上配置

如果道具 B 需要前置条件才解锁，就在 B 上配置：

- `useConditionalInteraction = true`
- `unlockConditions.requiredInteractionProgresses`

例如加一条：

- `progressId = DrawerOpened`
- `minimumCompletionCount = 1`

意思就是：

- 当前步骤里，`DrawerOpened` 至少完成 1 次
- B 才能执行主互动

## 4.3 如果 B 在未解锁时也要有反应

那就配置：

- `defaultInteraction`

你可以在这里单独配：

- `promptTextOverride`
- `interactionBehaviours`
- `interactionActions`
- `onInteract`

例如：

- 提示文案显示“抽屉还没打开”
- 互动时播放一句提示音
- 或弹一句对白

## 4.4 如果 B 在未解锁时应该完全不能互动

那就：

- 开启 `useConditionalInteraction`
- 配前置条件
- 不配置任何 `defaultInteraction` 的实际行为

这样锁定时它会被当作“当前无互动”。

注意：

只改 `defaultInteraction.promptTextOverride` 还不够。

当前代码里，“默认互动是否存在”看的是真正的互动内容：

- `interactionBehaviours`
- `interactionActions`
- 或持久化的 UnityEvent

如果这些都没配，系统会认为它没有默认互动。

## 4.5 如果某次互动完成后，继续游戏时要把角色放到指定位置

现在每个互动还可以可选地写入一个“继续坐标”。

注意，这个不是固定的 checkpoint 名字，而是一个你自己指定的场景坐标锚点。

配置方式：

- 主互动：
  - `primaryResumeOverride.saveResumeTransformOnInteract = true`
  - `primaryResumeOverride.resumeTransform = 你的空物体锚点`
  - `primaryResumeOverride.saveRotation = true/false`
- 默认互动：
  - `defaultInteraction.resumeOverride.saveResumeTransformOnInteract = true`
  - `defaultInteraction.resumeOverride.resumeTransform = 你的空物体锚点`
  - `defaultInteraction.resumeOverride.saveRotation = true/false`

建议做法：

1. 在场景里放一个空物体
2. 把它移动到你希望“继续游戏时玩家出现的位置”
3. 如果还希望玩家朝向也固定，就把这个空物体的旋转也摆好
4. 把这个空物体拖到上面的 `resumeTransform`

这样当这次互动执行后，系统会顺手把：

- 当前 `stepId`
- 对应的小进度
- 以及这个继续坐标

一起存下来。

之后继续游戏时，只要还是回到这个步骤，就会优先把玩家摆到这里。

---

## 5. 最常见的配置例子

## 例子 1：打开抽屉后，才能查看抽屉里的信

当前步骤：

- `Room_Search`

道具 A：抽屉

- `completionProgressId = DrawerOpened`

道具 B：信件

- `useConditionalInteraction = true`
- `unlockConditions.requiredInteractionProgresses`
  - `progressId = DrawerOpened`
  - `minimumCompletionCount = 1`

结果：

- 抽屉没开时，信件不能触发主互动
- 抽屉开过后，信件主互动解锁

如果你还给 B 配了默认互动：

- 未解锁时可以提示“信件还拿不到”

如果没配默认互动：

- 未解锁时信件直接视为当前不可互动

## 例子 2：同一个 `room.unity` 场景，不同剧情阶段有不同互动状态

步骤 1：

- `Room_Search`

步骤 2：

- `Room_AfterTalk`

这两个步骤都在 `room.unity`，但系统会分别记录：

- `step:Room_Search`
- `step:Room_AfterTalk`

所以：

- 第 1 阶段里点过的道具
- 不会自动污染第 2 阶段的互动进度

这就是为什么现在不需要再手动按场景名拆互动状态。

## 例子 3：需要“周目/通关次数”才开放互动

如果某个道具要在当前场景通关过至少 1 次后才开放：

- `minimumCurrentSceneCompletionCount = 1`

或者指定某个场景：

- `requiredSceneCompletions`
  - `sceneName = room`
  - `minimumCompletionCount = 1`

这类条件读的是：

- `GameProgressManager`

不是：

- `RoomInteractionProgressManager`

也就是说它判断的是“场景通关次数”，不是“当前步骤里点过哪些道具”。

## 例子 4：打开抽屉后，以后继续游戏时从门口位置开始

当前步骤：

- `Room_Search`

道具 A：抽屉

- `completionProgressId = DrawerOpened`
- `primaryResumeOverride.saveResumeTransformOnInteract = true`
- `primaryResumeOverride.resumeTransform = DoorResumeAnchor`

其中：

- `DoorResumeAnchor` 是你在场景里放的一个空物体
- 它的位置就是“继续游戏时玩家出现的位置”
- 它的朝向就是“继续游戏时玩家面朝的方向”（如果 `saveRotation = true`）

结果：

- 玩家打开抽屉后
- 当前步骤会记录 `DrawerOpened`
- 同时会记录一个继续坐标
- 之后如果玩家退出游戏，再继续回来，并且当前流程还是这个步骤
- 玩家就会直接出现在 `DoorResumeAnchor` 的位置

这件事和 `DrawerOpened` 是并列关系，不是同一件事：

- `DrawerOpened` 表示“抽屉开过了”
- `resumeTransform` 表示“继续游戏时角色应该站哪里”

---

## 6. 你可以这样理解“主流程”和“互动进度”的关系

建议用下面这套脑图来理解：

- `GameFlowManager`
  - 决定现在在哪个剧情步骤
- 当前 `stepId`
  - 决定现在应该使用哪套房间互动状态
- `RoomInteractionProgressManager`
  - 记录这个步骤里已经完成过哪些互动
- `RoomInteractable`
  - 根据这些互动状态，决定执行主互动还是默认互动

也就是说：

- 主流程是“章节/阶段”
- 互动进度是“这个阶段里玩家已经做了什么”

不要再把它理解成：

- 一个全局主流程
- 外加一个完全独立、和流程没关系的场景互动管理器

现在这两者已经绑定在一起了。

---

## 7. 推荐的配置习惯

### 习惯 1：互动进度 ID 用稳定字符串

推荐：

- `DrawerOpened`
- `PhotoChecked`
- `KeyTaken`

不推荐：

- `开了抽屉`
- `点过这个东西`

原因很简单：

- 字符串以后要复用、查找、排错
- 英文稳定 ID 更安全

### 习惯 2：一个道具只负责一件明确的事

例如：

- 抽屉只负责记录 `DrawerOpened`
- 照片只负责记录 `PhotoChecked`
- 密码锁只负责记录 `PasswordSolved`

不要让一个道具同时承担太多模糊状态。

### 习惯 3：默认互动只做“锁定态反馈”

默认互动最适合做这些：

- “现在还不能打开”
- “似乎还差点什么”
- “先去别处看看”

不建议默认互动里再塞太多复杂逻辑。

---

## 8. 调试时要注意的一点

如果当前没有有效的 `GameFlow currentStepId`，系统会退回到：

- 当前场景名

也就是说，单独直接运行某个场景时，这套互动进度仍然能工作，只是作用域临时变成：

- `scene:room`
- `scene:Office`

这只是调试兜底逻辑。

正式流程里，你应该默认认为互动进度是跟着 `stepId` 走的。

---

## 9. 一份最小配置清单

如果你现在要做一个“必须先互动 A，再互动 B”的最小案例，按下面做：

1. 给 A 挂 `RoomInteractable`
2. 在 A 上配置主互动逻辑
3. 在 A 上填 `completionProgressId = DrawerOpened`
4. 给 B 挂 `RoomInteractable`
5. 在 B 上勾 `useConditionalInteraction = true`
6. 在 B 的 `unlockConditions.requiredInteractionProgresses` 里加：
   - `progressId = DrawerOpened`
   - `minimumCompletionCount = 1`
7. 在 B 上配置主互动逻辑
8. 如果 B 未解锁时也要有反应，再配置 `defaultInteraction`
9. 如果 B 未解锁时应该完全没有反应，就不要配置默认互动

做到这里，这套逻辑就能跑起来。

---

## 10. 你最容易搞混的几个点

### Q1：`stepId` 和场景名是什么关系？

不一定一一对应。

可能：

- 一个 `stepId` 对应一个场景

也可能：

- 多个 `stepId` 都在同一个场景里

现在房间互动进度主要按：

- `stepId`

隔离，不是按场景名隔离。

### Q2：互动进度会不会自动推进主流程？

不会。

`completionProgressId` 只负责记录互动状态，不负责切换 `GameFlow`。

如果你要推进剧情，还是要走你自己的流程逻辑，例如：

- `GameFlowManager.JumpToStep(...)`
- `CompleteCurrentStepAndLoadNext()`

### Q3：为什么我只配了默认提示文案，但它还是像“没有互动”？

因为当前代码判断“默认互动是否存在”时，看的不是文案，而是实际互动内容。

要至少配一项：

- `interactionBehaviours`
- `interactionActions`
- 或 UnityEvent

### Q4：场景通关次数条件和互动进度条件能一起用吗？

可以。

它们是并且关系。

也就是：

- 互动进度满足
- 场景通关次数也满足

主互动才会解锁。

### Q5：为什么还要单独存继续坐标，不能只看 `DrawerOpened`？

因为这是两种不同的信息。

- `DrawerOpened`
  - 说明抽屉开过
- `resumeTransform`
  - 说明继续游戏时人要出现在哪

一个状态负责“逻辑进度”，一个状态负责“恢复位置”。

不要让它们互相代替。

---

## 11. 对应代码入口

如果你后面要回头看代码，主要看这几个文件：

- `Assets/Script/Room/RoomInteractable.cs`
  - 决定执行主互动还是默认互动
- `Assets/Script/Room/RoomInteractionUnlockConditions.cs`
  - 定义前置条件
- `Assets/Script/Room/RoomInteractionProgressManager.cs`
  - 记录并读取当前步骤作用域下的互动进度
- `Assets/Scripts/Root/GameFlow/GameFlowManager.cs`
  - 提供当前 `stepId`

---

## 12. 最后一句话版总结

你现在可以直接把这套系统理解成：

- `GameFlow stepId` 决定当前处于哪个剧情互动阶段
- 这个阶段里做过哪些互动，由 `RoomInteractionProgressManager` 记录
- 某个道具要不要解锁，就看这个阶段里的互动进度是否满足条件
- 不满足时可以走默认互动，不配默认互动就视为当前无互动

如果你愿意，我下一步可以继续给你写一份“带 Inspector 截图说明风格”的版本，把每个字段按面板顺序解释成傻瓜式配置表。
