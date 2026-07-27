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

## 已验证

- 小火种放置、回收、上限、升级为公共篝火；
- 预置/运行时篝火保存与恢复，预置小火种升级后的隐藏；
- 停留、观星镜头引导/退出复原、烤棉花显示/旋转；
- 公共篝火等级视觉桥接（占位缩放，Light/VFX 可选）。

场景、Prefab 或 Inspector 接入发生变化时，请同步更新本清单。
