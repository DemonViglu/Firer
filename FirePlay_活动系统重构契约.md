# FirePlay 活动系统重构契约

> 本契约以当前已保存的可运行版本为切换基线。重构允许破坏旧兼容层；旧 Ritual、旧 UI 和旧 Player 动态服务不作为新系统的兼容目标。

## 1. 产品边界

项目的长期核心只有四件事：

1. 美术表现；
2. 玩家余火、篝火、小火种与大树贡献组成的火焰资源循环；
3. 小活动的快速接入和组合；
4. 玩家之间一起参与活动、互相影响并最终实时联机。

活动是内容扩展的第一公民。地点、Player、UI 和网络层都不能持有某个具体活动的类型分支。

## 2. 活动不是 Ritual，也不默认绑定地点或坐姿

核心概念统一叫 `Activity`。Rest、Marshmallow、Fishing、Guitar、Stargazing 都只是活动内容。

活动定义不包含“必须坐下”这种固有前提。活动可以是：

- `Anywhere`：随时随地可开始，例如表情、吉他；
- `Anchor`：需要一个地点或目标，例如烤棉花、观星；
- `Targeted`：需要目标对象，例如把烤好的棉花糖交给另一名玩家。

玩家当前状态、地点规则和全局规则共同决定当前是否可用。飞天、睡觉、游泳、坐下等都只是可被规则读取的状态事实，不写死在 ActivityDefinition 中。

## 3. 活动内容契约

每个活动由以下内容组成：

- `ActivityDefinition`：稳定 ID、显示名、作用域、UI Prefab、可选 Input Action Map、逻辑工厂和表现资源引用；
- `ActivitySession`：一次玩家活动的纯状态；
- `ActivityLogic`：活动自己的规则、动作、资源消耗和结果；
- `ActivityUIPrefab`：活动专属 UI；
- `ActivityPresentation`：动画、道具、音频等表现；
- `ActivityCameraRequest`：活动向 Player Camera 请求镜头，不直接操作相机组件。

简单活动可以只拥有一个 Session 和一个 UI。复杂活动（例如 21 键吉他）可以拥有独立 Input Action Map、专属状态和瞬时音符事件，不进入通用 Player Router。

### 3.1 活动与地点规则的边界

活动逻辑可以知道自己当前运行上下文，但只能看到只读数据：

```csharp
public interface IActivityContext
{
    string PlayerId { get; }
    string ActivityId { get; }
    string AnchorId { get; } // Anywhere 活动为空
    IActivityLocationView Location { get; }
    IActivityRuleSnapshot Rules { get; }
    IActivityFlameResource Flame { get; }
    IReadOnlyList<ActivityParticipant> Participants { get; }
}
```

- 活动可以读取 `AnchorId`、地点标签、目标对象和最终规则快照；
- 活动不能拿到 `ActivityAnchor`、地点规则脚本或 UI／Camera MonoBehaviour 的可变引用；
- 活动不能修改规则结果，也不能在运行中重新覆盖地点规则；
- 活动自己的特殊前提以 `IActivityRuleProvider` 形式参与规则计算，而不是在计算完成后偷偷改结果；
- 规则结果是只读快照，Session 开始后仍可因状态变化重新预检，但不能由活动逻辑直接篡改。

因此，活动“知道自己在哪个 Anchor 上”，但只知道稳定 ID 和只读地点视图，不依赖地点组件的实现细节。

## 4. 地点与组合规则

`ActivityAnchor` 只表示一个可发现的空间锚点，不复制活动动作配置。锚点可以引用多个可复用 ActivityDefinition。

地点自己的规则脚本实现 `IActivityRuleProvider`，例如：

- `CampfireActivityRules`；
- `TentActivityRules`；
- `LakeActivityRules`。

规则结果包含：

- 是否允许；
- 优先级；
- 拒绝原因；
- 可选的覆盖数据。

规则来源可以叠加：

1. 活动默认规则；
2. 地点／地区规则；
3. 玩家当前能力与模式规则；
4. 目标对象规则；
5. 联机权威规则。

规则不是固定的 `requiresSeat` 字段。例：

