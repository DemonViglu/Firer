# FirePlay 核心架构

> 本文只记录长期稳定的职责边界、运行链路和组装方法，不记录迁移历史。当前进度与待验收项见 `FirePlay_开发状态.md`。

## 1. 核心系统

FirePlay 由四条边界清楚的链路组成：

1. **角色基础**：输入、移动、朝向和语义相机目标；
2. **火焰循环**：玩家余火、公共篝火、小火种、余火源、世界树和存档；
3. **活动内容**：地点能做什么、活动规则、独立 UI、道具、动画、音频和 VFX；
4. **实时联机**：客户端意图、Host 裁决、状态事实和远端表现。

跨模块语义请求通过 `GameEventBus` 传递；全局服务通过 `GameInstanceSubsystem` 显式定位。逐帧移动、镜头输入和活动内部 Tick 不经过事件总线。

## 2. Player 资产与模块

| 资产 | 用途 | 使用规则 |
|---|---|---|
| `PlayerCoreOnly.prefab` | 纯角色基础：输入、移动、Look、`LocalPlayerContext`、语义 Camera Targets 和碰撞体 | 新实验角色的起点；不含 Flame、Activity、Rest、Interaction 或 Network |
| `PlayerFlameModule.prefab` / Flame scene override | 可选火焰能力：余火、玩家火苗归属、小火种/公共火命令与火焰表现桥 | 推荐作为可见子模块挂到 Player Core 下；场景副本也可通过 Prefab Override 显式添加同一组组件。不能脱离 `LocalPlayerContext` 单独运行 |
| `Player.prefab` | 完整单机 Gameplay 组合 | `DemoScene`、`ArtScene` 和单机内容场景直接使用 |
| `PlayerNetworkBase.prefab` | NGO、网络身份及权威 Flame 的技术父资产 | 不直接放入场景，也不单独注册为可生成对象 |
| `PlayerNetworkGameplay.prefab` | 正式网络 Player | 只由 `NetworkManager` 生成；在 `DefaultNetworkPrefabs` 注册 |

Player 的可选能力放在可见子层级：

- `FlameModule`：余火状态与火焰命令；
- `ActivityModule`：`PlayerActivityHost`、活动表现转发和通用活动能力；
- `RestModule`：坐下、起身和共享休息状态；
- `InteractionModule`：世界扫描、交互路由和动作入口。

具体的棉花糖、钓鱼、吉他等玩法 Logic 不挂在 Player 上。核心服务缺失时应明确报配置错误，不允许运行时用 `AddComponent` 隐式补齐。

`PlayerCameraTargetSet` 只暴露跟随、观察等语义目标，不引用 Cinemachine。基础探索相机可使用 `PlayerCameraObstruction` 处理地形/墙体遮挡，它只调整相机臂距离，不控制 Look、Player 朝向或活动镜头。场景中的 `ActivityCameraRigExecutor` 拥有具体 Cinemachine Profile、TargetGroup 和活动机位；`PlayerActivityPresentationHost` 负责把活动请求转给它。

## 3. 活动模型

| 概念 | 职责 |
|---|---|
| `ActivityDefinitionAsset` | 稳定 Activity ID、作用域、UI Key、Camera Profile、Logic Key 和参与模式 |
| `ActivityCatalogAsset` | 全局可用活动目录 |
| `ActivityAnchorNode` | 稳定地点身份、Region/Tags、可用活动引用和地点规则引用 |
| `ActivityLogic` + Factory | 玩法状态、动作、资源消耗和结果 |
| `ActivitySession` | 一名玩家的一次活动状态、参与者与 revision |
| Activity Form | 玩法自己的 UI 与输入映射 |
| Activity Visuals | 道具、动画 Cue、音频和 VFX 表现 |
| `PlayerActivityHost` | Player 唯一活动宿主、Session 路由与权威入口 |

活动作用域只有三类：

- `Anywhere`：不需要地点，例如表情、随地弹吉他；
- `Anchor`：需要地点，例如篝火旁烤棉花、池边钓鱼；
- `Targeted`：需要明确稳定 `TargetId`，例如把产物交给另一名玩家。

“必须坐下”“游泳时不能飞”“需要余火”等不是写死在活动定义里的状态字段，而是规则提供者读取玩家、地点、目标和系统状态后给出的结果。

