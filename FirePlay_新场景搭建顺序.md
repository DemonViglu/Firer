# FirePlay 新互动场景搭建顺序

本文用于从一个空场景重新搭建一个包含 Player、Cinemachine、活动、篝火和火焰资源的 FirePlay 互动场景。

目标是先按依赖顺序建立一个可运行的单人闭环：

```text
Player 基础移动
    -> Explore Camera
    -> UI 系统
    -> 活动注册与活动逻辑
    -> 篝火 / RestSpot / ActivityAnchor
    -> 火焰资源
    -> 活动专属 Camera
    -> 活动进入、交互、退出
```

## 0. 先确定使用哪一个 Player Prefab

根据场景需要选择 Player：

| Prefab | 适用场景 |
|---|---|
| `Assets/FirePlay/Runtime/Prefab/Player.prefab` | 完整互动场景。包含余火、活动、交互、Rest、表现和相关模块。 |
| `Assets/FirePlay/Runtime/Prefab/PlayerCoreOnly.prefab` | 只验证基础移动、重力和镜头，不包含活动、篝火交互和余火模块。 |

如果目标是新建“包含活动、篝火等互动内容”的场景，应直接使用完整的 `Player.prefab`。不要从空 GameObject 手动逐个添加 Player 组件，否则很容易漏掉模块之间的引用。

## 1. 建立场景层级

建议先建立以下层级。层级名称不是运行时身份，稳定身份仍然要通过 `StableSceneId` 配置。

```text
NewInteractionScene
├── Systems
│   ├── GameManager
│   ├── FirePlayUiBootstrap
│   └── ActivityLogicFactory
├── Cameras
│   ├── Main Camera
│   ├── CM_Explore
│   ├── CM_Marshmallow
│   ├── CM_Fishing
│   ├── CM_Stargazing
│   ├── Activity Target Groups
│   └── ActivityCameraRig
├── GamePlay
│   ├── Players
│   ├── Campfires
│   ├── ActivityAnchors
│   ├── FlameSources
│   └── RestSpots
├── Environment
└── SpawnPoints
```

最省事的方式是复制当前 `DemoScene`，保留 `Systems`、`Cameras`、完整 Player 和活动设施，再替换 `Environment`。如果必须从空场景开始，则按下面的顺序配置，不要一开始就同时配置所有活动。

## 2. 先放地面和 Player

### 2.1 地面

先建立环境地面，并确认地面具有 Collider。Player 使用 `CharacterController`，没有 Collider 的地面无法提供正常的重力落地和移动碰撞。

### 2.2 Player

1. 将 `Assets/FirePlay/Runtime/Prefab/Player.prefab` 拖入场景。
2. 放到 `GamePlay/Players/Player` 下。
3. 将 Player 放在地面上方一点的位置，避免一开始嵌入地面。
4. 单人场景中保留 `LocalPlayerContext.IsLocalPlayer = true` 和 `PlayerActivityHost.IsLocalPlayer = true`。
5. 确认 Player 根节点的 `CharacterController` 尺寸与模型高度匹配。

当前 Player Prefab 已经包含：

- `FirePlayPlayerInput`
- `PlayerMovement`
- `PlayerLook`
- `LocalPlayerContext`
- `PlayerActivityHost`
- `PlayerActivityPresentationHost`
- `PlayerModeController`
- FlameModule、ActivityModule、InteractionModule
- Body、Hand、CameraPivot 和镜头目标节点

### 2.3 Player 基础移动引用

完整 Player 使用以下镜头目标：

- `PlayerLook.Camera Pivot`：`CameraPivot/ExploreCameraAnchor`
- Explore Camera 的 Look At：`CameraPivot/ExploreCameraLookAt`

不要把场景中的 `Main Camera` 直接拖进 `PlayerMovement` 的 Camera Transform。当前移动逻辑默认从 `PlayerLook.CameraPivot` 获得输入参考方向，保持该字段为空即可。

此时先进入 Play Mode，只验收：

- WASD / Move 输入正常；
- Look 输入正常；
- Player 能站在地面上；
- 重力和 CharacterController 碰撞正常；
- 没有活动和篝火时也不报错。

## 3. 配置基础 Cinemachine 镜头

### 3.1 Main Camera

在 `Cameras/Main Camera` 上配置：

