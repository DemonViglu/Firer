# FirePlay 当前开发状态

> 每个开发任务先读本文。稳定职责、运行链路和组装方法见 `FirePlay_核心架构.md`；迁移历史只保存在 Git 中。

## 1. 当前方向

- Unity `6000.5.5f1`，URP `17.5.0`，Input System `1.19.0`；目标平台为 Android 与 Windows。
- 核心 Gameplay 与 Player 组合已完成本轮收口，下一主线转向场景、美术技术、角色/道具、动画、VFX、音频和 UI 视觉。
- 实时联机代码保留并继续使用同一套 Gameplay；具体双端验收暂缓，不以重连、Lobby、Relay 或网络优化阻塞美术内容。
- 异步功能未来只保存实时玩法产生的必要事实，不维护第二套玩法兼容逻辑。
- 当前工作树包含尚未提交的大型重构；不得使用破坏性 Git 命令覆盖用户改动。
- 文档只保留“当前状态、核心架构、美术技术、联机测试”四类；旧迁移说明、阶段审计和重复场景手册不再维护。

## 2. 当前可依赖能力

| 系统 | 当前事实 | 状态 |
|---|---|---|
| 火焰循环 | 玩家余火、公共篝火成长、SmallFire 放置/上限、余火源、世界树贡献和存档保持独立权威状态 | 核心单机链路已多轮验收 |
| 活动架构 | `Definition + Catalog + Anchor + Rules + Logic/Factory + Session + Form + Visuals`；地点组合活动，不复制玩法 | 已收口，进入冻结维护 |
| Player 组合 | `PlayerCoreOnly` 纯基础、`Player` 完整单机、`PlayerNetworkBase` 技术父资产、`PlayerNetworkGameplay` 正式网络 Player | 静态结构与编译检查通过 |
| Marshmallow | 余火拟造、火候判定、翻面、品质、食用/返还和独立表现 | 可运行；正式动画/音效/VFX 待补 |
| Fishing | 拟造、抛竿、咬钩、纵向追踪捕鱼、逃脱、收获和独立 UI | 玩法已验收 |
| Guitar | Anywhere、21 键三音区、PC/Android 输入、自然音阶和程序钢琴音 | 可运行；正式音色与动画待补 |
| Emote | Anywhere、独立表情 UI、语义 Cue、不锁移动 | 逻辑已接入；正式表情动画待补 |
| Rest / Stargazing | 共享休息、观星相机、伙伴 TargetGroup、起身与移动恢复 | 已验收；明确不是计分小游戏 |
| 活动轮盘 | 发现附近 Anchor，并合并 Anywhere 活动；进入活动后由独立 Form 接管 | 可运行；最终视觉和移动刷新体验仍可优化 |
| 世界树 | 原子扣除余火、个人光点与阶段推进 | 已验收 |

## 3. Player 与场景现状

- 核心 Player 服务必须在 Prefab/Hierarchy 中显式可见，不再运行时 `AddComponent` 隐藏装配。
- `PlayerCoreOnly.prefab` 仅含输入、移动、Look、`LocalPlayerContext`、语义 Camera Targets 与角色碰撞基础。
- `Player.prefab` 用于完整单机场景；`PlayerNetworkGameplay.prefab` 由 `NetworkManager` 生成。
- `PlayerNetworkBase.prefab` 只保留网络技术父资产职责，禁止直接放入场景；`DefaultNetworkPrefabs` 只登记正式可生成对象。
- Player 不持有 Cinemachine Profile。活动机位和 TargetGroup 由场景级 `ActivityCameraRigExecutor` 统一执行。
- 新实验场景可从 `PlayerCoreOnly` 开始，只按需要接入 Flame、Activity、Rest 和 Interaction 模块。

## 4. 网络现状

- NGO 与 UTP 已接入；`DemoScene` 存在唯一 `NetworkManager + UnityTransport + FirePlayNetworkBootstrap`。
- 活动意图/事实 DTO、Host 权威入口、远端镜像、late-join 快照，以及 Flame/Campfire/SmallFire/WorldTree 的网络适配代码已经存在。
- 当前 `DemoScene` 开发配置为自动 Client 连接 `127.0.0.1:7777`，连接窗口默认隐藏。
- `NetworkConnectionForms` 是开发期 Client 直连工具，可设置地址/端口、连接和断开；它不是正式房间 UI，也不启动 Host。
- Windows 可用命令行启动 Host/Client；Android 使用连接窗体。完整 PC/Android 双端结果尚未由用户验收，因此不能宣称联机完成。
- 需要继续联机工作时，严格按 `FirePlay_联机测试清单.md` 分层验收。

## 5. 美术与内容缺口

- Terrain、路径、植被、远景、光照、阴影、雾、Volume 与综合色彩仍主要是原型状态。
- 火焰状态已有表现桥梁，但环境级颜色、花草生长、温暖区域和世界树阶段反馈不足。
- 正式角色模型、骨架、Locomotion、Emote/Rest/活动动画、道具、VFX 与大部分音频尚未接入。
- 活动轮盘与 Guitar 已采用透明简约方向，但所有 UI 仍需在真实场景亮度和 Android 安全区下统一视觉、点击区与对比度。
- Blender 到 Unity 的尺度、Pivot、材质槽、贴图、LOD、碰撞和导出规范尚未建立。
- Android 尚未形成基于目标设备的 GPU/CPU、Overdraw、灯光、阴影、植被和内存预算。

下一轮美术任务使用 `FirePlay_美术技术开发提示.md`，先建立独立 LookDev 小切片，不直接整体重做 `DemoScene`。

## 6. 已知边界与技术债

1. 伙伴赠送烤好产物、投火等特殊互动需要单独设计稳定持有状态与明确 Target 契约，不能靠 payload 绕过校验。
2. 网络异常恢复、重连、Lobby/Relay、匹配和产品级房间流程尚未完成。
3. 旧 `ColorSource / RestorableNode` 是实验视觉，不继续扩展颜色解谜；后续清理需先确认没有场景引用。
4. 连接窗体、活动 UI 和程序生成音色都是可用开发资源，不代表最终美术质量。
5. 核心架构已冻结；后续只在真实功能需要或回归缺陷下修改，不再为了兼容旧迁移路径增加中间层。

## 7. 下一步

1. 在 Unity 完成一次冻结回归：移动、余火、篝火、SmallFire、世界树、活动轮盘、Marshmallow、Fishing、Guitar、Emote、Rest/Stargazing 与退出后的相机/移动恢复。
2. 确认四种 Player 资产无 Missing 引用，场景中没有 `PlayerNetworkBase` 实例，也没有两个本地输入拥有者。
3. 通过后创建 Git 提交，作为“玩法与 Player 架构冻结”里程碑。
4. 新开美术技术任务，完成视觉/性能审计和一个安全 LookDev 切片。
5. 雪景、角色模型、Animator 与基础移动表现稳定后，先完成一次 **Player 自主装配里程碑**：提供从 `PlayerCoreOnly` 开始的装配沙盒、显式模块 Prefab/配置、依赖校验器、事件链路与断点说明；验收标准是用户能够亲手依次接入 Flame、Interaction、Activity 并定位故障，而不是只拿到一个已经装好的 Player。
6. 自主装配通过后，再把完整 Gameplay 合入正式美术场景；最后恢复 PC Host + Windows/Android Client 的专项联机验收。

