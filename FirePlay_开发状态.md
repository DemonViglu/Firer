# FirePlay 当前开发状态

> 所有会话必须先读本文。本文只记录当前事实，不记录重构过程；历史以 Git 为准。

## 1. 当前结论

- 当前稳定基线提交：`52dc9a3 联机机制补充`。
- 唯一正式接入场景：`Assets/Scenes/SnowValley_Playable.unity`。
- SnowValley 的单机玩法、PC 直连联机、Host 权威 SmallFire、网络角色表现、最小 late-join 与断线清理门槛已经可运行并由用户验收。
- 当前架构已经足够继续做内容、美术、动画和具体伙伴互动；**不要再主动发起 Player、Activity、Camera 或 Network 的无目标重构**。
- 下一阶段主线是游戏内容与表现。网络层只在具体玩法需要新增事实，或出现可复现同步错误时增量修改。

## 2. 当前可依赖能力

| 模块 | 当前状态 |
|---|---|
| Player Core | 输入、移动、跳跃、Look、Camera Target、碰撞和角色表现已拆分；核心依赖在 Prefab/Hierarchy 显式可见，不用运行时 `AddComponent` 隐藏装配 |
| Camera | `Gameplay_CameraOutput + CinemachineBrain` 是 SnowValley 唯一输出；Explore/Activity Camera、TargetGroup、地形防穿透与恢复链路已接入 |
| 火焰循环 | 玩家余火、公共篝火、SmallFire、三个 FlameSource、世界树和本地存档均已接入；资源通过 `TryConsume/Restore` 原子结算 |
| 活动系统 | `Definition + Catalog + Anchor + Rules + Logic/Factory + Session + Form + Visuals` 已稳定；地点组合活动但不复制玩法逻辑 |
| 已有活动 | Marshmallow、Fishing、Guitar、Emote、Rest/Stargazing 有独立逻辑或表现入口；Stargazing 是共享休息，不是独立计分小游戏 |
| 实时联机 | NGO + UTP 直连、协议 7、Host 权威位姿/世界/活动事实、远端镜像和 late-join 快照已接入 |
| 异步边界 | `IAsyncInteractionFactStore + LocalAsyncInteractionFactStore` 保存与实时相同的互动事实；没有第二套玩法逻辑，也未绑定后端 SDK |
| 雪景表现 | 雪地、冰面/水体交互、场景热场、植被、正式网络角色模型、基础 Locomotion、HUD 与透明活动 UI 已存在 |

## 3. SnowValley 组装关系

```text
Player_Core（单机）
  -> FirePlayPlayerInput / PlayerMovement / PlayerLook
  -> LocalPlayerContext / PlayerCameraTargetSet
  -> FlameModule / FlameResourceController / PlayerFlameController

Gameplay_SceneServices
  -> PlayerSceneServiceBindings
  -> Activity Logic Registry / Activity Camera Executor
  -> Flame Prefab Factory / Network Spawn Point

Gameplay_CameraOutput
  -> Camera / AudioListener / CinemachineBrain

Gameplay_NetworkBootstrap
  -> NetworkManager / UnityTransport / FirePlayNetworkBootstrap
  -> PlayerNetworkGameplay / DefaultNetworkPrefabs / NetworkWorldState

Gameplay_WorldContent
  -> 3 个 Stable FlameSource
  -> snow.world-tree.main

Gameplay_UI
  -> FirePlayUiBootstrap / HUD / Activity Forms / 开发期直连窗体
```

- 单机运行时使用 `Player_Core`；网络启动后 `StandalonePlayerNetworkHandoff` 停用单机 Player，由 `NetworkManager` 生成 `PlayerNetworkGameplay`。
- `PlayerNetworkBase` 只是 Prefab 技术父资产，不放入场景，也不注册为 NGO 可生成 Player。
- Player 不持有活动 Camera Profile；活动只发 UI、Camera、Player 语义请求，由场景/Player 执行器处理。
- 预放置 FlameSource 和公共 Campfire 由唯一 `NetworkWorldState` 按稳定 ID 同步；运行时 SmallFire/Campfire 使用各自 NetworkObject 适配器。

## 4. 已验收基线

