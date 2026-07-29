# FirePlay 场景接入清单

新 session 接手顺序：先读 `FirePlay_开发状态.md`，再读本清单和当前里程碑路线图。

## Player

- 基础：`FirePlayPlayerInput`、`PlayerMovement`、`PlayerLook`、`PlayerInteraction`、`PlayerFlameController`、`FlameResourceController`；
- `PlayerMovement.Camera Transform` 可留空：运行时会优先使用 `PlayerLook` 的 `CameraPivot` 作为移动参考方向。不要为了移动而给 Unity Input System 的 `PlayerInput` 组件配置 Camera；本项目输入入口是 `FirePlayPlayerInput`。
- 火焰：`FlameContractionController`（C 原型）、`FlameResourceVisualBridge`；
- 小火种：`CampfirePlacement`（F 原型）、`CampfireUpgradeController`，其 `Campfire Prefab` 必须引用 Project 中的 `Assets/FirePlay/Runtime/Prefab/CampFire.prefab`，不可引用 Hierarchy 中对象；
- 公共篝火数量：`CampfireUpgradeController.Maximum Active Runtime Campfires` 默认是 `4`，限制玩家由小火种创建且尚未退役的公共篝火数量；中心广场等场景预置火堆不计入。低级火熄灭并退役后会腾出名额；满级熄火后留下的可复燃火堆仍占用一个名额。
- 停留：`RestInteraction`；
- 烤棉花：`MarshmallowInteraction`，引用 Player 下的手持占位物；Q 用于拟造和在火候区翻面，E 用于收取完成的棉花糖。
- 钓鱼：在 Player 根物体添加 `FishingInteraction`；可选赋予一个默认关闭的手持鱼竿占位物。Q 用于拟造鱼竿与抛竿，鱼咬钩时 E 收线。未指定鱼竿物体时玩法仍可验证。
- 烤制表现：将 `MarshmallowVisuals` 挂到该手持棉花糖道具的根物体；它自动读取子 Renderer 与父级的 `MarshmallowInteraction`，以 MaterialPropertyBlock 切换 Raw / Roasting / Perfect / Toasted / Scorched 颜色。不要为此复制或实例化材质；若自动查找失败，再在组件 Inspector 手动指定 Interaction 与 Renderers。可选地在同物体挂一个 AudioSource，并给组件的 Materialize、Perfect、Toasted、Scorched、Eat、Cancel Clip 槽位分别拖入短音效；缺任何引用都会静默跳过。
- 可选动作：在 Player 根物体添加唯一的 `PlayerRitualAnimationController`，由它引用 Animator 并统一管理所有仪式动作。未添加该组件或 Animator 参数尚未配置时，玩法保持正常且不会报错。
- 输入模式：Player 挂载 `PlayerModeController`。Exploring / Placing / Resting 由放火与停留自动切换；不要让其他组件自行维护第二套模式状态。
- 探索交互：无需新增场景组件。E 键目标由 `PlayerInteraction` 统一选择，调试面板的 `Interaction` 行直接显示实际将响应的目标；重叠时优先大树、公共篝火、小火种，最后才是路线燃料节点。

## HUD（SUIFW）

- SUIFW 资源已归入 `Assets/Resources/SUIFW`。`SysDefine` 使用 `SUIFW/...` 路径，`UIFormsConfigInfo.json` 的窗体路径也已同步；项目 TagManager 已注册 `_TagCanvas` 和 `_TagUICamera`。框架可加载 `SUIFW/Canvas` 与其窗体资源。
- `Resources/SUIFW/Canvas.prefab` 已改为 **Screen Space - Overlay**，其旧版 `UICamera` 已停用。FirePlay 的 Cinemachine 世界相机是唯一的世界渲染相机；不要重新启用该 UICamera，除非将来专门重做多相机 UI。
- 同一 Canvas 的旧 `StandaloneInputModule` 已停用：项目只启用新 Input System，旧模块会调用 `UnityEngine.Input` 并抛异常。当前 HUD 没有按钮，因此不需要 UI 输入模块；未来做可点击菜单时，在 EventSystem 改用 `InputSystemUIInputModule` 并配置独立 UI Actions，不能重新启用旧模块。
- 在 `Assets/Resources/SUIFW/Canvas.prefab` 的 `Fixed` 子节点下创建 `FirePlayHUD`，把 `FirePlayHudForm` 挂在该对象。不要在 DemoScene 额外放第二个 Canvas；后续由 SUIFW 的 UIManager 实例化此 Canvas。分别配置 `Fuel Root`、Filled 类型的 `Fuel Fill Image`、可选 Flame Image、Fuel Text、Interaction Prompt Root/Text、Ritual Prompt Root/Text。Player 状态引用可留空，运行时自动查找；明确指定可减少查找。
- 在 DemoScene 任一常驻空物体（推荐 `GameSystems`）添加 `FirePlayUiBootstrap`。它只在启动时调用 SUIFW `UIManager`，由后者从 `Resources/SUIFW/Canvas` 生成并持久化唯一的 Canvas 根；不应同时手动把该 Canvas 拖进场景。
- `Fuel Fill Image` 的 Image Type 必须是 `Filled`，Fill Method 推荐 Horizontal。HUD 仅显示余火、探索交互和当前仪式提示，不显示调试状态、背包或任务目标。

