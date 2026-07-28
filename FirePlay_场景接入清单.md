# FirePlay 场景接入清单

新 session 接手顺序：先读 `FirePlay_开发状态.md`，再读本清单和当前里程碑路线图。

## Player

- 基础：`FirePlayPlayerInput`、`PlayerMovement`、`PlayerLook`、`PlayerInteraction`、`PlayerFlameController`、`FlameResourceController`；
- `PlayerMovement.Camera Transform` 可留空：运行时会优先使用 `PlayerLook` 的 `CameraPivot` 作为移动参考方向。不要为了移动而给 Unity Input System 的 `PlayerInput` 组件配置 Camera；本项目输入入口是 `FirePlayPlayerInput`。
- 火焰：`FlameContractionController`（C 原型）、`FlameResourceVisualBridge`；
- 小火种：`CampfirePlacement`（F 原型）、`CampfireUpgradeController`，其 `Campfire Prefab` 必须引用 Project 中的 `Assets/FirePlay/Runtime/Prefab/CampFire.prefab`，不可引用 Hierarchy 中对象；
- 停留：`RestInteraction`；
- 烤棉花：`MarshmallowInteraction`，引用 Player 下的手持占位物；Q 旋转仅为原型输入。
- 输入模式：Player 挂载 `PlayerModeController`。Exploring / Placing / Resting 由放火与停留自动切换；不要让其他组件自行维护第二套模式状态。
- 探索交互：无需新增场景组件。E 键目标由 `PlayerInteraction` 统一选择，调试面板的 `Interaction` 行直接显示实际将响应的目标；重叠时优先大树、其次自然火源、最后小火种。

## Prefab 与 ID

| 对象 | 关键组件 / 规则 |
|---|---|
| `SmallFire.prefab` | `SmallFire`；运行时放置不需要 Stable ID。若场景预置小火种可能升级，添加唯一 `StableSceneId`。 |
| `CampFire.prefab` | `Campfire`、`StableSceneId`；Prefab 的 ID 留空，勾选 `Allow Runtime Assignment`。动态升级时生成 `campfire.<GUID>`。 |
| 场景预置 Campfire | 手动填写唯一 Stable ID，例如 `center.campfire`；保存时按 ID 覆盖等级/投入，不会被重建。 |

## 篝火与存档

- `CampfireConfig_Prototype`：3 级，原型成本 25 / 35 / 50；G 为升级原型输入；
- `CampfirePersistence`：挂在任意常驻场景对象，引用 `CampFire.prefab`；保存文件为 `Application.persistentDataPath/fireplay-demo.json`；
- 运行时 Campfire 会实例化恢复；预置 Campfire 只覆盖状态；预置 SmallFire 若有 Stable ID 且被升级，加载时会隐藏来源小火种；
- 只有未来筛选后的少量 Campfire 才可能进入异步/联机同步；当前全部为本地模型。

## 停留仪式

- `RestSpot` 可直接挂在 `CampFire.prefab`，R 进入/退出原型停留；
- `StargazingRitual`：与 RestSpot 同物体，指定场景 `Look Target`（天空目标）与可选 `Companion Frame Target`（朋友/同伴模型胸口处的空物体）。进入时由独立 `CM_Stargaze` 将玩家、朋友和天空目标加入 Target Group；退出时自动清理。天空目标建议放在观星点前上方约 20–40 米处；
- `MarshmallowRitual`：与 RestSpot 同物体，标记可烤；在篝火火焰中心上方创建空物体 `MarshmallowLookTarget`（建议约高于地面 1.0–1.4m），并赋给仪式的 `Look Target`。停留时玩家会水平转向篝火、锁定常规视角并平滑看向该目标；Player 的 `MarshmallowInteraction` 同时显示手持占位物并支持 Q 旋转；
- 需要固定构图的后续仪式继承 `RestLookTargetRitual`；只需指定 `Look Target`，不直接修改 `RestInteraction`。同一 RestSpot 不要同时挂多个会锁定视角的仪式。

### Cinemachine 烤棉花镜头

- `CM_Explore` 的 `ExploreCameraAnchor`（Follow）与 `ExploreCameraLookAt`（Look At）均保留在 `CameraPivot` 下，使镜头能围绕玩家进行上下轨道旋转；推荐起点分别为 `(0, 1, -6)` 与 `(0, 1, 8)`。玩家移动逻辑只读取该 Pivot 的世界 Yaw，绝不读取其 Pitch/Roll。
- Player 下创建两个仪式构图锚点：`RitualCameraFollowAnchor` 放在约 `(0, 1.2, 0)` 并设 Local Rotation 为 `(0, 180, 0)`；`RitualCameraFrameTarget` 放在约 `(0, 1.25, 0)`。前者让 `Third Person Follow` 出现在角色正面偏侧，后者让 Target Group 对准胸口而非脚底。
- 场景常驻对象挂载 `RitualCameraDirector`，并引用 `CM_Explore`、`CM_Marshmallow`、`CinemachineTargetGroup`、Player 的 `RitualCameraFollowAnchor` 与 `RitualCameraFrameTarget`。探索/仪式优先级默认是 10 / 20。
- `CM_Explore` 保持当前已验证的 Follow/Look At 配置。`CM_Marshmallow` 的 Position Control 应使用 `Third Person Follow`，Rotation Control 使用 `Rotation Composer`；Director 会在仪式开始时将其 Follow 指向正面镜头 Anchor、Look At 指向 Target Group。
- Target Group 初始不放任何动态篝火目标。进入烤棉花时，Director 自动将玩家胸口锚点与当前 `MarshmallowLookTarget` 加入；退出时移除，因此运行时生成和存档恢复的 Campfire 都可使用同一组镜头。
- `MarshmallowRitual` 找不到有效 Director 时会回退为原先的固定 Look Target 镜头，仍可完成停留与棉花旋转。