### 单机与内容

- 基础移动、跳跃、相机跟随/恢复和地形防穿透可用。
- 活动轮盘、Marshmallow、Fishing、Guitar、Rest/Stargazing 的既有入口可运行；吉他鼠标/触摸琴键和钓鱼 UI 已修复。
- SmallFire 预览、确认放置、余火扣除、上限和 Host 拒绝链路可运行。
- SnowValley 已有三个余火源与一个世界树内容点；公共篝火和 SmallFire 升级走火焰系统，不属于 Activity 状态机。

### PC 实时联机

- Windows Development Build 成功；PC Host/Client 使用协议 `7` 正常准入，Owner、稳定 PlayerId 和本地输入归属正确。
- 位姿采用 20Hz 不可靠实时流和可靠校准；已修复 Client Owner 回溯与相机撕扯。
- Client 放置 SmallFire 后 Host/Client 均生成同一可见火种；late-join Client 能恢复已存在的 SmallFire。
- 网络玩家使用 `SnowTraveler_Female` 正式模型与 Locomotion Controller；旧胶囊 Renderer 已禁用。
- `CharacterFacingRoot/SnowTravelerVisual` 分离了移动朝向和局部表情动作；用户已验收 Q 后不会持续拉回固定角度，远端仍可看到表情。
- 第三个 Client late-join 不补播此前 Q 表情；断线时 Host/Peer 事件及场景服务注销正常，日志无异常。

### 事实与社交代码

- `ActivityFactDto` 及动作请求具有稳定 `ActorId`、`TargetKind/TargetId`、`EventId`、Host 时间戳和 revision。
- Target 支持 `player/place/instance`；Host 统一拒绝重复 EventId、旧 revision、无效目标和非法 payload。
- `marshmallow.give(targetId)` 已实现 Host 原子校验、产物转移、双方事实和语义 Cue；一次性 Cue 不进入 late-join 快照。
- `expression.wave` / `expression.thanks` 通过 Host 广播 Presentation Cue；Observer 不打开对方 UI、不切换本地 Camera、不锁本地移动。
- 本地异步事实文件已验证稳定元数据和 EventId 幂等；它是后端可替换边界，不是另一套 Gameplay。

## 5. 尚未完成或未完整验收

以下内容不能被其他会话误判为“架构必须先重构”：

- PC/Android 真机互通与公网/内网穿透完整矩阵尚未最终验收。
- 自动重连、恢复原 Activity Session、Lobby、Relay、匹配、账号和正式房间 UI 不在当前范围。
- 互喂棉花糖代码链路已完成，但目标离线、容量不足、活动并行等完整拒绝矩阵尚未逐项签字。
- 活动 UI 已采用透明低干扰基线，Guitar 已有程序钢琴短音，Marshmallow 已接入拟造、翻面、完成及收到赠礼的一次性 VFX；正式 Emote、Marshmallow、Fishing、Guitar 角色动画、正式音频和最终 UI 美术仍需内容制作，现有语义入口不需要为此重构。
- 火焰表现的状态读取链已经存在。`FlameSource.prefab` 的一次性拾取余烬、`SmallFire.prefab` 的显式 Point Light/持续火焰，以及 SnowValley 公共 `Hero_Campfire` 由等级/热量驱动的 `CampfireWarmFill + Campfire_RisingEmbers` 均已通过用户 Play Mode 验收。以上都是表现绑定，没有新增 VFX Manager 或修改火焰权威状态。
- 世界树贡献逻辑、稳定 ID、个人光点和一次性贡献反馈已接入；正式承载物已经改为 SnowValley 现有 `SnowGrove_Tree`，场景中的显式节点为 `Gameplay_WorldContent/SnowGrove_WorldTree`。贡献粒子只响应真实 `Contributed` 事件，存档/网络快照与 late-join 不补播；该节点不再挂载 `RestorableNode`、`WorldBloom` 或 `ColorSource` 颜色复苏实验链。
- 冰纹裂缝、路径 RenderTexture 等短时美术状态尚未定义网络事实；当前日志仍可能提示 Ice Path Stamp Shader 缺失。
- 普通墙体遮挡未启用通用 Cinemachine Decollision，因为 Player 与环境仍共用 Default Layer；应在美术层级明确后再配置 Environment/Player Layer。
- 异步系统目前只有本地事实适配器，没有具体服务器后端。