### 移动端 UI（M3 当前接入）

- UI 结构固定为五个互不持有玩法状态的根节点：`PersistentHUD`（余火）、`ContextActions`（火堆/火源/大树）、`RitualPanel`（停留后的仪式）、`MobileControls`（摇杆和常用按钮）、`ExpressionWheel`（表情/动作）。它们都置于 SUIFW Canvas 的 `Fixed` 下；不要把按钮写进 Campfire、RestSpot 或 Player Prefab。
- `FirePlayHudForm` 仍只负责读取余火与文本提示。它会自动寻找名为 `FuelRoot`、`FuelFill`、`FuelText`、`InteractionPromptRoot`、`InteractionPromptText`、`RitualPromptRoot`、`RitualPromptText` 的子节点；`FuelFill` 会在运行时强制设为 Horizontal Filled。当前 Canvas 中的 `RitualPromptText` 必须指向 `RitualPromptRoot` 下的真实 Text，而不是留空。
- 在 `MobileControls` 根节点添加 `FirePlayMobileInputRouter`。所有普通 UI Button 的 OnClick 直接绑定同一对象：`Interact`（添火/捡余烬/贡献大树）、`Rest`（坐下或起身）、`RitualPrimary`（棉花糖拟造/翻面、钓鱼拟杆/抛竿）、`RitualSecondary`（吃棉花糖/收线）、`WithdrawOrReclaim`（取火/回收小火种）、`PlaceFire`、`CycleTreeLightColor`。按钮不直接调用任何 World 组件。
- 在左下的摇杆背景对象添加 `FirePlayMobileJoystick`，并填入同一 `FirePlayMobileInputRouter`、自身 RectTransform（`Joystick Area`）和摇杆 Handle。建议 Area 约 `180 × 180`、`Handle Range` 约 `60–72`；它只向 `FirePlayPlayerInput` 写入虚拟 Move，仍由现有 `PlayerMovement` 和 Camera Yaw 决定实际移动方向。
- `FirePlayUiBootstrap` 会在 SUIFW 实例化 Canvas 后，为 EventSystem 自动添加并配置 `InputSystemUIInputModule`；旧 `StandaloneInputModule` 保持禁用。不要重新启用旧模块，也不需要在 PlayerInput 上指定 UI Camera。
- 推荐移动端首版布局：左上余火；屏幕中央下方 `ContextActions` 单个主按钮（文字随目标变化）；右下 `Rest` 与表情轮盘入口；进入停留后隐藏探索主按钮、显示 `RitualPanel` 的主/次按钮。火堆和大树各自的详细信息面板留在对应子根节点中，只读取 `PlayerInteraction.CurrentInteractPrompt`、`Campfire` 或 `WorldTreeContribution` 状态，后续添加而不修改移动输入。
- 当前已提供可直接 Play Mode 验收的 `FirePlayMobileOverlay`：`FirePlayUiBootstrap` 会在 SUIFW Canvas 实例化后的 Start 自动将它生成到 `Fixed` 下，同时隐藏旧的 `FuelRoot`、`InteractionPromptRoot`、`RitualPromptRoot` 占位节点。首版包含余火条、左摇杆、动态场景主互动、Rest/Stand、仪式主/次按钮和四项表情轮盘；美术阶段只需替换其运行时层级中的 Image/Text 样式，逻辑无需重绑。
- 表情轮盘的 Wave / Thanks / Warmth / Sit 会请求 Player 上 `PlayerRitualAnimationController` 的 `EmoteWave`、`EmoteThanks`、`EmoteWarmth`、`EmoteSit` Trigger。当前 Animator 未配置这些参数时会静默无动作；后续导入动作后只需在该 Controller Inspector 改映射，不改 UI。

