# FirePlay Player 组件收口审计

## 1. 审计结论

当前 `Assets/FirePlay/Runtime/Prefab/Player.prefab` 的根节点有 18 个 Unity 组件：

- `Transform`、`CharacterController`、`AudioSource`；
- 15 个 FirePlay 自定义 `MonoBehaviour`。
- `Modules/FlameModule` 子节点挂载火焰逻辑、放置/升级、纯火焰表现和音景桥接；`Modules/ActivityModule` 子节点挂载活动服务；`Modules/InteractionModule` 子节点挂载 `PlayerInteraction` 与 `InteractionRouter`，不再占用 Player 根节点。

数量本身不是唯一问题。真正的问题是 Player 根节点同时承担了移动、交互、火焰资源、世界操作、活动权威、活动表现、视觉桥接和旧 Rest 逻辑。接入实时网络前必须完成职责收口，否则网络所有权和表现同步会继承这组混合依赖。

当前阶段只做审计和边界整理，不直接删除已经验收的功能。

## 2. 当前根组件清单

| 分组 | 当前组件 | 判断 |
|---|---|---|
| 运动与输入 | `FirePlayPlayerInput`、`PlayerMovement`、`PlayerLook`、`PlayerModeController` | 保留独立执行边界；后续只统一入口，不先合并移动代码 |
| 火焰资源 | `PlayerFlameController`、`FlameResourceController` | 保留分离；前者管理火苗/道具，后者管理余火资源权威 |
| 火焰表现 | `FlameResourceVisualBridge`、`FlameContractionController` | 保留为表现/输入桥接，必要时迁移到 Player 表现子节点 |
| 世界操作 | `PlayerInteraction`、`CampfirePlacement`、`CampfireUpgradeController`、`TreeLightColorSelector` | 需要收口；当前是 Player 上最混杂的一组玩法入口 |
| 旧 Rest | `RestInteraction` | 暂时保留；观星仍依赖，不能与 Activity 强行合并 |
| 活动权威 | `PlayerActivityHost` | 保留；是活动 Session 和未来网络权威入口 |
| 活动表现 | `PlayerActivityPresentationHost` | 保留并与 Activity Host 分离；负责 UI、Camera、移动锁、朝向和表现请求执行 |
| 活动专属表现 | `FishingActivityVisuals` | 不应长期放在 Player 根节点，迁移到钓鱼活动表现对象/子节点 |
| 通用表现 | `PlayerAnimationController`、`PlayerAtmosphereBridge` | 保留执行边界；后续可归入 Player 表现子节点 |
| 核心服务 | `LocalPlayerContext`、`PlayerSharedStateAdapter`、`PlayerExpressionController`、`PlayerProximityEffects`、`WorldCommandExecutor`、`InteractionRouter` | 合并候选；当前 6 个组件共同组成 Player 通用服务，但各自拥有 Unity 生命周期 |

## 3. 已确认的结构问题

### 3.1 核心服务数量多，但不是都应继续独立为 MonoBehaviour

动态 `AddComponent` 已经删除，这是正确的第一步；但现在只是把隐式创建改成了显式挂载。`LocalPlayerContext` 仍要协调 5 个通用服务，导致初始化、禁用、远端对象过滤和 Inspector 引用分散在多个组件中。

下一步应将这些服务变为一个明确的 `PlayerCoreHost`（名称可调整）加若干纯 C# 服务对象。目标是减少 Unity 生命周期入口，而不是把所有代码塞进一个巨型类。

### 3.2 `PlayerInteraction` 与 `InteractionRouter` 必须做重复职责审计

当前 `PlayerInteraction` 仍被多个 UI、世界操作和邻近效果直接引用；`InteractionRouter` 负责新的输入意图路由。两者不能长期并列成为两个“大入口”。

迁移顺序应是：先列出所有调用方，统一到一个语义接口，再删除旧入口；不能直接删 `PlayerInteraction`。

### 3.3 活动专属脚本不应挂在 Player 根节点

