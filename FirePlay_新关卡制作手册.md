# FirePlay 新关卡制作手册（作者内部版）

本手册只面向项目作者。它的目标是：在**不改动核心代码**的前提下，新建一张可独立游玩的单机关卡，并接入余火循环、公共篝火、仪式、大树与 UI。

当前封包边界：一张关卡默认只有一名本地 Player、一棵可贡献大树和一套共享 HUD。不要在新关卡内复制或修改 `Assets/FirePlay/Runtime/Prefab/Player.prefab` 的核心逻辑。

## 一、从空场景到可进入 Play Mode

1. 新建场景，加入 Build Settings。
2. 放入 `Player.prefab`；确认其 Input Action、CharacterController、`PlayerLook`、`PlayerMovement`、`PlayerInteraction`、`RestInteraction`、`CampfirePlacement`、`CampfireUpgradeController`、`FlameResourceController`、两类仪式 Interaction 与 `PlayerModeController` 均保持完整。
3. 放入 `SkyDome.prefab`、Terrain、主 Directional Light、Global Volume，以及实际需要的环境模型。
4. 放置 Main Camera / Cinemachine 现有相机组，并按 DemoScene 的 `RitualCameraDirector` 配置 Explore、Marshmallow、Stargaze、Fishing 四组镜头。不要让 Explore Anchor 脱离 Player 的 `CameraPivot` 层级。
5. 在任意常驻根物体挂 `FirePlayUiBootstrap`。不要手动拖入 `Resources/SUIFW/Canvas.prefab`；它会被 SUIFW 自动实例化并跨场景保留。
6. 在任意常驻根物体挂 `CampfirePersistence`，指定 `CampFire.prefab`，开启 `Load On Start`。这是当前本地存档入口。

首次进 Play Mode，确认：可走、可转镜头、余火 HUD 可更新、不会生成第二个 Canvas、Console 没有 Input System 的 `StandaloneInputModule` 异常。

## 二、地形与夜景基线

- Terrain Layer 的 `Smoothness Source` 必须设为 **Constant**，`Smoothness = 0`，`Metallic = 0`。不要使用 `Diffuse Alpha Channel`，草贴图 Alpha 会被读成平滑度，Android 下会产生强烈假镜面高光。
- Terrain 的 `Reflection Probe Usage` 按该关卡是否需要地表反射环境决定；它不是草地假镜面高光的修复手段。
- Android 默认使用 Mobile Quality / Mobile URP：Forward、无 SSAO、阴影较轻。每张新关卡至少在 PC 与 Android 平台各看一次，不要只在 PC Deferred + SSAO 下调氛围。
- 夜景的目标不是“完全看不见”，而是“沉静 ↔ 苏醒”。暗处也要能行走和辨识轮廓；火光与地点苏醒负责温暖、色彩、生机和声音变化。

## 三、余火路线与自然火源

### FlameSource

在路线节点放置 `FlameSource`，并添加唯一 `StableSceneId`。默认拾取后 10 分钟刷新；可在 Inspector 关闭 `Respawn Enabled` 使它成为一次性补给，或调整 `Respawn Seconds`。不要布置成几秒刷新一次的充电桩。

- 每条 5–10 分钟的探索支线先布置 3–6 个补给点，再通过 Play Mode 调整数量与数值；
- 让玩家看得见下一个希望到达的点，但不要把所有补给排成等距直线；
- 使用树脂、余烬、萤火草、暖石等不同美术表现时，仍可复用同一 `FlameSource` 状态组件。

### WarmthNode

需要让“这里有火所以环境活了”的区域，创建空物体挂 `WarmthNode`。它只读附近 SmallFire / Campfire 的热度；可选绑定 Point Light，后续 Shader、音频、粒子也应该读取它或地点成长状态，而非反向修改篝火。

## 四、小火种与公共篝火

### 自由小火种

`CampfirePlacement` 已在 Player 上。允许放置的地面 Collider 必须挂 `CampfirePlacementSurface`，并处于 `SmallFireConfig.Placement Layers` 可射线命中的 Layer。

小火种是临时照明与公共篝火的起点。它没有 Stable ID 也可运行；但如果你在场景中**预置**一个小火种，并允许它升级且需要读档后保持隐藏，必须手工给该 SmallFire 填唯一 `StableSceneId`。

### 公共篝火

