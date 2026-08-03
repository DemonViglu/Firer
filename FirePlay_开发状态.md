# FirePlay 当前开发状态

> 本文只保留当前事实、已验收能力、已知缺口和下一阶段计划。历史迁移日志以 Git 提交为准。

## 1. 当前基线

- Unity `6000.5.5f1`，URP `17.5.0`，Input System `1.19.0`。
- 当前 Demo 已保存可运行版本，单机功能已由用户验收通过。
- 当前目标顺序：先接入实时联机，再将实时活动产生的必要数据用于异步保存。
- Packages 中尚未安装 Netcode、Photon、Mirror、Relay 等网络 Transport；实时联机尚未开始。

## 2. 已验收能力

### 火焰与世界

- 玩家余火、篝火成长、SmallFire 放置/上限、世界树贡献和存档基础链路可运行。
- 活动拟造通过 `IActivityFlameResource` / 玩家 `TryConsume` 消耗余火；余火不足不会部分扣除。
- 火焰数值、篝火状态、世界进度与视觉表现分离，视觉只读取状态。

### 活动

- Marshmallow：独立 Logic、Factory、Definition、Form、Visuals；支持拟造、翻面、食用、余火返还和表现请求。
- Fishing：独立 Logic、Factory、Definition、Form、Visuals；支持拟造、抛竿、咬钩、收线和镜头请求。
- Emote：Anywhere 活动，只发送动画 Cue，不依赖 Anchor 或坐姿。
- Guitar：Anywhere 活动，固定 `guitar.key.01`～`guitar.key.21` 语义动作，独立 UI 和道具 Visuals 已建立，动画/音频资源可后补。
- Stargazing：当前仍是 `RestSpot` + `RestSpotRitual` + `RestLookTargetRitual` 的 Rest 表现，不是新 Activity Offer。

### 活动基础设施

- `ActivityDefinition` / `ActivityDefinitionAsset`：玩法元数据。
- `ActivityCatalogAsset`：全局活动目录。
- `ActivityAnchorNode`：地点身份、活动引用和规则引用；优先使用 `StableSceneId.Value` 作为 AnchorId。
- `ActivityRuntime` / `ActivitySystem`：定义查找、预检、Commit、Session、Action、Tick、End。
- `PlayerActivityHost`：Player 侧唯一活动宿主，不保存具体玩法字段，也不动态创建玩法组件。
- `ActivitySelectionForms`：当前为列表式选择面板，后续可替换为轮盘布局。
- `PlayerActivityPresentationHost`：统一转发 UI、Camera、移动锁、朝向、动画和 VFX 请求。
- 旧 `World.ActivityAnchor`、旧 Offer 类型、旧 Ritual Panel 和已迁移的玩法 Interaction 已删除。

## 3. 当前核心逻辑链路

```text
输入 / 活动 UI
    -> GameEventBus: ActivitySelectionRequested
    -> PlayerActivityHost
    -> ActivityRuntime / ActivitySystem
    -> Catalog + Anchor + Rule Providers + Player State
    -> Preflight -> Prepare -> Commit
    -> ActivitySession
    -> PresentationHost 打开活动 UI / Camera / Player 能力

活动 UI / 独立输入
    -> ActivityActionRequested
    -> PlayerActivityHost
    -> 当前 Session 消费语义动作
    -> ActivityInteractionOccurred
    -> Logic 状态变化 -> Visuals / UI / 动画 / 音频

退出、状态失效或权威拒绝
    -> ActivitySessionEnded
    -> PresentationHost 对称释放表现请求
```

火焰链路独立运行：

```text
Player / Campfire / Activity
    -> FlameResourceController 或 IActivityFlameResource
    -> FlameStateChanged
    -> Visuals / UI / Save / Network
```

## 4. 当前代码分层

| 层 | 主要入口 | 评价 |
|---|---|---|
| Activity Domain | `ActivitySystem`、`ActivityRuntime`、`ActivitySession`、`ActivityRuleResolver` | 纯 C#，可测试，已形成稳定核心 |
| Activity Composition | `DefinitionAsset`、`CatalogAsset`、`AnchorNode`、Logic Registry | 新增活动和地点组合路径清晰 |
| Player Boundary | `PlayerActivityHost`、`PlayerActivityPresentationHost` | 活动与 Player/UI/Camera 的边界明确 |
| Global Services | `GameInstanceSubsystem`、`GameEventBus` | 可定位核心服务，跨模块事件统一 |
| Flame/World | `FlameResourceController`、`Campfire`、`SmallFire`、`WorldTreeContribution`、Save | 独立于活动，但仍需网络权威适配 |
| Legacy Rest | `RestInteraction`、`RestSpot`、`RestSpotRitual`、`RestLookTargetRitual` | 观星仍依赖，暂不等同于 Activity |
| Input/UI | `FirePlayMobileInputRouter`、各 Activity Form、SUIFW UIManager | 活动 UI 已分离，轮盘尚为列表选择 |

### Player Core Services 收口