本轮审阅结论：可以进入美术阶段，但当前里程碑限定为独立 LookDev 小切片，不直接整体改造 `DemoScene`。`ArtScene` 已作为独立环境资产容器存在，包含地形、路径、植被/远景、篝火相关对象与 Volume/灯光配置；但静态检查确认它没有 Camera、Player 或 NetworkManager，因此不能直接作为可运行 Play Mode 验收场景，需在下一步补齐安全的 LookDev 相机/运行时装配或使用独立副本。

## 8. 最近验证证据

- 2026-08-13 基础移动补齐陆地跳跃：`Jump` 现在是 Player Core 必需 Input Action，键盘 `Space`、手柄南键和移动端 HUD 都进入同一个一次性输入入口；陆地仅在 CharacterController 接地且移动未锁定时起跳，水中继续使用原浮力/跃出规则。
- `PlayerMovement` 暴露只读 `IsGrounded / IsJumping / VerticalVelocity`，供后续 Animator 接入，不让动画反向控制移动状态。默认陆地跳高 `1.25m`，仍需结合正式角色比例和动画在 Play Mode 调整。
- 固定 HUD 新增右下角透明 `JumpButton`，点击调用 `FirePlayMobileInputRouter.Jump()`；Activity Session 期间会收起交互提示，只有活动明确锁住移动时才隐藏探索控制，避免 Fixed 层遮挡活动 Form，同时保持 Emote 等不锁移动活动仍可使用摇杆。
- 本次 `dotnet build Assembly-CSharp.csproj --nologo --no-restore`：0 错误、0 警告；`Canvas.prefab` 新增 fileID、按钮回调和 HUD 引用已通过静态检查。仍需 Unity 导入和 Play Mode 人工验证陆地/水中/活动三种状态。
- `dotnet build Assembly-CSharp.csproj --nologo --no-restore`：0 错误、0 警告（最近一次代码收口检查）。
- 本轮审阅先执行 `dotnet restore Assembly-CSharp.csproj --nologo` 生成本地验证资产，再执行 `dotnet build Assembly-CSharp.csproj --nologo --no-restore`：0 错误、0 警告；此前直接 `--no-restore` 的失败原因为缺少 `Temp/obj/Assembly-CSharp/project.assets.json`，不是编译错误。
- 静态场景检查：`ArtScene` 21 个 GameObject、137 个组件、无 Camera/Player/NetworkManager；`DemoScene` 仍是唯一启用的构建场景。
- 渲染检查：已存在 `PC_RPAsset` 与 `Mobile_RPAsset`；PC 使用 1.0 Render Scale/2048 主光阴影图，Mobile 使用 0.8 Render Scale/1024 主光阴影图且关闭额外灯光阴影，但目标设备、帧率、GPU/CPU、Overdraw 和内存基线尚未实测。
- Player Prefab 静态检查：`PlayerCoreOnly`、`Player`、`PlayerNetworkBase`、`PlayerNetworkGameplay` 均为显式序列化组件，未发现 `AddComponent` 文本装配；`ArtScene` 未放入 `PlayerNetworkBase`。
- Player Prefab 与 Network Prefab 的静态 GUID/fileID 关系已检查；`PlayerNetworkBase` 不再作为独立生成项。
- Fishing 纵向追踪玩法、世界树贡献、Rest/Stargazing 恢复链路已由用户验收。
- Guitar 按钮 Raycast 问题已由用户修正；后续 UI 必须保留可交互控件自身的 Target Graphic。
- 本轮文档清理只修改 Markdown，不触碰场景、Prefab 或运行时代码。

## 9. 本轮美术落地：Campfire LookDev

- 已新增编辑器工具 `Assets/FirePlay/Editor/FirePlayLookDevBuilder.cs`，目标是产出一个独立的美术展示切片，不改写 `DemoScene` 或 `ArtScene`。
- 工具会生成 `Assets/Scenes/LookDev_Campfire.unity`：以现有 Stylized Nature MegaKit 的树木、灌木、花草、蕨类、岩石路径和岩石模型为主体，组合成中央篝火空地、弯曲入场石径、月光池塘和环形远景。
- 篝火使用现有 `campfire-pit.fbx` 作为主体，再叠加火焰、余烬、篝火点光和近距离暖色补光；环境使用冷色月光、暖色火光、夜空、雾、Bloom、Vignette 和色彩调整，重点是建立可截图的冷暖对比与镜头构图。
- 导入模型只通过 `MaterialPropertyBlock` 做轻微的统一色处理，保留资产原有纹理；不实例化共享材质。天空材质和 Volume Profile 会保存到 `Assets/FirePlay/LookDev/Materials/`，场景可重复生成。
- Unity 菜单入口：`FirePlay/LookDev/Build Campfire Presentation Scene`；需要同时输出根目录 PNG 时使用 `FirePlay/LookDev/Build Campfire + Render Preview`。
- 使用方式：等待 Unity 编译完成后执行菜单，打开 `Assets/Scenes/LookDev_Campfire.unity`，先以 Scene/Game 视图检查第一版构图，再根据截图调整树木密度、篝火位置、雾量和镜头，不把第一版直接当作最终 DemoScene。
- 当前限制：本轮已完成“可重复生成的真实模型美术切片”，但当前环境无法代替用户操作已打开的 Unity 编辑器，也无法在批处理副本中完成 Unity Licensing 初始化，因此尚未取得最终 PNG 截图；需要在本地 Unity 中执行一次菜单完成画面验收。
- 下一步只做具体审美迭代：基于实际截图调整主镜头和构图，补一个正式角色/篝火交互焦点，再决定是否把这套 LookDev 资源回接到 `ArtScene`，不继续扩张空泛的技术审计。

## 10. 宽阔山谷 LookDev

- 在保留 `LookDev_Campfire` 的基础上，新增 `FirePlay/LookDev/Build Wide Valley (100x Area)` 与带截图版本的菜单入口；输出为 `Assets/Scenes/LookDev_WideValley.unity`，不会覆盖原小场景。
- 新场景主地面约 `240m × 240m`；原篝火切片约 `24m × 24m`，因此面积约为原切片的 100 倍。原篝火林地保留为高密度兴趣点，不把其细节密度复制到整张地图。
- 空间分为中央开放草坡、约 `70m × 105m` 的主湖、约 `62m` 长桥、两组可行走低丘、稀疏树群/花草口袋与六组外围远山轮廓。大面积区域只保留草坡、色块和视线，不均匀铺满植被。
- 湖泊、桥面、低丘、远山和基础地面使用简单 Cube/程序网格构成；基础地面、桥面和低丘保留碰撞，远山与装饰岸线不保留碰撞。细节模型继续复用现有 Nature MegaKit。
- 固定远景镜头同时读取篝火核心、桥、湖泊与远山三层关系；宽场景使用更低雾密度和更远裁剪面，强调宽度与留白。
- 命令行编译验证：`dotnet build Assembly-CSharp-Editor.csproj --nologo --no-restore`，0 错误、0 警告。实际场景生成和画面验收仍需在 Unity 中执行对应菜单。

## 11. Grand Valley 白膜场景