## 6. 当前优先级

### P0：现在做

1. 新增伙伴玩法时，复用现有 Host 权威动作/事实模型，先做单机 Logic，再增加网络目标与表现。
2. 以具体活动为单位补正式角色动画、短音效和最终透明 UI；现有 Marshmallow/Fishing/Guitar 的 Logic、Session 和表现请求边界不需要重构。

### P1：随具体内容验证

- 互喂棉花糖完整双端拒绝矩阵。
- Activity Owner/Observer、目标离线和活动中断恢复。
- Android Client 与 PC Host 真机互通。

### P2：产品化阶段再做

- 自动重连、Lobby、Relay、匹配、房间码、账号、后端同步和复杂弱网策略。

## 7. 新会话接续要求

新会话不得从旧评估文档重新推导一次架构。开始工作时应遵守：

1. 先读本文，再按任务读取 `FirePlay_核心架构.md`、`FirePlay_美术技术开发提示.md` 或 `FirePlay_联机测试清单.md`。
2. 默认当前架构与 SnowValley 接线可用；除非有可复现错误，不重新拆 Player、Activity、Camera 或 Network。
3. 只修改 `Assets/Scenes/SnowValley_Playable.unity`；不修改 DemoScene、ArtScene 或其他会话的美术资产。
4. 不使用运行时 `AddComponent`、GameObject 名称/层级路径或 InstanceID 隐藏依赖与稳定身份。
5. 每次只完成可验证的一段功能，更新本文，并明确区分“代码完成”“Build 成功”和“用户 Play Mode 验收通过”。

## 8. 当前交付状态

