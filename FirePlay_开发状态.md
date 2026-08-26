# FirePlay 当前开发状态

> 本文是每次开发任务的第一入口，只记录当前事实、接线、风险、验收和下一步。历史过程以 Git 提交为准。

## 1. 当前基线与范围

- Unity `6000.5.5f1`、URP `17.5.0`、Input System `1.19.0`；目标平台为 Windows 与 Android。
- 当前正式接入场景是 `Assets/Scenes/SnowValley_Playable.unity`。后续玩法、火焰和联机接入默认只改这个场景。
- `DemoScene` 只作为 NGO/UTP 配置与回归参考，不复制其层级到 SnowValley，也不把美术 LookDev 场景当作运行入口。
- 单机必须始终可运行；实时联机使用 NGO + UTP、Host 权威；异步只保存实时玩法产生的事实，不维护第二套玩法逻辑。
- 当前仓库最新基线提交：`5ef67f0 接入FlameModule`。不要用破坏性 Git 操作覆盖用户版本。

## 2. 当前可依赖能力

| 模块 | 当前状态 |
|---|---|
| Player Core | `PlayerCoreOnly` 提供输入、移动、Look、基础相机目标、碰撞和角色表现；核心依赖显式存在于 Prefab/场景，不用运行时隐藏装配 |
| 火焰系统 | 玩家余火、公共篝火、SmallFire、余火源、世界树和存档各自拥有状态权威；余火通过 `TryConsume/Restore` 原子结算 |
| 活动系统 | `Definition + Catalog + Anchor + Rules + Logic/Factory + Session + Form + Visuals`；地点组合活动，玩法逻辑不复制 |
| 已有活动 | Marshmallow、Fishing、Guitar、Emote、Rest/Stargazing 均有独立 Logic/UI/表现入口；Stargazing 是共享休息，不是计分小游戏 |
| 网络基础 | NGO/UTP、Host 权威入口、活动意图/事实、远端镜像、late-join 快照，以及火焰/篝火/SmallFire/世界树网络适配已存在 |
| 雪景表现 | SnowValley 已有雪地、冰面/水体交互、场景热场、植被、角色模型/动画基础、相机防穿模和 HUD 接入 |

## 3. SnowValley 当前场景接线

场景中的主要对象和职责如下：

```text
Player_Core (PlayerCoreOnly + 场景级 Flame override)
  -> FirePlayPlayerInput / PlayerMovement / PlayerLook
  -> LocalPlayerContext / CameraTargetSet
  -> FlameModule / FlameResourceController / PlayerFlameController
  -> Flame.prefab (由场景服务工厂按稳定 PlayerId 创建)

Gameplay_SceneServices
  -> PlayerSceneServiceBindings
  -> Flame 工厂、Activity 相机执行器、网络出生点等按能力独立配置

Gameplay_UI
  -> FirePlayUiBootstrap -> HUD / 活动 UI / 开发期连接窗体

NetworkWorldState.prefab（Host/Server 启动后生成）
  -> NetworkObject / FirePlayNetworkWorldState
  -> 统一同步预放置 FlameSource 与公共 Campfire

SnowValley 环境
  -> 场景热场、WarmthNode、冰面/水体交互、雪景光影与表现
```

- Player 不持有 Cinemachine Profile；活动相机、TargetGroup 和镜头恢复由场景级执行器处理。
- `PlayerNetworkBase` 不放入场景；正式网络对象使用 `PlayerNetworkGameplay`，由 `NetworkManager` 生成。
- 新功能优先以显式模块或场景服务接入，不在 `Awake` 中 `AddComponent` 隐藏补依赖。

## 4. 联机现状与待补闭环

### 已有

- `FirePlayNetworkBootstrap` 是唯一 Host/Server/Client 启动入口。
- 客户端发送稳定意图，Host 预检、裁决和执行，之后广播状态事实；客户端只根据稳定 ID 驱动本地表现。
- 活动 Session、交互、状态变化、结束事实和 late-join 快照已接入 `PlayerActivityHost` 与网络层。
- Emote 已有 `expression.*` 动作和远端 Presentation Cue 通道；本地成功播放后由 Host 转发 `expression.wave` / `expression.thanks`，一次性 Cue 不进入 late-join 快照。