## Prefab 与 ID

| 对象 | 关键组件 / 规则 |
|---|---|
| `SmallFire.prefab` | `SmallFire`；运行时放置不需要 Stable ID。若场景预置小火种可能升级，添加唯一 `StableSceneId`。 |
| `CampFire.prefab` | `Campfire`、`StableSceneId`；Prefab 的 ID 留空，勾选 `Allow Runtime Assignment`。动态升级时生成 `campfire.<GUID>`。 |
| 场景预置 Campfire | 手动填写唯一 Stable ID，例如 `center.campfire`；保存时按 ID 覆盖等级/投入，不会被重建。中心广场的 Level 3 只在场景实例上设置，不作为动态篝火起始状态。 |

## 篝火与存档

- `CampfireConfig_Prototype`：3 级，原型成本 25 / 35 / 50；G 为升级原型输入；
- `CampfirePersistence`：挂在任意常驻场景对象，引用 `CampFire.prefab` 并保持 `Load On Start` 开启；成功创建/添火会自动保存，应用暂停/退出也会保存最终热量。保存文件为 `Application.persistentDataPath/fireplay-demo.json`；
- 公共篝火交互：`E` 添火（原型为 5 余火）；`G` 应急取火（原型为恢复 5 余火、消耗 25 热量）。取火只在玩家至少缺少 5 余火、火尚未熄灭且热量足够时成功；
- 运行时 Campfire 会实例化恢复；预置 Campfire 只覆盖状态；预置 SmallFire 若有 Stable ID 且被升级，加载时会隐藏来源小火种；
- 只有未来筛选后的少量 Campfire 才可能进入异步/联机同步；当前全部为本地模型。

## 天然火源与余火补给

- `FlameSource` 现在是路线上的一次性主动补给：靠近后按 E 收集，恢复余火后立刻失去 Collider 和可见模型。本局不会刷新，重新进入 Play Mode 后恢复；表现可替换为树枝、树脂、余烬、萤火草或暖石。
- `Restore Amount` 初始用约 `15–25`。不要重新加入短冷却：它是路线资源，不是原地充电点。
- 当前不保存已采集状态；只有验证确实需要跨重启保留消耗时，再为资源节点独立增加存档记录。它已有 Stable ID 基础，但不应现在扩大存档模型。
- 推荐职责分工：小树脂（约 +15）用于常规暗路补给；暖石/萤火草（约 +25）作为重要喘息点；燃烧中的公共篝火不直接恢复余火，而是将夜间消耗降低到附近 `x0.375`、坐下 `x0.125`，提供停留、续火和留下温暖的选择。

## 停留仪式

