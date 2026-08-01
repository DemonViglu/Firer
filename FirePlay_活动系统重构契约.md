# FirePlay 活动系统核心契约

> 这是活动系统的唯一设计基准。它描述稳定的职责边界和运行链路，不记录历史迁移过程。当前 Demo 已保存可运行版本，允许删除旧兼容层。

## 1. 产品边界

FirePlay 的核心由三条相互协作但不混淆的链路组成：

1. **火焰资源链**：玩家余火、篝火、小火种、世界树贡献、存档与视觉表现；
2. **活动内容链**：坐火、烤棉花、钓鱼、吉他、表情以及未来的小型玩法；
3. **多人同步链**：请求、权威裁决、状态事实和远端表现。

活动是内容扩展的第一公民。地点不拥有玩法逻辑，Player 不挂载具体活动，UI 不直接修改活动状态，网络层不依赖 Unity 对象。

当前 Demo 边界仍是单机可运行版本；下一阶段接入实时联机，异步保存只记录实时活动产生的必要数据，不单独设计另一套玩法兼容层。

## 2. 核心概念

| 概念 | 职责 | 不负责 |
|---|---|---|
| `ActivityDefinition` | 稳定 ID、作用域、UI 键、Camera Profile、LogicKey、参与模式 | 具体运行状态 |
| `ActivityDefinitionAsset` | Unity 资产形式的定义 | 玩法分支与 Player 引用 |
| `ActivityCatalogAsset` | 全局可用活动目录 | 地点局部规则 |
| `ActivityAnchorNode` | 地点身份、标签、活动引用、规则组件引用 | 复制活动动作或 UI |
| `ActivityLogic` | 该玩法的状态、动作、资源消耗、结果 | 直接操作 UI、Camera、Animator |
| `ActivitySession` | 一名玩家的一次活动状态和 revision | 传输层状态 |
| `ActivityForm` | 活动自己的 UI 和输入映射 | 直接改 Player/火焰状态 |
| `ActivityVisuals` | 道具、动画 Cue、音频、VFX 表现 | 活动规则与权威裁决 |
| `PlayerActivityHost` | Player 侧活动宿主、Session 路由、Presentation 边界 | 保存具体活动字段 |

活动作用域只有三类：

- `Anywhere`：不需要地点，例如表情、吉他；
- `Anchor`：需要地点，例如篝火旁的烤棉花、池边钓鱼；
- `Targeted`：需要可用目标，例如把产物交给另一名玩家。

“必须坐下”“不能飞行”“游泳时禁止”等不是定义字段，而是规则提供者读取玩家状态、地点状态和目标状态后给出的结果。

## 3. 活动与地点的组合

一个活动定义可以被多个地点复用；一个地点可以引用多个活动定义。

地点只配置：

- `AnchorId`：优先来自同物体 `StableSceneId.Value`；
- `RegionId`、Tags；
- `Activities`：拖入可复用的 `ActivityDefinitionAsset`；
- `Rule Provider Behaviours`：显式引用实现 `IActivityRuleProvider` 的规则组件。

活动逻辑可读取只读上下文：

```csharp
public interface IActivityContext
{
    string PlayerId { get; }
    string ActivityId { get; }
    string AnchorId { get; }       // Anywhere 为空
    IActivityLocationView Location { get; }
    IActivityPlayerStateView PlayerState { get; }
    IActivityTargetView Target { get; }
    IActivityRuleSnapshot Rules { get; }
    IActivityFlameResource Flame { get; }
    IReadOnlyList<ActivityParticipant> Participants { get; }
}
```

活动可以知道自己在哪个稳定 Anchor 上，但不能持有 `ActivityAnchorNode`、规则组件、UI 控件、Camera、Animator 或移动组件的可变引用。活动不能在规则计算后偷偷覆写规则结果；自己的前提必须作为规则提供者参与计算。

## 4. 规则裁决

规则评估必须无副作用，产生不可变的 `ActivityRuleResult`。默认优先级：

| 优先级 | 来源 |
|---:|---|
| 0 | Activity 默认规则 |
| 100 | Anchor / Region 规则 |
| 200 | Player 状态与能力规则 |
| 300 | Target 规则 |
| 1000 | 系统安全与资源保护 |
| 2000 | 联机 Host 权威规则 |

裁决顺序固定为：

1. 收集所有适用规则；
2. Host 权威层出现 `Deny` 时直接拒绝；
3. 否则取最高优先级的 `Override`；
4. 没有 Override 时取最高优先级层；同级 `Deny` 优先；
5. 没有任何允许结果时默认拒绝；
6. 结果以只读 `ActivityRuleSnapshot` 传给 Session。

活动逻辑不能修改快照。Session 运行中因玩家状态、地点、目标或权威状态变化而重新预检；失败时以明确的 `ActivityEndReason` 结束或按定义暂停。

## 5. Session 生命周期

```text
Requested
  -> Preflight
  -> Prepare
  -> Commit
  -> Running
  -> Switching / Interrupted / Completed
  -> Ended
```

- **Preflight**：检查定义、作用域、地点、目标、规则和活动自身的 `CheckStart`；不扣余火、不打开 UI；
- **Prepare**：生成无副作用的开始操作；
- **Commit**：创建 Session 的唯一提交点。需要余火拟造时在此调用 `IActivityFlameResource.TryConsume`；失败必须回滚；
- **Running**：只由当前 Session 消费语义动作并更新活动状态；
- **Switching**：新活动先完成预检和 Commit，成功后旧 Session 才以 `Switched` 结束；失败则旧 Session 保持不变；
- **Interrupted**：离开地点、玩家状态变化、目标失效、资源失败或 Host 拒绝；
- **Ended**：结束幂等，事实事件只发布一次。

