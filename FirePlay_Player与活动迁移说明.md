# FirePlay 活动接入与场景配置说明

> 本文是开发时的操作手册。设计原则见 `FirePlay_活动系统重构契约.md`，当前进度和风险见 `FirePlay_开发状态.md`。

## 1. Player 侧应保留什么

Player 的活动域只需要一个 `PlayerActivityHost`，并配置以下接口实现：

- `Logic Factory Behaviour`：通常拖入场景中的 `ActivityLogicRegistryBehaviour`；
- `Presentation Behaviour`：拖入 `PlayerActivityPresentationHost`；
- `Flame Behaviour`：拖入实现 `IActivityFlameResource` 的玩家火焰组件；
- `Player State Behaviour`：拖入实现 `IActivityPlayerStateView` 的状态桥；
- `Activity Catalog Asset`：拖入全局 `ActivityCatalogAsset`。

本地 Player 将 `IsLocalPlayer` 保持为 true；网络生成的远端 Player 必须设为 false。远端对象不应覆盖 `LocalPlayerContext.Current` 或 `PlayerActivityHost.Local`，也不应注册本地输入/世界命令服务。

活动 Logic、Factory、Form 和 Visuals 不挂在 Player 上，也不由 `LocalPlayerContext` 动态创建。

`LocalPlayerContext` 负责组合通用 Player 服务，但这些服务已经在 Player Prefab 上显式挂载和绑定；新增活动不得再把玩法脚本塞进这条初始化链。

## 2. 新增一个活动

### 2.1 定义资产

创建 `ActivityDefinitionAsset`，至少设置：

- `ActivityId`：稳定且全局唯一，例如 `guitar`；
- `Scope`：`Anywhere`、`Anchor` 或 `Targeted`；
- `LogicKey`：与 Factory 注册键一致；
- `UiPrefabKey`：UIManager 注册的窗体键；没有专属 UI 可以为空；
- `CameraProfileId`：需要镜头时填写；
- `ParticipationMode`：默认独立 Session，只有明确需要时才使用共享模式。

然后将资产加入 `ActivityCatalogAsset`。一个活动只创建一份定义，不因地点不同而复制。

### 2.2 Logic 与 Factory

活动 Logic 应负责：

- `CheckStart`：无副作用的开始预检；
- `PrepareStart`：返回可回滚的开始操作；
- 自己的动作 ID、状态、计时和结果；
- 在明确的 Commit/Action 阶段通过 `IActivityFlameResource` 消耗或返还余火；
- 发出语义的 UI、Camera、Player、Animation、VFX 请求。

Logic 不应直接引用 `Button`、`UIManager`、`Camera.main`、`Animator`、`PlayerMovement` 或网络 Transport。

Factory 只根据 `LogicKey` 创建该活动 Logic。将 Factory 注册到场景中的 `ActivityLogicRegistryBehaviour`，不要把 Factory 挂到 Player。

### 2.3 活动自己的 UI

每个活动拥有自己的 UI Prefab 和 Form：

- 棉花糖的按钮只提交 `marshmallow.*`；
- Fishing 的按钮只提交 `fishing.*`；
- Guitar 的 21 个按键只提交 `guitar.key.01`～`guitar.key.21`；
- 关闭按钮只提交 `activity.exit`。

Form 可以通过 `IActivityActionRequester` 直接提交，也可以发布 `ActivityActionRequested` EventBus 事件。不要让 Form 直接调用活动 Logic 或火焰组件。

UI 打开/关闭最终由 `PlayerActivityPresentationHost` 转发给现有 UIManager，因此继续使用 UIManager 的堆栈和 `HideOther` 能力。

### 2.4 活动表现

活动 Visuals 订阅当前 Session 的只读状态和语义 Cue，负责：

- 道具显隐与 Transform；
- Animator 参数和触发器；
- 音频、粒子、特效和键位高亮。