- `RestSpot` 可直接挂在 `CampFire.prefab`，R 进入/退出原型停留；
- `StargazingRitual`：与 RestSpot 同物体，指定场景 `Look Target`（天空目标）与可选 `Companion Frame Target`（朋友/同伴模型胸口处的空物体）。进入时由独立 `CM_Stargaze` 将玩家、朋友和天空目标加入 Target Group；退出时自动清理。天空目标建议放在观星点前上方约 20–40 米处；
- `MarshmallowRitual`：与 RestSpot 同物体，标记可烤；在篝火火焰中心上方创建空物体 `MarshmallowLookTarget`（建议约高于地面 1.0–1.4m），并赋给仪式的 `Look Target`。停留时玩家会水平转向篝火、锁定常规视角并平滑看向该目标；第一次按 Q 消耗 `Materialize Fuel Cost`（默认 3）拟造棉花糖。随后屏幕底部自动显示火候条：指针单向从左向右循环，每轮随机生成金黄区；在金黄区按 Q 翻面后，下一面会重置指针并生成新金黄区，两轮后按 E 吃掉。两轮全中为 Perfect、命中一轮为 Toasted、零命中为 Scorched；`Perfect / Toasted / Scorched Eat Fuel Refund` 默认是 2 / 1 / 0，可在组件 Inspector 调整。离座或篝火熄灭会取消本次棉花糖，且结果不保存；
- `FishingRitual`：与湖边的 `RestSpot` 同物体；在水面中央前方创建空物体 `FishingLookTarget` 并赋给其 `Look Target`，人物会朝向水面。场景常驻的 `RitualCameraDirector` 还需配置独立的 `CM_Fishing`、`Fishing Target Group` 与可选 `Fishing Follow Anchor`；进入钓鱼时只将玩家胸口锚点和水面目标加入该 Group，离座恢复探索镜头。将 `CM_Marshmallow` 复制为 `CM_Fishing` 后，把镜头距离 / FOV 调大即可做更宽广的湖景构图，二者互不影响。无需篝火。按 Q 拟造鱼竿（默认消耗 30），再按 Q 抛竿；随机等待后出现咬钩窗口，按 E 收线。默认每条返还 8、每根竿成功钓满 4 条，共返还 32；离座、漏钩或未钓满都不会返还竿成本。`Rod Fuel Cost`、`Catches Per Rod`、`Fuel Per Catch` 与咬钩时间均可在 Inspector 调整。不要与 `MarshmallowRitual` 挂在同一 RestSpot，以免共用 Q/E 输入。
- 仪式 Animator 参数由 Player 上唯一的 `PlayerRitualAnimationController` 管理；默认包含 Bool `IsResting`、`IsMarshmallowRoasting`、`IsGuitarPlaying`、`IsFishing` 与 Trigger `MarshmallowMaterialize`、`MarshmallowTurn`、`MarshmallowEat`、`MarshmallowCancel`、`RitualOffer`、吉他/钓鱼动作。后续仪式只请求抽象状态或动作，不再持有 Animator 或参数字符串；参数映射仅在该 Controller Inspector 里调整。
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

## 地点苏醒表现

- `WarmthNode` 只负责近场、临时的温暖值（例如局部灯光或环境音），不是地点成长状态。
- 对需要“沉静 → 初醒 → 温暖 → 盛放”变化的作者布置地点，创建空物体并挂载 `CampfireSiteAwakeningVisuals`。若是场景预置公共篝火，赋给 `Source Campfire`；若从预置小火种开始，则为该 SmallFire 填写唯一 `StableSceneId`，并把该组件拖到 `Source Small Fire Site`。后者在运行时升级和读档恢复后都会自动找到对应公共篝火。
- 创建四个同级根物体并分别填入 `Stage Roots[0..3]`。每个根可包含任意模型、Light、ParticleSystem、AudioSource 和植被对象；组件始终只激活与篝火持久化 `Level` 相同的一组。
- 该表现由等级而非 `Warmth` 驱动：火焰熄灭后，已苏醒的地点仍保留对应美术状态；恢复存档后也会自动回到正确阶段。
- 中心广场是默认满级的特殊地点，不挂本组件；它只随当前热量改变火焰、近场灯光和音效。

### 篝火生命周期（M3）

- 不需要新增场景组件。运行时生成的 0–2 级公共篝火热量耗尽后会自行移除，并以 `retired` 记录保存；其来源 `SmallFire` 必须已有唯一 `StableSceneId`，这样读档后才会保持隐藏。
- 运行时达到满级的公共篝火耗尽后会保留模型与交互组件；火焰、灯光和粒子由现有 `CampfireVisuals` 按热量熄灭，玩家可以再次按 E 添火。
- 若地点需要保留苏醒表现，请为 `CampfireSiteAwakeningVisuals > Source Small Fire Site` 指向该来源小火种的 `StableSceneId`。即使低级火已消失，读档后仍会显示它最后达到的阶段。

## 玩家氛围桥接

- 场景只保留一套固定的夜色 Global Volume；不要再用 Receiver/Giver 两套全局 Volume 随余火改变整屏曝光或颜色。余火强弱由现有 `FlameResourceVisualBridge` 与 `FlameVisuals` 驱动随身火苗尺寸、Shader 和 Point Light，温暖应主要出现在玩家附近的真实光照中。
- 可选：在 Player 挂载 `PlayerAtmosphereBridge`；它只读取同物体的 `FlameResourceController` 与 `RestInteraction` 来控制音景，不修改余火、停留、Volume 或存档状态。
- 建立三个循环 AudioSource 并关闭 `Play On Awake` 以外的随机启停逻辑：`Ambient Bed`（夜风/虫鸣/水声）、`Warmth Layer`（火焰/暖音乐层）、`Rest Layer`（近场篝火或仪式层）。先把 Clip 的无缝循环和 Mixer 路由配置好，再交给桥接器控制音量。
- `Ambient Minimum Volume` 防止火旺时环境完全消失。所有音景过渡都使用平滑插值；完成引用后在 Play Mode 用余火调试步进及进入/退出停留验证淡入淡出。

