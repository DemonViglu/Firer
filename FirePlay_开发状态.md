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
5. 美术基线稳定后，再恢复 PC Host + Windows/Android Client 的专项联机验收。

## 8. 最近验证证据

- `dotnet build Assembly-CSharp.csproj --nologo --no-restore`：0 错误、0 警告（最近一次代码收口检查）。
- Player Prefab 与 Network Prefab 的静态 GUID/fileID 关系已检查；`PlayerNetworkBase` 不再作为独立生成项。
- Fishing 纵向追踪玩法、世界树贡献、Rest/Stargazing 恢复链路已由用户验收。
- Guitar 按钮 Raycast 问题已由用户修正；后续 UI 必须保留可交互控件自身的 Target Graphic。
- 本轮文档清理只修改 Markdown，不触碰场景、Prefab 或运行时代码。