- Unity `Camera`；
- `CinemachineBrain`；
- `AudioListener`；
- Tag 设置为 `MainCamera`。

### 3.2 CM_Explore

建立一个 `CinemachineCamera`，命名为 `CM_Explore`，放到 `Cameras` 下：

- Priority 设置为基础探索镜头的优先级，当前 Demo 使用 `10`；
- Tracking Target 指向 Player 的 `CameraPivot/ExploreCameraAnchor`；
- Look At 指向 Player 的 `CameraPivot/ExploreCameraLookAt`；
- 保留当前 Demo 使用的跟随、朝向组件，例如 `HardLockToTarget` / `HardLookAt`；
- 确认镜头距离、视野和阻尼符合场景需要。

重新进入 Play Mode，先只验收“Player 移动 + 镜头跟随”。基础镜头没有稳定前，不要继续排查活动镜头。

如果使用 `PlayerCoreOnly.prefab`，其目标节点名称是：

- `CameraPivot/PlayerCameraFollowTarget`；
- `CameraPivot/PlayerCameraLookAtTarget`。

## 4. 配置 UI 启动器

在 `Systems/GameManager` 或一个专门的 `Systems/UI` 对象上添加：

```text
FirePlayUiBootstrap
```

它负责初始化 SUIFW 的 `UIManager`，并绑定 `InputSystemUIInputModule`。当前 Demo 采用的是 `FirePlayUiBootstrap`，不需要为了新场景强行添加空的 `GameBootstrapper`。

进入 Play Mode 后，确认 UI Manager 初始化没有报错。活动 UI 使用的窗体 key 已登记在：

`Assets/Resources/SUIFW/UIFormsConfigInfo.json`

当前活动使用的 UI key 包括：

- `FishingActivityForms`；
- `MarshmallowActivityForms`；
- `GuitarActivityForms`。

## 5. 配置活动逻辑注册表

在 `Systems/ActivityLogicFactory` 上添加：

```text
ActivityLogicRegistryBehaviour
```

在其 `Factories` 数组中注册当前需要的 Factory。完整互动场景建议先注册全部已有活动：

| Activity | Factory |
|---|---|
| Marshmallow | Marshmallow Activity Logic Factory |
| Fishing | Fishing Activity Logic Factory |
| Emote | Emote Activity Logic Factory |
| Guitar | Guitar Activity Logic Factory |
| Stargazing | Stargazing Activity Logic Factory |

如果场景只需要篝火烤棉花糖，也可以先只注册 Marshmallow Factory；但 Player 上的 `ActivityCatalog`、Anchor 定义和 Registry 必须保持一致。

### PlayerActivityHost 必须绑定的对象

选中场景中的 Player，在 `PlayerActivityHost` 上确认：

- `Catalog Asset`：`Assets/FirePlay/Content/Activities/ActivityCatalog.asset`；
- `Logic Factory`：场景中的 `ActivityLogicRegistryBehaviour`；
- `Presentation`：Player 子节点上的 `PlayerActivityPresentationHost`；
- `Flame`：Player 子节点 `Modules/FlameModule` 下的 `FlameResourceController`；
- `Player State`：Player 子节点上的 `PlayerModeController`。

其中 Catalog、Flame 和 Player State 通常已经在完整 Player Prefab 中配置好；`Logic Factory` 和场景级 Presentation / Camera 引用通常需要在场景中重新绑定。

## 6. 放置篝火

将以下 Prefab 拖入 `GamePlay/Campfires`：

```text
Assets/FirePlay/Runtime/Prefab/CampFire.prefab
```

命名为 `CenterCampfire` 或其他能表达用途的名称。该 Prefab 已经包含：

- `Campfire`；
- `CampfireVisuals`；
- `RestSpot`；
- 一个 `ActivityAnchorNode`；
- 默认的 Marshmallow Activity Definition 引用；
- `CampfireConfig` 引用。

### 必须设置 StableSceneId

在场景实例上设置唯一稳定 ID，例如：

```text
campfire.center.01
```

不要把 GameObject 名称、层级路径或 Unity Instance ID 当作存档或未来网络身份。

如果后续要支持 SmallFire 放置，在环境地面或可放置区域添加 `CampfirePlacementSurface`，并确认其碰撞和可放置范围正确。第一轮只验证固定篝火时可以暂时不配置。

## 7. 放置 FlameSource