### 本里程碑目标（代码已落地，待 Unity 验收）

1. 统一实时/异步共用的互动事实元数据：稳定 `actorId`、目标类型与目标 ID、事件 ID、时间戳、revision、幂等和可重放语义（已落地）。
2. Host 权威 `marshmallow.give(targetId)`：在线目标校验、产物/余火归属结算、双方事实和表现 Cue（已落地）。
3. 两个以上可识别的远端表情动画入口，并验证 late-join 不重复播放一次性动作（已落地，待运行验证）。
4. 本地异步持久化适配边界：保存火堆、小火种和社交互动事实；不引入具体后端 SDK，不复制活动逻辑（已落地）。

### 当前接续里程碑：联机社交闭环代码（代码已完成，待 Unity 重载/网络验收）

- 新增 `ActivityFactProtocol.cs`：`ActivityTargetKind` 支持 `player/place/instance`，`ActivityTargetReference` 只承载稳定 ID，`ActivityFactMetadata` 承载 `ActorId/EventId/OccurredAtUnixMs/FactRevision`。
- `ActivityActionRequest`、`ActivityActionRequestDto`、`ActivityActionRequested` 和 `ActivityFactDto` 已带动作目标与 EventId；Host 发布的 Session/Interaction/State/Ended 事实会生成稳定元数据并沿 RPC/late-join 传播。
- `PlayerActivityHost` 已对动作目标形状、在线 Player 目标、Session revision 和重复 EventId 做统一入口校验；镜像端按 EventId 幂等过滤。
- `FirePlayNetworkProtocol.CurrentVersion` 当前为 `7`；PC/Android 必须使用同一版本。版本 3 增加独立社交接收事实及连续活动状态的实时/可靠双通道；版本 4 增加 Host 真正拒绝位姿时才发送的定向 Owner 校正事实；版本 5 引入场景世界快照，版本 6 将其收口为 Host 显式生成的注册 Prefab；版本 7 修复 NGO SmallFire 注册 Prefab 根对象为 Inactive 导致 Client 生成隐藏实例的问题。
- `marshmallow.give` 已由 Host 原子执行：只有已完成棉花糖、在线且可接收的 Player 才能接收；目标必须能容纳完整余火返还，否则整笔拒绝，不发生钳制式部分恢复。目标余火恢复和源 Logic 清除在同一 Host 调用链中完成。成功时发送者产生 `marshmallow.give` Session 事实，接收者产生不依赖活动 Session 的 `marshmallow.receive` 社交事实，并收到 `marshmallow.receive` 动画 Cue 与 `marshmallow.receive.vfx` 语义 Cue；一次性接收事实不进入 late-join 快照。
- `MarshmallowActivityForms.prefab` 已显式加入 `TargetButton + GiveButton`：两人联机自动选择唯一伙伴，三至四人时点击目标按钮按稳定 PlayerId 顺序切换；控件与玩法 Form 序列化绑定，不在运行时动态创建。
- `PlayerNetworkBase.prefab` 已显式挂载 `PlayerExpressionController`；`PlayerNetworkGameplay` 变体会继承该依赖，不通过运行时 `AddComponent` 补齐。
- `DefaultNetworkPrefabs.asset` 已移除误注册的技术父资产 `PlayerNetworkBase`；当前只注册正式 `PlayerNetworkGameplay`、SmallFire、Campfire、Tree 和 `NetworkWorldState`。父资产仍仅用于 Prefab 继承，不能被 NGO 直接生成。
- SnowValley 的 `FirePlayNetworkBootstrap` 已显式绑定 `NetworkWorldState.prefab`：Host/Server 启动成功后生成唯一 `NetworkObject + FirePlayNetworkWorldState`，统一同步预放置 FlameSource 与公共 Campfire。美术篝火本身继续只保留 `Campfire + StableSceneId`，不会为每个场景道具堆 NetworkObject；SmallFire 升级出的运行时 Campfire 仍走自己的 `FirePlayNetworkCampfire`，两条链路按 `IsRuntimeCreated` 严格分开。
- `PlayerExpressionController` 在本地校验并播放成功后发布 `PlayerExpressionPlayed`；网络 Player 仅允许 `wave` / `thanks` 两个稳定 Cue，由 Host 排序并发送给观察者。远端仍只通过 Presentation RPC 执行动画，不打开对方 UI、不切换本地 Camera、不锁本地移动；一次性表情不缓存给 late-join。
- 新增 `IAsyncInteractionFactStore` 与 `LocalAsyncInteractionFactStore`：活动交互事实自动记录，篝火/SmallFire/WorldTree/小火种放置提供同一稳定元数据的本地 Append 边界；不维护第二套玩法状态机，也不绑定后端。
- 已检查当前真实落盘文件 `fireplay-async-facts.json`：现有 54 条活动交互记录全部带稳定 ActorId、EventId、时间戳和 revision，重复 EventId 与无效元数据均为 0；普通无目标活动以 `TargetKind=None` 保存，格式与实时 `ActivityFactDto` 一致。持久化适配器现会在写入和重新加载时同时校验 stable ID、TargetKind/TargetId 联合形状、action/activity 字段及 payload 上限；Campfire/SmallFire/WorldTree 事实必须带 place 或 instance 等明确目标。世界事实与 `marshmallow.receive` 的实际新增记录仍待对应联机动作验收后检查。
- 代码阶段未修改 `DemoScene`、`ArtScene` 或美术会话的模型、Shader、动画和音频资产；本次单独的场景接入只修改 `SnowValley_Playable` 的 Gameplay/Camera/Network 绑定。
- `SnowValley_Playable` 已完成第一轮显式场景绑定：`Gameplay_SceneServices` 现在指向 `Gameplay_ActivityServices/ActivityLogicRegistryBehaviour`、`Gameplay_ActivityCameraRig/ActivityCameraRigExecutor`、玩家 Flame Prefab 和 `NetworkPlayerSpawnPoint`；`Gameplay_NetworkBootstrap` 同一对象上持有 `NetworkManager + UnityTransport + FirePlayNetworkBootstrap`，默认 `Auto Start=Manual`、`127.0.0.1:7777`，Player Prefab 指向 `PlayerNetworkGameplay`，Network Prefab 列表指向 `DefaultNetworkPrefabs.asset`。活动相机已提供 `activity.ritual`、`custom.fishing`、`activity.stargazing` 三个稳定 Profile，并绑定到 Player 语义目标；本轮没有复制 DemoScene 的相机或地点对象。
- Player Build 入口已从旧 `DemoScene` 切换为唯一启用的 `SnowValley_Playable`；Windows/Android 构建不会再因 `EditorBuildSettings` 遗留配置进入错误场景。SnowValley 的 Bootstrap 保持 `Manual`，Windows 双实例通过 `--fireplay-mode host/client` 启动参数选择角色，单机无参数启动仍保持原链路。
- 相机链路已收口为：`Gameplay_CameraOutput` 是场景唯一启用的 `Camera + AudioListener + CinemachineBrain`，PlayerCoreOnly 的子 Camera 在 SnowValley 场景实例中显式禁用；Explore/Activity 使用场景 Cinemachine 虚拟相机，直接跟随 Player 语义目标，避免 Player CameraPivot 与 Cinemachine 双重跟随造成跳动/远距。探索相机的位置和旋转阻尼为 0；单机与网络 `PlayerCameraFollowTarget` 均统一为相对 CameraPivot `(0, 1, -6)`。活动相机保留轻微阻尼用于进入/退出过渡。`LocalPlayerContext._localCamera` 也绑定到场景输出 Camera。
- Explore/Activity 两台 Cinemachine Camera 均显式挂载 `CinemachineDecollider`：`TerrainResolution` 使用 0.28 Camera Radius、12m 向下检测和 0.4s 恢复阻尼，防止镜头进入雪地/冰面下方；碰撞时仍立即推出以避免穿模，解除碰撞后的回位改为平滑恢复。用户已在 Play Mode 确认此前版本镜头不穿地、不抖动；本次 0.4s 回位手感待用户验收。FollowTarget 不再重复挂 `PlayerCameraObstruction`；该脚本只保留在 `PlayerCoreOnly` 自带 Camera 的非 Cinemachine 独立测试路径。普通障碍层 Decollision 暂未开启，因为当前 Player 与环境都在 Default 层，启用会把 Player 自身误判为障碍。墙体遮挡的正式收口应先建立独立 Environment/Player Layer，不在本阶段用名称或 Tag 猜测。
- 活动选择窗体已明确配置为 SUIFW 的透明 `PopUp + ReverseChange`：窗体与输入遮罩位于常驻 `Fixed` HUD 上方，避免 HUD 按钮覆盖轮盘、截获点击。活动条目仍由同一 Catalog 与附近 Anchor 生成，不新增活动状态机或硬编码玩法按钮。代码与 Prefab 静态绑定已检查，Play Mode 中的条目数量、文字、选择和关闭待用户验收。
- 活动选择窗体已从“普通 Button 围成一圈”升级为无贴图的真实径向控件：`ActivityRadialWheelGraphic` 按当前可用活动数量绘制透明环形分区，鼠标/触摸方向实时高亮，在同一分区按下并松开即可选择；动态 Button 仅作为文字槽位与无障碍点击入口。根面板和 Lucency 输入遮罩均不再视觉压暗场景。活动来源仍是 Catalog + 附近 Anchor，轮盘没有写死玩法；当前透明文字槽位版的 Play Mode 布局、方向选择与点击已由用户验收通过，正式活动图标仍待美术资源。
- 吉他窗体同样已明确配置为透明 `PopUp + ReverseChange`，修复键盘能演奏但 `Fixed` HUD 截获鼠标/触摸点击的问题；21 个琴键仍由独立 Guitar Form 映射到同一 `guitar.key.*` 活动动作，未改玩法 Logic。
- 吉他琴键的鼠标/触摸输入已改为 `GuitarActivityForm` 内按键 RectTransform 直接命中并在按下时提交，避免依赖 Button 抬起事件及外层 Graphic Raycast 顺序；键盘仍提交完全相同的 `guitar.key.*` 动作。Form 被其他 PopUp 冻结时停止接收所有琴键输入，恢复到栈顶后再启用。21 键 Target Graphic 也会在 Display 时显式校正；鼠标/触摸与键盘演奏已由用户在 Play Mode 验收通过。
- SUIFW 的 PopUp 遮罩已兼容 SnowValley 使用的 Screen Space Overlay Canvas：停用或不存在独立 `UICamera` 时不再在 `UIMaskMgr.Awake/CancleMaskWindow` 空引用；`UIMaskMgr` 已显式挂在公共 `Canvas.prefab/_UIScripts`，不再首次打开 PopUp 时动态 `AddComponent`。`CloseOrReturnUIForms(name)` 也已按指定窗体出栈；活动选择成功并同步打开玩法 UI 后，只关闭活动轮盘，不会误关新打开的 Guitar/Fishing/Marshmallow Form。该项命令行编译通过，实际打开、选择、返回的堆栈行为待用户在 Play Mode 验收。
- 小火种放置不再打开旧的全屏 `FirePlayPlacementPanel`：该控制器及 Canvas 绑定已删除，遗留 Background 固定停用。`PlaceFire` 仍统一走 `FirePlayPlayerInput -> InteractionRouter -> CampfirePlacement`，键盘 F 或 HUD 的同一按钮第一次进入世界预览、第二次确认，不复制放置逻辑。
- SnowValley 的 `00_Snow_Grand_Ground` 已显式挂载 `CampfirePlacementSurface` 作为区域父标记，其子级雪地主碰撞体可用于小火种预览与放置；冰湖、水体和装饰物没有被整体放开。此前该场景完全缺少放置 Surface，导致输入已进入 `CampfirePlacement` 但射线始终返回 `No ground`。`CampfirePlacementSurface` 现在允许挂在 Collider 本体或区域父节点，便于大块组合地形显式配置；运行时仍按命中的 Collider 向父级查找标记。
- 小火种预览不再只依赖第三人称相机屏幕中心射线：中心命中在玩家放置距离内时使用该点，否则向玩家前方 2.5 米做向下地面探测；预览实例放大到 1.75 倍并抬离雪面 0.16 米，仍禁用 SmallFire 状态组件、Collider 和 Light，因此不会占用火种数量或参与世界状态。预览/确认使用 Player 根位置做同一距离校验；SnowValley 的预览可见性、确认放置与资源行为已由用户在 Play Mode 验收通过。
- HUD 已将已有键盘绑定直接写入按钮提示：`交互 [E]`、`跳跃 [Space]`、`休息 [R]`、`放置小火种 [F]`；吉他 Form 继续在每个琴键显示 Q/W/E… 与对应音名。未为活动轮盘虚构键盘绑定，因此轮盘按钮暂不显示快捷键。
- 网络带宽已分层：Owner 位姿 20Hz 不可靠上行/下行，0.5 秒可靠 `NetworkVariable` 校准；余火最多 10Hz 且变化小于 0.05 不发送；Session、动作、结束、表情和社交事实可靠传输；棉花糖指针/钓鱼仪表等连续状态走不可靠实时快照，并保留 0.5 秒可靠最终校准与 late-join 可靠快照。这样连续表现不会阻塞可靠 Gameplay/Social 队列。
- 单机/联机角色交接由场景中显式的 `StandalonePlayerNetworkHandoff` 完成：网络 Starting 时释放单机 Camera 与身份并停用 `Player_Core`，网络失败/停止后恢复；不通过运行时 `AddComponent` 或第二个输入 Player 兜底。
- 公共 `Canvas.prefab` 的 `EventSystem` 已显式挂载 `InputSystemUIInputModule`，并序列化绑定 Input System 包提供的默认 UI Actions；`FirePlayUiBootstrap` 只检查依赖，不再用运行时 `AddComponent` 隐藏补齐。活动、篝火和移动端按钮的指针/触摸入口因此可以直接在 Prefab Inspector 中检查。
- 本轮场景 YAML 与编辑器脚本均已写入，但当前 Unity 编辑器实例占用该项目，无法由命令行重新打开并执行场景重载；因此 `SnowValley_Playable` 的 Prefab 引用解析、场景 CameraOutput/Cinemachine Brain 实际输出、单机活动相机切换和 NetworkManager Inspector 状态仍标记为“待用户验收”，没有宣称 Play Mode 通过。若 Unity 重载后发现手工 YAML 未被编辑器接受，可在关闭其他 Unity 实例后执行 `FirePlay/Scene Integration/SnowValley/Configure Scene Bindings`，该命令只打开并保存正式 SnowValley 场景。
- 代码检查：`dotnet build Assembly-CSharp.csproj --nologo --no-restore` 与 `dotnet build Assembly-CSharp-Editor.csproj --nologo --no-restore` 均为 0 错误、0 警告；Unity 重新导入新脚本后仍需用户验证 RPC 序列化、远端表情、互喂棉花糖与单机活动回归。