`FishingActivityVisuals` 仍在 Player 根节点，说明活动表现和 Player 核心仍有残留耦合。`MarshmallowVisuals`、`GuitarActivityVisuals` 已经位于道具/子对象附近，钓鱼应按同一方向迁移。

### 3.4 表现执行器与活动权威不能合并

`PlayerActivityHost` 负责活动权威、Session 和 Action；`PlayerActivityPresentationHost` 负责 UI、Camera、移动锁、朝向、动画和 VFX 请求执行。两者看似都服务 Activity，但生命周期和未来联机归属不同，必须保持分离。

### 3.5 火焰资源控制器与火苗道具控制器不能按名字合并

`FlameResourceController` 是余火资源权威，`PlayerFlameController` 是玩家火苗/道具集合。它们都属于火焰体系，但不是同一个状态，合并会重新制造资源边界混乱。

## 4. 目标 Player 结构：基础 Player + 可插拔模块

本轮收口的终点不是“把所有系统合并进 PlayerCoreHost”，而是让 Player 可以只运行基础移动：

```text
基础 Player
  ├─ CharacterController
  ├─ 基础输入 / 移动 / Look
  └─ 可选的基础动画与模式状态

可插拔模块
  ├─ FlameModule：余火、火苗、冲刺消耗、放置/添火、世界操作和火焰表现
  ├─ ActivityModule：Activity Host、Presentation Host、表情、活动表现和活动输入
  └─ RestModule：当前坐下/观星旧入口，未来可替换为 Activity
```

`PlayerCoreHost` 只负责基础 Player 上下文、身份、输入和模块注册，不再把火焰或活动服务视为核心必需依赖。缺少 FlameModule 或 ActivityModule 时，基础移动仍应可运行；模块接入后通过接口获取 Player 能力和状态。

建议目标：基础 Player 根节点约 6～9 个组件；模块组件挂在独立子节点或模块 Prefab 上。活动道具 Visuals 和纯表现桥接尽量下沉到活动/模块对象，而不是回到 Player 根节点。

## 5. 收口实施顺序

### 阶段 A：建立调用方基线

- 冻结 Network SDK/Transport 接入；
- 记录每个根组件的所有序列化引用、公共方法调用方和事件订阅；
- 为移动、交互、火焰、活动、Rest 建立最小回归清单；
- 不删除代码，不改变行为。

### 阶段 B：先建组合宿主，不搬逻辑