- `LocalPlayerContext` 仍是 Player 通用服务的组合入口，但不再动态 `AddComponent`。
- `Player.prefab` 已显式挂载并绑定 `PlayerSharedStateAdapter`、`PlayerExpressionController`、`PlayerProximityEffects`、`WorldCommandExecutor` 和 `InteractionRouter`。
- 缺少任一核心服务时，启动会明确报错并将 `CoreServicesReady` 置为 false，而不是静默生成组件。
- `InteractionRouter` 在显式服务初始化完成后重新绑定输入事件，避免组件 Awake 顺序导致输入丢失。
- `LocalPlayerContext.IsLocalPlayer` 与 `PlayerActivityHost.IsLocalPlayer` 明确本地/远端边界；远端对象不会覆盖 `Current`/`Local`，也不会注册本地输入路由或世界命令执行器。

## 5. 客观架构评估

### 优点

1. **活动扩展路径正确**：新增玩法主要增加 `Definition + Logic + Factory + Form + Visuals`，不需要修改棉花糖、钓鱼或通用 Player Router。
2. **组合能力足够**：同一活动可挂到多个 Anchor；同一 Anchor 可提供多个活动；是否坐下、是否飞行、是否游泳由规则提供者决定，而不是写死在活动定义中。
3. **表现边界清楚**：活动只发 UI/Camera/Player 请求，具体执行在 Player 侧；因此补动画、特效、音频通常只改对应活动的 Visuals 或新增执行器。
4. **资源域独立**：余火、篝火和世界树不被某个活动类型污染，拟造机制也通过接口复用。
5. **联机切入点已经存在**：Host 已发布带稳定 ID、Action、Payload、Revision 的 Started/Interaction/Ended 事实事件；请求 DTO、事实 DTO 和 `IActivityAuthority` 已落地，未来可序列化而不传 Unity 引用。
6. **旧兼容层已明显收口**：旧 ActivityAnchor、旧 Offer 和通用 Ritual 面板已经移除，新增活动不再依赖历史入口。

### 目前的不足与风险

1. **实时联机还没有权威执行链**：现在的 `PlayerActivityHost` 是本地宿主，事实事件是本地提交后的通知；没有 Network SDK、Host DTO、请求确认、远端 Session 和远端表现回放。因此“可扩展到联机”成立，但“已经联机就绪”不成立。
2. **规则框架比实际接线更完整**：`ActivityRuleResolver` 支持优先级和 Host 权威，但当前 Demo 的 Player 状态规则、目标规则和 Anchor 规则还没有形成完整的统一注册/刷新管线。下一阶段必须先建立权威规则输入，再做多人活动。
3. **Player 仍有多个通用 MonoBehaviour**：动态创建已经移除，依赖现在能在 Prefab 上看见并验收；本地/远端所有权也已分开，但输入、世界命令、表达和邻近效果仍是多个组件，未来可进一步合并为单一 Player Service Host。当前风险已从“隐藏创建”降为“组件边界较多”。
4. **全局服务定位器有初始化风险**：`GameInstanceSubsystem.GetOrCreate` 很方便，但可能掩盖注册顺序错误；EventBus 的订阅/取消订阅也应减少“未订阅即警告”的噪声。联机后必须在启动阶段显式注册 Host、Transport 和权限服务。
5. **Unity 工程边界仍偏粗**：当前没有按 Core、Activity、World、Network 拆分 asmdef，主要代码仍编译在 `Assembly-CSharp`；短期开发快，长期会降低编译隔离和依赖约束。
6. **UI 和 Camera 仍有字符串/历史命名耦合**：UIManager 窗体键、Camera Profile 和部分 `RitualCameraDirector` 命名属于配置约定，能用但缺少编译期检查；后续应把请求结果、缺失配置和生命周期日志标准化。
7. **选择表现尚未完成产品形态**：当前活动选择是列表式 `ActivitySelectionForms`，移动中动态刷新和真正的活动轮盘仍属于 UI 工作，不需要改 Activity Domain。

### 结论评分

| 维度 | 当前评价 | 结论 |
|---|---:|---|
| 新活动接入 | 8/10 | 适合快速增加独立小玩法 |
| 地点组合与规则 | 7/10 | 契约清晰，实际规则注册仍需补全 |
| UI/Camera/动画扩展 | 7.5/10 | 边界正确，主要受配置和执行器完整度影响 |
| Player 结构 | 6.5/10 | 动态创建已移除，但通用服务组件仍可进一步合并 |
| 实时联机准备度 | 4/10 | 事实事件已准备，权威 Transport 尚未接入 |
| 长期维护性 | 6.5/10 | 方向正确，但需补 asmdef、显式依赖和网络边界 |

**总体判断：** 当前项目已经从“玩法脚本堆叠”进入“可组合活动核心”的阶段，适合继续扩展本地活动；它还不是可以直接开多人测试的代码框架。Player 的隐式动态服务已经收口，接入联机前最重要的工作转为 Host 权威、规则输入、DTO/Transport 和远端表现边界。

## 6. 联机前的唯一待办

