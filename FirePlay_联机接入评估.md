# FirePlay 联机接入评估（功能性第一次封包后）

## 结论

**现在不要直接接入真实多人。先做一次很小、明确的“联机前边界整理”，然后按“异步痕迹 → 中心广场实时同游”推进。**

原因不是 MonoBehaviour 数量多。当前 67 个 FirePlay Runtime 脚本约 5,600 行，对于 Unity Demo 属于可维护规模；而且余火、篝火生命周期、仪式、镜头和视觉桥接都已经有相对清晰的职责。真正不适合直接联网的是：它们默认全世界只有一个本地 Player、一个存档、一个大树，并允许客户端直接改状态。

不要为了网络而把所有 MonoBehaviour 改成 ECS、DI、事件总线或完整状态机。这会破坏已验收的单机体验，且不能自动解决权威、断线和持久化问题。

## 当前结构审视

### 可以原样保留

| 模块 | 原因 |
|---|---|
| `FlameResourceState` / `FlameResourceController` | 资源状态与视觉已分离，可成为本地预测/展示基础。 |
| `Campfire` / `CampfireConfig` / `CampfireRecord` | 有稳定 ID、明确可序列化记录和生命周期，适合演进为服务端权威状态。 |
| `RestSpotRitual` + 独立仪式组件 | 地点与仪式分开；吉他等未来仪式可以沿用。 |
| `PlayerRitualAnimationController` | 动作出口集中，远端玩家只需同步状态/触发，不必复制每种仪式逻辑。 |
| `CampfireSiteAwakeningVisuals` / `WorldTreeProgressVisuals` / `MarshmallowVisuals` | 都主要读取状态做表现，适合继续留在客户端。 |
| `StableSceneId` | 场景对象的网络/持久化寻址基础；新内容必须持续使用。 |

### 联机前必须整理的边界

| 现状 | 风险 | 最小处理 |
|---|---|---|
| `PlayerInteraction` 既做探测、输入分流，又直接调用 `TryTend/TryContribute/...` | 客户端可直接改世界状态；同帧多按钮也可能多次请求。 | 提炼“意图”方法：`RequestTendFire(id)`、`RequestDrawFire(id)`、`RequestContributeTree(id,color)`；单机实现仍直接执行，联网实现把意图发给权威端。 |
| `CampfirePersistence` 直接监听静态事件、`FindFirstObjectByType`、读写本地 JSON | 只能有一个本地世界与一棵树；不能区分本地存档和服务器状态。 | 抽出 `IWorldStateRepository`/`WorldStateService` 的小接口。保留 `LocalSaveRepository` 作为单机实现；以后新增远端实现。 |
| `Campfire.ActiveInstances`、`SmallFire.ActiveFires` 静态列表 | 对单场景单人很好；多场景/网络生成需要明确注册和注销时机。 | 暂保留，但将来由 `WorldStateService` 统一索引稳定 ID；不要让网络代码依赖“当前第一个对象”。 |
| UI 使用 `FindAnyObjectByType` | 多 Player 后可能绑到远端玩家。 | UIBootstrap 明确接收本地 Player 的引用或 `LocalPlayerContext`，不再全局查找。 |
| `WorldTreeContribution` 用固定 `local.player` 且单人只可贡献一次 | 不能表示多个身份、每日/总量规则或服务端累计。 | 将 `playerId` 改为身份服务提供；将个人贡献与全局树进度分成记录。 |
| 火源拾取、余火、钓鱼结果都由客户端直接决定 | 联机后易作弊，且不同客户端会不同步。 | 先只把它们当本地体验；如果进入共享世界，需权威端校验位置、冷却、成本与奖励。 |

## 推荐路线

```text
封包单机体验
      ↓
联机前边界整理（不接网络）
      ↓
异步火堆 / 留言 / 回流（持久化后端）
      ↓
中心广场 2–4 人实时同游（Relay + NGO）
      ↓
按需扩展仪式协作与更多实时区域
```

### 阶段 A：联机前边界整理（建议先做）

目标：不改变现有单机玩法，只新增可替换入口。完成线：单机行为与现在完全一致。

1. 建立 `LocalPlayerContext`：持有本地 Player、身份 ID、HUD/UI；UI 和相机只读它。
2. 建立非常薄的 `WorldActionService`：处理添火、取火、升火、回收、贡献；输入/UI 不直接调用 World MonoBehaviour。
3. 建立 `IWorldStateRepository`：最少包含读取/保存 Campfire 与 Tree 的快照；`LocalSaveRepository` 是第一实现。
4. 将 `CampfirePersistence` 改成上述服务的单机适配器，去掉对“全局第一个大树”的硬依赖。
5. 为 `CampfireRecord`、`TreeProgressData` 加入未来需要但不立刻启用的 `worldId`、`updatedAt`、`actorId`、`revision`；旧存档做迁移。

这一步不是大重构，预计应被拆成 3–5 个可运行提交；每一步都要跑现有单机验收。

### 阶段 B：先做异步社交

这是你的第二层社交，也比实时联机风险低。

- 后端存“世界火堆快照 / 投火操作 / 表情 token / 回流计数”，不存自由文本；
- 客户端上传的是操作意图，不是直接上传任意热量数值；
- 服务器校验额度、冷却、Stable ID、允许的表情词库；
- 一座火只显示有限条痕迹或聚合计数；
- 没有网络或没有其他玩家痕迹时，退回完整的本地体验。

异步阶段完成后，你可以验证“别人的火 / 回应 / 回流”是否真的动人，再决定实时社交投入是否值得。

### 阶段 C：中心广场实时同游

范围严格限定为：中心广场、2–4 人、无语音、无自由文本、只有移动、坐下、表情、火焰外观与有限的篝火互动。

Unity 6 当前优先选 **Multiplayer Services SDK + Netcode for GameObjects + Relay**；Unity 官方建议 Unity 6 使用统一的 Multiplayer Services SDK，Relay 适合 listen-server 模式的小型会话。不要从旧的独立 Relay SDK 或“全地图 MMO”开始。

实时同步最小集：

- 玩家：位置、朝向、移动/停留/仪式/表情状态；
- 外观：火焰颜色、强度档位、手持道具可见状态；
- 中心火：服务器/Host 权威的热量与等级；
- 进入/离开：会话成员与简单 emote。

不同步：本地相机、UI、音频混音、粒子随机数、任何全局 Canvas；它们只根据同步状态在各自客户端表现。

Host 断开是 Relay listen-server 的核心限制；广场 Demo 可以直接结束会话并让所有人回落到异步/单机，不要在第一版做 Host Migration。

## 何时可以真正开始接网

满足以下条件后再装 NGO/MPS 包：

- 单机封包已提交，并能从空存档完整跑通；
- `WorldActionService` 与 `IWorldStateRepository` 的单机实现已完成，且没有改变核心数值；
- UI 只绑定本地 Player；
- 所有场景可同步对象都有 Stable ID；
- 决定中心广场上限为 2–4 人，并接受 Host 断线即会话结束的第一版体验。

因此答案是：**先做小范围的代码整理，再接异步，最后接实时。** 不是因为当前架构失败，而是因为现在直接加网络会让每个已验收 MonoBehaviour 同时承担本地、远端、权威与表现四种责任，调试成本会立刻失控。

