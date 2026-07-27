# FirePlay 场景接入清单

新 session 接手顺序：先读 `FirePlay_开发状态.md`，再读本清单和当前里程碑路线图。

## Player

- 基础：`FirePlayPlayerInput`、`PlayerMovement`、`PlayerLook`、`PlayerInteraction`、`PlayerFlameController`、`FlameResourceController`；
- 火焰：`FlameContractionController`（C 原型）、`FlameResourceVisualBridge`；
- 小火种：`CampfirePlacement`（F 原型）、`CampfireUpgradeController`，其 `Campfire Prefab` 必须引用 Project 中的 `Assets/FirePlay/Runtime/Prefab/CampFire.prefab`，不可引用 Hierarchy 中对象；
- 停留：`RestInteraction`；
- 烤棉花：`MarshmallowInteraction`，引用 Player 下的手持占位物；Q 旋转仅为原型输入。

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
- `StargazingRitual`：与 RestSpot 同物体，指定场景 `Look Target`。停留时保持相机位置，只转向目标；
- `MarshmallowRitual`：与 RestSpot 同物体，标记可烤；Player 的 `MarshmallowInteraction` 在停留时显示手持占位物并支持 Q 旋转；
- 后续仪式都继承 `RestSpotRitual`，不直接修改 `RestInteraction`。

## 温暖节点

- 在需要被火点亮的区域创建空物体并挂载 `WarmthNode`；默认半径为 6 米，可按关卡空间调整。
- 可选：在该物体或其子物体添加 Point Light，并将其引用赋给 `Warmth Light`。Light 的最终 Intensity/Range 由 0–1 温暖值驱动；未引用 Light 时节点仍正常计算，可供后续 Shader、树或世界进度读取。
- `SmallFire` 按较低温暖值供热；等级大于 0 的 `Campfire` 提供更高温暖值，且随等级提高。节点不会改变火源、余火或存档。

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
