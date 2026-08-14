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
  -> LocalPlayerContext / CameraTargetSet / PlayerCameraObstruction
  -> FlameModule / FlameResourceController / PlayerFlameController
  -> Flame.prefab (由场景服务工厂按稳定 PlayerId 创建)

Gameplay_SceneServices
  -> PlayerSceneServiceBindings
  -> Flame 工厂、Activity 相机执行器、网络出生点等按能力独立配置

Gameplay_UI
  -> FirePlayUiBootstrap -> HUD / 活动 UI / 开发期连接窗体

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
- Emote 已有 `expression.*` 动作和远端 Presentation Cue 通道，但正式远端动画资源仍需补齐。

### 目前优先补齐

1. 统一实时/异步共用的互动事实元数据：稳定 `actorId`、目标类型与目标 ID、事件 ID、时间戳、revision、幂等和可重放语义。
2. Host 权威 `marshmallow.give(targetId)`：在线目标校验、产物/余火归属结算、双方事实和表现 Cue。
3. 两个以上可识别的远端表情动画入口，并验证 late-join 不重复播放一次性动作。
4. 本地异步持久化适配边界：保存火堆、小火种和社交互动事实；不引入具体后端 SDK，不复制活动逻辑。

实时与异步不是两套玩法：

```text
实时：actor -> player:<stablePlayerId>
异步：actor -> place:<StableSceneId> / instance:<stableInstanceId>
```

两者共用事实模型；差别只在投递时机和接收方。

## 5. 持续任务目标

在 `SnowValley_Playable` 上完成可验收的联机社交闭环，同时保持单机链路可用：

1. 事实协议收口；
2. Host 权威互发表情；
3. Host 权威互喂棉花糖；
4. late-join 与断线后的状态一致性；
5. 异步本地事实适配器；
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
- 正式 Emote/Marshmallow/Fishing/Guitar 动画、VFX、音效和最终 UI 仍可由美术会话继续制作；玩法只依赖稳定语义 Cue。
- 冰面破坏、路径 RenderTexture 等表现状态尚未纳入存档或网络同步；后续只同步稳定 ID + 事件/位集，不传整张 RenderTexture。
- `ColorSource`、`RestorableNode` 属于旧实验视觉，不再扩展颜色解谜；清理前先检查场景引用。

## 8. 文档入口

- `FirePlay_核心架构.md`：稳定职责边界、运行链路、Player/场景组装和活动接入。
- `FirePlay_美术技术开发提示.md`：场景、Shader、模型、动画、VFX、音频和 UI 约束。
- `FirePlay_联机测试清单.md`：NGO + UTP 配置和分层联机验收。
- 本文：当前基线、持续目标和待验收项。

下一步从“互动事实协议 + 动作目标字段”开始，不先接入后端，也不新增第二套活动状态机。