将以下 Prefab 放入 `GamePlay/FlameSources`：

```text
Assets/FirePlay/Runtime/Prefab/FlameSource.prefab
```

每个场景实例都要有唯一的 `StableSceneId`，例如：

```text
flamesource.01
flamesource.02
```

第一轮建议至少放一个 FlameSource，并先验证：

1. Player 的余火可以被 FlameSource 恢复；
2. 恢复后余火视觉状态正确；
3. FlameSource 本身不重复触发或产生异常消耗。

## 8. 配置活动 Anchor

### 8.1 篝火烤棉花糖

`CampFire.prefab` 已自带 Marshmallow Activity Anchor。确认该 Anchor 的：

- `StableSceneId` 唯一；
- `Activities` 中包含 Marshmallow Definition；
- Anchor 与篝火位置、玩家可达位置一致；
- 需要时配置对应的 Rule Provider。

### 8.2 Fishing

将以下 Prefab 放入 `GamePlay/RestSpots` 或 `GamePlay/ActivityAnchors`：

```text
Assets/FirePlay/Runtime/Prefab/Fishing_RestPot.prefab
```

它已经包含 Fishing Activity Anchor。设置场景实例的 Stable ID，并将钓鱼点放在玩家能接近的位置。

### 8.3 Stargazing

将以下 Prefab 放入 `GamePlay/RestSpots`：

```text
Assets/FirePlay/Runtime/Prefab/RestPot.prefab
```

它已经包含：

- `RestSpot`；
- Stargazing Activity Anchor；
- `StargazingActivityTrigger`；
- 活动镜头用的 Follow / LookAt / companion target；
- Stargazing Activity Definition。

如果场景有特定的陪伴角色，将其绑定到 RestPot 的 companion target；否则使用 Prefab 默认目标即可。

### 8.4 手动建立新的 Anchor

如果以后要在普通场景物体上增加活动：

1. 建立活动设施的视觉 GameObject；
2. 添加或放置 `StableSceneId`；
3. 添加 `ActivityAnchorNode`；
4. 为 Anchor 设置唯一 Stable ID；
5. 在 `Activities` 数组中添加对应的 `ActivityDefinitionAsset`；
6. 按需要添加距离、坐下、朝向或目标规则 Provider；
7. 确认 Player 能在 Anchor 的检测距离内接近它。

当前 `ActivitySelectionForms` 默认只会检查 Player 附近的 Anchor，常用检测距离约为 `3m`。距离、Anchor Definition、Catalog 或 Logic Registry 任一项缺失，都可能导致活动列表为空。

### 8.5 Anywhere 活动

Emote 和 Guitar 属于 Anywhere 活动，不需要 Anchor。它们仍然需要：

- Activity Catalog 中存在对应 Definition；
- Logic Registry 中注册对应 Factory；
- 需要 UI 时，UI key 已登记；
- Player 的 ActivityModule 和 PresentationHost 完整。

## 9. 配置活动专属 Camera

### 9.1 ActivityCameraRig

在 `Cameras/ActivityCameraRig` 上添加：

```text
ActivityCameraRigExecutor
```

至少绑定：

- `Explore Camera`：`CM_Explore`；
- `Fallback Player Frame Target`：Player 的通用镜头目标；
- 活动 Camera Profile；
- 每个活动需要的 Cinemachine Camera、Target Group、Follow Anchor 和 Look Target。

完整场景建议准备以下 profile：

| Profile ID | Camera | 用途 |
|---|---|---|
| `activity.ritual` | `CM_Marshmallow` | 篝火烤棉花糖 |
| `custom.fishing` | `CM_Fishing` | 钓鱼 |
| `activity.stargazing` | `CM_Stargazing` | 观星 |

Profile ID 必须与对应 `ActivityDefinitionAsset` 中的 `CameraProfileId` 完全一致，大小写也要一致。

### 9.2 PlayerActivityPresentationHost

在 Player 的 `PlayerActivityPresentationHost` 上确认：

- `Camera Executor` 指向场景中的 `ActivityCameraRigExecutor`；
- `Movement` 指向 PlayerMovement；
- `Look` 指向 PlayerLook；
- `Animation` 指向 Player 的动画表现组件（如果当前 Prefab 已配置则保持不变）。

活动进入时由 PresentationHost 请求镜头、移动锁定和表现；活动退出时由同一入口释放这些请求。因此不要给每个活动再添加一套独立的镜头控制器。

