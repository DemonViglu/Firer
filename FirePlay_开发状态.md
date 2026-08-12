# FirePlay 当前开发状态

> 本文只记录当前事实、待验收项、已知风险和下一步。设计契约见 `FirePlay_活动系统重构契约.md`；历史迁移以 Git 提交为准，不再追加到本文。

## 1. 当前目标与技术基线

- Unity `6000.5.5f1`，URP `17.5.0`，Input System `1.19.0`。
- 当前优先级：**完成 Player 资产组合收口与回归后冻结核心玩法架构，转入场景、美术技术、动画、VFX、音频和 UI 视觉生产**。
- 实时联机代码保留现状；在用户明确开始联机验收前，不以重连、异常恢复或网络优化阻塞内容开发。
- 异步功能只保存实时游戏产生的必要数据，不维护第二套玩法兼容逻辑。
- 当前工作树包含一轮尚未提交的大型重构；不得使用破坏性 Git 命令覆盖用户已有改动。

## 2. 当前可依赖的架构事实

### 活动系统

- `ActivityDefinitionAsset + ActivityCatalogAsset` 保存可复用活动定义；同一活动不因地点不同而复制。
- `ActivityAnchorNode` 只保存稳定地点身份、活动引用和规则引用；一个地点可组合多个活动。
- `ActivityRuntime / ActivitySystem / ActivitySession` 负责 Preflight、Prepare、Commit、Action、Tick 和 End。
- `PlayerActivityHost` 是 Player 唯一活动宿主；不保存具体玩法字段，也不动态生成玩法组件。
- 每个具体玩法拥有自己的 `Logic + Factory + Form + Visuals`；通用 Host、Player Router 和网络层不写玩法分支。
- 活动只请求 UI、Camera、Movement、Look、Animation 和 VFX；具体 Unity 组件由 Player/场景表现执行器拥有。
- `GameEventBus` 传递跨模块请求与事实；`GameInstanceSubsystem` 显式定位全局服务。
- 活动状态快照属于对应玩法，通用 Host/Transport 只搬运不透明 payload。

### Player 与场景组合

- Player 不再运行时 `AddComponent` 核心服务；缺少显式组件时直接报告配置错误。
- `PlayerCoreOnly.prefab` 是真正的纯基础资产，只包含输入、移动、Look、`LocalPlayerContext`、语义 Camera Targets 和占位 Body；不含 Network、Flame、Activity、Rest 或 Interaction。
- `Player.prefab` 是完整单机 Gameplay 组合，供 `DemoScene`、`ArtScene` 和后续单机内容场景使用。
- `PlayerNetworkBase.prefab` 保留原网络父资产 GUID，只为 `PlayerNetworkGameplay.prefab` 提供 NGO、网络身份和权威 Flame 基础；禁止直接放入场景。
- `PlayerNetworkGameplay.prefab` 是正式网络 Player，由 `NetworkManager` 生成；`DefaultNetworkPrefabs` 不再把技术基座 `PlayerNetworkBase` 注册为可生成对象。
- Player 功能按 `FlameModule / ActivityModule / RestModule / InteractionModule` 放在可见层级中，具体玩法 Logic 不挂在 Player 上。
- `PlayerCameraTargetSet` 只暴露语义镜头目标，不引用 Cinemachine。
- 场景级 `ActivityCameraRigExecutor` 统一拥有 Marshmallow、Fishing、Stargazing 等 Cinemachine Profile 和 TargetGroup。
- `PlayerActivityPresentationHost` 负责请求与执行器之间的唯一转发；远端观察者不能获得本机 UI、Camera 或 MovementLock。

### 2026-08-12 Player 资产组合收口

- 已纠正联机接入时对 `PlayerCoreOnly` 的语义污染：原网络父资产保留 GUID 并改名为 `PlayerNetworkBase`，因此 `PlayerNetworkGameplay` 的 Variant 与 NGO 引用不需要兼容层或重新绑定。
- 已恢复独立 GUID 的纯 `PlayerCoreOnly`：静态检查只包含 `FirePlayPlayerInput / PlayerMovement / PlayerLook / LocalPlayerContext / PlayerCameraTargetSet` 五个 FirePlay 脚本。
- `DefaultNetworkPrefabs` 已移除技术基座，只登记正式 `PlayerNetworkGameplay` 和需要独立生成的网络世界对象。
- `Player.prefab` 的已验收单机组合和本轮玩法/UI 改动均未迁移、删除或重写。
- 静态 Prefab fileID 检查和命令行编译已通过；仍需 Unity 完成资源重导入并按第 8 节做一次人工回归，之后才正式标记该组合冻结完成。

