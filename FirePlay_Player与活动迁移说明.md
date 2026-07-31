# FirePlay Player 与活动迁移说明

## 当前阶段

新活动核心已经独立于旧 Ritual/Interaction 链：

- `ActivityDefinitionAsset`：一套玩法只创建一次，保存玩法 ID、作用域、专属 UI Prefab、Input Action Map、Camera Profile 和 LogicKey。
- `ActivityCatalogAsset`：集中登记所有活动定义。
- `ActivityAnchorNode`：地点只选择“这里提供哪些活动”，并显式指定该地点的规则组件；不复制玩法逻辑。
- `ActivityRuntime`：纯 C# 活动运行时，负责 Preflight、Prepare、Commit、Session、Action 和 End。
- `PlayerActivityHost`：Player 侧唯一的 Activity Unity 宿主。它不保存棉花糖、钓鱼、吉他等玩法字段，也不动态 `AddComponent`。

当前旧场景还没有切换到 `PlayerActivityHost`，因此不要把它直接加入现有可运行 Prefab 后就删除旧脚本。本阶段先完成一个真实活动的纵向迁移，再做 Prefab 切换。

## 目标 Player Prefab

最终 Player 只保留这些职责组件（具体名称可以随现有移动/动画实现调整）：

1. 身份与输入宿主；
2. 移动/角色控制；
3. Flame 状态宿主；
4. `PlayerActivityHost`；
5. Camera 请求执行器；
6. Presentation/UI 请求执行器；
7. 动画与表现桥接。

棉花糖、钓鱼、吉他、观星、表情不再各自作为 Player MonoBehaviour。它们是 ActivityDefinition + ActivityLogic + 专属 UI/表现资源，由 ActivityHost 按定义创建 Session。

## 地点配置

一个篝火、帐篷或池边只需要一个 `ActivityAnchorNode`：

- `AnchorId`、`RegionId`、Tags；
- `Activities` 中拖入可复用的 `ActivityDefinitionAsset`；
- `Rule Provider Behaviours` 中显式拖入该地点的规则脚本。

例如同一个 `Guitar` 定义可以同时放在篝火、帐篷和空旷地点；是否必须坐下、是否允许飞行或游泳，由地点规则与 Player 状态规则决定，不写死在 Guitar 定义里。

## 删除顺序

迁移完成后按以下顺序删除旧链：

1. 旧 `PlayerActivityController` 的活动选择/动作分发；
2. `MarshmallowInteraction`、`FishingInteraction` 等 Player 挂载玩法脚本；
3. `RestInteraction`、`RitualInteractionCoordinator` 与旧 Ritual 兼容层；
4. `LocalPlayerContext.EnsureCoreServices()` 中全部动态 `AddComponent`；
5. 旧通用 Activity Form/旧 Ritual Panel 的重复路径。

删除前每次只迁移一个可验收活动，并通过 Unity 编译和 Play Mode 验收；旧可运行版本已经由用户保存，可随时回退。

## 当前临时验收探针

`ExampleMarshmallowActivityProbe` 也是临时组件，只用于验证新核心，不代表最终场景配置。它会自动测试：

1. 余火不足时 `marshmallow.materialize` 被拒绝且余火不变化；
2. 余火足够时拟造成功；
3. 两次 `marshmallow.turn` 都由同一 Session 消费；
4. `marshmallow.eat` 恢复余火并以 `Completed` 结束 Session。

将它挂到空物体即可。这个探针通过后，下一步才会把逻辑接到真正的专属 UI、动画和 FlameResourceController。

## UI / Camera 请求执行器

新宿主不直接把 UI 控件交给活动逻辑：

- `PlayerActivityPresentationHost` 实现 `IActivityPresentationRequests`；
- UI 请求最终调用现有 `UIManager.ShowUIForms` / `CloseOrReturnUIForms`，因此继续保留现有 UI 栈和 `HideOther` 行为；
- Camera 请求转发给显式配置的 `IActivityCameraRequestExecutor`，没有 Camera Profile 的活动不需要配置执行器；
- `PlayerActivityHost` 的 `Presentation Behaviour` 字段拖入 `PlayerActivityPresentationHost`；
- `ActivityDefinitionAsset.UiPrefabKey` 必须填写 UIManager 使用的窗体名/注册键，而不是把按钮引用放进 ActivityLogic。

