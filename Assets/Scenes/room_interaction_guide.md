# Room 场景交互系统说明

## 结论

仓库里确实有一套“玩家靠近特定道具后，可显示提示、高亮目标，并按键触发交互”的房间交互系统，相关脚本集中在 `Assets/Script/Room/`。

这套系统的核心特征是：

- 不依赖 Trigger 碰撞器进入事件。
- 由玩家脚本每帧扫描场景中所有已激活的可交互物。
- 通过“距离最近且在交互半径内”的规则确定当前目标。
- 自动驱动提示 UI、高亮状态和交互回调。

## 当前项目中的实际状态

我查到的结果是：

- 这套交互脚本是在提交 `fdbc1dac`（`男主开场白`）中引入的。
- 当前 `Assets/Scenes/room.unity` 中没有直接挂载这套交互系统的脚本引用。
- 当前 `Assets/Anim/Room/RoomHansSuite.prefab` 上只确认挂有 `RoomTopDownPlayerMovement`，没有挂 `RoomPlayerInteractor`。
- 当前 `Assets/Scenes/room.unity` 里能确认在使用的房间相关脚本主要是：
  - `RoomFogPlane`
  - `RoomCameraFollow`
- 也就是说：这套“靠近后可交互”的系统现在仍保留在工程里，但当前 `room.unity` 版本并没有真正接通。

## 脚本职责总览

### 1. `RoomPlayerInteractor`

路径：`Assets/Script/Room/RoomPlayerInteractor.cs`

作用：

- 挂在玩家身上。
- 每帧遍历 `RoomInteractable.ActiveInteractables`。
- 从 `detectionOrigin` 位置出发，找出“范围内最近的可交互物”。
- 更新提示 UI。
- 更新所有可交互物的高亮状态。
- 玩家按下交互键后，调用当前目标的 `Interact(gameObject)`。

关键字段：

- `detectionOrigin`：检测起点，通常是玩家根节点或胸口/脚下参考点。
- `interactionKey`：默认是 `E`。
- `promptUI`：提示 UI。
- `autoFindPromptUI`：如果没手动拖引用，会自动在场景里查找 `RoomInteractionPromptUI`。

### 2. `RoomInteractable`

路径：`Assets/Script/Room/RoomInteractable.cs`

作用：

- 挂在可交互道具上。
- 在 `OnEnable` 时注册到静态列表 `ActiveInteractables`。
- 提供交互范围、提示文案、高亮控制和交互回调入口。
- 被玩家选中并按键后，创建 `RoomInteractionContext` 并执行交互逻辑。

关键字段：

- `isInteractable`：是否允许被检测和交互。
- `detectionCenter`：距离检测中心，不填时用当前物体位置。
- `interactionRange`：交互半径。
- `promptText`：提示文本，支持 `{key}` 占位符。
- `highlightController`：高亮控制器。
- `interactionBehaviours`：挂在场景物体上的行为脚本数组。
- `interactionActions`：`ScriptableObject` 动作数组。
- `onInteract` / `onInteractWithPlayer` / `onInteractWithTarget`：UnityEvent 回调。

### 3. `RoomInteractableHighlight`

路径：`Assets/Script/Room/RoomInteractableHighlight.cs`

作用：

- 专门处理“进入可交互范围时”和“成为当前目标时”的视觉反馈。
- 现在除了开关对象/组件外，还支持给 Renderer 的材质数组运行时追加整物体高亮材质。

支持两层状态：

- `highlightedObjects` / `highlightedBehaviours`
  - 只要进入交互范围就生效。
- `highlightedOverlayMaterial`
  - 只要进入交互范围，就会追加到目标 Renderer 的材质列表尾部，形成整物体高亮。
- `currentTargetObjects` / `currentTargetBehaviours`
  - 只有当前最近目标才生效。
- `currentTargetOverlayMaterial`
  - 只有当前最近目标才会额外追加。

这意味着可以做出两级反馈：

- 靠近时整物体高亮并闪烁。
- 当前目标再额外亮一点、显示特效或箭头。