### 火焰与世界

- 玩家余火、篝火成长、SmallFire 放置/上限、FlameSource、世界树贡献和存档属于独立火焰/世界链路。
- 活动只能通过 `IActivityFlameResource.TryConsume/Restore` 消耗或返还余火；余火不足不得部分扣除。
- 火焰数值与视觉分离；材质表现通过 `MaterialPropertyBlock` 读取状态。
- 稳定对象使用 `StableSceneId`，不使用 GameObject 名称、层级路径或 Instance ID 作为存档/网络身份。

## 3. 当前运行链路

```text
活动轮盘 / 活动 UI / 输入
    -> GameEventBus: ActivitySelectionRequested / ActivityActionRequested
    -> PlayerActivityHost
    -> ActivityRuntime / ActivitySystem
    -> Definition + Anchor + Rules + PlayerState + Flame
    -> Preflight -> Prepare -> Commit
    -> ActivitySession
    -> ActivityStateChanged / InteractionOccurred / SessionEnded
    -> Form / Visuals / PlayerActivityPresentationHost
```

```text
Activity Presentation Request
    -> PlayerActivityPresentationHost
    -> UIManager / ActivityCameraRigExecutor / PlayerAnimationController
    -> UI、相机、移动锁、朝向、动画或 VFX
```

```text
Player / Campfire / Activity
    -> FlameResourceController 或 Host 世界命令
    -> Flame / Campfire / SmallFire / WorldTree 权威状态
    -> Visuals / HUD / Save / Network Snapshot
```

所有 Session 结束都必须对称关闭 UI、退出 Activity Camera、释放活动拥有的 Player 请求并发布一次结束事实。

## 4. 内容能力与验收状态

| 内容 | 当前实现 | 验收状态 |
|---|---|---|
| 火焰资源与单机世界循环 | 余火、篝火、SmallFire、FlameSource、世界树、存档 | 重构前后核心单机功能已多轮人工验收；最新网络权威版本暂不作为当前验收门槛 |
| 活动轮盘 | 径向布局；打开期间每 `0.15s` 刷新最近 Anchor；Anywhere 与 Anchor 候选并存；真实开始结果回执 | 新逻辑与透明简约样式待本轮 Play Mode 验收 |
| Emote | Anywhere；独立四项表情 UI；只发送 `expression.*` Cue；不锁移动 | 待本轮 Play Mode 验收 |
| Marshmallow | 独立拟造、火候指针、随机亮区、两次翻面、品质、食用与余火返还 | 纯逻辑探针通过；新版时机 UI 待 Play Mode 验收 |
| Fishing | 独立拟造、抛竿、咬钩、提竿；搏鱼改为鱼标与绿色捕获区的纵向追踪玩法 | Play Mode 玩法验收通过 |
| Guitar | Anywhere；独立 21 键 UI；PC 三排键盘；Android 触控；三组 C 大调自然音阶与可替换程序钢琴音 | 演奏流程已可用；新版音色待 Play Mode 听感验收 |
| Stargazing | RestSpot 组合出的共享休息氛围；观星相机、伙伴 TargetGroup 与起身退出 | 既有休息/相机恢复已验收；明确不增加计分、胜负或独立小游戏 UI |

### Stargazing 产品边界

Stargazing 是“一起休息、看天空”的放松方式，不是小玩法。它可以使用轻量 Activity Session 管理：

- `resting` 前提；
- 参与者事实；
- 观星 Camera Profile；
- 伙伴 TargetGroup；
- 起身后的对称退出。

它不拥有星图记忆、操作循环、计分、奖励或专属玩法面板。

## 5. 本轮新增但尚待人工验收

### 活动轮盘视觉

