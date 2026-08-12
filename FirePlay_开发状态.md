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
