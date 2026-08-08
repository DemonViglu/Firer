# FirePlay 当前开发状态

> 本文只记录当前事实、待验收项、已知风险和下一步。设计契约见 `FirePlay_活动系统重构契约.md`；历史迁移以 Git 提交为准，不再追加到本文。

## 1. 当前目标与技术基线

- Unity `6000.5.5f1`，URP `17.5.0`，Input System `1.19.0`。
- 当前优先级：**暂停不阻塞游戏性的网络收口，优先补齐活动玩法、UI 和反馈**。
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
- `PlayerCoreOnly.prefab` 用于基础移动、视角、重力和最小模块实验。
- `Player.prefab` 是旧完整单机场景组合；`PlayerNetworkGameplay.prefab` 是当前包含活动、火焰、Rest 和交互模块的网络 Gameplay 组合。
- Player 功能按 `FlameModule / ActivityModule / RestModule / InteractionModule` 放在可见层级中，具体玩法 Logic 不挂在 Player 上。
- `PlayerCameraTargetSet` 只暴露语义镜头目标，不引用 Cinemachine。
- 场景级 `ActivityCameraRigExecutor` 统一拥有 Marshmallow、Fishing、Stargazing 等 Cinemachine Profile 和 TargetGroup。
- `PlayerActivityPresentationHost` 负责请求与执行器之间的唯一转发；远端观察者不能获得本机 UI、Camera 或 MovementLock。

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
| Fishing | 独立拟造、抛竿、咬钩、提竿、收/放线、张力与捕获进度 | 纯逻辑探针通过；新版搏鱼 UI 待 Play Mode 验收 |
| Guitar | Anywhere；独立 21 键 UI；PC 三排键盘；Android 触控；可替换程序化拨弦占位音 | 待本轮 Play Mode 验收 |
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

- `ActivitySelectionForms.prefab` 使用半透明深蓝灰色切片面板，不依赖背景贴图。
- 轮盘信息层级由标题、活动按钮与关闭按钮构成；视觉重点来自透明度、留白和按钮状态，不叠加插画纹理。
- 当前样式可直接适配 PC 与 Android，不增加贴图导入、缩放和压缩维护成本。

### Marshmallow 时机玩法

- `Needle / TargetCenter / PerfectZone` 保存在 Marshmallow 自有快照中。
- UI 显示火候轨道、亮区和指针；翻面时按命中情况记录品质。
- 纯逻辑验证：Perfect 路径正确扣除/返还余火；Scorched 路径不错误返还。

### Fishing 搏鱼玩法

- 咬钩后进入 Fighting；收线提高进度与张力，放线降低张力并损失少量进度。
- 张力 100% 断线，捕获进度 100% 才结算鱼和余火返还。
- UI 显示捕获进度、鱼线张力和独立放线按钮。
- 两条显示条使用显式内置 UI Sprite 与 `Filled/Horizontal` 模式；`fillAmount` 可正确驱动画面宽度。
- 纯逻辑验证：交替收放可以捕获；连续猛收会断线且不错误结算。

### 世界树贡献链

- 世界交互扫描与 Host 权威距离校验现在都以目标 Collider 表面为准，不再用大型物体的 Transform 原点否决合法交互。
- Host 本地命令被拒绝时会输出意图、目标 Stable ID 与具体原因，避免世界动作静默失效。
- 大树贡献仍由 `WorldTreeContribution` 原子扣除余火、记录玩家光点并推进世界树阶段；本轮只修复网络 Player 的距离门禁。

### Guitar 演奏

- PC：`QWERTYU / ASDFGHJ / ZXCVBNM` 对应 21 个语义音位。
- 鼠标与 Android 继续使用同一组 21 个 UI Button。
- 未配置 AudioClip 时使用 Inspector 可关闭的程序化拨弦声；正式 Clip 自动优先。
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

1. 最新轮盘、Emote、Marshmallow、Fishing、Guitar 尚缺本轮 Unity Play Mode 人工验收。
2. 正式角色模型、Animator、活动动作、粒子和大部分正式音频尚未接入；占位表现不能当成最终美术。
3. 活动轮盘透明度与按钮对比度仍需根据实际 Game View 验收；不再使用背景贴图。
4. “烤好的棉花糖交给伙伴”不能只增加一个按钮：当前产物没有跨 Session 的稳定持有状态，Target 又在 Session 开始时确定。实现前需选择完整的“可携带活动产物”或“动作级权威目标”契约，禁止用 payload 绕过目标校验。
5. Stargazing 不扩成小游戏；网络异常恢复、重连和非阻塞优化继续后置。
6. 旧 `ColorSource / RestorableNode` 仍是早期实验视觉链路，不继续扩展颜色解谜。
7. 其他操作手册中仍可能残留迁移前措辞；以活动契约和本文为准，发现时按当前事实修正，不建立兼容层。
8. 世界树 Collider 距离门禁已修复，但仍需 Play Mode 验收贡献按钮、余火扣除、个人光点和阶段切换。

## 8. 下一步顺序

### 当前唯一验收批次

1. 活动轮盘：透明度、中文可读性、按钮点击、移动中 Anchor 候选增减。
2. Emote：四个 Cue 可重复触发，移动不锁，关闭正常。
3. Marshmallow：拟造、亮区计时、Perfect/Toasted/Scorched、食用返还、退出恢复。
4. Fishing：拟造、抛竿、提竿、断线、交替收放捕获、退出恢复。
5. Guitar：21 个 UI 键与 PC 键盘均可演奏、有占位音、道具显隐和移动恢复正确。
6. 世界树：靠近大树贡献一次，确认扣除 10 余火、按钮隐藏/提示已贡献、个人光点出现且树阶段推进。

### 该批次通过后

1. 只修复验收暴露的问题并完成参数调优；
2. 为已有活动补正式 UI 图标、过渡动画、音效、VFX 和角色动作资源；
3. 设计可复用的社交产物/目标交互契约，再实现棉花糖赠送等伙伴互动；
4. 游戏性稳定后恢复 Host/Client、Windows/Android 专项联机验收；
5. 最后选择需要异步持久化的实时事实。

## 9. 当前验证证据

- `dotnet build Assembly-CSharp.csproj --nologo --no-restore`：0 错误、0 警告。
- Marshmallow 与 Fishing 的纯逻辑探针已通过；一次性探针和 Prefab 生成器已删除。
- ActivitySelection、Fishing、Marshmallow、Emote、NetworkConnection Prefab 已做本地 fileID/引用静态检查。
- 静态检查不能代替 Unity Play Mode；第 8 节验收完成前，本轮内容保持“待验收”。
