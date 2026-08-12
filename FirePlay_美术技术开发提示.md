# FirePlay 美术技术开发提示

> 建议将本文作为新任务的首条上下文。新任务应先建立视觉与性能基线，不继续扩张已经冻结的玩法架构。

## 可直接复制到新任务的提示

```text
请接手 E:\Unity\UnityProject\Firer 的美术技术与场景重建工作。

开始前必须依次完整阅读：
1. FirePlay_开发状态.md
2. FirePlay_美术技术开发提示.md
3. FirePlay_核心架构.md（只用于理解不可破坏的玩法/表现边界和场景组装）

当前玩法、火焰、活动、Player 与网络适配的核心架构已经冻结。不要继续为了整洁重构 Activity、Player、EventBus、GameInstanceSubsystem 或网络层；美术表现只读取状态、事件和只读快照。不要改变已经验收的玩法规则。

产品视觉方向是温暖、安静、简约、有呼吸感的低压力小箱庭。可参考 Mountain、Sky 等作品的留白、色彩节奏和情绪，但不要复制具体资产、构图或 UI。UI 不使用厚重背景贴图，倾向透明、清晰、低干扰，并适配 Android。

请先审核 Assets/Scenes/ArtScene.unity、DemoScene 的 URP/Volume/灯光/地形/材质/植被和现有 Rendering 代码，给出当前视觉问题、技术风险、Android 性能风险与第一阶段 LookDev 计划。然后只实现一个可在 Unity 中明确验收的小切片，优先建立：视觉基准场景 + 光照/雾/色彩基线 + 一小块可复用地表和植被样板。不要一开始整体重做 DemoScene。

每个阶段都必须给出 Unity Inspector/场景配置说明、验收清单，并更新 FirePlay_开发状态.md。正式替换 DemoScene 前保留可回退的对照场景或 Git 提交。
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

这些只是桥梁，不是完整美术系统。当前缺口包括：

- Terrain 与路径缺乏构图、层次和视线引导；
- 植被种类、密度节奏、风摆和交互反馈不足；
- URP 主光、阴影、环境光、雾、Volume 与色彩没有统一基线；
- 余火、篝火成长和世界树阶段缺少环境级颜色、植被和氛围响应；
- 角色、活动道具、Animator、VFX 和正式音频仍多为占位；
- 尚未建立 Blender 到 Unity 的正式尺度、Pivot、材质、LOD 和碰撞规范；
- 尚未为 Android 建立可测量的渲染预算。

## 3. 推荐实施顺序

### 阶段 A：LookDev 基线

1. 检查 `ArtScene`，保留现状或复制为独立 LookDev 场景；
2. 收集 3–5 张只表达色彩、材质、雾和构图的参考，不照搬资产；
3. 固定一个代表性机位：玩家、篝火、中景植被、远景轮廓和天空同时可见；
4. 建立主光、环境光、阴影、雾和 Volume 的低/中/高质量对照；
5. 用灰盒和少量正式材质先验证视觉层次。

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
3. 一个独立 LookDev 场景或 `ArtScene` 的安全小切片；
4. 一小块地表、3–5 类植被、篝火近景和远景轮廓；
5. PC 与 Android 两档 URP/Volume/阴影配置建议；
6. Unity Play Mode 截图和可重复的验收步骤；
7. 对 `FirePlay_开发状态.md` 的事实更新。

在这批通过前，不批量替换 DemoScene、不批量生成模型、不重写玩法架构。