最稳妥的做法是复制 `DemoScene/Cameras` 整棵子树，再把其中的 Player 镜头目标重新指向新场景 Player。

## 10. 推荐的验收顺序

每完成一层就进入 Play Mode 验证，不要等所有对象都配置完再一次性排查。

### A. 基础 Player

- Player 生成在正确位置；
- Move / Look 输入正常；
- 重力、地面碰撞、转向正常；
- 没有 NullReference 或 Core Services 错误。

### B. 基础镜头

- Main Camera 有 CinemachineBrain；
- CM_Explore 正常跟随 Player；
- Player 离开和回到镜头范围时表现正常。

### C. UI 与活动注册

- UI Manager 初始化成功；
- `ActivityLogicRegistryBehaviour` 已注册所需 Factory；
- PlayerActivityHost 没有报告 Catalog / Logic Factory / Presentation 未就绪。

### D. 世界互动

- FlameSource 可以恢复余火；
- Player 接近篝火时能发现 Marshmallow 活动；
- Fishing / Stargazing Anchor 能在接近后出现在活动列表；
- 活动列表不会显示超出范围的 Anchor 活动。

### E. 活动生命周期

- 活动可以进入；
- 对应 UI、动画和镜头打开；
- 活动 Action 可以触发；
- 余火消耗和返回逻辑正常；
- 活动退出后移动、朝向和探索镜头恢复；
- 退出或销毁后没有重复订阅、残留 TargetGroup 或残留 UI。

## 11. 常见问题定位

| 现象 | 优先检查 |
|---|---|
| Player 不移动 | `FirePlayPlayerInput`、Input Actions、CharacterController、地面 Collider、LocalPlayerContext |
| 镜头不跟随 | Main Camera 的 CinemachineBrain、CM_Explore Target、Player CameraPivot 目标 |
| 活动列表为空 | Player 距离、Anchor Stable ID、Anchor Activities、Catalog、Logic Registry |
| 活动进入时报未就绪 | `PlayerActivityHost` 的 Logic Factory、Presentation、Flame、Player State 引用 |
| 活动镜头不切换 | `ActivityCameraRigExecutor`、Camera Profile ID、对应 Cinemachine Camera / TargetGroup |
| 活动退出后仍不能移动 | `PlayerActivityPresentationHost` 的锁定释放、ActivitySession 结束回调、RestInteraction 的坐下锁 |
| 余火恢复或消耗异常 | Player 的 FlameModule、FlameSource Stable ID、活动是否正确使用 `IActivityFlameResource` |

## 12. 和后续实时联机的边界

这份新场景配置流程先按单人运行闭环设计。进入 NGO 联机阶段时：

- Player 应改为通过 NetworkManager 的 PlayerPrefab 生成，而不是场景中固定放置多个本地 Player；
- LocalPlayerContext 的本地性不能继续依赖手工勾选的序列化 bool；
- 远端 Player 不能注册本地输入、EventBus、本地 Activity Logic 或本地 UI；
- 篝火、FlameSource、Anchor 和活动镜头的网络同步不放在第一阶段；
- 先验收 Host、Client、两个 Player 生成、个人输入归属和断开清理，再接活动与火焰权威。

## 13. 相关资源

- `Assets/FirePlay/Runtime/Prefab/Player.prefab`
- `Assets/FirePlay/Runtime/Prefab/PlayerCoreOnly.prefab`
- `Assets/FirePlay/Runtime/Prefab/CampFire.prefab`
- `Assets/FirePlay/Runtime/Prefab/FlameSource.prefab`
- `Assets/FirePlay/Runtime/Prefab/Fishing_RestPot.prefab`
- `Assets/FirePlay/Runtime/Prefab/RestPot.prefab`
- `Assets/FirePlay/Content/Activities/ActivityCatalog.asset`
- `Assets/FirePlay/Runtime/UI/FirePlayUiBootstrap.cs`
- `Assets/FirePlay/Runtime/Activity/ActivityLogicRegistryBehaviour.cs`
- `Assets/FirePlay/Runtime/Activity/ActivityAnchorNode.cs`
- `Assets/FirePlay/Runtime/Player/ActivityCameraRigExecutor.cs`
- `FirePlay_Player与活动迁移说明.md`
- `FirePlay_开发状态.md`