```text
篝火规则：
- Rest：允许
- Marshmallow：必须处于坐下状态且篝火燃烧
- Guitar：无需坐下

游泳状态规则：
- Guitar：允许或拒绝由活动规则决定
- Flying：拒绝所有需要水面交互的活动
```

同一活动可以在多个地点复用，地点只声明“是否提供”和“如何覆盖规则”。

### 4.1 规则冲突与确定性裁决

规则不按脚本执行顺序或最后写入顺序覆盖。所有规则先收集，再按显式优先级统一裁决。默认优先级从低到高为：

| 层级 | 来源 | 作用 |
|---:|---|---|
| 0 | Activity 默认规则 | 活动自身的一般前提 |
| 100 | 地区／Anchor 规则 | 这个地点提供什么、如何覆盖 |
| 200 | 玩家状态规则 | 飞天、睡觉、游泳、当前模式等 |
| 300 | 目标对象规则 | 目标是否存在、是否可用、是否已被占用 |
| 1000 | 系统安全规则 | 强制禁止或资源/生命周期保护 |
| 2000 | 联机权威规则 | Host 最终裁决 |

每条规则产生一个不可变决定：`Allow`、`Deny` 或明确的 `Override(Allow/Deny)`。

实际算法固定为：

1. 先收集所有适用规则，规则评估不能产生副作用；
2. 联机权威层出现 `Deny` 时直接最终拒绝，不能被本地规则覆盖；
3. 其余层中，最高优先级的 `Override` 覆盖所有更低优先级结果；
4. 没有 `Override` 时，取最高优先级层的结果；同一优先级同时出现 `Allow` 和 `Deny`，`Deny` 优先；
5. 没有任何 `Allow` 时默认拒绝；
6. 活动逻辑不能产生最终 `Override`，只有规则提供者可以产生，并且必须声明覆盖理由。

这意味着规则既不是“最严格永远优先”，也不是“后写覆盖前写”，而是由优先级、同级拒绝优先和权威最终裁决共同决定。

## 5. 余火消耗契约

活动可以消耗余火拟造道具或推进玩法。这是活动自己的经济规则，不是全局 Ritual 特判。

活动通过上下文获取能力接口：

```csharp
public interface IActivityFlameResource
{
    float CurrentFuel { get; }
    bool TryConsume(float amount);
    bool Restore(float amount);
}
```

当前单机实现可以直接代理玩家已有的 `TryConsume`。未来联机时替换为 Host 权威实现，活动逻辑不改。

规则评估本身不能产生副作用；进入活动或提交动作时，由活动逻辑执行 `TryConsume`，失败则返回明确结果。

## 6. 活动会话生命周期

活动 Session 必须经过明确阶段：

```text
Requested
    -> Preflight
    -> Committed / Running
    -> Switching / Interrupted / Completed
    -> Ended
```

### 6.1 开始与切换

1. **Preflight**：收集 Player、Anchor、目标、参与者、规则和资源快照；不创建 Session，不扣余火，不打开 UI；
2. **Commit**：规则全部通过后创建 Session。需要余火的活动在此阶段执行 `TryConsume`，失败则整个开始失败；
3. **Running**：活动逻辑处理自己的动作和状态；
4. 切换活动时先对新活动执行 Preflight。新活动通过后，旧 Session 以 `Switched` 原因结束，再提交新 Session；新活动预检失败时旧 Session 保持不变。

### 6.2 中断与结束

- 玩家离开 Anchor：对 `Anchor`/`Targeted` 活动重新预检；不再满足时以 `LeftAnchor` 中断；`Anywhere` 活动不受影响；
- 玩家状态变化：收到 `PlayerStateChanged` 后重新预检；不再满足时以 `StateChanged` 中断；活动若明确声明可暂停，才允许进入 `Suspended`，否则不隐式暂停；
- 目标消失、对象被占用、资源失效或 Host 拒绝：以对应原因中断；
- UI 关闭只能发出退出请求，不能直接结束事实 Session；
- `End` 幂等，任何结束原因只发布一次 `ActivityEnded`。

### 6.3 多人同一 Anchor

默认情况下，每名玩家拥有独立 Session。同一 Anchor 上两名玩家可以同时进行不同活动。