- 根据实际画面反馈，正式保留“有颜色与光影的白膜体块”方向，并新增 `FirePlay/LookDev/Build Grand Valley (400x Area)`；输出 `Assets/Scenes/LookDev_GrandValley.unity`，不会覆盖已通过方向验证的 `LookDev_WideValley`。
- Grand Valley 主地面约 `480m × 480m`：边长为 Wide Valley 的 2 倍、面积为其 4 倍，也是最初约 `24m × 24m` 篝火切片的约 400 倍。
- 地形高度分为谷底约 `0–3m`、缓坡/低丘约 `8–18m`、东西高台约 `25–40m`、北侧阶梯山脊约 `20–52m`、外围山体约 `65–110m`。不依赖 Terrain 笔刷，继续使用简单 Cube、程序平面和明确的大体块轮廓。
- 新增西侧三层高原与约 `40m` 高的高原湖、东侧长缓坡、北侧三级山脊与鞍部坡道、南侧双壁峡谷和约 `32–37m` 高的峡谷桥。主要高度层之间设置连续宽坡，白膜场景不只是远景背景，也保留基础可达关系。
- 细节继续克制：保留中央篝火、主湖和原石桥作为谷底兴趣点，只在高原湖、东侧高台、北山脊和峡谷桥头设置少量大树群、花草口袋和三根石柱作为尺度参照。
- Grand Valley 使用更低雾密度、`1400m` 远裁剪和更高远景机位，让中央盆地、高原、峡谷与外围山脊同时可读。菜单 `FirePlay/LookDev/Build Grand Valley + Render Preview` 可同时输出根目录预览图。
- Editor 工程再次编译验证：0 错误、0 警告。下一步在 Unity 生成场景后，重点检查长坡倾角、桥头衔接、高原湖水面高度，以及固定镜头下谷底—高台—山脊三层是否清楚。

## 12. Snow Grand Valley：雪天、路径碎冰与单体冰面

- 雪天方向确定为明亮、安静的日间雪谷，不强制使用偏暗蓝调。新增 `FirePlay/LookDev/Build Snow Grand Valley` 与带预览版本的菜单；输出 `Assets/Scenes/LookDev_SnowGrandValley.unity`，保留所有已有 LookDev 场景。
- 雪景继续使用 Grand Valley 的 `480m × 480m` 白膜地形骨架，以暖白亮面、蓝灰背光面、低密度空气雾和日光/天空补光显示体块起伏。第一次北峡谷生成虽然对象存在，但偏东北且被连续雪台、山脊和天际山体遮挡，谷底不可读；现已把峡谷移到盆地正北中轴，并同时拆开底层雪台、左右三级山肩与最远天际轮廓，形成约 `50m` 宽、从主湖可见且可行走的真实谷口，包含峡谷桥、进场坡和远端山口。
- 植被由少量孤立地标扩充为有节奏的稀疏雪林：北峡谷两侧与山口最密，主湖岸、东西高台和南侧只布置小型树组及岩石。新增约 33 棵雪松/桦木/枯树和 10 组雪岩，同时保留中央雪原、湖面及主要移动路线的大面积留白；营地、湖岸与北峡谷继续用帐篷、木料、路牌和箱桶形成三个生活化小焦点。
- 项目已有 Ultimate Nature Pack 的正式雪树、枯雪树、雪灌木和雪岩石 FBX；Snow Grand Valley 直接使用这些雪地模型，而不是把绿色资产简单染白。
- Ultimate Nature Pack 的源资产已由用户在模型导入设置中统一放大 `500` 倍；场景生成器不再额外补偿这一基础尺度，只保留各实例约 `1.4–2.8` 的构图差异。该资产包模型还带有 X 轴 `+90°` 初始朝向，`InstantiateModel` 现按资产根路径识别并统一叠加局部 X 轴 `-90°` 校正，同时保留每个实例原有的 Y 轴构图朝向；其他美术包不受影响。
- 大范围雪花 ParticleSystem 的 PC LookDev 初版上限为 2200 粒子、约 145/s 发射，带轻微横向风和 Noise。正式跟随玩家时应改为相机/玩家附近的小发射盒；Android 档需降低粒子数和透明 Overdraw，当前值不是移动端最终预算。
- 主湖和高原湖各只使用一个连续冰面 GameObject、一个 `MeshRenderer` 与一个 `MeshCollider`。下层冷水面独立存在；不再用 11 块固定冰板或伪造的常开水洞。
- 冰下水体已从“透明水面片”改为有真实空间的湖：主谷地与西侧高台的承重表面均在湖区切出碰撞开口，主湖生成约 `11.5m` 深、高原湖约 `9m` 深的碗状湖壁/湖底 `MeshCollider`。水面本身没有实心 Collider，冰格破裂后角色会穿过水面进入湖中，不再被隐藏地基挡在水面附近。
- 用户验收发现，纯陆地重力会让角色落到湖底后形成软锁。现已在现有 `PlayerMovement` 中加入克制的基础水中状态，并为两个湖生成显式椭圆 `PlayerWaterVolume`：角色根节点沉入水面约 `0.18m` 后进入约 `0.72s` 的下潜制动阶段，保留落水冲量但把极端下坠速度限制在可控范围；随后浮力平滑接管并回升至水面。水中水平速度为陆地的约 `62%`、下潜阶段约 `42%`，且禁用冲刺；到达水面附近后可按 `Space` / 手柄南键跃出水面，完整冰层仍会以真实碰撞挡住跃起。到达湖岸后也可借碗状缓坡离水。这仍是 Demo 水中移动，不包含氧气、死亡或正式游泳动画。
- `FirePlay.inputactions` 新增独立 `Jump` Action（键盘 `Space`、Gamepad `buttonSouth`），`FirePlayPlayerInput` 提供一次性消费输入，`FirePlayMobileInputRouter.Jump()` 为后续移动端跳跃按钮保留显式入口；当前 Jump 仅用于水面跃出，不改变既有陆地移动规则。
- 新增 `URP_DepthWater.shader` 与 `SnowDepthWater` 材质：读取 URP Scene Depth，使岸边浅青、中央随可见湖底距离过渡为深蓝，并叠加轻微程序波纹、高光和 Fresnel；雪景相机显式请求 Depth Texture。水面双面可见，湖内配置局部 Underwater Volume，进入水下后降低曝光/饱和并偏向深蓝，以便从湖底仍能读到水体空间。
- 新增 `IcePathCrackField`、`URP_IcePathCrack.shader` 与隐藏路径写入 Shader：本地玩家踏上冰面后，运行时 RenderTexture 按移动路径累积压力；快速经过主要留下程序化裂纹，反复踩踏或停留会把路径压到透明破洞并露出下层水体。
- 视觉遮罩与物理破坏共用同一圆形压力笔刷。主湖碰撞格约 `1.35m`，高原湖约 `1.1m`；格子达到破裂阈值时从同一个 `MeshCollider` 网格中移除对应三角形，因此视觉洞口与可坠落区域基本重合。Shader 保留像素级柔边，物理边缘仍受格子尺寸约束，不承诺逐像素完全一致。
- 每个连续冰面保留稳定 ID，并提供 `ApplyWarmth(worldPosition, normalizedWarmth, radius, deltaTime)` 作为未来空间暖意驱动入口；视觉层只读取传入暖意并写入本地破坏结果，不修改火源或世界进度状态。当前 RenderTexture 与格子破坏结果尚未存档或联网同步，后续 Host 权威应同步稳定 ID + 格子位集/路径事件，而不是传整张 RenderTexture。
- 验收方式：等待 Unity 导入新脚本和 Shader 后执行 `FirePlay/LookDev/Build Snow Grand Valley` 重新生成场景，进入 Play Mode 后沿主湖行走并在局部停留，检查裂纹是否跟随路径、洞口是否露水、角色是否只从已破格子坠落；选中冰面可执行 `Ice Path/Clear Recorded Path` 重置。坠入水中后角色应有短暂而连续的下潜、平滑减速并自动回升；靠近水面后按 `Space` 应向上跃出，冰层完整处应被挡回水中，破洞或开放岸边则应真正越过水面。保持方向输入沿冰层下移动至岸边，也应能从湖盆缓坡离水。
- Runtime 与 Editor 工程最终编译验证：0 错误、0 警告。北峡谷、雪林、Ultimate Nature 轴向校正、深水湖盆和玩家水域触发体需要重新执行 `FirePlay/LookDev/Build Snow Grand Valley` 才会写入场景。水体验收应检查：冰洞下无隐藏地面、角色能穿过无碰撞水面、入水后不会滞留湖底、能移动到岸边离水、岸边/中央有明显深浅色差、入水后局部调色生效；Shader 与实际出水手感仍需以 Unity Console 和 Game View 完成最终验收。随后再决定是否实现正式游泳动画/输入、碎冰 VFX/音效和 Host 权威破冰命令。