Stargazing 是共享休息和放松方式，不是计分小游戏。它可使用轻量 Activity Session 管理 resting 前提、参与者、Camera Profile、伙伴 TargetGroup 与对称退出，但不拥有独立玩法循环或专属玩法面板。

## 4. 地点、规则与稳定 ID

同一活动定义可被多个地点复用；一个地点也可以组合多个活动。地点只配置：

- `AnchorId`：优先使用同对象 `StableSceneId.Value`；运行时对象使用稳定的权威 ID；
- `RegionId` 与 Tags：区域归类和规则筛选，不是活动实例 ID；
- Activities：可复用的 `ActivityDefinitionAsset` 引用；
- Rule Provider Behaviours：显式引用 `IActivityRuleProvider`。

活动可读取只读上下文中的 `PlayerId`、`ActivityId`、`AnchorId`、Location、PlayerState、Target、Rules、Flame 和 Participants，但不能持有 Anchor 组件、UI、Camera、Animator 或移动组件。活动不能在规则计算后私自覆写规则结果。

规则评估无副作用，默认优先级为：

| 优先级 | 来源 |
|---:|---|
| 0 | 活动默认规则 |
| 100 | Anchor / Region 规则 |
| 200 | Player 状态与能力规则 |
| 300 | Target 规则 |
| 1000 | 系统安全与资源保护 |
| 2000 | Host 权威规则 |

裁决固定为：Host Deny 最终拒绝；否则取最高优先级 Override；没有 Override 时在最高适用层内 Deny 优先；没有任何 Allow 时默认拒绝。

## 5. Session 与表现生命周期

```text
Requested
  -> Preflight（只检查，不扣资源、不打开 UI）
  -> Prepare（生成无副作用的开始计划）
  -> Commit（唯一提交点，可原子 TryConsume）
  -> Running
  -> Completed / Switched / Interrupted
  -> Ended
```

- 切换活动时，新活动成功提交后才结束旧 Session；
- 离开 Anchor、状态改变、目标失效、资源失败或 Host 拒绝都必须给出明确结束原因；
- 默认每名玩家拥有独立 Session；只有定义为 `SharedGroup` 或 `TargetedInteraction` 时才共享局部状态；
- Session 结束事件只发布一次，并对称关闭 UI、退出活动相机、释放移动/朝向请求和结束持续表现；
- Owner 可以获得本地 UI、Camera 与输入锁；Observer 只能收到远端道具、动画、音效和 VFX 事实。

活动只发语义请求：

- `ActivityUiRequest`：打开或关闭自己的 Form；
- `ActivityCameraRequest`：请求 Camera Profile、目标或恢复；
- `ActivityPlayerRequest`：请求移动锁、Look Target、Animation State/Cue 或 VFX Cue。

实际的 `UIManager`、Cinemachine、移动和动画组件由 Player/场景表现执行器控制。

## 6. 运行链路

```text
活动轮盘 / 活动 UI / 输入
  -> GameEventBus: ActivitySelectionRequested / ActivityActionRequested
  -> PlayerActivityHost
  -> ActivityRuntime / ActivitySystem
  -> Definition + Anchor + Rules + PlayerState + Flame
  -> Preflight -> Prepare -> Commit
  -> ActivitySession
  -> Started / Interaction / Ended facts
  -> Form / Visuals / PlayerActivityPresentationHost / Network
```

```text
Player / Campfire / Activity command
  -> FlameResourceController 或 Host 世界命令
  -> Flame / Campfire / SmallFire / WorldTree 权威状态
  -> Visuals / HUD / Save / Network Snapshot
```

活动只能通过 `IActivityFlameResource.TryConsume/Restore` 消耗或返还余火。余火不足不得部分扣除。篝火成长、小火种上限、世界树贡献与存档属于火焰/世界系统，不属于具体活动。

## 7. 新活动的最小接入

