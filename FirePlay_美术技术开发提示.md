# FirePlay 美术技术开发提示

> 建议将本文作为新任务的首条上下文。新任务应先建立视觉与性能基线，不继续扩张已经冻结的玩法架构。

## 可直接复制到新任务的提示

```text
请接手 E:\Unity\UnityProject\Firer 的美术技术与 SnowValley 场景深化工作。

开始前必须依次完整阅读：
1. FirePlay_开发状态.md
2. FirePlay_美术技术开发提示.md
3. FirePlay_核心架构.md（只用于理解不可破坏的玩法/表现边界和场景组装）

当前玩法、火焰、活动、Player 与网络适配的核心架构已经冻结。PC 联机开发基线已经验收。不要继续为了整洁重构 Activity、Player、EventBus、GameInstanceSubsystem 或网络层；美术表现只读取状态、事件和只读快照。不要改变已经验收的玩法规则。

产品视觉方向是温暖、安静、简约、有呼吸感的低压力小箱庭。可参考 Mountain、Sky 等作品的留白、色彩节奏和情绪，但不要复制具体资产、构图或 UI。UI 不使用厚重背景贴图，倾向透明、清晰、低干扰，并适配 Android。

当前唯一正式场景是 Assets/Scenes/SnowValley_Playable.unity。它已经具有可玩的雪谷、受篝火影响的雪地/植被、可交互冰面、角色模型和 Gameplay/Camera/Network 接线。请直接审核并渐进改善该场景的 URP/Volume/灯光/地形/材质/植被和现有 Rendering 代码；ArtScene 与 DemoScene 只作为历史参考，不再作为交付入口。先给出当前视觉问题、技术风险和 Android 性能风险，再只实现一个可明确验收的小切片。不要复制场景后另起一套正式 Gameplay 接线，也不要整体重建已经可玩的 SnowValley。

每个阶段都必须给出 Unity Inspector/场景配置说明、验收清单，并更新 FirePlay_开发状态.md。大范围修改 SnowValley 前保留同机位对照和可回退的 Git 提交。
```

## 1. 已冻结的技术边界

- 活动继续使用 `Definition + Logic + Factory + Form + Visuals`；美术资源接入 Visuals、Animator、Audio、VFX 或 Camera Profile，不把表现字段塞进通用 Host。
- 余火、篝火、SmallFire 和世界树是状态权威；Shader、灯光、粒子、花草和音景只能读取状态。
- Player 只暴露语义 Camera Targets；Cinemachine Profile 属于场景级 `ActivityCameraRigExecutor`。
- 跨模块事实使用现有 `GameEventBus`，全局服务使用 `GameInstanceSubsystem`；逐帧材质风摆、粒子模拟和 Camera Tick 不走 EventBus。
- 单对象材质变化使用 `MaterialPropertyBlock`；不要实例化共享材质。
- 美术替换不得改变 `StableSceneId`，不得用名称、Hierarchy 路径或 Instance ID 代替稳定身份。
- `Player.prefab` 是单机完整组合；`PlayerNetworkGameplay.prefab` 是正式网络 Player；`PlayerNetworkBase.prefab` 禁止直接使用。

## 2. 当前美术事实与主要缺口

现有工程有少量状态到表现的桥梁：

- `FlameVisuals`、`CampfireVisuals`、`SmallFire`：火焰颜色、强度和 Point Light；
- `WarmthNode`：读取附近篝火并驱动局部 Renderer/Light；
- `WorldBloom`：通过 `MaterialPropertyBlock` 驱动 `_LitAmount / _InkColor / _BaseColor / _BloomColor`；
- `PlayerAtmosphereBridge`：当前主要混合环境、温暖和 Rest 音频层；
- `TreePersonalLightVisuals`：世界树个人光点；
- `ActivityCameraRigExecutor`：活动镜头 Profile。

SnowValley 已有雪景地表、篝火热场、局部植被、交互冰面、基础光照和带 Locomotion 的角色模型。这些已经是正式可玩基线，不应再被描述为“尚未搭建场景”。当前缺口包括：

- Terrain 与路径仍需要进一步强化构图、层次和视线引导；
- 植被种类、密度节奏、风摆和交互反馈不足；
- URP 主光、阴影、环境光、雾、Volume 与色彩没有统一基线；
- 余火、篝火成长和世界树阶段缺少环境级颜色、植被和氛围响应；
- 角色、活动道具、Animator、VFX 和正式音频仍多为占位；
- 尚未建立 Blender 到 Unity 的正式尺度、Pivot、材质、LOD 和碰撞规范；
- 尚未为 Android 建立可测量的渲染预算。

当前已有 `EnvironmentWarmthDirector + WarmthSnowReceiver + WarmthGrowthReceiver + WarmthIceReceiver + WarmthAtmosphereReceiver` 的环境热场读取链，不要再建立第二套热场管理器。第一批表现作者任务应直接补现有挂点：

- `FlameSource.prefab`：为 `_restoreVfx` 绑定一次性、非循环、`Play On Awake=false` 的余烬反馈；没有合适短音效时保持 `_restoreAudio` 为空；
- `SmallFire.prefab`：显式绑定现有 Point Light 到 `_fireLight`，为 `_fireVfx` 绑定低粒子数、`Play On Awake=false` 的循环火焰，由 `SmallFire.Initialize` 启动，避免预览阶段误播放；
- SnowValley 公共篝火：使用已有 `CampfireVisuals` 挂点组织灯光、粒子和成长阶段，不把成长状态写进表现脚本；
- 世界树：在现有贡献/成长事实上增加阶段模型和一次性 Cue，不复制贡献逻辑。