## 13. Snow Grand Valley：火源区域热场表现

- 第一版区域影响选择“火源热场”，暂不实现沿玩家路径永久开花。新增场景级 `EnvironmentWarmthDirector`：以约 `0.15s` 间隔统一读取 `Campfire.ActiveInstances` 与 `SmallFire.ActiveInstances`，把最多 8 个火源压缩为空间暖度场；当前雪谷按 1 个公共篝火 + 最多 3 个 SmallFire 配置表现槽。Director 与 Receiver 不消耗余火、不修改篝火等级/热量、不写存档，也不产生新的权威玩法状态。
- Receiver 均为纯表现：`WarmthSnowReceiver` 通过 `MaterialPropertyBlock` 把统一火源数组交给 `URP_WarmthSnow.shader`，在火源周围形成柔边融雪/湿润地表圈；`WarmthGrowthReceiver` 让作者预埋的草、蕨和小花按局部暖度从雪下生长；`WarmthAtmosphereReceiver` 把每个活跃火源映射为暖色补光、低密度融雪薄雾和空间化篝火音景；`WarmthIceReceiver` 使用现有 `IcePathCrackField.ApplyWarmth` 让 SmallFire 附近冰面逐步脆化。远离湖面的火源会先做范围相交检查，不会误把热量夹取到冰面边缘。
- 雪谷生成器会把中央 Hero Campfire 绑定到真实只读 `Campfire` 状态，并创建 `05_Environment_Warmth_Session` 层级、12 个稀疏生长点、4 组可复用火源氛围槽以及两个冰面 Receiver。植物仍是场景作者提前摆好的美术资产，不在运行时随机生成，也不承担 gameplay 状态。
- 首轮验收：重新执行 `FirePlay/LookDev/Build Snow Grand Valley`。进入 Play Mode 后中央篝火周围应出现暖棕融雪圈，预埋草花应在数秒内舒展，薄雾/暖光/篝火声随暖度出现；在远处放置 SmallFire，应在新位置形成独立的小型融雪圈并激活附近预埋植物；放在冰面附近应逐步加深裂纹，火种消失后地表、植物、灯光、VFX 和音景平滑回冷。由于当前是可逆 LookDev 表现，不保存融雪和生长结果。
- Runtime 与 Editor 工程编译验证：0 错误、0 警告。`URP_WarmthSnow.shader`、材质导入和实际冷暖边界仍需 Unity Game View 验收；Android 后续应重点检查透明薄雾 Overdraw 与额外点光数量。沿玩家路径开花继续延后到移动、角色动画与 Shader 风格稳定之后。
- 2026-08-13 修复首轮生成对象显示 `Missing Script`：原因是四个可挂载 Receiver 与抽象基类曾共同放在 `EnvironmentWarmthReceivers.cs`，Unity 无法为这些非同名 `MonoBehaviour` 建立可序列化的 MonoScript 资源。现已拆分为 `EnvironmentWarmthReceiverBehaviour.cs`、`WarmthSnowReceiver.cs`、`WarmthGrowthReceiver.cs`、`WarmthAtmosphereReceiver.cs` 与 `WarmthIceReceiver.cs`，每个可挂载组件都与文件同名。修复后 Runtime/Editor 编译均为 0 错误、0 警告；旧场景中已经写入的 Missing 引用不会自动复原，必须等待 Unity 导入完成后重新执行一次 `FirePlay/LookDev/Build Snow Grand Valley`。
- 2026-08-13 完成雪谷第一版光影氛围基线：雪谷生成器现在使用较柔的暖色日光、冷蓝天空补光、克制的阴影强度、雪面不过曝的 Volume 色彩和更柔的远景雾层，目标是让谷底—中景山体—远景轮廓形成空气透视，而不是靠整屏蓝色或强 Bloom 制造氛围。仍需在 Unity Game View 依据实际截图微调雾密度与主光方向。
- 2026-08-13 将热场花草表现扩展为可选动画驱动：每个预埋生长点会生成独立的 `WarmthGrowth_XX.anim` 与 Animator Controller，Receiver 仍只读取场景级热度并把归一化暖度映射到动画进度；若 Animator 资源缺失则自动回退到原有缩放/下沉表现。该表现不写回余火、世界进度或 gameplay 状态。
- 本轮 Runtime/Editor 编译验证：`dotnet build Assembly-CSharp.csproj --nologo --no-restore` 与 `dotnet build Assembly-CSharp-Editor.csproj --nologo --no-restore` 均为 0 错误、0 警告。Unity 侧需重新执行 `FirePlay/LookDev/Build Snow Grand Valley + Render Preview` 才会写入新的光照、Volume、动画资产和场景引用。
- 2026-08-13 修复花草 Animator 生成异常：Ultimate/自然资产的 FBX 根节点不能作为可靠的 Animator 宿主，且 Unity 对象的伪空语义会使 C# `??` 检查失效。生成器现为每个花草创建普通 `ThawGrass_Receiver`/`ThawFlower_Receiver` 宿主，把 Animator 与 `WarmthGrowthReceiver` 挂在宿主上，把导入模型放入 `_Visual` 子节点；Receiver 对 Animator 仍有显式有效性检查，缺失时回退到程序化缩放生长。Runtime/Editor 编译再次为 0 错误、0 警告；需要重新执行雪谷生成菜单刷新已中断的场景。
- 2026-08-13 修复热场花草抽搐：原实现每次约 `0.15s` 热场采样都直接 `Animator.Play(..., normalizedTime)`，同时 Animator 自身仍会推进时间，造成反复跳回动画相位。现改为“采样只写目标暖度、Receiver 每帧平滑追踪、Animator speed=0 且仅在相位变化时采样更新”，程序化回退路径也使用同一帧间平滑。重新生成雪谷后，花草应连续舒展/收拢，不再按采样频率抖动。
- 2026-08-13 已确认本机 Blender 5.2.0 LTS 可通过 `E:\Blender\blender.exe --background --python` 执行。新增 `Assets/FirePlay/Art/Character/Blender/build_snow_traveler.py`，输出原创雪白兜帽斗篷旅人模型、骨骼、模块化 OutfitSlots、Idle/Walk/Run/JumpStart/JumpLoop/Fall/Land 动作、`.blend`、`.fbx` 与 PNG 预览。
- 新增 `Assets/FirePlay/Editor/FirePlaySnowTravelerBuilder.cs`：可生成 SnowTraveler Animator Controller/Prefab，并把角色作为显式 `SnowTravelerVisual` 子层级安装到 `Player.prefab` 与 `PlayerCoreOnly.prefab`，关闭旧 Body 占位体，挂载 `PlayerLocomotionAnimationBridge`；不改变 PlayerMovement、Activity、Flame 或网络玩法职责。
- 代码验证：`Assembly-CSharp.csproj` 与 `Assembly-CSharp-Editor.csproj` 串行编译均为 0 错误、0 警告。Unity 菜单执行和 Play Mode 视觉验收仍需用户本地完成；验收重点为 FBX 朝向、脚底贴地、动画片段名称和现有活动动作触发。
- 2026-08-13 根据视觉反馈重做 Snow Traveler 角色轮廓：放弃原先“圆球头 + 环形脸框 + 硬锥形身体”，改为深色完整面罩、柔软白色兜帽、宽圆斗篷、隐藏式手臂、露出的软手套和小靴子。当前 Blender 预览已作为新的造型基线，后续坐下、钓鱼、烤棉花糖、观星动作均基于此版本继续。
- 本轮重新导出 `SnowTraveler.blend`、`SnowTraveler.fbx` 与 `SnowTraveler_Preview.png`；Blender 脚本仍保持可重复生成。Runtime/Editor 串行编译均为 0 错误、0 警告。下一步应在 Unity 中执行角色生成/安装菜单，并在真实 Game View 验收比例与动画，不再只依赖 Blender 单张预览继续盲目改形。
- 2026-08-13 根据参考图重新拆分 Snow Traveler：不再使用整块前置斗篷，改为独立短袍、清晰双臂、双腿与靴子，以及只位于肩背后的独立披风；头部保留深色无嘴面罩，仅使用两只暖色发光眼睛。Blender 预览、FBX 和源文件已重新生成，脚本中已确认没有嘴巴对象。请在 Unity 重新执行角色生成/安装菜单后验收真实 Game View。
- 2026-08-13 角色生成管线改为静态 LookDev 分阶段：新增 `build_snow_traveler_v3.py` 与 `SnowTraveler_V3.blend`，先用重叠有机体素体积融合出一张连续身体网格，再叠加独立短袍、兜帽、无嘴面罩、后半边披风和靴子；V3 暂不绑定骨骼、不接入 Unity，只输出主视图/正面/背面预览供轮廓验收。这样后续骨架简化为约 12–15 根即可，不再用大量 Cube/球体零件承担最终角色造型。当前 V3 仍是原创低模 LookDev，不宣称达到最终《光·遇》级资产；用户确认静态轮廓后再进入简化骨架与动作阶段。
- 2026-08-13 根据用户反馈生成 Snow Traveler V4 静态对照版：缩短并收窄短袍、降低躯干视觉重量、让腿部比例更可读；脸部改为较柔和的暖色无嘴面罩，增大双眼并加入极小眼睛高光与脸颊色点，不增加嘴巴或面部骨骼。V4 保留独立后半边披风，仍未绑定骨骼或接入 Unity；待用户确认 V4 静态轮廓后再进入简化骨架与动作阶段。
- 2026-08-13 检查 `Assets/Resources/Art/Character/1-4` 中用户下载的 4 组 GLB/FBX：每组 GLB 与 FBX 的包围盒尺寸、面数和 UV 结构对应，确认没有错配；但四组文件均为单一静态 Mesh，没有 Armature、Skin Weights 或 Animation。GLB 内含打包的 BaseColor/Normal/Metallic-Roughness 贴图，FBX 侧贴图引用不适合直接作为 Unity 外观来源。后续应选定一组，以 GLB 恢复材质，再在 Blender 中绑定简化骨架并导出 Unity 使用的 FBX。
## 14. 下载角色双人组：第 1 组男生与第 5 组女生