- 已新增非 `MonoBehaviour` 的 `PlayerCoreHost`，由 `LocalPlayerContext` 持有；
- 现有服务仍然显式挂在 Prefab 上并由原组件执行，宿主只集中基础上下文、可选服务初始化和模块注册；
- 远端 Player 不会通过宿主注册本地输入、EventBus 命令执行器或本地全局服务；
- `IPlayerModule` / `PlayerModuleContext` 已建立，缺少 Flame 或 Activity 模块时不再因为可选服务缺失而阻止基础 Player 就绪；
- `FirePlayPlayerInput` 现在只把 `Move`、`Look` 作为基础动作，Sprint、火焰、Rest、Activity 和 UI 动作均为可选动作；
- `PlayerMovement` 通过 `IPlayerSprintPolicy` 接入冲刺消耗，基础 Player 没有 FlameModule 时仍可移动和冲刺；
- 已新增 `Assets/FirePlay/Runtime/Prefab/PlayerCoreOnly.prefab`，作为基础 Player 的实验资产；它只保留 CharacterController、基础输入、移动、视角、LocalPlayerContext 和最小视觉子节点，不绑定任何可选功能组件。
- 已新增 `PlayerCameraTargetSet` / `IPlayerCameraTargetProvider`，Player 只提供框架无关的 Follow、Frame、LookAt、InputPivot 目标；Cinemachine 和活动专属目标不进入 Player Core。
- 静态 Prefab 检查和命令行编译已通过；需要 Play Mode 验收后，才能把它作为后续模块挂载基线。
- 已新增场景级 `ActivityCameraRig` / `ActivityCameraRigExecutor`，DemoScene 的活动 Presentation 已改为请求它；`RitualCameraDirector` 暂时只保留旧 Rest/观星/旧钓鱼入口和兼容字段。
- 已移除 `RitualCameraDirector` 中重复的新 Activity profile 字段和接口实现；Activity profile 只在 `ActivityCameraRig` 配置，旧 Director 只负责 Rest/观星/旧钓鱼。
- 已新增 `Modules/FlameModule` 子节点和 `FlameModule`，并由 `PlayerCoreHost` 自动发现和初始化；模块同时承接冲刺余火策略以及放置/升级篝火的世界操作组件。
- `PlayerMovement` 现在只依赖 `IPlayerSprintPolicy`，完整 Player 由 FlameModule 提供策略，Core-only Player 没有该模块时不再依赖火焰。
- `LocalPlayerContext`、`PlayerInteraction`、`WorldCommandExecutor` 与放置 UI 已改为从 Player 子树解析这两个世界操作组件；放置输入按 Player 根归属判断，避免子节点迁移后事件失效。
- `FlameResourceController`、`PlayerFlameController`、`FlameResourceVisualBridge`、`FlameContractionController`、`PlayerAtmosphereBridge` 已迁入 FlameModule；表现桥接、Rest、交互和邻近效果已改为从 Player 子树解析。
- 已建立 `ActivityModule`，并将 `FishingActivityVisuals` 移入其子节点；`PlayerActivityHost` 和 `PlayerActivityPresentationHost` 暂留根节点作为统一适配入口。
- `PlayerInteraction` 现在只负责扫描/目标描述，`InteractionRouter` 负责唯一的原始输入转语义意图入口；二者不再重复持有火焰世界执行字段。
- `InteractionModule` 已完成下沉；`LocalPlayerContext`、`PlayerProximityEffects`、TreeLight 选择器和子节点初始化均已适配父级查找。
- 下一步确认本地/远端 Player 生命周期，再处理 Player 根上剩余的 Rest、Activity Host 和通用表现服务；资源权威不再继续复制或拆分。

### 阶段 C：先建立模块边界，再迁移服务

`PlayerSharedStateService` 已迁入 `PlayerCoreHost`，但它不能继续成为把表情、活动或火焰强行绑定到基础 Player 的理由。后续先完成：

1. 将 `PlayerSharedStateAdapter`、表达/邻近效果归入 Activity/Presentation 或独立 StateModule；
2. 将 Flame 相关服务归入 FlameModule；
3. 将 Activity Host、Presentation Host 和活动 Visuals 归入 ActivityModule；
4. 基础 Player 在没有这些模块时仍能移动、转向和运行基础输入。

每迁移一个模块就进行一次编译和 PlayMode 回归，不做批量删除。

### 阶段 D：收口重复入口和活动残留

- 完成 `PlayerInteraction` 与 `InteractionRouter` 调用方迁移后，删除旧入口或降为纯扫描器；
- 将 `FishingActivityVisuals` 从 Player 根节点迁移到活动表现对象；
- 观星迁移为 Activity 前，保留 `RestInteraction`；
- 确认活动 Host、Presentation Host、火焰资源控制器的独立边界。

### 阶段 E：联机前门禁

只有以下条件全部满足，才恢复 Network SDK/Transport 工作：

- Player 根节点职责表已稳定；
- 本地与远端 Player 的核心服务初始化行为明确；
- 活动权威和表现请求仍能通过现有验收；
- 火焰资源、世界操作和活动动作没有绕过统一入口；
- 至少完成一次完整 Demo 回归。

## 6. 当前明确不做的事情

- 不为了减少组件数量，把 `PlayerActivityHost` 和 `PlayerActivityPresentationHost` 合成一个类；
- 不把 `FlameResourceController` 与 `PlayerFlameController` 合并；
- 不在观星迁移前删除 `RestInteraction`；
- 不在没有调用方清单的情况下直接删除 `PlayerInteraction`；
- 不在 Player 收口完成前继续增加网络同步代码。