1. 选择并安装实时网络 Transport/SDK；
2. 将 `ActivitySelectionRequestDto`、`ActivityActionRequestDto` 接入 Transport 的请求消息；
3. 使用 `IActivityAuthority` 调用 `PlayerActivityHost` 的权威入口；本地 EventBus 和未来网络请求已共用同一条执行路径。
4. Host 执行统一 Preflight/Commit，向客户端广播 `ActivityFactDto`（Started/Interaction/Ended）；
5. 建立远端 Player 的 Activity Presentation，不在远端复制本地 Logic；
6. 为共享 Anchor 状态和 Targeted Interaction 增加明确的 Group/Target 状态；
7. 联机稳定后，再把需要持久化的实时事实接入异步保存。

## 7. 验证基线

- Unity Play Mode：当前 Demo、活动 UI、活动表现、Rest/观星、篝火和火焰资源已由用户验收通过。
- 命令行编译：`dotnet build Assembly-CSharp.csproj --nologo --no-restore`，0 错误；仅有 `CampfirePersistence` 的 2 个既有 API 过时警告。
- Unity Prefab 空序列化字段造成的 `git diff --check` 尾随空白属于 YAML 格式噪声，不是逻辑错误。

## 8. 本次联机前切片：请求与事实 DTO

- `ActivitySelectionRequestDto` 和 `ActivityActionRequestDto` 只携带 Player、Anchor、Activity、Action、Payload 与 Session revision 等稳定数据。
- `IActivityAuthority` 是网络适配器的唯一活动入口；`PlayerActivityHost` 同时实现本地 EventBus 与未来网络请求。
- 动作必须携带当前 Session revision；旧 Session、错误 Player、错误 Anchor 或错误 Activity 会在进入 Logic 前拒绝。
- `ActivityFactDto` 可由 Started/Interaction/Ended 事实事件映射得到，后续可直接序列化为 SDK 消息。
- 本切片没有引入 Network SDK、Transport 或远端 Logic；下一步才是选定 SDK 并实现传输适配器。

## 9. Player 收口门禁

- 已完成 `Player.prefab` 根组件审计：当前 27 个 Unity 组件，其中 24 个是自定义脚本；问题是职责混合，不是单纯数量问题。
- 已确认 `LocalPlayerContext`、5 个通用服务、`PlayerInteraction`/`InteractionRouter` 重复入口和 Player 根上的 `FishingActivityVisuals` 是收口重点。
- 已建立非 `MonoBehaviour` 的 `PlayerCoreHost` 组合宿主：集中服务依赖、缺失配置诊断和初始化顺序，暂不删除现有组件。
- `PlayerSharedStateService` 已迁入 `PlayerCoreHost`，`PlayerSharedStateAdapter` 暂作为兼容外观保留；状态 Tick、远端快照和 Changed 通知均保持可用。
- 收口目标已调整为“基础移动 Player + 可插拔 FlameModule + 可插拔 ActivityModule”；不能继续把火焰、活动和表情服务当作 Player Core 的必需组件。
- `IPlayerModule`、`PlayerModuleContext` 和可选模块查询已加入 `PlayerCoreHost`；缺少可选服务不会阻止基础 Player Core 就绪。
- 基础输入现在只要求 `Move`、`Look`；`PlayerMovement` 通过 `IPlayerSprintPolicy` 接入可选余火冲刺消耗，Player 没有 FlameModule 时仍可运行基础移动。
- 已新增 `Assets/FirePlay/Runtime/Prefab/PlayerCoreOnly.prefab`：只包含 CharacterController、基础输入、移动、视角和 LocalPlayerContext，不挂火焰、活动、交互、Rest 或表现服务；移动无 FlameModule 时保留无限冲刺作为基础能力。
- 已新增 `PlayerCameraTargetSet` 与 `IPlayerCameraTargetProvider`：Player 只暴露 Follow、Frame、LookAt、InputPivot 四类语义目标，不引用 Cinemachine；完整 Player 和 Core-only prefab 均已配置。
- `PlayerModuleContext.CameraTargets` 可供 Flame/Activity/Presentation 模块读取通用目标；活动专属相机仍由 CameraSystem/ActivityCameraRig 提供额外 TargetGroup、FollowAnchor 和 LookTarget。
- `PlayerCoreOnly.prefab` 已通过静态 Prefab 结构检查和命令行编译；仍需在 Unity Play Mode 中确认移动、视角、重力和缺少可选模块时无启动错误。
- 已新增场景级 `ActivityCameraRig`（`ActivityCameraRigExecutor`），并将 DemoScene 的 `PlayerActivityPresentationHost` 改为请求该执行器；新 Activity 相机 profile 已从旧 `RitualCameraDirector` 的执行入口分离，旧 Rest/观星/旧钓鱼方法暂时保留。
- `ActivityCameraRig` 当前复用已有 Cinemachine 相机、TargetGroup 和稳定 profile ID，行为不变；仍需在 Unity Play Mode 验证烤棉花和钓鱼的进入、退出、镜头优先级与目标组清理。
- Play Mode 验收通过后，再移除 `RitualCameraDirector` 中重复的新 Activity profile 字段，并分别制作 FlameModule、ActivityModule 的独立挂载对象；不直接批量删除已验收功能。
- 在 Player 收口、完整回归和本地/远端生命周期确认前，暂停 Network SDK/Transport 接入。