- 已确认 `Assets/Resources/Art/Character/1` 与 `5` 的 GLB/FBX 为匹配的静态模型；两个源文件均没有骨骼、蒙皮或动画，GLB 内含完整打包 PBR 贴图。
- 生成脚本为 `Assets/FirePlay/Art/Character/Blender/build_downloaded_character_duo.py`，使用 GLB 保留材质，统一添加 `SnowTraveler_SimpleHumanoid_v1` 轻量骨架，并输出男女分离 FBX。
- 两个角色均已生成 `Idle/Walk/Run/JumpStart/JumpLoop/Fall/Land/Sit/Fishing/Marshmallow/Stargaze` 11 个动作；自动权重检测不稳定时改用确定性的近骨骼权重，当前每个角色 19 个骨骼组、全部顶点有有效权重。
- 输出：`Assets/FirePlay/Art/Character/Generated/SnowTraveler_Male_Rigged.fbx`、`SnowTraveler_Female_Rigged.fbx`、`SnowTraveler_Duo_Rigged.blend` 与预览图。Blender 重新导入验收通过；尚未在 Unity Game View 中验收朝向、脚底贴地和 Animator 状态机。
- 下一步：Unity 导入两个 Rigged FBX，Animation Type 设为 Humanoid，确认 Avatar/动作片段后再接入现有 `PlayerLocomotionAnimationBridge`；换装先沿用同一 `SnowTraveler_SimpleHumanoid_v1` 骨架。
- 2026-08-13 修复角色 FBX 材质丢失：原因不是 GLB 源材质缺失，而是 FBX 导出时只保留了 Blender packed image，Unity 无法解析空路径。现已把男女角色的 BaseColor、Normal、Metallic/Roughness 各自导出为 `Generated/SnowTraveler_*_*.png`，FBX 改用相对路径引用；重新导入 FBX 并渲染已恢复颜色，Unity 侧需等待这些 PNG 导入完成后再看材质。
- 2026-08-13 修复角色材质语义不一致：GLB 的 glTF 金属度/粗糙度图为 `B=Metallic/G=Roughness`，而 Unity URP Mask Map 需要 `R=Metallic/A=Smoothness`。新增 `FirePlay/Character/Build Downloaded Duo Materials`，生成 URP 材质、Mask Map、FBX 材质重映射和男女角色 Prefab；不再依赖 FBX 自动猜测材质。
- 2026-08-13 重新打开并渲染 `Generated/SnowTraveler_Duo_Rigged.blend` 验证：男女角色材质节点和 6 张 packed 图均存在，Blender 渲染外观正常。该 `.blend` 是当前外观基准；Blender 视口必须切换到 Material Preview/Rendered，不能用 Solid 模式判断贴图，也不要打开 `.blend1` 备份。Unity 侧应执行 `FirePlay/Character/Build Downloaded Duo Materials` 后使用 `Generated/Prefabs`。
- 2026-08-13 新增 `Assets/FirePlay/Art/Character/Blender/export_edited_duo_from_blend.py`：用于用户在 Blender 手动修改后的增量交付。脚本只打开已保存的 `SnowTraveler_Duo_Rigged.blend`、提取当前材质贴图并导出男女 FBX，不重新导入 GLB、不清空场景、不覆盖手动模型/骨骼/动作修改；导出后 Unity 仍执行 `FirePlay/Character/Build Downloaded Duo Materials`，再使用 `Generated/Prefabs`。
- 2026-08-13 清理角色冗余资源：删除原始下载的 `Resources/Art/Character/1-5`、旧版 `DEMON_FEMALE.fbx`、候选预览/诊断文件和会从 GLB 重建角色的旧 Blender 脚本；删除无效的旧版 `FirePlaySnowTravelerBuilder.cs`。保留双人 `.blend`、男女 Rigged FBX、当前贴图/MaskMap/URP 材质、男女 Prefab，以及只从已保存 `.blend` 导出的脚本。删除后编辑器工程编译为 0 错误、0 警告，旧资源无残留引用。
- 2026-08-13 修复双人 Blender 的 `All Actions` 导出串动作问题：Blender 的 Action 数据是 `.blend` 全局资源，单纯选择 Female 集合不能过滤 Male 动作；新增 `prepare_single_character_blends.py`，从双人源文件生成 `Generated/SnowTraveler_Male_Single.blend` 与 `SnowTraveler_Female_Single.blend`，每个文件只保留对应角色的 11 个动作。双人增量导出脚本同步改为临时显示隐藏角色、只启用当前角色 NLA、导出后恢复源文件的预览可见性和 NLA 静音状态；重新导出的男女 FBX 均已在 Blender 重新导入验证为 11 个动作、1 个骨架和 1 个网格。以后 Blender 手动导出时打开对应 `*_Single.blend`，可直接勾选 `All Actions`，不会混入另一角色。
- 2026-08-13 优化 Duo 的 Blender 工作流：`export_edited_duo_from_blend.py` 现在支持在 Blender 的 Scripting 工作区直接运行；选中某个角色的网格或骨架时只导出该角色，未选中角色时保持男女批量导出，选中两人会主动报错避免混合。已确认角色网格有 Armature Modifier、19 个顶点组且动作曲线确实写入对应骨架，不是“模型没有绑定动画”。
- 2026-08-13 明确原生 Blender 工作流边界：`All Actions` 属于 `.blend` 全局 Action 库，不能可靠地按选中角色过滤；已将 Duo 整理为两个角色各自一条 NLA 动作库轨道，每条轨道包含本角色的 11 个动作片段，打开源文件时默认静止且不自动让男女一起播放。纯 Blender 操作时：选中一个角色的网格和骨架，在 Dope Sheet/Action Editor 选择该骨架对应 Action 可预览；导出时使用 `Selected Objects`、开启 `NLA Strips`、关闭 `All Actions`，只启用当前角色的 NLA 轨道即可导出该角色的全部动作。`preview_duo_animation.py` 和 `export_edited_duo_from_blend.py` 保留为可选辅助工具，不再是日常必需。
- 2026-08-13 修复 NLA 导出无动画：确认两个角色的 NLA Strip 都有对应 Action 且未静音，但两个 NLA Track 本身处于 `Mute`，因此开启 `NLA Strips` 仍不会导出任何片段。现将 `SnowTraveler_Duo_Rigged.blend` 默认设为 Female Track 启用、Male Track 静音；用 Female 网格+骨架、`Selected Objects`、`Bake Animation`、`NLA Strips` 开启、`All Actions` 关闭的纯 Blender 设置重新导出，验证得到 Female 11 个动作。切换 Male 时只需在 NLA Editor 中启用 Male Track、静音 Female Track。
- 2026-08-13 核实男女动作内容：Duo 源文件与重新导出的男女 FBX 均包含 11 个动作片段；`Fall/Fishing/Idle/JumpLoop/JumpStart/Land/Run/Walk` 有实际骨骼姿态变化，`Marshmallow/Sit/Stargaze` 虽有关键帧和曲线，但所有关键帧值相同、没有姿态变化，属于当前生成器留下的占位动作，不是 FBX/NLA 导出丢失。后续进入 Unity 前必须先补齐这 3 组动作，或暂时不要把它们接入 Animator 状态机。
- 2026-08-13 修复角色接入时的 Nested Prefab 残留：`Player.prefab` 曾保留已删除旧角色 `SnowTravelerVisual` 的嵌套 Prefab GUID `c8d522d9e7f9ddf4eb7d4b5ad19b21b5`，导致 Unity 报 `Missing Nested Prefab Asset`。现已移除旧 PrefabInstance、Animator 残留引用，并暂时清空两个 Player 动画桥接的旧 Animator 引用；待在 Unity 执行 `FirePlay/Character/Build Female Animation Setup` 后由当前 Female Prefab/Avatar/Controller 重新绑定。
- 2026-08-14 修正下载角色的“陶瓷”反光：原 URP 生成器把 glTF 金属/粗糙度图完整映射，并强制 `_Metallic=1`、`_Smoothness=1`，不适合衣物共用单材质的角色。现有 Female/Male URP 材质及 `Build Downloaded Duo Materials` 菜单统一改为哑光布料基线：不使用 Metallic Gloss Map、`Metallic=0`、`Smoothness=0.18`，关闭环境反射和镜面高光；保留 BaseColor 与 Normal 细节。`Assembly-CSharp-Editor` 编译为 0 错误、0 警告，仍需在 Unity 中检查实际光照下的斗篷、头发与面罩明暗层次。
- 2026-08-14 修正第三人称探索控制：旧 `PlayerLook` 会直接旋转 Player 根节点，造成角色与相机始终同向。现将 Look 的 yaw/pitch 写入 `CameraPivot` 的世界旋转；`PlayerMovement` 仍按相机水平朝向计算移动，但只在有移动速度时以每秒 900°平滑让角色转向移动方向。因此可站立绕角色观察、侧向/后退移动时角色会自然转身，且不影响活动的显式 `TryFaceTarget`。Runtime/Editor 编译均为 0 错误、0 警告。
- 2026-08-14 修正与加强 Locomotion：`Build Female Animation Setup` 现在会把 Idle/Walk/Run/JumpLoop/Fishing 标记为可循环（含 loop blend）；Blender 源文件的 Female/Male Walk、Run 增加了清晰的交替摆臂、前后跨步、小腿弯曲、髋部上下起伏与轻微左右重心转移，首尾姿态相同保证循环连续。重新导出的 FBX 已写入两角色，数据核验显示 Walk/Run 的源动作曲线由 60 条/低幅度提升为 63 条/13 条有效变化曲线，姿态幅度显著提高。Unity 侧重新导入后须依次执行 `FirePlay/Character/Build Downloaded Duo Materials` 和 `FirePlay/Character/Build Female Animation Setup`。
- 当前角色骨架为正常的 19 骨简化 Humanoid 链（root/hips/spine/chest/head、双臂、双腿），不是由 Cube 零件构成；此前 Blender Armature 显示模式为 B-Bone，故视觉像方块。现已把两个 Armature 和骨骼显示改为 Octahedral（水滴形），不改变骨骼层级、蒙皮、动作或 Unity 导出。
- 2026-08-13 新增 `Assets/FirePlay/Editor/FirePlayCharacterAnimationBuilder.cs`：提供 `FirePlay/Character/Build Female Animation Setup` 菜单，从 `SnowTraveler_Female_Rigged.fbx` 自动筛选 8 个有实际内容的动作，生成 `SnowTraveler_Female_Locomotion.controller`，并建立 Idle/Walk/Run、JumpStart/JumpLoop/Fall/Land 与 Fishing 状态；同时保留现有 `PlayerAnimationController` 的休息/活动布尔参数与 Cue 触发参数，未把 3 个静止占位动作接入运行时状态机。
- 该工具会在编辑器中把当前 `SnowTraveler_Female.prefab` 作为 `SnowTravelerVisual` 安装到 `Player.prefab`，为角色显式挂载 Animator、Avatar 与 Controller，并把 `PlayerAnimationController`、`PlayerLocomotionAnimationBridge` 指向同一个 Animator。由于当前环境不能调用本地 Unity 编辑器进程，资源生成与 Play Mode 仍需在 Unity 菜单执行一次后验收；编辑器工程编译验证为 0 错误、0 警告。
- 2026-08-14 修复角色变向时的相机水平抖动：问题不在模型或骨骼的 89° 旋转，而在于移动曾旋转 Player 根节点，同时 `PlayerLook` 又为该根节点下的 `CameraPivot` 写入世界 yaw，导致父子 Transform 每帧互相补偿。现在 `PlayerMovement` 只旋转显式的 `SnowTravelerVisual`，Player 根节点保持稳定以承载碰撞、相机 Pivot 与游戏挂点；`TryFaceTarget` 也改为面向视觉角色。已核定当前 Blender 源 Armature、角色网格与 Unity 角色 Prefab 根节点均为 0°；若 Inspector 仍出现 89°，它是 Blender/FBX 到 Unity 的坐标轴换算或旧手动校正留下的 Euler 显示，不应再用 90° 对模型或骨骼做反向修正。Runtime/Editor 编译均为 0 错误、0 警告。
- 2026-08-14 补齐角色互动动作：新增 `Art/Character/Blender/author_activity_actions.py`，只在现有双人 `.blend` 的同一套骨架上重做 `Sit`、`Marshmallow`、`Stargaze` 三个 Action，不改模型、材质、蒙皮或活动逻辑。坐下为 24 帧一次性轻坐动作；烤棉花为 40 帧、双手前持/轻微取暖摆动循环；观星为 60 帧、坐姿抬头呼吸循环。已清除旧静止占位关键帧并同步 NLA 范围，重新导出男女 FBX；两角色数据核验均显示 Sit 11 条、Marshmallow 7 条、Stargaze 7 条有效姿态变化曲线。`Build Female Animation Setup` 现将三者接入既有参数：`EmoteSit`、`IsMarshmallowRoasting`、`IsResting`；后两者在状态期间循环，退出后回到 Idle。Runtime/Editor 编译为 0 错误、0 警告。Unity 导入完成后需依次执行 `FirePlay/Character/Build Downloaded Duo Materials` 与 `FirePlay/Character/Build Female Animation Setup`，再在篝火与 RestSpot 处验收。
- 当前里程碑只验收动画美术资产本身：Blender 中的可预览 Action、男女 FBX 导出与骨骼/材质完整性。虽然编辑器工具已预留现有参数的映射，但不要求此阶段执行 Unity 菜单、接入活动或进行玩法验收；动作进入真实篝火、休息和表情表现的时机，留待后续玩法/表现整合阶段统一处理。
- 2026-08-14 新增独立场景生成入口 `FirePlay/LookDev/Build Snow Grand Valley Lived-In`（及带预览版本），输出 `Assets/Scenes/LookDev_SnowGrandValley_LivedIn.unity`，不覆盖已验收的 `LookDev_SnowGrandValley`。该版不新增任何活动、Collider、StableSceneId 或玩法状态，只用现有 Kenney 雪地生存包在三处已有兴趣点补充克制的生活化叙事：篝火营地增加睡具、开箱、木料和两盏低亮暖灯；主湖岸增加补给架、水桶、工具箱与修缮木板；北峡谷入口增加挡风半帐、补给箱、木料、路标和一盏暖灯。雪地天空/雾/Volume 使用专用 Lived-In Sky/Profile：仍为明亮日间雪天，稍降冷雾密度、保留开阔留白并让小范围暖光可读。`Assembly-CSharp-Editor` 编译为 0 错误、0 警告。Unity 侧应生成新场景后，只验收三处停留点是否清楚、主湖和谷底留白是否仍完整、暖灯是否只作引导不抢日光；不要求接入 Player 或玩法。
- 2026-08-14 为 `LookDev_SnowGrandValley_LivedIn` 增加纯表现动态氛围层，原雪谷生成路径不变：雪花从单一宽域发射器拆为远景（850 粒子）、谷底中景（420）与营地近景（155）三层，使用不同尺度、速度和横风/Noise 形成景深，不做暴风雪；篝火仅在 Lived-In 版增加薄烟（最多 36 粒子、约 4.5/s）和上升余烬（最多 42 粒子、约 9/s）。所有新粒子关闭阴影、没有 Collider、没有活动/状态读写；其设计目的是让画面有风与温度，而不是接入火焰玩法。`Assembly-CSharp-Editor` 编译为 0 错误、0 警告。验收时重点检查近景雪花是否有层次但不遮挡视线、烟雾是否轻薄、余烬是否只在篝火近处可读。
- 2026-08-14 新增 `FirePlay/LookDev/Build Snow Character Showcase`（及带预览版本），输出 `Assets/Scenes/LookDev_SnowGrandValley_CharacterShowcase.unity`。该场景从 Lived-In 雪谷构图派生，但只实例化 `SnowTraveler_Female.prefab` 的静态美术外观，不创建 Player、Animator Controller、Activity、Collider 或状态组件；角色位于篝火前侧，画面同时保留营地暖点、主湖与北峡谷远景。单独使用一盏低强度冷色 Spot Rim Light、一盏极弱暖色 Face Fill 和半透明雪地接地压痕，解决白斗篷/雪地/背光叠加时的轮廓与脚底漂浮问题；它们仅用于固定 Demo 截图基线，不改变角色正式运行时材质。`Assembly-CSharp-Editor` 编译为 0 错误、0 警告。验收重点：斗篷边缘、面罩/眼睛与脚底是否可读，角色是否是前景主体而篝火/峡谷仍能引导视线。
- 2026-08-14 为三张雪谷 LookDev 场景补充独立的场景级音景根节点 `07_Snow_Soundscape`：低音量的整体空气底噪、主冰湖岸边声、篝火近场声及一条可替换的 Demo BGM 都是显式 `AudioSource`，不挂在 Player 上，也不读取或反向改写 Campfire/余火/热场状态。湖与篝火使用距离衰减，靠近时才清晰；空气与 BGM 保持轻量 2D 底层。两个雪谷相机现在显式带有 `AudioListener`，因此场景可直接 Play Mode 听到。生成新的雪谷 BGM 后，只需在 `Demo_Music_Bed_Replaceable` 的 Inspector 替换 `AudioClip`，无需改代码或重新布置。`Assembly-CSharp-Editor` 编译为 0 错误、0 警告。
- 2026-08-14 新增 `Assets/FirePlay/Editor/FirePlayUiLookDevBuilder.cs`，提供 `FirePlay/UI/Apply Snow Valley UI LookDev`。该可重复执行工具只修改 SUIFW 的 Canvas 与现有 Activity Form 的视觉序列化资产：生成一张可九宫格拉伸的圆角雪玻璃 Sprite，统一 HUD、活动轮盘、棉花糖、钓鱼、吉他和表情面板的圆角、深蓝雪玻璃底、暖余火主按钮、冰蓝进度/判定条、霜白文字、轻描边、Hover/Pressed/Disabled 状态与 1920×1080/移动端缩放基线。不更改任一 Form 脚本、Button 回调、活动状态、输入或 UI 路由；执行菜单后需要在真实场景的亮雪、篝火和移动端安全区下验收对比度与点击区。`Assembly-CSharp-Editor` 编译为 0 错误、0 警告。
- 2026-08-14 根据视觉反馈调整 UI LookDev：取消深蓝/暖橙配色，改为透明白、灰白/冰白的低干扰界面；标题、状态和按钮文字增大，活动轮盘条目为 148px 圆形，常用活动按钮收紧为更大的短圆角触控块，关闭键为独立圆形；HUD 的余火由水平长条改为圆形 Radial360 读数。工具另生成圆形 Sprite，用于余火与移动摇杆。需要再次执行 `FirePlay/UI/Apply Snow Valley UI LookDev` 写入资源后验收。
- 2026-08-14 静态核查移动摇杆链路：`FirePlayMobileJoystick.OnPointerDown/OnDrag` 调用 `FirePlayMobileInputRouter.SetMove`，后者调用本地 `FirePlayPlayerInput.SetVirtualMove`，`PlayerMovement.Update` 从同一输入门面读取 `Move` 并按相机水平朝向驱动当前角色；链路没有绕过 Player 或另建移动逻辑。发现并修复手柄 `Image` 可能拦截中心触点、使父级摇杆收不到 Pointer 事件的问题：`FirePlayMobileJoystick.Awake` 现在强制手柄 `raycastTarget=false`，UI LookDev 也会把对应资源写为不可拦截。`Assembly-CSharp-Editor` 编译为 0 错误、0 警告；仍需在真机或 Unity Game View 手动拖动摇杆确认触点、安全区与角色移动方向。

