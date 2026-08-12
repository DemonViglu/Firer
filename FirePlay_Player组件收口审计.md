# FirePlay Player 组合收口结论

> 本文记录 2026-08-12 收口后的最终资产职责。历史迁移以 Git 为准，不再把临时结构当作当前设计。

## 1. 结论

Player 的运行职责已经按“基础能力 + Gameplay 模块 + 网络适配”分开。后续美术、活动和场景开发不再继续重构 Player 总体架构；只有在具体需求证明现有边界无法承载时，才允许修改公共契约。

组件数量不是继续合并代码的理由。当前保留的独立边界具有不同生命周期或权威归属，例如：

- `PlayerActivityHost` 管理活动 Session 与权威动作；
- `PlayerActivityPresentationHost` 执行本地 UI、Camera 和 Player 表现请求；
- `FlameResourceController` 管理余火；
- `PlayerFlameController` 管理玩家持有的火苗对象；
- `FirePlayNetworkPlayer` 只负责 NGO 所有权、请求、快照与远端表现桥接。

这些职责不能为了减少 Inspector 中的组件数量而重新合并。

## 2. 正式 Player 资产

| Prefab | 用途 | 是否直接放入场景 |
|---|---|---|
| `PlayerCoreOnly.prefab` | 纯基础移动实验：输入、移动、Look、`LocalPlayerContext`、语义 Camera Targets 和占位 Body | 可以；只用于移动、镜头或模块实验 |
| `Player.prefab` | 完整单机 Gameplay 组合：基础 Player + Flame + Activity + Interaction + Rest + 表现 | 可以；`DemoScene`、`ArtScene` 与单机内容场景使用它 |
| `PlayerNetworkBase.prefab` | NGO 技术基座：基础移动、`NetworkObject`、`FirePlayNetworkPlayer` 与网络权威需要的 Flame 基础 | **不可以**；只作为网络 Variant 的父资产 |
| `PlayerNetworkGameplay.prefab` | 正式网络 Player：在 NetworkBase 上组合 Activity、Interaction、Rest、世界操作与活动 Visuals | 由 `NetworkManager` 生成，不手工放入玩法场景 |

`PlayerNetworkBase` 保留原网络基座 GUID，确保 `PlayerNetworkGameplay`、NGO Hash 和既有引用不因改名断裂。新的 `PlayerCoreOnly` 使用独立 GUID，避免网络与火焰能力再次污染纯基础资产。

`DefaultNetworkPrefabs.asset` 只登记实际可生成的正式网络 Prefab 和网络世界对象，不登记 `PlayerNetworkBase`。

## 3. 模块边界

```text
基础 Player
  ├─ FirePlayPlayerInput
  ├─ PlayerMovement
  ├─ PlayerLook
  ├─ LocalPlayerContext / PlayerCoreHost
  └─ PlayerCameraTargetSet

完整 Gameplay
  ├─ FlameModule
  ├─ ActivityModule
  ├─ InteractionModule
  ├─ Rest
  └─ Animation / Expression / Proximity / World Command 表现与服务

网络适配
  ├─ NetworkObject
  └─ FirePlayNetworkPlayer
       └─ 请求 Host 裁决并同步稳定事实，不拥有具体活动玩法
```

活动 Logic 不挂在 Player 上。活动内容继续由 `ActivityDefinition + Logic + Factory + Form + Visuals` 组合，Player 只持有通用 Host 和表现执行器。

Camera 的 Cinemachine 配置属于场景级 `ActivityCameraRigExecutor`。Player 只暴露 Follow、Frame、LookAt、InputPivot 等语义目标。

## 4. 冻结规则

美术技术阶段遵守以下规则：

1. 不把具体活动字段加入 `PlayerActivityHost`、`LocalPlayerContext` 或网络层；
2. 不让 Shader、灯光、粒子、植被或环境音直接修改余火、篝火或活动状态；
3. 表现层读取状态、事件或只读快照，再驱动 Renderer、VFX、Audio、Volume 和 Animator；
4. 不在运行时用 `AddComponent` 补核心 Player 能力；Prefab 缺配置时直接报告；
5. 不因为 Hierarchy 中组件较多而合并不同权威或生命周期的组件；
6. `PlayerNetworkBase` 不作为测试角色或场景 Player 使用；
7. `PlayerCoreOnly` 必须持续保持不含 Flame、Activity、Rest、Interaction 与 Network。

## 5. 有意保留的技术债

- `Player.prefab` 是已验收的完整单机组合，并非 `PlayerCoreOnly` 的 Variant；当前不为追求继承形式而重做它。
- `RestInteraction` 继续承载坐下与观星的轻量休息链路；观星不是独立小游戏，不强制重写为完整活动玩法。
- `PlayerActivityHost` 文件偏大，但公共职责仍集中在 Session、网络镜像和 Presentation 协调；没有真实修改压力前不做纯行数拆分。
- `PlayerSharedStateAdapter` 仍是 Unity 表现适配器；它不是新增玩法状态的入口。

以上项目不阻塞美术技术与内容生产。真正实现伙伴赠送、动作级目标选择或联机异常恢复时，再分别建立最小契约。

## 6. 收口验收

Unity 重导入后验证：

1. `PlayerCoreOnly` Inspector 中没有 Network、Flame、Activity、Rest 或 Interaction 组件；
2. `PlayerNetworkGameplay` Prefab Variant 的父资产显示为 `PlayerNetworkBase`，没有 Missing 引用；
3. `DefaultNetworkPrefabs` 包含 `PlayerNetworkGameplay`，不包含 `PlayerNetworkBase`；
4. `DemoScene` 继续使用 `Player.prefab`，完整单机移动、余火、活动、Rest 与世界交互正常；
5. 命令行编译为 0 错误、0 警告。