## 3. 推荐实施顺序

### 阶段 A：SnowValley 视觉审计与基线固化

1. 检查 `SnowValley_Playable` 当前画面并保留同机位前后对照；高风险 Shader/渲染实验才使用独立 LookDev，不复制正式 Gameplay 层级；
2. 收集 3–5 张只表达色彩、材质、雾和构图的参考，不照搬资产；
3. 在 SnowValley 固定一个代表性机位：玩家、篝火、中景植被、远景轮廓和天空同时可见；
4. 建立主光、环境光、阴影、雾和 Volume 的低/中/高质量对照；
5. 在现有雪谷资产上小范围迭代，避免用新灰盒覆盖已经验收的可玩区域。

验收重点：即使关闭玩法 UI，截图也能清楚看出焦点、行进方向、温暖区域和远景层次。

### 阶段 B：地表与植被样板

1. 只制作一小块完整地表样板，不立刻铺满地图；
2. 建立 Terrain Layer 或 Mesh Terrain 的使用边界；
3. 建立草、花、灌木、石头和树的密度/尺度/色相规则；
4. 风摆、踩踏或靠近反馈通过统一表现参数驱动；
5. 在 Android 目标设备上测量批次、Overdraw、阴影和 GPU 时间。

花草“生长”应读取世界阶段或温暖场事件，由场景级 Visual Director/区域表现组件驱动；不要让每株花逐帧寻找 Player 或直接修改世界状态。

### 阶段 C：火焰与环境响应

建立只读的环境表现层，将余火、篝火等级、世界树阶段或局部温暖转换为：

- 局部颜色温度与曝光的有限变化；
- 火光范围、材质发光、粒子密度与音景层；
- 植被颜色、生长量、花朵显隐或区域饱和度；
- 世界树阶段的材质、光点和远景可读性。

避免整屏颜色频繁随玩家余火抖动；全局调色应平滑、克制，近场变化优先由灯光、材质和 VFX 表达。

### 阶段 D：Blender 与角色/道具管线

先确定并记录：

- Unity 1 Unit 对应尺度；
- Forward、Up、Apply Transform 和 Pivot 规则；
- FBX 导出预设与命名；
- 材质槽数量、贴图通道、纹理尺寸和压缩；
- 静态碰撞、LOD、Lightmap UV 与阴影策略；
- 角色骨架、Animator、Root Motion 与活动道具挂点。

正式角色、吉他、鱼竿、棉花糖和篝火资产应通过现有 Visuals/Animator 引用接入，不让模型层反向拥有玩法 Logic。

### 阶段 E：UI、动画、VFX 与音频统一

- UI：透明、简约、明确点击区，文字与背景保持足够对比；按钮自身 Target Graphic 必须接收 Raycast；
- Animation：基础 Locomotion、Emote、Rest 与活动状态分层，Cue 与持续 State 分开；
- VFX：按语义 Cue 播放，不把一次性粒子状态写回活动 Logic；
- Audio：环境底层、温暖层、Rest/活动层和 UI Feedback 分层，避免所有声音集中在 Player 单一 AudioSource。

当前可直接接入的动画参数无需再改玩法代码：持续 Bool 为 `IsResting`、`IsMarshmallowRoasting`、`IsGuitarPlaying`、`IsFishing`；一次性 Trigger 为 `MarshmallowMaterialize/Turn/Eat/Cancel/Receive`、`GuitarBegin/Play`、`FishingCast/Reel`、`EmoteWave/Thanks`。当前 Controller 中持续状态已有入口，但大多数 Trigger 仍只有参数、没有正式状态跳转。动画会话只需补 Animator State/Transition 或替换 Clip；不要让 Animation Event 反向结算玩法。

`MarshmallowVisuals` 已暴露棉花糖道具、Renderer、六个可选短音 Clip 和一次性 VFX；`FishingActivityVisuals`、`GuitarActivityVisuals` 也已有各自表现出口。正式音频应各自绑定专用或明确分层的 AudioSource，不把所有活动声音继续合并到 Player 根节点。远端 Observer 只执行 Animation/VFX Cue，不得借动画接线打开 UI、切相机或锁本机移动。

## 4. Android 性能门禁

第一阶段不要凭经验写死预算；应先选定最低测试设备和目标帧率，再记录基线。每个 LookDev 版本至少检查：

- Render Scale、分辨率与目标帧率；
- Draw Calls、SetPass、Triangles、Visible Lights；
- Main Light 与 Additional Lights 的阴影成本；
- 透明植被、粒子与 UI 的 Overdraw；
- Terrain/植被剔除距离、LOD 与阴影距离；
- Shader Variant、纹理内存和运行内存；
- CPU/GPU 帧时间，不能只看 Editor Game View FPS。

正式场景材质和 Shader 必须有 Android 可用的降级路径。优先通过构图、色彩、雾和轮廓获得风格，不依赖大量实时灯光或高成本全屏效果。

## 5. 第一批推荐交付物

新任务第一批只交付：

1. 当前视觉与性能审计；
2. 一组明确参考和颜色/光照目标；
3. `SnowValley_Playable` 中一个可回退、可截图对比的安全小切片；
4. 一小块地表、3–5 类植被、篝火近景和远景轮廓；
5. PC 与 Android 两档 URP/Volume/阴影配置建议；
6. Unity Play Mode 截图和可重复的验收步骤；
7. 对 `FirePlay_开发状态.md` 的事实更新。

在这批通过前，不大范围改写 SnowValley、不批量生成模型、不重写玩法架构或复制正式 Gameplay 接线。