## 15. SnowValley_Playable 模块接入记录

### 接入前基线

- `SnowValley_Playable` 是从 LookDev 派生出的独立正式候选场景。接入审计确认场景原本只有环境根节点、场景热场、真实 `Campfire`、冰面/水域交互以及一台固定 LookDev 相机；没有 Player、HUD、Activity、Rest 或 Network 对象。
- `FirePlayUiLookDevBuilder.cs` 只负责 Canvas 与活动 Form 的视觉资产，不负责生成或组装 `SnowValley_Playable`。后续不能把它当作场景接入入口。
- 本记录按可验收的小步骤维护；每一步都会写明 Hierarchy、组件依赖和运行链路，不复制 `DemoScene` 的整套对象。

### Step 1：Player Core + 角色表现 + 探索相机（待 Unity 验收）

- 场景新增根对象 `Player_Core`，来源为 `PlayerCoreOnly.prefab`。它只包含 `CharacterController`、`FirePlayPlayerInput`、`PlayerMovement`、`PlayerLook`、`LocalPlayerContext`、`PlayerCameraTargetSet`、`PlayerLocomotionAnimationBridge` 和玩家自带探索相机；没有 Flame、Interaction、Activity、Rest、HUD 或 Network 组件。
- 原 `Snow Grand Valley Camera` 改名为 `Snow Grand Valley Camera [LookDev Reference]` 并停用、取消 `MainCamera` Tag。它保留为构图参考，不参与运行；唯一运行相机来自 `Player_Core/CameraPivot/Camera`。
- `PlayerCoreOnly.prefab` 显式嵌套现有 `SnowTraveler_Female.prefab`，命名为 `SnowTravelerVisual`；`PlayerLocomotionAnimationBridge` 把 `PlayerMovement` 的只读移动事实写入 Animator 参数，Animator 不反向驱动碰撞或移动。
- `PlayerMovement._visualTransform` 显式指向 `SnowTravelerVisual`：移动转向只旋转人物外观，Player 根节点、CharacterController 和 CameraPivot 保持稳定。完整单机 `Player.prefab` 同步修正为相同绑定。
- `FirePlayCharacterAnimationBuilder` 现会同时维护 `Player.prefab` 与 `PlayerCoreOnly.prefab`，并在重建角色时重新绑定 Animator 与 `_visualTransform`，避免下一次执行角色生成菜单后引用退回为空。