### 4. `RoomInteractionPromptUI`

路径：`Assets/Script/Room/RoomInteractionPromptUI.cs`

作用：

- 控制“按下 E 进行交互”这一类 UI 提示。
- 同时支持 `TMP_Text` 和旧版 `UnityEngine.UI.Text`。
- `Show()` 时会把 `RoomInteractable.promptText` 中的 `{key}` 替换成真实按键名。

### 5. `RoomInteractionContext`

路径：`Assets/Script/Room/RoomInteractionContext.cs`

作用：

- 交互触发时传递上下文数据。

包含：

- `Player`
- `Interactable`
- `Distance`
- `PlayerTransform`
- `InteractableTransform`

如果后续要写“开门、播放对白、触发 Timeline、切换状态”等逻辑，这个上下文就是标准入口。

### 6. `RoomInteractionBehaviour`

路径：`Assets/Script/Room/RoomInteractionBehaviour.cs`

作用：

- 场景组件式扩展点。
- 需要继承它，并实现 `Execute(RoomInteractionContext context)`。
- 适合做“直接挂在当前物体上的行为逻辑”。

当前工程里唯一现成实现是：

- `RoomUnityEventInteractionBehaviour`

它的作用是把交互转成 UnityEvent，方便不写代码直接在 Inspector 里绑事件。

### 7. `RoomInteractionAction`

路径：`Assets/Script/Room/RoomInteractionAction.cs`

作用：

- `ScriptableObject` 扩展点。
- 适合做可复用动作资产。

当前工程里唯一现成实现是：

- `RoomDebugLogInteractionAction`

它只是输出一条 Debug 日志，更多像示例或测试用动作。

## 实际运行逻辑

整个交互链路如下：

1. 场景中的每个 `RoomInteractable` 在启用时加入 `ActiveInteractables`。
2. 玩家身上的 `RoomPlayerInteractor` 在 `Update()` 中执行 `RefreshTargets()`。
3. `RefreshTargets()` 会：
   - 读取玩家检测原点位置。
   - 遍历所有 `RoomInteractable`。
   - 过滤掉不可交互或超出范围的对象。
   - 按平方距离选最近目标。
4. 选中目标后：
   - 所有范围内道具更新高亮状态。
   - 最近目标额外标记为 `CurrentTarget`。
   - `RoomInteractionPromptUI` 显示提示文本。
5. 当玩家按下交互键时：
   - `RoomPlayerInteractor` 调用 `CurrentTarget.Interact(gameObject)`。
6. `RoomInteractable.Interact()` 内部会：
   - 构造 `RoomInteractionContext`
   - 执行 `interactionBehaviours`
   - 执行 `interactionActions`
   - 触发 UnityEvent 回调

## 这个系统和 Trigger 的区别

这套系统不是“玩家进入碰撞器就触发”，而是“玩家在交互半径内时可被选中，按键后才真正执行”。

优点：

- 更适合多道具并存时做“最近目标”选择。
- 不容易因为碰撞器重叠而误触。
- 不需要给每个道具都做复杂 Trigger 事件脚本。
- 高亮、提示、交互逻辑被拆分得比较清楚。

代价：

- 需要玩家端一直扫描。
- 需要场景里明确挂好 `RoomPlayerInteractor` 和 `RoomInteractionPromptUI`。

## 使用教程

下面是把这套系统重新接回 `room.unity` 的标准步骤。

### 步骤 1：给玩家挂交互器

建议对象：

- `Assets/Anim/Room/RoomHansSuite.prefab`

操作：

1. 给玩家根物体添加 `RoomPlayerInteractor`。
2. `detectionOrigin` 可以先留空，默认使用玩家自身 Transform。
3. `interactionKey` 保持 `E`，或按需求改成别的键。
4. 如果场景里有提示 UI，就把 `promptUI` 拖进去。
5. 如果不想手拖，也可以保留 `autoFindPromptUI = true`。

说明：

