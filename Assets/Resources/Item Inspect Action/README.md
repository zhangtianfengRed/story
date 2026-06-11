# Item Inspect Action 配置教程

`RoomItemInspectAction` 用来做物品的默认浏览互动：打开一个全屏磨砂背景，在界面上显示对应的 3D 物品，并允许玩家拖拽旋转、滚轮缩放。

场景里只需要一个统一显示层：

```text
room 场景
└─ RoomItemInspectOverlay
```

这个对象已经放在 `Assets/Scenes/room.unity` 里，负责创建浏览 UI、背景模糊和 3D 预览相机。不同物品不需要各自挂 UI。

## 1. 创建 Action 资产

在 Project 里右键创建：

```text
Create -> Room -> Interaction -> Item Inspect Action
```

推荐保存路径：

```text
Assets/Resources/Item Inspect Action/room/
```

一个物品通常对应一个 `RoomItemInspectAction` 资产，例如：

```text
Paper.asset
MedicineBottle.asset
Phone.asset
```

## 2. 配置 Action

打开创建好的 `RoomItemInspectAction`，常用字段如下。

`Preview Source`

- `previewPrefab`：浏览界面里展示的 3D prefab。
- 如果不填 `previewPrefab`，会默认复制当前触发互动的 `RoomInteractable` 物体。
- 推荐给复杂物品单独做展示 prefab，避免把场景里的碰撞、互动脚本、音效等一起复制进去。

`Text`

- `displayName`：左侧标题。
- `description`：左侧描述。
- `useInteractableNameWhenDisplayNameEmpty`：标题为空时，是否使用当前互动对象名。

`View`

- `initialEulerAngles`：打开时的默认展示角度。
- `localOffset`：预览物体在浏览界面里的偏移。
- `scale`：当前物品的额外缩放。
- `cameraDistance`：预览相机距离。
- `fieldOfView`：预览相机视野角度。

一般只需要调：

```text
previewPrefab
displayName
description
initialEulerAngles
scale
cameraDistance
```

## 3. 配到默认互动

在具体物品的 `RoomInteractable` 上配置：

1. `配置模式` 选 `ConditionalWithDefault`，或者在完整模式里手动开启 `useConditionalInteraction`。
2. 配好主互动的解锁条件。
3. 展开 `默认互动/未解锁反馈`。
4. 把创建好的 `RoomItemInspectAction` 放进：

```text
默认互动资产 Actions
```

这样玩家在未满足主互动条件时，按 E 会执行默认浏览互动。

注意：

- 只填 `默认互动提示覆盖` 不算真正配置了默认互动。
- 默认互动里至少要有一个实际内容，例如 `RoomItemInspectAction`。

## 4. 富文本写法

标题和描述使用 TextMeshPro，支持 TMP 富文本标签。可以直接在 `displayName` 和 `description` 里写。

标题示例：

```text
<color=#ff6666><b>备忘录</b></color>
```

描述示例：

```text
<color=#dddddd>这是一张放在桌上的纸。</color>
<size=80%>边角有折痕，上面写着几行很小的字。</size>
```

常用标签：

```text
<b>加粗</b>
<i>斜体</i>
<color=#ff6666>红色文字</color>
<size=120%>更大的文字</size>
<size=80%>更小的文字</size>
```

如果要指定字体，需要项目里有对应 TMP Font Asset，之后可以使用 TMP 的字体标签。

## 5. 常见问题

物品一打开就被裁剪：

- 优先调当前 Action 的 `scale` 小一点。
- 或调大 `cameraDistance`。
- 全局边距在 `RoomItemInspectOverlay.previewFitPadding`。

物品太小：

- 调大当前 Action 的 `scale`。
- 或调小当前 Action 的 `cameraDistance`。

打开后没有显示模型：

- 检查 `previewPrefab` 是否有 Renderer。
- 如果 `previewPrefab` 为空，确认当前触发互动的对象本身有可渲染模型。
- 确认 `room` 场景里存在 `RoomItemInspectOverlay`。

标题和描述不显示：

- 检查 Action 里的 `displayName` 和 `description` 是否为空。
- 如果标题为空且不想用物体名，关闭 `useInteractableNameWhenDisplayNameEmpty`。

不想让白色底板出现：

- `RoomItemInspectOverlay.glassColor` 应保持透明：`a = 0`。

## 6. 推荐配置流程

```text
1. 给物品建一个展示 prefab
2. 创建 RoomItemInspectAction
3. 把展示 prefab 拖到 previewPrefab
4. 写 displayName 和 description
5. 调 initialEulerAngles / scale / cameraDistance
6. 把 Action 拖到 RoomInteractable 的默认互动资产 Actions
7. 进入 Play 测试默认互动
```
