# Game Flow 接线说明

## 1. 创建流程资产
- 在 Unity 里右键 `Create/Story/Game Flow Definition`
- 建议放到 `Assets/Resources/GameFlow/GameFlowDefinition.asset`
- 这样 `GameFlowManager` 在直接运行单个场景时也能自动找到它

## 2. 配置主流程步骤
- `Initial Step Id`: 第一条主流程步骤，例如 `Intro_Mirror`
- `Steps`: 每个步骤至少填这 4 个字段
- `stepId`: 全局唯一 ID
- `sceneName`: 要加载的场景名，例如 `Mirror`
- `contentKey`: 场景内内容 key，例如 `IntroTimeline`
- `nextStepId`: 完成后去的下一步，例如 `Room_Explore`

## 3. Main 场景
- 找到已有的 `PersistentGameRoot` 物体
- 给它新增的 `gameFlowDefinition` 字段拖入上面的流程资产
- 勾上 `ensureGameFlowManager`
- 如果你要统一黑屏转场，保持 `ensureSceneTransitionController` 勾选
- 在 `StartButton` 上挂 `MainMenuStartGameButton`
- 如果你不想依赖 `PersistentGameRoot` 注入，也可以把同一份流程资产拖到 `definitionOverride`

## 4. 具体场景
- 在 `Mirror`、`room`、`Talk` 这类场景里各放一个 `GameFlowSceneController`
- `contents` 里按 `contentKey` 配不同内容
- `activeRoots`: 该 key 进入时要显示的对象
- `inactiveRoots`: 该 key 进入时强制隐藏的对象
- `onEnter`: 可选，做额外初始化

## 5. Timeline 自动推进
- 以 `Mirror` 为例，新玩家第一步可以配成
- `stepId = Intro_Mirror`
- `sceneName = Mirror`
- `contentKey = IntroTimeline`
- `nextStepId = Room_Explore`
- 在 `GameFlowSceneController` 里给 `IntroTimeline` 这一项绑定 `PlayableDirector`
- 勾上 `playDirectorOnEnter`
- 勾上 `completeCurrentStepWhenDirectorStops`
- 这样 Timeline 播完后会自动标记当前步骤完成并加载下一步场景

## 6. Timeline 结束回调
- `ContentEntry` 里新增了 `onDirectorStopped`
- 如果你想“播完后执行回调”，就在这里绑事件
- 常见绑法：
- 绑定 `GameFlowSceneController.CompleteCurrentStepAndLoadNext()`：播完后按配置加载下一步
- 绑定 `GameFlowSceneController.LoadNextStep()`：只切到下一步，不标记完成
- 绑定你自己场景里的方法：比如激活某个 UI、开门、触发对话、切换特效
- 如果你只想走回调、不想自动推进，把 `completeCurrentStepWhenDirectorStops` 取消勾选即可

## 7. 其它场景通用 Timeline 回调
- 如果某个场景不想挂 `GameFlowSceneController`，也可以直接用 `PlayableDirectorEventBridge`
- 把它挂到任意 `PlayableDirector` 所在物体上
- 用它的 `onStopped` 直接绑回调
- 常见绑法：
- 绑定 `PlayableDirectorEventBridge.CompleteCurrentStepAndLoadNext()`
- 绑定 `PlayableDirectorEventBridge.LoadNextStep()`
- 绑定你自己的任意脚本方法
- 这样它在 `Mirror`、`Talk`、`room` 或以后新增的场景里都能直接复用

## 8. 同场景内分阶段切内容
- 如果两个步骤都在 `room` 场景，只要两个步骤的 `sceneName` 都填 `room`
- 但给不同的 `contentKey`，例如 `Room_Explore`、`Room_AfterTalk`
- `GameFlowManager` 跳到下一步时，如果还是同一个场景，不会强制重载，只会通知 `GameFlowSceneController` 切内容

## 9. 全局黑屏转场
- `PersistentGameRoot` 里新增了场景切换配置
- `ensureSceneTransitionController`：是否启用全局转场
- `transitionFadeOutDuration`：切场景前淡到黑屏的时间
- `transitionHoldDuration`：全黑时额外停留多久
- `transitionFadeInDuration`：新场景加载后从黑屏淡出的时间
- `transitionSortingOrder`：黑屏 UI 的排序层级，越大越在最上面
- `transitionBlockInputWhileVisible`：黑屏时是否拦截点击
- 以后不要直接在业务脚本里写 `SceneManager.LoadScene(...)`
- 如果是流程切换，继续走 `GameFlowManager`
- 如果是独立跳场景，也可以调用 `GameFlowManager.LoadSceneByName(string sceneName)` 或 `SceneTransitionController.LoadScene(string sceneName)`

## 10. 场景自己有开场黑屏时
- 像 `Mirror` 这种场景，如果自己已经有黑屏开场逻辑，就不要让全局黑幕再做“加载后淡出”
- 在该场景放一个 `SceneTransitionSceneAdapter`
- 勾上 `takeOverGlobalFadeIn`
- `onSceneLoadedBehindBlack` 里绑定场景自己的黑幕准备方法
- 例如 `TimelineBlackFadeOverlay.SetBlackImmediate()`
- 这样切到这个场景时：
- 先走全局黑幕加载场景
- 场景加载完成后，由本地黑幕接管
- 全局黑幕立即隐藏
- 后续由场景自己的 Timeline / Signal 去执行 `FadeFromBlack()`