- 当前玩家 prefab 已经有 `RoomTopDownPlayerMovement` 和 `CharacterController`。
- 但当前没有 `RoomPlayerInteractor`，这一步是接通系统的前提。

### 步骤 2：创建提示 UI

在场景里新建一个 Canvas，用于显示交互提示。

推荐做法：

1. 新建 `Canvas`。
2. 新建一个文字对象，优先用 `TextMeshPro - Text`。
3. 在 Canvas 或提示节点上添加 `CanvasGroup`。
4. 在同一对象上挂 `RoomInteractionPromptUI`。
5. 把：
   - `canvasGroup`
   - `promptText`
   - 或 `legacyPromptText`
   对应拖好。

提示：

- `hideOnAwake` 通常保持开启。
- 当前 `room.unity` 里没有查到这套提示 UI 的接线，所以需要自行补上。

### 步骤 3：给可互动道具挂 `RoomInteractable`

对任意需要交互的道具：

1. 在道具根节点或合适的父节点上挂 `RoomInteractable`。
2. 设置 `interactionRange`，例如 `1.5` 到 `2.5`。
3. 设置 `promptText`，例如：
   - `按下 {key} 查看`
   - `按下 {key} 打开`
   - `按下 {key} 调查`
4. 如果道具的模型中心不适合作为距离判断点，就指定 `detectionCenter`。

注意：

- 不需要额外 Trigger 才能工作。
- 只要对象启用且 `isInteractable = true`，玩家就能检测到它。

### 步骤 4：配置高亮反馈

如果希望玩家靠近时有视觉反馈：

1. 在道具上或其子节点挂 `RoomInteractableHighlight`。
2. 如果想用“追加高亮材质”的方式高亮：
   - 把要处理的 Renderer 拖到 `highlightRenderers`
   - 或保留 `autoFindHighlightRenderers = true`
   - 把 `highlightedOverlayMaterial` 设为整物体高亮材质
3. 如果想让“当前最近目标”再额外明显一点：
   - 可以继续设置 `currentTargetOverlayMaterial`
   - 或使用 `currentTargetObjects` / `currentTargetBehaviours`
4. 如果想用“开关对象/组件”的方式高亮：
   - 把要显示/隐藏的物体拖到 `highlightedObjects`
   - 把要启停的组件拖到 `highlightedBehaviours`
5. 在 `RoomInteractable` 里把 `highlightController` 指向它。
6. 也可以保留 `autoFindHighlightController = true` 自动查找子节点。

常见用法：

- 靠近时给材质列表追加一层整物体高亮材质。
- 靠近时显示一个额外高亮代理物体。
- 当前最近目标时再显示一个额外图标或发光。

注意：

- 这次实现是“运行时追加材质”，不会替换原材质。
- 对单材质 Renderer 最直接。
- 对多子网格 / 多材质 Renderer，Unity 的额外材质绘制规则可能导致追加材质主要作用在最后一个子网格上。
- 如果遇到这种情况，优先把 `highlightRenderers` 指到单独的目标 Renderer，或改成专门的高亮代理物体方案。

### 步骤 5：配置交互触发后的实际行为

有三种接法。

#### 方案 A：直接用 UnityEvent

适合：

- 开关物体
- 播放 Animator 参数
- 播放声音
- 调用现有组件公开方法

做法：

1. 在同一个道具对象上挂 `RoomUnityEventInteractionBehaviour`。
2. 在 `RoomInteractable.interactionBehaviours` 数组里填入这个组件。
3. 在 `RoomUnityEventInteractionBehaviour.onInteract` 中绑定你要触发的方法。

#### 方案 B：用 `RoomInteractable` 自带事件

适合简单场景，少一个中间脚本。

做法：

1. 直接使用 `RoomInteractable` 自身的：
   - `onInteract`
   - `onInteractWithPlayer`
   - `onInteractWithTarget`
2. 在 Inspector 中绑定需要调用的方法。

#### 方案 C：写自定义 Behaviour 或 Action

适合：