实时与异步不是两套玩法：

```text
实时：actor -> player:<stablePlayerId>
异步：actor -> place:<StableSceneId> / instance:<stableInstanceId>
```

两者共用事实模型；差别只在投递时机和接收方。

## 5. 持续任务目标

在 `SnowValley_Playable` 上完成可验收的联机社交闭环，同时保持单机链路可用：

1. 事实协议收口（代码完成）；
2. Host 权威互发表情（代码完成，待 Unity 联机验收）；
3. Host 权威互喂棉花糖（代码完成，待 Unity 联机验收）；
4. late-join 与断线后的状态一致性（待 Unity 验收）；
5. 异步本地事实适配器（代码完成，待检查本地 JSON）；
6. PC Host、PC/Android Client 分层验收。

不在当前范围内：Lobby、Relay、匹配、排行榜、语音、自由留言、产品级账号系统，以及任何新的兼容层。

## 6. 每个里程碑的验收要求

- 先静态编译，再在 Unity 中验证；不能用“理论可行”代替 Play Mode 结果。
- 单机回归：移动/跳跃、相机恢复、余火、篝火、SmallFire、世界树、活动轮盘、Marshmallow、Fishing、Guitar、Emote、Rest/Stargazing。
- 联机回归：Host/Client 启动、双方移动、余火与世界对象权威、活动 Owner/Observer、目标校验、late-join、退出和断线清理。
- 新增网络事实必须能说明：谁产生、Host 如何裁决、如何广播、如何重放、何时幂等丢弃。
- 场景配置必须能在 Hierarchy/Prefab 中找到；活动 UI、相机和表现资源不得偷偷挂到不相关的玩法或 Player 脚本中。