- 动态篝火由小火种第一次“升火”生成，自动拥有运行时 ID；
- 场景预置篝火必须手填唯一 `StableSceneId`，例如 `forest.campfire.01`；
- 中心广场的篝火是预置、默认满级的特殊火堆，不计入“最多四座运行时篝火”；
- `CampfireConfig` 管理添火成本、热量、衰减、取火规则与成长阈值；新关卡优先复制现有 Config 后调数值，不要在代码里写关卡特例；
- 小火种与公共篝火的场景交互要同时提供“升火/喂火”和“回收/取火”两种独立 UI 行为。

### 地点苏醒

在地点根物体挂 `CampfireSiteAwakeningVisuals`：

1. 预置公共篝火：填 `Source Campfire`；
2. 预置小火种成长：填该小火种的 `StableSceneId` 到 `Source Small Fire Site`；
3. 创建 Stage 0–3 四个同级根物体，填入 `Stage Roots`；
4. 每个 Stage 根下只放美术：模型、灯、粒子、音频、植被。

Stage 依据累计等级，而非当前热量。因此火熄灭后，地点仍保留“曾被唤醒”的痕迹。

## 五、停留点与仪式

### RestSpot

火堆或湖边对象添加 `RestSpot`。玩家进入半径后可坐下；`RestInteraction` 处理移动锁定、镜头下沉和离座，不要在仪式脚本里重复做这些事。

一个 RestSpot 只挂一种会占用 Q/E 的主仪式：

- `MarshmallowRitual`：必须与仍燃烧的 Campfire 同物体，设置火焰 `Look Target`；
- `FishingRitual`：设置水面 `Look Target`，不需要 Campfire；
- `StargazingRitual`：设置天空 Look Target，可附朋友占位点；不占 Q/E 的资源循环。

不要把钓鱼与烤棉花挂在同一 RestSpot；当前两者都会读取相同的主/次仪式输入。

未来吉他遵循同一模式：新建 `GuitarRitual : RestSpotRitual`（地点配置）与 Player 侧 `GuitarInteraction`（玩法输入/道具），复用 `PlayerRitualAnimationController` 和 `RitualCameraDirector`，不要塞进 `MarshmallowInteraction`。

## 六、大树

每张关卡当前只支持一棵可保存的大树：

1. 树根放 `WorldTreeContribution` 和 Collider；
2. 可选挂 `TreePersonalLightVisuals` 与 `WorldTreeProgressVisuals`；
3. 后者配置按累计贡献切换的 Stage 根节点；
4. 移动端按钮使用 `ContributeWorldTree`；贡献前颜色未来通过专用颜色 Panel 调用 `SelectPersonalLightColor(Color)`。

限制：`CampfirePersistence` 当前通过全局查找读写第一棵大树。新关卡中不要放第二棵可贡献大树；多树是未来重构项。

## 七、移动端 UI 场景接入

UI 的布局只在 `Assets/Resources/SUIFW/Canvas.prefab` 内维护。建议按照以下状态组织，不要把 Button 放到 World Prefab：

- Persistent HUD：余火条、文本；
- Context Actions：按附近对象显示火源 / 小火种 / 篝火 / 大树的独立按钮；
- Ritual Panel：坐下且拥有对应仪式时显示；
- Placement Overlay：进入放置后显示，点空白取消、点确认放下；
- Mobile Controls：摇杆、坐下、放火；
- Look Area：右半屏透明触摸区，拖动控制镜头；
- Expression Wheel：仅预留。

所有 Button 只绑定 `FirePlayMobileInputRouter`。常用方法：`GatherEmber`、`TendFire`、`DrawFire`、`StartPublicFire`、`ReclaimSmallFire`、`ContributeWorldTree`、`Rest`、`PlaceFire`、`CancelFirePlacement`、`RitualPrimary`、`RitualSecondary`。

`FirePlayRitualPanel`、`FirePlayCampfirePanel`、`FirePlayPlacementPanel` 必须挂在始终激活的 `UIControllers`，再把实际会隐藏的 Panel 根拖入字段；不要把控制组件挂在它将自行关闭的 Panel 根上。

## 八、每张新关卡验收顺序

1. PC Play Mode：移动、镜头、余火消耗、火源拾取；
2. 放火 → 回收 → 升公共篝火 → 喂火 → 取火；退出重进验证存档；
3. 每个 RestSpot：坐下 / 离座、对应仪式、镜头回到探索；
4. 大树：选色（后续 Panel）、贡献、读档恢复、阶段视觉；
5. Android 平台 Editor：Terrain 无假镜面、UI 可点击、镜头与摇杆不冲突；
6. 空存档与已有存档各测一次，避免 Stable ID 冲突或旧记录误恢复；
7. 通过后提交 Git。新关卡的模型、Terrain、灯光、音频、Canvas 改动可以独立于 Runtime 代码提交。