如果需要新的执行能力，增加 Player 侧的请求执行器，不要让每个活动直接获取 Player 组件。动画资源尚未准备时，Cue 可以安全忽略，不应阻塞 Logic。

## 3. 将活动挂到地点

在篝火、帐篷、池边等地点添加一个 `ActivityAnchorNode`：

1. 为该对象配置 `StableSceneId`；
2. `AnchorId` 留空时自动读取 `StableSceneId.Value`；
3. 设置 `RegionId` 和 Tags；
4. 在 `Activities` 中拖入可复用的 `ActivityDefinitionAsset`；
5. 如有特殊地点规则，将实现 `IActivityRuleProvider` 的组件拖入 `Rule Provider Behaviours`。

地点不配置按钮、不配置活动动作、不复制 Logic。运行时生成的篝火只需在生成时分配稳定 ID，并保留 Prefab 中的 `ActivityAnchorNode` 和活动定义引用。

`RestSpot` 是坐下/观星的世界表现入口，不是活动 Anchor。当前观星仍由 `RestSpotRitual` 和 `RestLookTargetRitual` 驱动；未来若要将观星升级为独立活动，再创建新的 Activity Definition。

## 4. 选择与动作事件

输入层、列表面板、未来轮盘或网络层都只发布语义事件；网络适配器再将事件映射为稳定 DTO：

```csharp
new ActivitySelectionRequested(playerId, anchorId, activityId);
new ActivityActionRequested(playerId, anchorId, activityId, actionId, payload);
```

- `anchorId` 为空：只允许 `Anywhere` 活动；
- `anchorId` 有值：由 Host 根据稳定 ID 查找 Anchor；
- 选择、动作和退出都必须经过 `PlayerActivityHost`；
- `ActivitySelectionRequestDto` / `ActivityActionRequestDto` 必须经过 `IActivityAuthority`；动作必须携带当前 Session revision；
- 不能让旧输入路由和新 Host 同时消费同一个动作。

当前 `FirePlayMobileInputRouter` 提供：

- `SelectActivity(activityId)`：按位置选择最近 Anchor；
- `SelectActivityAtAnchor(anchorId, activityId)`：明确指定 Anchor；
- `SelectAnywhereActivity(activityId)`：启动 Anywhere 活动；
- `OpenActivitySelection()`：打开当前列表式活动选择面板。

真正的活动轮盘只需要替换选择面板的布局，不需要修改 Activity Domain、Logic 或 Session。

## 5. 运行验收顺序

新增活动或地点时按以下顺序验收：

1. Catalog 能找到 Definition，Factory 能创建 Logic；
2. 正确作用域能通过 Preflight，错误作用域被拒绝；
3. 活动专属 UI 能打开，按钮提交正确 Action ID；
4. 余火不足时无部分扣除，失败原因可见；
5. Camera、移动锁、朝向、动画和道具表现由请求执行；
6. 退出、切换、离开地点和状态变化能结束或重新预检 Session；
7. 同一地点的多个活动互不污染；
8. 单机验收通过后，再接入 Host DTO/Transport，不为联机复制玩法 Logic。

## 6. 常见问题定位

| 现象 | 优先检查 |
|---|---|
| 选择项不存在 | Anchor 的 StableSceneId、Activities、Catalog 是否配置 |
| `PlayerActivityHost is not ready` | Logic Factory、Catalog、Presentation/Flame/Player State 引用 |
| 按钮有日志但无效果 | Action ID 是否与当前 Logic 的 Handler 一致，Session revision 是否匹配 |
| UI 被遮挡 | 是否残留旧 Canvas/旧面板层级，活动 UI 是否走 UIManager |
| 模型显示异常 | Visuals 的 Transform/Renderer 引用和 Session 状态通知 |
| Camera/移动未恢复 | PresentationHost 是否收到 SessionEnded，活动是否成对释放请求 |
| 联机无法同步 | 当前尚未接入 Transport；本地事实事件不等于远端权威执行 |