1. 创建稳定 `ActivityId` 和 `ActivityDefinitionAsset`；
2. 编写独立 `ActivityLogic` 与 `IActivityLogicFactory`，在 Registry 注册；
3. 创建该玩法自己的 UI Prefab/Form；复杂玩法可使用独立 Input Action Map；
4. 创建 Visuals，监听 Session 状态和语义 Cue；正式动画、音频、VFX 缺失不应让 Logic 失效；
5. `Anchor` 活动把定义引用到地点的 `ActivityAnchorNode`；`Anywhere` 活动不需要伪造 Anchor；
6. 地点的特殊优先级或进入条件写成独立 `IActivityRuleProvider`；
7. 余火只在 Commit 或明确动作阶段调用 `IActivityFlameResource`；
8. 镜头、移动锁、朝向和动画只发 Presentation Request；
9. 先做单机纵向验收，再复用同一 Logic 接入 Host DTO，不写第二套联机玩法逻辑。

## 8. 新场景的最小组装

1. 放入一个明确用途的 Player：纯实验用 `PlayerCoreOnly`，完整单机用 `Player`，网络场景由 `NetworkManager` 生成 `PlayerNetworkGameplay`；
2. 配置 EventSystem、SUIFW/UI Bootstrap 与活动目录/Logic Registry；
3. 放入主相机和场景级 `ActivityCameraRigExecutor`，把 Camera Profiles/TargetGroup 绑定在场景执行器，不塞回 Player；
4. 世界持久化或同步对象配置 `StableSceneId`；活动地点配置 `ActivityAnchorNode` 与活动定义引用；
5. 篝火、余火源、小火种和世界树继续使用各自独立的状态组件，不复制状态到活动；
6. 先验收移动、余火、活动打开/退出与相机恢复，再添加美术、动画和网络表现。

`PlayerSceneServiceBindings` 的活动执行器、相机执行器、玩家火苗工厂和网络出生点是彼此独立的可选场景服务。实验场景可以只配置当前模块需要的字段；例如只接 Flame 时，只需显式配置玩家火苗 Prefab，不得为了满足统一 `IsReady` 把 Activity 或 Network 占位对象一起带入。

不要把 `PlayerNetworkBase` 放入场景，不要让完全体单机 Player 与网络生成 Player 同时成为本地输入拥有者，也不要通过复制 Canvas/Camera 解决缺失配置。

## 9. 实时联机边界

```text
Client intent
  -> Host 预检、裁决与执行
  -> Host 发布 Started / Interaction / Ended DTO 和世界快照
  -> Client 通过稳定 ID 驱动本地表现
```

网络层只运输稳定 DTO 和不透明活动状态，不传输 Unity 对象或 `ActivityLogic` 实例。`PlayerActivityHost` 是本地 EventBus 与网络权威共用的活动入口。异步功能未来只保存实时玩法产生的必要事实，不维护另一套兼容逻辑。

### 实时/异步共用互动事实

所有可被伙伴观察、重放或异步保存的社交互动，都应抽象为同一种事实记录。最小字段为：

- `ActorId`：稳定的行为发起者身份；
- `TargetKind + TargetId`：目标联合类型，至少支持 `player`、`place` 和 `instance`；
- `EventId`：幂等键，重复投递只能产生一次效果；
- `OccurredAt`：Host 产生事实的时间戳；
- `Revision`：同一权威状态流中的单调版本；
- `Payload`：活动自己的不透明数据，必须经过 Host 校验。

实时通道把事实即时投递给在线 Player；异步通道只负责保存、查询和重放同一事实。两条通道不能各自实现一套“赠送”“投火”或活动状态机。

当前协议迁移期间，已有 Activity DTO 可以继续运行；新增社交动作必须优先使用上述元数据和稳定目标，不得把 Unity 对象引用、层级路径或临时 Instance ID 写进 payload。

`NetworkConnectionForms` 当前是开发期直连 Client 工具：可以配置地址/端口、连接和断开，但不是正式房间 UI，也不负责启动 Host。具体配置与验收顺序见 `FirePlay_联机测试清单.md`。

## 10. 不变量

- 地点组合活动，但不复制玩法逻辑；
- 每个独立玩法拥有自己的 Logic、UI 和 Visuals；
- Activity 不直接控制 Player、UI、Camera 或 Network；
- 火焰资源只有一个权威状态源；
- 存档和同步 ID 不使用 GameObject 名称、层级路径或 Unity Instance ID；
- 核心 Player 组件必须在 Prefab 中可见，禁止动态隐藏依赖；
- 所有 Session 结束必须可解释、幂等且可由网络事实重放。