运行链路：

`FirePlay.inputactions -> FirePlayPlayerInput -> PlayerMovement -> CharacterController`

`PlayerMovement + FirePlayPlayerInput -> PlayerLocomotionAnimationBridge -> SnowTravelerVisual/Animator`

`FirePlayPlayerInput.Look -> PlayerLook -> CameraPivot -> Camera`

验收要求：打开 `SnowValley_Playable` 进入 Play Mode，只验证键鼠移动、视角、陆地跳跃、动画、冰面破裂、落水/浮起/出水和唯一 AudioListener；本步骤不应出现余火 HUD、交互提示、活动轮盘或网络对象。通过后下一步再显式接入 FlameModule，并把场景 `Hero_Campfire` 与玩家余火/HUD 链路写入本节。

- 静态验证：Runtime 与 Editor 工程均为 0 错误、0 警告；场景仅序列化 1 个 `PlayerCoreOnly` 实例，Prefab/场景 YAML 无重复对象 fileID，角色 Animator、移动视觉根节点和 SceneRoots 引用均已核对。最终资源导入与 Play Mode 结果仍以本步骤人工验收为准。

### Step 2：FlameModule + HUD + 探索相机防穿模（已直接写入 SnowValley，待 Unity 重载验收）

- 新增独立 `PlayerFlameModule.prefab`，只包含 `FlameModule`、`FlameResourceController`、`PlayerFlameController`、`CampfirePlacement`、`CampfireUpgradeController`、火焰视觉桥、收束控制器和显式 `FlameAnchor`。它不携带 Interaction、Activity、Rest 或 Network。
- `PlayerSceneServiceBindings` 改为按服务独立就绪：活动、活动相机、玩家火苗工厂、网络出生点不再要求一次性全部配置。SnowValley 本步骤只配置玩家火苗工厂。
- 单机 `FlameModule` 初始化时通过 `GameInstanceSubsystem` 查找 `IPlayerSceneServiceBindings`，请求场景工厂为当前稳定 `PlayerId` 分配 `Flame.prefab`，再由 `PlayerFlameController` 建立归属；模块卸载时只释放该工厂创建的火苗。
- `SnowValley_Playable` 已直接序列化接入 Flame 模块：通过 `PlayerCoreOnly` 的场景级 Prefab Override 添加 `FlameModule`、玩家火焰控制/资源/放置/升级/视觉组件和 `FlameAnchor`，并新增 `Gameplay_SceneServices`、`Gameplay_UI` 两个场景根节点；不会重建环境，也不会复制 `DemoScene`。原 `SnowValleyGameplayIntegrationBuilder` 菜单仍保留给后续新副本场景使用，但不再是本场景的必需步骤。
- HUD 在部分组装场景中只显示已有能力：Fuel 与移动/跳跃可以显示；PlaceFire、Rest、Expression 在对应 Intent Router/消费者尚未接入时保持隐藏，避免出现能看见但无效的按钮。
- `PlayerCoreOnly` 的直接探索 Camera 新增显式 `PlayerCameraObstruction`。它从相机 Pivot 到作者设定机位做球形检测，遇到雪地、山体、墙壁或天花板时立即拉近，离开遮挡后平滑复位；只处理相机臂，不修改 Look、移动或人物根节点。重点修复 SnowValley 抬头看天空时第三人称相机穿入地面/山体的问题。完整 `Player` 的活动/Cinemachine 链路暂不挂该组件，避免覆盖活动镜头执行器。