- `ActivitySelectionForms.prefab` 已改为接近无整块背景的透明轮盘，不依赖任何背景贴图。
- 地点活动使用低透明暖色，Anywhere 活动使用低透明冷色；标题、状态与关闭按钮统一为中性白灰层级。
- `GuitarActivityForms.prefab` 同步作为第二个样板：三排按键以冷色、中性、暖色轻量区分三个音区，保留 Do4-Si6 的清晰文字。
- 两个样板都只修改现有 UGUI Prefab 的颜色、透明度、留白和内置圆角 Sprite，不新增运行时样式组件或玩法代码。
- 可交互按钮自身的 Target Graphic 必须保持 `Raycast Target` 开启；按钮文字等非交互子 Graphic 可以关闭。透明根背景是否参与 Raycast由窗体遮挡策略决定，不能代替按钮自己的点击接收面。
- 当前样式可直接适配 PC 与 Android，不增加贴图导入、缩放和压缩维护成本；具体对比度仍以 Game View 人工验收为准。

### Marshmallow 时机玩法

- `Needle / TargetCenter / PerfectZone` 保存在 Marshmallow 自有快照中。
- UI 显示火候轨道、亮区和指针；翻面时按命中情况记录品质。
- 纯逻辑验证：Perfect 路径正确扣除/返还余火；Scorched 路径不错误返还。

### Fishing 搏鱼玩法

- 咬钩并提竿后进入 Fighting：橙色鱼标会在纵向轨道内移动，玩家控制绿色捕获区追踪它。
- 按住 `Space` 或 UI 的“按住上浮”区域使捕获区上升，松开后捕获区受重力下降；PC 鼠标与 Android 触控共用同一按住语义。
- 鱼标位于绿色区域内时增加捕获进度，离开时逐步损失；100% 后点击“收获”结算鱼和余火返还，0% 则鱼逃脱并允许重新抛竿。
- UI 只发布 `fishing.lift.start / fishing.lift.stop`，鱼移动、捕获区物理和结算仍由 Fishing Logic 在 Host 侧推进；通用 Activity/Player 不包含钓鱼规则。
- 两条显示条使用显式内置 UI Sprite 与 `Filled/Horizontal` 模式；`fillAmount` 可正确驱动画面宽度。

### 世界树贡献链

- 世界交互扫描与 Host 权威距离校验现在都以目标 Collider 表面为准，不再用大型物体的 Transform 原点否决合法交互。
- Host 本地命令被拒绝时会输出意图、目标 Stable ID 与具体原因，避免世界动作静默失效。
- 大树贡献仍由 `WorldTreeContribution` 原子扣除余火、记录玩家光点并推进世界树阶段；本轮只修复网络 Player 的距离门禁。

### Guitar 演奏

- PC：`QWERTYU / ASDFGHJ / ZXCVBNM` 分别对应 `Do4-Si4 / Do5-Si5 / Do6-Si6` 三组 C 大调自然音阶。
- 鼠标与 Android 继续使用同一组 21 个 UI Button。
- 未配置 AudioClip 时使用 Inspector 可关闭的程序化柔和钢琴音；正式 Clip 仍按 Do4-Si6 顺序自动优先。
- 逐键声音由 Player 表现 Cue 直接触发，不再轮询 `PlayedKeyCount`；快速连按或同帧多键不会只剩最后一个音。
- 两种完整玩法 Player prefab 均显式绑定 AudioSource，不运行时添加组件。

## 6. 当前网络事实（暂缓专项验收）

- NGO/UTP 已导入；DemoScene 包含 `NetworkManager + UnityTransport + FirePlayNetworkBootstrap`。
- `PlayerNetworkGameplay.prefab` 是当前网络 Player；本地/远端输入、UI、相机和活动宿主边界已分开。
- 活动请求、事实 DTO、Host 权威入口、远端状态镜像与 late-join 快照代码已经存在。
- SmallFire、Campfire、FlameSource 世界状态和 WorldTree 已有显式网络适配器与稳定 ID 链路。
- Android/Windows Client 可通过连接窗体或命令行配置地址与端口；协议准入与最大人数配置已存在。
- `NetworkConnectionForms.prefab` 的截断损坏已修复。
- **以上只代表代码与静态配置存在；用户已明确暂不验收具体联机功能，因此不得宣称联机完成。**

## 7. 已知风险与明确不做