- 需要代码逻辑
- 希望复用
- 希望拿到 `context.Player`、`context.Distance` 等数据

方式一：继承 `RoomInteractionBehaviour`

- 用于挂在场景对象上的逻辑组件。

方式二：继承 `RoomInteractionAction`

- 用于做成 `ScriptableObject` 资产，可在多个道具间复用。

当前工程自带示例：

- `RoomDebugLogInteractionAction`

它只能打印日志，更像模板。

### 步骤 6：运行检查

进入 Play Mode 后，应确认以下结果：

1. 玩家移动到道具附近后，提示 UI 出现。
2. 如果配置了高亮，道具会进入高亮状态。
3. 如果附近有多个道具，最近的那个会成为当前目标。
4. 按下 `E` 后，配置好的事件或逻辑会触发。

## 推荐的最小接线方案

如果你只是想最快恢复一个可用版本，建议最小配置如下：

1. 给 `RoomHansSuite.prefab` 加 `RoomPlayerInteractor`
2. 在场景里做一个 `Canvas + TMP_Text + RoomInteractionPromptUI`
3. 给一个道具挂 `RoomInteractable`
4. 给该道具挂 `RoomInteractableHighlight`
5. 给 `RoomInteractableHighlight.highlightedOverlayMaterial` 指向 `Assets/Material/RoomInteractableOverlayPulse.mat`
6. 给该道具挂 `RoomUnityEventInteractionBehaviour`
7. 在 `onInteract` 里先绑一个简单动作，例如：
   - 打开一个 UI
   - 切一个物体显隐
   - 调用已有脚本的公开方法

这样可以先验证整条链路通不通。

## 常见问题

### 1. 靠近了但没有提示

优先检查：

- 玩家是否挂了 `RoomPlayerInteractor`
- 场景里是否存在 `RoomInteractionPromptUI`
- `promptUI` 是否拖引用成功，或 `autoFindPromptUI` 是否开启
- 道具是否挂了 `RoomInteractable`
- `isInteractable` 是否为 true
- 玩家到道具的距离是否小于 `interactionRange`

### 2. 提示出现了，但按键没反应

优先检查：

- `interactionKey` 是否是你实际按下的键
- `RoomInteractable` 是否配置了：
  - `interactionBehaviours`
  - `interactionActions`
  - 或 `onInteract`
- 目标对象是否在交互时被其他逻辑禁用了

### 3. 没有高亮效果

优先检查：

- 是否挂了 `RoomInteractableHighlight`
- `highlightedObjects` / `currentTargetObjects` 是否填了内容
- `highlightController` 是否引用正确
- `autoFindHighlightController` 是否能找到子节点上的高亮组件

### 4. 为什么没有 Trigger 也能交互

因为这套系统完全是距离判定，不依赖 `OnTriggerEnter` / `OnTriggerExit`。

## 适合后续扩展的方向

这套结构比较适合继续扩展成下面几类玩法：

- 调查类道具
- 开门 / 开抽屉
- 播放对白
- 触发 Timeline
- 收集物
- 一次性交互后失效
- 按条件解锁交互

最合适的扩展点通常是：

- 轻量逻辑用 `RoomUnityEventInteractionBehaviour`
- 复杂逻辑用自定义 `RoomInteractionBehaviour`
- 可复用资产逻辑用自定义 `RoomInteractionAction`

## 相关文件

- 场景：`Assets/Scenes/room.unity`
- 玩家 prefab：`Assets/Anim/Room/RoomHansSuite.prefab`
- 交互脚本目录：`Assets/Script/Room/`
- 核心脚本：
  - `RoomPlayerInteractor.cs`
  - `RoomInteractable.cs`
  - `RoomInteractableHighlight.cs`
  - `RoomInteractionPromptUI.cs`
  - `RoomInteractionContext.cs`
  - `RoomInteractionBehaviour.cs`
  - `RoomUnityEventInteractionBehaviour.cs`
  - `RoomInteractionAction.cs`
  - `RoomDebugLogInteractionAction.cs`