接线链路：

`LocalPlayerContext -> PlayerCoreHost -> PlayerFlameModule -> FlameResourceController`

`PlayerFlameModule -> IPlayerSceneServiceBindings -> Flame.prefab -> PlayerFlameController`

`FlameResourceState -> FirePlayHudForm/FuelRoot`

`PlayerLook -> CameraPivot -> PlayerCameraObstruction -> Camera final position`

验收前让 Unity 重载并保存 `SnowValley_Playable` 后直接进入 Play Mode。检查：Hierarchy 中 `Player_Core` 的场景 Override 下可见 Flame 组件和 `FlameAnchor`，两个场景根节点可见；角色旁生成唯一 `Flame[local.player]`；HUD 显示余火且随时间缓慢下降；靠近真实 `Hero_Campfire` 时只应用既有附近篝火倍率；PlaceFire/Rest/Expression 暂不显示；抬头、贴近坡面和背靠山体时相机拉近且离开后恢复，不再穿模。SmallFire 放置和篝火交互明确留到 InteractionModule 接入步骤验收。

- 静态验证：新 Camera 组件、FlameModule/场景服务改动与接入菜单已纳入 Runtime/Editor 编译检查，最终为 0 错误、0 警告；新 Module Prefab 的 7 个脚本 GUID、配置资产、SmallFire/Campfire Prefab 引用和对象 fileID 均已核对。接入菜单现在会在加载 Prefab 前执行同步 AssetDatabase 刷新，避免新资源尚未导入时首轮执行误报“找不到 Prefab”。Unity 菜单执行后的场景序列化结果与 Play Mode 表现仍需人工验收。