## 7. 已知边界

- PC/Android 双端完整联机验收仍未由用户最终签字；当前网络是直连开发基础，不是产品级房间系统。
- SnowValley 当前已接入玩家余火、场景篝火、SmallFire 放置/培育/回收，并在 `Gameplay_WorldContent` 显式放置三个 `FlameSource` 与一个 `WorldTreeContribution`；基础资源恢复和大树贡献路径已具备可验收内容点。`FlameRecoveryVolume` 仍未作为常驻恢复区接入，避免与一次性路线余火源混淆。
- 正式 Emote/Marshmallow/Fishing/Guitar 动画、VFX、音效和最终 UI 仍可由美术会话继续制作；玩法只依赖稳定语义 Cue。
- 冰面破坏、路径 RenderTexture 等表现状态尚未纳入存档或网络同步；后续只同步稳定 ID + 事件/位集，不传整张 RenderTexture。
- `ColorSource`、`RestorableNode` 属于旧实验视觉，不再扩展颜色解谜；清理前先检查场景引用。

## 8. 文档入口

- `FirePlay_核心架构.md`：稳定职责边界、运行链路、Player/场景组装和活动接入。
- `FirePlay_美术技术开发提示.md`：场景、Shader、模型、动画、VFX、音频和 UI 约束。
- `FirePlay_联机测试清单.md`：NGO + UTP 配置和分层联机验收。
- 本文：当前基线、持续目标和待验收项。