当前只是完成请求边界和转发层，尚未替换旧 `ActivityUIOrchestrator`，也尚未把旧 RitualCameraDirector 改造成通用 Camera Executor。

## 棉花糖专属 UI

新增 `MarshmallowActivityForm`，Prefab 只需继承 `BaseUIForms` 并配置：

- 一个状态 `Text`；
- `MaterializeButton`；
- `TurnButton`；
- `EatButton`；
- `CloseButton`。

按钮动作固定由 Form 提交，分别对应 `marshmallow.materialize`、`marshmallow.turn`、`marshmallow.eat`、`activity.exit`。它不需要引用 `LocalPlayerContext`、`FlameResourceController` 或任何 Ritual 组件。
Inspector 引用可以留空，Form 会按子物体名称自动查找上述控件；这让每个活动 Prefab 的配置保持局部，不需要修改通用 Activity Form。

当前已提供独立资源：

- `Assets/Resources/SUIFW/UIPrefabs/MarshmallowActivityForms.prefab`
- `Assets/FirePlay/Content/Activities/MarshmallowActivityDefinition.asset`
- `Assets/FirePlay/Content/Activities/ActivityCatalog.asset`
- `MarshmallowActivityLogicFactory`（应放在全局 Activity Bootstrap，不要挂到 Player）

Player 的 `PlayerActivityHost` 只需引用 Catalog、Logic Factory 和 `PlayerActivityPresentationHost`；地点只引用 `MarshmallowActivityDefinition`，不再配置按钮或玩法参数。

## 事件入口

活动轮盘或输入层发布 `DemonViglu.FirePlay.Activity.ActivitySelectionRequested(playerId, anchorId, activityId)` 即可启动活动；`anchorId` 为空时仅允许启动 Catalog 中声明为 `Anywhere` 的活动。活动按钮可以继续通过 `IActivityActionRequester` 获取同步结果，也可以发布 `DemonViglu.FirePlay.Activity.ActivityActionRequested` 交由 Host 消费。EventBus 只承载跨模块语义事件，不承载逐帧 Tick、移动或镜头拖拽。

新 Host 接管场景后，旧 `PlayerActivityController` 不应继续订阅同一组活动事件；切换阶段不要让两条链同时消费。

## 本阶段 Unity 验收探针

可在场景空物体上挂 `ExampleActivityHostProbe`，拖入 `PlayerActivityHost`、带 `ActivityAnchorNode` 的篝火对象，并将 Activity Id 保持为 `marshmallow`。进入 Play Mode 后应看到：

`[ExampleActivityHostProbe] START/EVENTBUS 通过：... 已创建 Session revision=...`

该探针会发布新 Activity 命名空间下的 EventBus 事件；它不是最终输入层。验收后删除探针，保留 Host、Catalog、Factory、PresentationHost 和 Anchor 配置。

## 轮盘/输入接管

活动轮盘或移动端按钮可以直接绑定 `FirePlayMobileInputRouter` 的三个公共入口：

- `SelectActivity(activityId)`：按玩家当前位置选择最近 Anchor；
- `SelectActivityAtAnchor(anchorId, activityId)`：明确指定地点；
- `SelectAnywhereActivity(activityId)`：不需要地点的活动。

这些入口只发布新 Activity EventBus 事件，不把棉花糖、钓鱼或吉他分支写进输入路由。

## 活动选择面板

当前提供 `ActivitySelectionForms`（列表式基础实现）和 `ActivitySelectionForms.prefab`。它会读取最近 Anchor 的可用定义，并追加 Catalog 中的 `Anywhere` 活动；按钮点击后发布 `DemonViglu.FirePlay.Activity.ActivitySelectionRequested`，成功后关闭选择面板并打开目标活动自己的 UI。

现阶段只需把一个入口按钮绑定到已有 `FirePlayMobileInputRouter.OpenActivitySelection()`。这不是最终视觉轮盘；以后将按钮布局替换为圆盘，不需要改 ActivityDefinition、ActivityLogic 或 Session。