## 夜色、雾与天空

- 地形、树和路径使用场景 Fog 隐入远处；场景内 SkyDome、Stars、Moon 应使用 `DemonViglu/FirePlay/URP Sky No Fog`，避免指数雾吞掉星星。
- 为 SkyDome、Stars、Moon 分别复制材质：SkyDome 用深蓝黑 Tint；Stars/Moon 保持较亮的 Tint。把原材质的主纹理填入新材质的 `Texture`，`Cull` 初始保留 Back；若从半球内部看不到，再改为 Front 或 Off。

## 大树贡献（M4 基础）

- 在大树根物体挂载 `WorldTreeContribution`。树必须有启用的 Collider，且该 Collider 所在 Layer 包含在 Player 的 `Interaction Layers` 中；建议使用球形 Trigger 覆盖树根附近的停留区域。
- 靠近时按 E 消耗 `Contribution Cost`（默认 10）余火；本地玩家仅能贡献一次，状态与颜色会随存档恢复。Demo 不设探索或篝火前置。
- 可选：在树冠预留一个 Point Light，初始关闭后赋给 `Personal Light`；首次贡献后它会启用，并使用已选择的颜色。Player 挂载 `TreeLightColorSelector` 后，靠近大树按 V 可循环选择原型颜色，按 E 贡献；移动端 UI 直接调用 `SelectPersonalLightColor(Color)`。位置尚未固定为玩法数据，后续可扩展多个锚点。
- 多光点占位：树上额外挂 `TreePersonalLightVisuals`，赋予一个 Point Light Prefab 与树冠 `Light Root`。它按光点记录生成实例；当前本地仅有一条记录，未来可直接显示同步而来的其他玩家记录。
- 大树阶段反馈：在与 `WorldTreeContribution` 同物体或其子物体挂 `WorldTreeProgressVisuals`，指定 `Source Tree` 和阶段根节点 `0..N`。每个根可放树冠模型、灯光、粒子、音景或植被；`Stage Contribution Thresholds` 的第 1 项对应阶段 1（默认 `10`，正好对应首次本地贡献）。组件只读取累计贡献、切换表现，不会消耗余火或写存档。当前单人 Demo 至少配置阶段 0（沉静）与阶段 1（苏醒）即可。

## 已验证

- 小火种放置、回收、上限、升级为公共篝火；
- 预置/运行时篝火保存与恢复，预置小火种升级后的隐藏；
- 停留、观星镜头引导/退出复原、烤棉花显示/旋转；
- 公共篝火等级视觉桥接（占位缩放，Light/VFX 可选）。
- 大树个人光点原型颜色选择（靠近大树按 V 循环后再按 E 贡献）。

场景、Prefab 或 Inspector 接入发生变化时，请同步更新本清单。
# 连续添火（M3 当前原型）

- 取消 `G` 和一次性升级：靠近小火种或公共篝火按 `E`，每次投入 `CampfireConfig.Tend Fuel Cost`（原型为 5）点余火。
- 首次添火会把临时小火种转换为拥有运行时 Stable ID 的公共火；`CampfireUpgradeController` 名称暂时保留，仅用于兼容 Player Prefab 上已有引用。
- 同时最多保留 4 座运行时公共篝火（可在 Player 的 `CampfireUpgradeController` 调整）。达到上限时，E 不会扣余火或销毁小火种；交互提示会显示当前数量，仍可按 G 回收小火种。
- `CampfireConfig_Prototype` 使用累计投入阈值 `0 / 20 / 55 / 110` 自动成长。`Warmth` 最大为 100、每秒衰减 0.1、每次添火恢复 16；已满时不会扣除余火。
- 中心广场篝火在 Inspector 将 `Level` 设为 3，作为默认满级火堆，只接受续火。火堆模型与火焰模型应保留独立子物体，供后续实现“满级熄火仍留火堆”。