1. 最新轮盘、Emote、Marshmallow 与 Guitar 音色尚缺本轮 Unity Play Mode 人工验收；Fishing 纵向追踪玩法已验收通过。
2. 正式角色模型、Animator、活动动作、粒子和大部分正式音频尚未接入；占位表现不能当成最终美术。
3. 活动轮盘与 Guitar 已完成第一版透明简约样板，仍需根据实际 Game View 验收文字对比度、按钮点击区和不同场景亮度；不再使用背景贴图。
4. “烤好的棉花糖交给伙伴”不能只增加一个按钮：当前产物没有跨 Session 的稳定持有状态，Target 又在 Session 开始时确定。实现前需选择完整的“可携带活动产物”或“动作级权威目标”契约，禁止用 payload 绕过目标校验。
5. Stargazing 不扩成小游戏；网络异常恢复、重连和非阻塞优化继续后置。
6. 旧 `ColorSource / RestorableNode` 仍是早期实验视觉链路，不继续扩展颜色解谜。
7. 其他操作手册中仍可能残留迁移前措辞；以活动契约和本文为准，发现时按当前事实修正，不建立兼容层。
8. Player 组合已经冻结为 `PlayerCoreOnly / Player / PlayerNetworkBase / PlayerNetworkGameplay` 四种明确职责；不再通过把网络或 Gameplay 能力塞回基础 Prefab 来共享配置。

## 8. 下一步顺序

### 切换美术主线前的收口验收

1. Unity 重导入四个 Player Prefab，确认 `PlayerCoreOnly` 纯净、`PlayerNetworkGameplay` 父资产为 `PlayerNetworkBase` 且没有 Missing 引用；
2. 确认 `DefaultNetworkPrefabs` 只登记正式 `PlayerNetworkGameplay`，不登记 `PlayerNetworkBase`；
3. 在 `DemoScene` 回归完整单机移动、余火、活动轮盘、Rest 与世界交互；
4. 通过后将核心玩法与 Player 组合架构标记为冻结，并创建 Git 提交。

### 内容与 UI 验收批次

1. 活动轮盘：无背景方向、中文可读性、按钮对比度与点击、移动中 Anchor 候选增减。
2. Emote：四个 Cue 可重复触发，移动不锁，关闭正常。
3. Marshmallow：拟造、亮区计时、Perfect/Toasted/Scorched、食用返还、退出恢复。
4. Fishing：已通过——拟造、抛竿、提竿、纵向追踪、鱼逃脱、收获返还与退出恢复均正常。
5. Guitar：确认透明面板与三音区配色清晰、三行按键依次为 Do4-Si6、快速连按不丢音、程序钢琴音色可接受、道具显隐和移动恢复正确。
6. 世界树：已通过——靠近贡献、余火扣除、按钮状态、个人光点与阶段推进均正常。

### 该批次通过后

1. 新开美术技术任务，按 `FirePlay_美术技术开发提示.md` 建立视觉基线、场景构图、光照、植被、Shader/VFX 与 Blender 管线；
2. 统一活动 UI 的透明简约视觉语言与 Android 安全区，不改变玩法 Logic；
3. 为已有活动补正式道具、角色动作、音效与 VFX；
4. 需要伙伴赠送或投火特殊交互时，单独设计社交产物/动作级目标契约；
5. 游戏性与美术基线稳定后恢复 Host/Client、Windows/Android 专项联机验收；
6. 最后选择需要异步持久化的实时事实。

## 9. 当前验证证据

- `dotnet build Assembly-CSharp.csproj --nologo --no-restore`：0 错误、0 警告。
- Player 收口静态结果：`PlayerCoreOnly` 5 个基础脚本、`PlayerNetworkBase` 9 个基础/网络/Flame 脚本、`PlayerNetworkGameplay` Variant 无重复本地 fileID；网络基座 GUID 只被正式 Variant 引用，不再作为独立网络生成项。
- Fishing 纵向追踪玩法已完成人工验收；Guitar 已完成自然音阶、程序钢琴音与逐键 Cue 改造，待 Play Mode 听感验收。
- ActivitySelection 与 Guitar 透明 UI 样板已完成本地 fileID/引用静态检查；Fishing、Marshmallow、Emote、NetworkConnection Prefab 的既有静态检查仍有效。
- 静态检查不能代替 Unity Play Mode；第 8 节验收完成前，本轮内容保持“待验收”。