默认每名玩家拥有独立 Session。同一地点的多人活动只有在定义声明 `SharedGroup` 或 `TargetedInteraction` 时才共享局部状态；同一 Anchor 不会自动合并所有玩家。

## 6. 运行时逻辑链路

```text
输入 / 活动 UI / 网络请求
    -> GameEventBus: ActivitySelectionRequested
    -> PlayerActivityHost
    -> ActivityRuntime / ActivitySystem
    -> Catalog + Anchor + Rule Providers + Player State
    -> Preflight / Prepare / Commit
    -> ActivitySession
    -> GameEventBus: ActivitySessionStarted

活动 UI / 独立 Input Action Map
    -> ActivityActionRequested
    -> PlayerActivityHost
    -> 当前 ActivitySession 消费动作
    -> ActivityInteractionOccurred
    -> ActivityPresentation / 远端表现 / 存档订阅

退出 / 状态失效 / Host 拒绝
    -> ActivitySessionEnded
    -> PresentationHost 关闭 UI、释放 Camera/Player 能力
```

活动只发出语义请求：

- `ActivityUiRequest`：打开/关闭自己的 UI；
- `ActivityCameraRequest`：请求 Camera Profile、目标或恢复；
- `ActivityPlayerRequest`：移动锁、Look Target、Animation State、Animation Cue、Vfx Cue。

`PlayerActivityPresentationHost`、`RitualCameraDirector`（当前 Camera Executor）以及 Player 能力执行器负责真正调用 `UIManager`、Camera、移动和动画系统。活动逻辑不能直接拿这些组件。

## 7. 火焰资源链路

火焰系统不属于某个活动：

```text
Player / Campfire / Activity
    -> Flame command or IActivityFlameResource
    -> FlameResourceController / Campfire authority
    -> FlameStateChanged
    -> UI / Visuals / Save / Network
```

活动只通过 `IActivityFlameResource` 请求消耗或返还。余火不足必须返回失败原因，不能产生部分扣除。篝火成长、SmallFire 上限、世界树贡献和存档继续由各自火焰/世界系统负责。

## 8. Player、EventBus 与全局服务

Player 的活动入口只有 `PlayerActivityHost`。活动内容不作为十几个玩法脚本挂在 Player 上；Logic、Factory、Form、Visuals 在活动或全局注册表中组合。`IsLocalPlayer` 决定对象是否拥有本地输入和本地世界命令；远端 Player 只能由网络事实驱动表现，不覆盖本地单例。

保留现有 `GameInstanceSubsystem` 与 `GameEventBus`：

- `GameInstanceSubsystem` 负责显式注册全局服务，例如 `IEventPublisher`、稳定 ID 注册表和世界命令服务；
- `GameEventBus` 只传递跨模块语义事件：活动请求/事实、火焰命令/状态、表情请求、Camera 请求结果；
- 逐帧移动、镜头拖拽和纯本地 Tick 不经过 EventBus；
- 同一个事实只能由一个权威宿主消费，避免多个旧脚本猜测动作归属。

当前 `LocalPlayerContext` 负责一部分通用 Player 服务的组合，但服务已经由 Player Prefab 显式挂载；后续可将这些通用服务进一步合并为单一 Player Service Host，不应再向其中加入具体活动分支。

## 9. 实时联机边界

网络层只处理稳定数据，不传输 Unity 引用或 `ActivityLogic` 实例。

输入方向：

```text
客户端请求 ActivitySelectionRequested / ActivityActionRequested
    -> Host 权威预检与执行
    -> Host 发布 Started / Interaction / Ended DTO
    -> 各客户端按稳定 ID 驱动本地表现
```

当前代码已经具备可序列化的 `ActivitySessionStarted`、`ActivityInteractionOccurred`、`ActivitySessionEnded` 事实事件，并提供 `ActivitySelectionRequestDto`、`ActivityActionRequestDto`、`ActivityFactDto` 与 `IActivityAuthority`。`PlayerActivityHost` 是本地 EventBus 与未来网络请求共用的权威入口；尚未接入 Network SDK、Host Transport 或远端 Player 表现。接入顺序应是：先确定 Transport，再实现消息适配与请求确认，随后做远端事实回放，最后处理活动共享状态。

## 10. 新增活动的最小步骤

1. 新建一个稳定 `ActivityId` 和 `ActivityDefinitionAsset`；
2. 编写该活动自己的 `ActivityLogic` 与 `IActivityLogicFactory`，在 `ActivityLogicRegistryBehaviour` 注册；
3. 创建专属 UI Prefab/Form；需要 21 键、节奏输入等复杂操作时使用独立 Input Action Map；
4. 创建 Visuals，监听 Session 状态和语义 Cue；动画、音频、VFX 资源缺失不应让 Logic 失效；
5. 若需要地点，在目标对象添加 `ActivityAnchorNode` 并引用定义；地点规则写成独立 `IActivityRuleProvider`；
6. 若需要余火，通过 `IActivityFlameResource` 在 Commit 或明确动作阶段消费；
7. 若需要镜头、移动锁、朝向或动画，只发 `ActivityPlayerRequest` / Camera Request；
8. 先做单机纵向验收，再接入 Host DTO，不为网络另写一套玩法逻辑。

## 11. 不变量

- 地点可以组合活动，但不复制活动逻辑；
- 活动拥有自己的 UI 和表现，不复用一个“万能活动面板”承载所有玩法；
- Activity 不直接控制 Player、UI、Camera 或网络；
- 火焰资源只有一个权威状态源；
- 稳定 ID 不使用 GameObject 名称、层级路径或 Unity Instance ID；
- 所有 Session 结束必须可解释、幂等、可被网络事实重放。