径向活动轮盘、吉他鼠标/触摸输入和 SnowValley 小火种预览/确认已由用户在 Play Mode 验收通过。联机前静态检查已确认 SnowValley 的 `NetworkManager -> UnityTransport`、`PlayerNetworkGameplay`、`DefaultNetworkPrefabs`、Connection Approval、30Hz Tick、Manual Bootstrap 和单机/网络交接引用均已序列化；Runtime/Editor C# 编译为 0 错误、0 警告。下一步需要从当前唯一构建场景生成 Windows Development Build，并按 `FirePlay_联机测试清单.md` 启动 Host + Client；这一步开始属于双进程运行验收，尚未宣称通过。重点验证远端表情、互喂棉花糖、SmallFire 权威生成、late-join 和断线清理。

### PC 双进程验收记录（进行中）

- Windows Development Build 已从 SnowValley 生成，并在本机端口 `7781` 启动独立 Host/Client；测试日志位于 `Build/NetworkTestLogs/host-7781.log` 与 `client-7781.log`。
- 第一版 Windows Build 的自动日志门槛通过：Host 生成 `network.player.0`（本地控制为 true）和 `network.player.1` 远端镜像；Client 生成 `network.player.1`（本地控制为 true）和 `network.player.0` 远端镜像；Host Connection Approval 以协议版本 3 接受 clientId 1，未出现 NGO/RPC/Prefab/空引用错误。该构建随后暴露 Client Owner 周期性追逐 0.5 秒旧可靠快照、造成位置回溯和相机撕扯，已被版本 4 位姿校正链路取代，需重新构建后复验。
- 位姿修复后，Client Owner 不再把普通可靠 checkpoint 当作逐帧校正目标；20Hz 不可靠位姿继续由 Host 校验并广播，0.5 秒可靠快照只服务远端最终一致与 late-join。只有 Host 因速度/距离规则明确拒绝请求时，才向该 Owner 发送一次可靠定向回正，并输出 requested/allowed distance 诊断日志。
- 协议版本 4 的 Windows Build 已在端口 `7782` 完成 Host/Client 复验：双方出生位置、Owner 操作、远端移动和本地相机均正常；用户确认不再出现位置回溯或网络位姿造成的相机撕扯。正常移动期间日志未出现 Host 位姿拒绝或 Owner 定向校正。基础位姿门槛通过，下一项转入余火、SmallFire 与公共篝火的 Host 权威一致性验收。
- 用户同时指出镜头解除地形/冰面阻挡时回位偏突然；本轮将 Explore/Activity 的 `TerrainResolution.Damping` 从 `0.12` 调整为 `0.4`，仅改变表现层回位速度，不修改 Player、活动或网络状态。该参数需要在下一次 SnowValley Play Mode/Windows Build 中验收。
- 进入余火/世界对象验收前的静态盘点发现：SnowValley 预放置主篝火此前没有网络适配器，Client 会保留本地独立状态。协议版本 5 的首次双进程自动门槛确认手工放入初始场景的 NetworkObject 没有被 NGO 自动 Spawn，因此没有执行 WorldState；该失败版本未进入玩法验收。版本 6 改为 Host 显式生成并注册 `NetworkWorldState.prefab`，双窗口启动、协议准入和 WorldState 生成已通过用户验收。
- 版本 6 的 SmallFire 双端验收暴露 Prefab 配置缺陷：Client 的放置请求已到达 Host 且被权威执行，但注册的 `SmallFire.prefab` 根对象原为 Inactive；Host 预备流程手动激活实例，NGO 在 Client 侧生成的实例则保持隐藏，造成只有一端可见。版本 7 已将根 Prefab 显式设为 Active，并让 `CampfirePlacement` 把 Inactive Prefab 视为无效配置、直接报错而不再静默扣余火。Runtime/Editor 编译均为 0 错误、0 警告。
- 协议版本 7 已完成 SmallFire 首项双进程运行验收：Client 放置后 Host 与 Client 均出现同一网络火种，未再出现 Client 隐藏实例。Host 随后的交互日志对 SmallFire 升级和公共篝火添火明确返回 `Not enough fuel`，说明世界命令已进入 Host 权威链路；当前无法继续升级是 SnowValley 尚未布置余火恢复内容点造成的资源条件不足，不是升级网络命令缺失。
- SnowValley 当前的冰纹裂缝仍属于本地场景表现，尚未定义需要持久化/重放的网络事实；构建同时报告 `IcePathCrackField` 缺少 Ice Path Stamp Shader。该项仍属于后续美术表现与短时场景事件设计，不作为世界状态同步失败处理。
- 网络人物表现已通过 Unity 作者配置正式写入：`PlayerNetworkGameplay` 复用现有 `SnowTraveler_Female` Prefab、有效 Avatar 和 Locomotion Controller，旧胶囊 MeshRenderer 显式禁用但碰撞根保留。角色朝向与临时社交动作现已分离为 `CharacterFacingRoot/SnowTravelerVisual`：`PlayerMovement` 只拥有父级世界朝向，`PlayerAnimationController` 只在子级施加短时局部挥手/鞠躬偏移，Locomotion 与 Activity Animation 仍引用同一 Animator。此前按 Q 后表情恢复逻辑持续把模型拉回初始化角度的双写问题已在代码和 Prefab 接线中修复；表情恢复到 authored pose 后会停止写 Transform。远端 Locomotion Bridge 在没有本地输入时从网络位姿变化推导走/跑速度、垂直速度和朝向。现有 Animator 尚无挥手/感谢动作状态，因此网络 Prefab 显式开启仅针对这两个 Cue 的子节点占位表现；美术动作补齐后可在 Inspector 关闭，不改网络协议。Runtime/Editor 编译均为 0 错误、0 警告；最终 Windows Development Build 成功，协议 7 的 Host/Client 已在端口 `7794` 连接且双方 Owner/PlayerId 正常。用户已验收 Q 表情、后续移动/转向和远端表现，人物不再持续拉回固定角度；本项通过。
- `Gameplay_WorldContent` 已由 Unity 放置三个可拾取 `FlameSource`（营地路线、山谷路线、冰湖路线）和一个 `WorldTreeContribution`，稳定 ID 分别为 `snow.flame-source.camp-route`、`snow.flame-source.valley-route`、`snow.flame-source.lake-route`、`snow.world-tree.main`，且每个 ID 在场景中只出现一次。世界树的 In-Scene NetworkObject Hash 已由 Unity 生成；余火源继续由 `NetworkWorldState` 镜像。Runtime/Editor 编译为 0 错误、0 警告；Play Mode 与双进程视觉/交互仍待同一最终构建验收。
- 上述组合已生成一次 Windows Development Build，Unity 报告 `Build Finished, Result: Success`；Host/Client 已在端口 `7793` 以协议 7 连接，双方 PlayerId、Owner 与本地控制权正确，启动日志没有 NGO/RPC/Prefab/Animator/空引用错误。正式模型外观、远端 Locomotion、三个余火源、世界树、小火升级、表情及活动社交仍标记为“待用户在当前双窗口连续验收”。
- 版本 7 同轮静态收口继续修复了四个联动隐患，不再要求逐项重构建：`Campfire.prefab` 不再把网络 `SmallFire.prefab` 资产误当阶段视觉对象；一次性表情/棉花糖接收 Cue 不再替换活动表现 Session 或破坏 Fishing/Guitar 的移动锁恢复；Client 活动动作新增 Host 最终结果 RPC 与 `ActivityActionAuthorityResolved`，棉花糖窗体可显示目标离线、容量不足、重复或旧 revision 的明确原因；异步动作/世界 EventId 加入单次权威运行 nonce，重启 Host 后不会与旧本地事实序号碰撞。
- 协议 7 的最小 late-join/断线门槛已在端口 `7795` 完成：Host 与首个 Client 先放置网络 SmallFire 并播放一次 Q 表情，随后第三个 Client 以 `network.player.2` 晚加入；用户确认它立即看到已有持续世界状态且没有补播此前的一次性表情。关闭测试窗口时，Host 依次收到 `ClientDisconnected/PeerDisconnected`，剩余 Client 收到对应 `PeerDisconnected`，各端正常注销 `IInteractionRouter`、`IPlayerIdentityService`、`IPlayerSceneServiceBindings` 和网络会话服务，日志未出现异常。此项只代表开发期最小一致性门槛通过，不包含自动重连或恢复原活动 Session。
- Host 现在也会按 Player 当前探索状态校验 `wave/thanks` 表情；Observer 仍只执行动画/VFX。无正式 Animator 的网络占位模型对 `wave` 使用左右摆动、对 `thanks` 使用鞠躬，两个远端入口可肉眼区分；正式 Animator 继续走原有 `EmoteWave/EmoteThanks` Trigger，不需要改网络代码。
- 本轮统一静态门槛已通过：所有 NGO 注册运行时 Prefab 根对象为 Active，`PlayerNetworkBase` 未被误注册，SmallFire/NetworkWorldState 的场景与 Prefab 引用一致；Runtime/Editor C# 均为 0 错误、0 警告。协议版本 7 的 Windows Host/Client 已完成连接准入、Player Owner/身份和 Client 放置 SmallFire 的双端可见性验收；其余社交、活动、late-join 与断线项目仍待同一构建连续验收。
- 构建日志仍报告 `IcePathCrackField` 缺少 Ice Path Stamp Shader；该警告属于现有美术资源边界，没有阻断网络启动，本联机阶段不修改 Shader。