### Cinemachine 观星镜头

- 新建 `CM_Stargaze` 和 `CM_Stargaze_TargetGroup`，初始 Priority 为 0。`CM_Stargaze` 推荐使用 `Third Person Follow` + `Rotation Composer`，并使用比烤棉花稍远的距离与更宽的 FOV（先试 Distance 5–7、FOV 70）。
- Player 下新增 `StargazingCameraFollowAnchor`，放在约 `(0, 1.4, 0)`；先以本地 Y 约 150–180° 作为从角色正面偏侧取景的起点。将它赋给 `RitualCameraDirector > Stargazing Follow Anchor`。
- `RitualCameraDirector` 还需引用 `CM_Stargaze` 和 `CM_Stargaze_TargetGroup`。Target Group 初始保持空，运行时由观星仪式填充玩家、朋友和天空。

## 温暖节点

- 在需要被火点亮的区域创建空物体并挂载 `WarmthNode`；默认半径为 6 米，可按关卡空间调整。
- 可选：在该物体或其子物体添加 Point Light，并将其引用赋给 `Warmth Light`。Light 的最终 Intensity/Range 由 0–1 温暖值驱动；未引用 Light 时节点仍正常计算，可供后续 Shader、树或世界进度读取。
- `SmallFire` 按较低温暖值供热；等级大于 0 的 `Campfire` 提供更高温暖值，且随等级提高。节点不会改变火源、余火或存档。

## 玩家氛围桥接

- 场景只保留一套固定的夜色 Global Volume；不要再用 Receiver/Giver 两套全局 Volume 随余火改变整屏曝光或颜色。余火强弱由现有 `FlameResourceVisualBridge` 与 `FlameVisuals` 驱动随身火苗尺寸、Shader 和 Point Light，温暖应主要出现在玩家附近的真实光照中。
- 可选：在 Player 挂载 `PlayerAtmosphereBridge`；它只读取同物体的 `FlameResourceController` 与 `RestInteraction` 来控制音景，不修改余火、停留、Volume 或存档状态。
- 建立三个循环 AudioSource 并关闭 `Play On Awake` 以外的随机启停逻辑：`Ambient Bed`（夜风/虫鸣/水声）、`Warmth Layer`（火焰/暖音乐层）、`Rest Layer`（近场篝火或仪式层）。先把 Clip 的无缝循环和 Mixer 路由配置好，再交给桥接器控制音量。
- `Ambient Minimum Volume` 防止火旺时环境完全消失。所有音景过渡都使用平滑插值；完成引用后在 Play Mode 用余火调试步进及进入/退出停留验证淡入淡出。

## 大树贡献（M4 基础）

- 在大树根物体挂载 `WorldTreeContribution`。树必须有启用的 Collider，且该 Collider 所在 Layer 包含在 Player 的 `Interaction Layers` 中；建议使用球形 Trigger 覆盖树根附近的停留区域。
- 靠近时按 E 消耗 `Contribution Cost`（默认 10）余火；本地玩家仅能贡献一次，状态与颜色会随存档恢复。Demo 不设探索或篝火前置。
- 可选：在树冠预留一个 Point Light，初始关闭后赋给 `Personal Light`；首次贡献后它会启用，并使用已选择的颜色。Player 挂载 `TreeLightColorSelector` 后，靠近大树按 V 可循环选择原型颜色，按 E 贡献；移动端 UI 直接调用 `SelectPersonalLightColor(Color)`。位置尚未固定为玩法数据，后续可扩展多个锚点。
- 多光点占位：树上额外挂 `TreePersonalLightVisuals`，赋予一个 Point Light Prefab 与树冠 `Light Root`。它按光点记录生成实例；当前本地仅有一条记录，未来可直接显示同步而来的其他玩家记录。

## 已验证

- 小火种放置、回收、上限、升级为公共篝火；
- 预置/运行时篝火保存与恢复，预置小火种升级后的隐藏；
- 停留、观星镜头引导/退出复原、烤棉花显示/旋转；
- 公共篝火等级视觉桥接（占位缩放，Light/VFX 可选）。
- 大树个人光点原型颜色选择（靠近大树按 V 循环后再按 E 贡献）。

场景、Prefab 或 Inspector 接入发生变化时，请同步更新本清单。