- Runtime/Editor 静态编译：`0` 错误、`0` 警告。
- Unity Pipeline `0.5.0-exp.1` 已安装并连接当前编辑器；后续可直接执行编译、Play Mode、Console、场景与组件检查，不再依赖用户手动刷新引擎。
- FlameSource/SmallFire 表现 Prefab 已由 Unity 正常保存并重新导入；SmallFire `GlobalObjectIdHash=1156549450` 保持不变，未修改 NGO 注册身份。
- FlameSource 拾取余烬与 SmallFire 正式放置火焰已由用户完成 Play Mode 验收；预览阶段未误播放粒子。
- SnowValley 公共篝火的 WarmFill 与 RisingEmbers 已随热量/等级正确变化并由用户验收。
- Unity 已成功编译并写入本轮 P0 表现资产：SnowValley 场景 `SnowGrove_WorldTree` 的贡献粒子/个人光点、单机/网络 Player 的 Marshmallow VFX，以及 SnowValley 的 VFX Executor 引用；一次性 Editor 作者脚本已删除。
- SnowValley `ActivityModule` 下历史残留的 5 份 `PlayerActivityVisualModule` 已清理为 1 份；`SnowValleySceneBindingBuilder` 已改用组件边界判断，重复运行不再继续生成模块。
- SnowGrove 世界树继续使用稳定 ID `snow.world-tree.main`，新的场景内 `NetworkObject GlobalObjectIdHash=4068931262`；旧 `Tree.prefab/WorldTree_Main` 不再被 SnowValley 或场景构建器引用。SmallFire `GlobalObjectIdHash=1156549450` 保持不变。世界树、Marshmallow VFX 与环境冷热平滑均已通过 Unity 自动 Play Mode 验证。
- 世界树首次验收暴露了两个输入边界：Canvas 可能在单机/网络 Player 交接后继续缓存旧 `LocalPlayerContext`，密集雪景也可能填满原先 8 个 Collider 的扫描缓存。`FirePlayMobileInputRouter` 与 `FirePlayContextActionsPanel` 现会随 `LocalPlayerContext.Current` 重绑，扫描容量提升为 32；单机世界树拒绝会输出余火与 `LastContributionStatus`。修复已静态编译通过，Play Mode 结果待用户复验。
- 世界树扫描不再依赖固定容量侥幸命中：`PlayerInteraction` 从 32 个 Collider 起步，只有缓冲区确实填满时才按需扩容，最多复用 256 个条目，避免 SnowValley 密集植被/冰面 Collider 抢占结果后让真实交互体静默丢失。该修复不改变交互距离、优先级或权威命令；Play Mode 仍待用户复验。
- 世界树一次性贡献反馈现已补齐实时旁观链路：Host 仍由本地 `Contributed` 事件播放，纯 Client 通过 `FirePlayNetworkWorldTree` 的可靠一次性 Cue 播放同色粒子；状态快照、存档加载和 late-join 仍只恢复累计值/个人光点，不补播历史粒子。正式 SnowValley 节点显式绑定 `WorldTreeProgressVisuals`，运行时不查名称、不补组件；已弃用的旧 Tree Prefab 不作为正式配置来源。
- 世界树单机贡献失败已定位并修复：`WorldCommandExecutor` 位于 `InteractionModule`，原先只搜索自身子节点，无法解析兄弟 `FlameModule`，导致有效目标被错误拒绝为 `Missing setup`。现在通过显式 `LocalPlayerContext`/模块引用解析火焰能力，不新增动态组件。Unity 自动 Play Mode 已走真实 HUD 动作链验证：首次贡献扣除 10 余火、累计值/次数/revision 更新、一次性粒子播放并生成 1 个个人光点；重复贡献保持累计值/次数/revision 不变且不重播粒子。late-join 规则仍由快照不携带一次性 Cue 保证，双端视觉验收留在联机内容矩阵。
- Marshmallow 已通过 Unity 自动 Play Mode 的真实 Session/Action 链验证：拟造扣除 3 余火并显示道具；两次翻面得到 Z=90°/180° 的持久姿态并分别触发反馈；完成态、收到赠礼 VFX Cue 和吃掉后的 Session/道具/移动锁/Look 锁恢复均正确。正式动画和正式音频资源仍属于内容制作，不阻塞玩法。
- SnowValley 环境热场已通过 Unity 自动 Play Mode 验证：当前篝火事实驱动 16 个雪面 Renderer、4 组灯/融雪雾/空间音频和 12 个植被 Receiver；空场采样与真实采样之间按 `_responseSpeed` 平滑过渡，辅助灯阴影保持关闭，雪面 MPB 收到同一热源，6 处植被目标正在响应。该链路只读取权威火焰状态，不写回 Gameplay。
- 活动镜头过近/钓鱼异常拉远的根因已修复：SnowValley 唯一 `ActivityCamera` 原使用 `CinemachineHardLockToTarget`，会把镜头放到 `PlayerCameraFrameTarget` 内部，再由地形避障强行推离。现在场景显式使用 `CinemachineFollow`（WorldSpace、offset `(0, 2, -5.5)`、位置阻尼 `(0.25, 0.3, 0.45)`），保留原 TargetGroup/LookAt/Profile/恢复链。Marshmallow 与 Fishing 实测输出距离稳定约 5.85 米，退出后 Explore/Activity 优先级恢复为 10/0，移动与 Look 锁无残留。
- 互喂棉花糖补齐失败可重试语义：接收方只在容量检查与余火恢复全部成功后提交 Gift EventId 幂等标记；容量不足或资源写入失败不会被错误记录成“已接收”，调用方可用新的动作 EventId 重试。重复动作 EventId 仍由上游权威入口幂等拒绝；成功链路、事实 DTO 与 late-join 策略未改变。
- 角色 Animator Controller 已静态盘点：`IsResting / IsMarshmallowRoasting / IsGuitarPlaying / IsFishing` 持续状态已有状态机入口；大部分 Marshmallow、Guitar、Fishing 与 Emote Trigger 目前只有参数占位，尚无正式动作跳转。代码侧 `PlayerAnimationController` 语义 Cue 已就绪，后续应由美术/动画资源接入现有参数，不修改 Activity Logic。
- Windows Development Build：成功。
- PC Host/Client 基础联机、SmallFire、网络模型、表情朝向、最小 late-join/断线：用户验收通过。
- 当前改动已由用户提交；后续以 `52dc9a3` 为稳定联机基线。