只有 ActivityDefinition 明确声明共享模式时，ActivitySystem 才创建 Anchor 级 `ActivityGroupState`：

- `Independent`：每个玩家完全独立，默认模式；
- `SharedGroup`：共享局部进度、容量或产物，但每个玩家仍有自己的 Session；
- `TargetedInteraction`：通过目标玩家/对象事件交互，不合并双方 Session。

不允许因为“同一地点”就隐式共享所有活动状态。

## 7. 运行流程

```text
输入 / 活动 UI
    -> EventBus: ActivitySelectionRequested
    -> ActivitySystem 查找全局活动、附近 Anchor 和规则提供者
    -> 解析候选、优先级和拒绝原因
    -> Preflight 规则与资源
    -> Commit：创建或切换玩家 ActivitySession
    -> PlayerActivityPresentationSystem 执行 UI 请求
    -> PlayerCameraSystem 执行 Camera 请求
    -> UI / 独立 Input Action Map 发布 ActivityActionRequested
    -> 当前 ActivitySession 消费动作并更新状态
    -> 发布 ActivityStateChanged / ActivityInteractionOccurred
    -> 本地表现、远端玩家表现、动画和音效分别消费
    -> 退出请求
    -> ActivitySystem 结束 Session
    -> PlayerActivityPresentationSystem 关闭 UI
    -> PlayerCameraSystem 恢复 Camera
```

活动逻辑只能发布 `ActivityUIRequest` 和 `ActivityCameraRequest`，不能直接调用 `UIManager`、`Button`、`Camera.main` 或 Cinemachine 组件。Player 侧的 PresentationSystem 和 CameraSystem 是唯一执行者，并以只读结果回报请求是否成功。

火焰资源采用独立链路：

```text
Player / Activity
    -> FlameCommandRequested
    -> FlameSystem 权威处理
    -> FlameStateChanged
    -> 视觉、UI、存档、网络分别消费
```

## 8. Player 组合目标

Player 只保留输入、移动、余火、ActivityHost、Camera、Presentation 和身份等核心组件。

不再由 `LocalPlayerContext` 在运行时动态 `AddComponent` 玩法服务。不再把棉花糖、钓鱼、吉他、观星各自挂在 Player 上。

ActivitySession 由 ActivitySystem 创建，活动 UI 和活动表现由 ActivityDefinition 提供。

## 9. EventBus 使用边界

保留 `GameInstanceSubsystem` 与 `GameEventBus`，但服务只在启动阶段显式注册。

EventBus 只承载跨模块语义：

- Activity 选择、动作、状态和交互事件；
- Flame 命令与状态事件；
- 表情语义事件；
- Camera 请求和结果事件。

移动、视角拖拽、逐帧计时和纯本地 Session 推进不通过 EventBus。

不再让多个旧脚本同时订阅一个宽泛的 `PlayerIntentRequested` 并各自猜测谁拥有动作。

## 10. 明确删除范围

重构完成后删除或替换：

- `RestSpotRitual` / `RestLookTargetRitual` 兼容体系；
- 固定 Primary／Secondary 的 `RitualViewState`；
- `RitualInteractionCoordinator`；
- `RestInteraction` 的旧总控职责；
- `ActivityAnchor` 的 `_legacyOffers`、`_additionalOffers`；
- Player 上的 `MarshmallowInteraction`、`FishingInteraction` 玩法挂载模式；
- 动态 `AddComponent` 初始化链；
- 旧 Ritual Panel 与通用 Activity Form 的重复路径。

保留并整理火焰状态、篝火状态、Stable ID、存档基础、视觉桥接和 EventBus 基础设施。

## 11. 验收顺序

1. 一个 `Anywhere` 活动：无需地点、无需坐下；
2. 一个 Anchor 活动：同一地点提供两个活动；
3. 地点规则覆盖：一个活动需要坐下，另一个不需要；
4. 玩家状态规则：飞天、睡觉、游泳可以拒绝或覆盖活动；
5. 活动专属 UI 与 Camera 接口；
6. 活动余火消耗和失败回滚；
7. 两名玩家同时选择同一地点的不同活动；
8. 活动语义事件进入实时联机 Host 权威链路。
