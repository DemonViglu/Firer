# FirePlay 场景接入清单

这是一份 Inspector 速查。完整从零搭建步骤见 `FirePlay_新关卡制作手册.md`。

## 场景常驻对象

- `FirePlayUiBootstrap`：启动唯一的 SUIFW Canvas；不要手动放第二个 Canvas Prefab；
- `CampfirePersistence`：引用 `CampFire.prefab`，开启 `Load On Start`；
- `RitualCameraDirector`：配置 Explore / Marshmallow / Stargaze / Fishing 相机与对应 Target Group；
- 全局夜景 Volume、主 Directional Light、SkyDome、Terrain。

## Player

使用 `Assets/FirePlay/Runtime/Prefab/Player.prefab`，不要删改其核心组件。确认：

- Input Action Asset 已引用；
- `PlayerMovement` 的 Camera Transform 指向 CameraPivot 或可自动解析；
- `CampfirePlacement` 引用 SmallFire Config、SmallFire Prefab、放置相机和可选 Preview；
- `CampfireUpgradeController` 引用 CampFire Prefab；
- Marshmallow / Fishing 道具 Transform、动画 Controller、相机锚点已按需求配置。

## Terrain 与 Android

- Terrain Layer：`Smoothness Source = Constant`、`Smoothness = 0`、`Metallic = 0`；
- 切 Android 平台后检查 Mobile URP 的画面、雾和阴影；
- Terrain Reflection Probe Usage 按场景反射需求决定，不是修复草地镜面感的手段。

## 世界对象

| 对象 | 必要组件 / 规则 |
|---|---|
| FlameSource | `FlameSource` + 唯一 `StableSceneId` + Collider；默认拾取后 600 秒刷新，可在 Inspector 关闭 `Respawn Enabled` 或调整 `Respawn Seconds`。 |
| 放置表面 | Collider + `CampfirePlacementSurface`，Layer 位于 Placement Layers。 |
| 预置 SmallFire | `SmallFire`；若会升级并需读档隐藏，添加唯一 `StableSceneId`。 |
| 预置 Campfire | `Campfire` + 唯一 `StableSceneId`；中心广场可设为满级。 |
| RestSpot | `RestSpot`；同一地点只挂一种占用主/次仪式输入的仪式。 |
| 大树 | `WorldTreeContribution` + Collider；当前每场景仅一棵可贡献树。 |

## 地点表现

- `CampfireSiteAwakeningVisuals`：填来源篝火或来源小火种 ID，配置 Stage 0–3 根物体；
- `WarmthNode`：局部温暖/灯光占位；
- `WorldTreeProgressVisuals`：按累计贡献切换大树阶段；
- Visuals、Light、VFX、Audio、Shader 只读取状态，不能反向改余火或篝火。

## UI

- SUIFW Canvas 位于 `Assets/Resources/SUIFW/Canvas.prefab`；布局只在 Prefab 内完成；
- `FirePlayMobileInputRouter` 是所有 Button 的唯一玩法入口；
- `FirePlayRitualPanel`、`FirePlayCampfirePanel`、`FirePlayPlacementPanel` 挂在始终启用的 `UIControllers`，Panel Root 初始可关闭；
- `FirePlayContextActionsPanel` 同样挂在 `UIControllers`，填入 ContextActions Root 和各自的拾取/添火/取火/回收/贡献/坐下 Button；`添火` 在小火种旁会创建公共篝火，在公共篝火旁会继续投入暖意。它只在对应对象附近显示按钮，没有任何交互对象时整个 ContextActions Root 自动隐藏；
- 右半屏创建透明 `LookArea`（Image 保持 `Raycast Target`），挂 `FirePlayMobileLookArea` 并引用同一 `FirePlayMobileInputRouter`；它置于普通 Button 后面，拖动右半屏空白即可转动视角；
- 旧 `StandaloneInputModule` 必须保持禁用，使用 `InputSystemUIInputModule`；
- Editor 测试按 F1 暂时释放/重新锁定鼠标。

## 移动端 Button 方法

- 世界：`GatherEmber`、`AddFire`、`DrawFire`、`ReclaimSmallFire`、`ContributeWorldTree`；
- 基础：`Rest`、`PlaceFire`、`CancelFirePlacement`；
- 仪式：`RitualPrimary`、`RitualSecondary`；
- 表达：`ExpressionWave`、`ExpressionThanks`、`ExpressionWarmth`、`ExpressionSit`；
- 大树颜色 Panel 后续调用 `WorldTreeContribution.SelectPersonalLightColor(Color)`。

## 每次改场景后的最小验证

1. 移动、镜头、余火与火源；
2. 放火 / 取消 / 回收 / 升火 / 喂火 / 取火；
3. 重进场景后的篝火与大树存档；
4. 每个 RestSpot 的坐下、仪式与镜头退出；
5. Android 平台 Terrain 与可点击 UI。
