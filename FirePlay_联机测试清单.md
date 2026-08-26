# FirePlay 实时联机启动与测试清单

> 本文只描述当前 NGO + UTP 直连测试入口和最终验收顺序。开发完成情况以 `FirePlay_开发状态.md` 为准。

## 1. 唯一启动入口

正式联机入口是 `SnowValley_Playable` 中的 `Gameplay_NetworkBootstrap`。`DemoScene` 只保留为历史配置参考，不进入当前 Player Build：

- `NetworkManager`：Player Prefab 与 Network Prefab 注册；
- `UnityTransport`：传输实现；
- `FirePlayNetworkBootstrap`：唯一 Host / Server / Client 启停入口；
- `ConnectionApproval`：必须显式开启；Bootstrap 独占准入回调；
- 当前 `SnowValley_Playable` 配置为 `Manual + 127.0.0.1:7777`，单机启动不会自动进入网络；
- Gameplay、Activity 和 World 脚本不得直接读取 `NetworkManager` 来启动会话。

`ProjectSettings/EditorBuildSettings.asset` 当前只启用 `SnowValley_Playable`。PC 构建无需修改场景或 Inspector，即可通过启动参数覆盖：

```text
<WindowsBuild>.exe --fireplay-mode host --fireplay-port 7777
<WindowsBuild>.exe --fireplay-mode client --fireplay-address 127.0.0.1 --fireplay-port 7777
<WindowsBuild>.exe --fireplay-mode client --fireplay-address 公网或内网穿透地址 --fireplay-port 映射端口
<WindowsBuild>.exe --fireplay-mode manual
```

同时支持 `--key=value` 写法。合法 mode 只有 `manual / host / server / client`；端口必须为 `1..65535`，无效参数会明确拒绝启动。

Android 不依赖命令行。`NetworkConnectionForms` 只调用：

```text
FirePlayNetworkBootstrap.ConfigureEndpoint(address, port)
FirePlayNetworkBootstrap.StartClient()
```

连接界面不持有 `NetworkManager` 或 `UnityTransport`。它目前只是开发期 Client 直连入口，支持填写地址/端口、连接和断开，不负责启动 Host，也不是正式房间 UI。制作 Android Client 测试包时，将唯一 Bootstrap 的 `Auto Start` 设为 `Manual`，并在场景 `FirePlayUiBootstrap` 开启 `Show Network Connection On Start`；玩家在窗体内填写地址与端口后连接。PC Host 使用命令行或在专用测试场景中明确设为 Host。

## 2. 连接准入边界

- PC 与 Android 都发送同一个 `fireplay.realtime` 协议版本；当前版本为 `7`（版本 3 的互动事实/连续状态双通道、版本 4 的 Host 定向 Owner 位姿校正、版本 6 的 Host 生成式公共世界快照，以及版本 7 的 SmallFire 网络 Prefab 可见性修复）；
- 握手只判断实时联机兼容性，不包含账号、身份或玩法状态；
- SnowValley 的 `Maximum Players` 为 `4`，Host 本机也占一个玩家名额；
- 协议不匹配、缺少握手或房间已满时，Host 不生成 Player，并把明确原因返回连接窗体；
- 修改活动、UI 或美术不需要递增协议版本；只有破坏网络 DTO / Prefab / 状态语义兼容性时才递增。

## 3. SnowValley 联机前置配置（已写入场景，待 Unity 验收）

只在 `Assets/Scenes/SnowValley_Playable.unity` 操作，不修改 `DemoScene`、`ArtScene` 或美术会话资产。当前场景已经写入以下配置；打开 Unity 后先逐项核对引用是否有效：

1. `Gameplay_SceneServices` 的 `PlayerSceneServiceBindings`：已绑定 `Gameplay_ActivityServices/ActivityLogicRegistryBehaviour`、`Gameplay_ActivityCameraRig/ActivityCameraRigExecutor`、玩家 Flame Prefab 和 `NetworkPlayerSpawnPoint`；VFX Executor 暂为空是允许的。
2. `Gameplay_NetworkBootstrap` 是场景唯一 `NetworkManager + UnityTransport + FirePlayNetworkBootstrap`；`NetworkManager` 的 Player Prefab 与 Network Prefab 列表已使用 `PlayerNetworkGameplay` / `DefaultNetworkPrefabs.asset`，Transport 指向同一对象上的 `UnityTransport`，Bootstrap 默认 `Manual`，不会破坏单机启动。
3. `Gameplay_NetworkBootstrap/FirePlayNetworkBootstrap._worldStatePrefab` 必须显式指向已注册的 `NetworkWorldState.prefab`。Host/Server 启动后生成唯一 WorldState，按稳定 ID 同步预放置 FlameSource/公共 Campfire；美术篝火无需各自挂 NetworkObject，运行时 Campfire 继续使用 Prefab 上的 `FirePlayNetworkCampfire`。
4. `PlayerNetworkBase.prefab` 已显式包含 `PlayerExpressionController`，变体无需运行时补组件；确认 `PlayerNetworkGameplay` 的 `PlayerActivityHost`、`PlayerActivityPresentationHost`、`PlayerAnimationController` 和 Flame 引用没有丢失。
5. `FirePlayUiBootstrap` 的网络窗体按测试模式决定是否显示；`MarshmallowActivityForms.prefab` 已显式配置 `TargetButton + GiveButton`。两人时自动选择唯一伙伴；三至四人时点击目标按钮切换稳定 PlayerId，再点击赠送。

6. `Gameplay_CameraOutput` 是 SnowValley 唯一启用的 `Camera + AudioListener + CinemachineBrain`；`Player_Core` 的内置 Camera 必须保持禁用。单机与网络 `PlayerCameraFollowTarget` 应为相对 CameraPivot `(0, 1, -6)`。Explore/Activity Cinemachine Camera 上的 `CinemachineDecollider.TerrainResolution` 负责地形防穿透；FollowTarget 不再重复挂 `PlayerCameraObstruction`。`PlayerCameraTargetSet._activityFollowTarget` 已绑定到 Player Frame Target；探索相机位置/旋转阻尼应为 0，活动 Camera 只由 `ActivityCameraRigExecutor` 请求切换。以上引用必须在 Unity Inspector 中确认没有变成 Missing。

7. 网络同步频率应保持：`TickRate=30`；位姿 20Hz 不可靠实时流 + 0.5 秒可靠校准；余火最多 10Hz、epsilon 0.05；连续活动仪表不可靠实时流 + 0.5 秒可靠校准；Session/Interaction/Ended、世界命令、表情和社交事实全部可靠。Profiler 中不应出现连续位姿或钓鱼仪表挤压可靠队列。
8. `SmallFire.prefab` 是 NGO 注册 Prefab，根 GameObject 必须保持 Active。预览显隐只由运行时预览实例控制，不能通过把源 Prefab 设为 Inactive 实现；否则 Host 可能因权威预备阶段激活而可见，Client 的网络实例却保持隐藏。
9. `PlayerNetworkGameplay` 应显示 `CharacterFacingRoot/SnowTravelerVisual`，旧 `Body` 胶囊 Renderer 为 Disabled；`PlayerMovement._visualTransform` 必须指向父级 `CharacterFacingRoot`，`PlayerAnimationController._placeholderVisual` 必须指向子级 `SnowTravelerVisual`，且 Animation/Locomotion 必须引用同一 Animator。`Gameplay_WorldContent` 应只有三个稳定 FlameSource 和一个 `snow.world-tree.main`，不得因重复运行配置器产生副本。

## 4. 最终验收顺序

每一项失败时停止继续叠加后续测试，先修复当前层。

1. **启动与准入**：Host 和一个 Client 均成功连接；每端只有一个本地输入拥有者；PlayerId 分别稳定为 `network.player.0/1`。第 5 个玩家应被拒绝且看到“房间人数已满”，不同协议版本不得生成 Player。
2. **基础移动与相机**：双方能移动和转向；对方姿态可见；远端 Player 不接管本机输入、Camera 或 UI。
3. **玩家余火**：Host 是唯一模拟者；Client 余火来自快照；冲刺、活动消耗和恢复后双方看到相同结果。
4. **余火源**：任一方拾取后双方同时隐藏；另一方不能重复领取；Host 刷新后双方同时恢复；晚加入 Client 直接得到当前显隐状态。
5. **SmallFire**：Client 只能提交候选点；Host 校验地面、距离、坡度、上限和余火；成功后双方生成同一火种，回收/熄灭由 Host despawn。
6. **公共篝火**：添火、取火、SmallFire 升级只执行一次；Level、贡献、Warmth 与稳定 ID 一致；晚加入 Client 得到当前状态。
7. **世界树**：每个 Player 只能贡献一次；颜色、总贡献和个人光列表一致；重复、越界、余火不足与旧 revision 不产生部分写入。
8. **活动 Owner**：Marshmallow、Fishing、Guitar、Stargazing 的 Owner 只打开自己的 UI/Camera，移动锁和退出恢复对称。
9. **活动 Observer**：对方能看到道具、持续动画状态与动作/VFX 事实，但不得打开对方 UI、切换本机 Camera 或锁住本机移动。
10. **共享与目标契约**：SharedGroup 只合并同 Anchor + 同 Activity；Targeted 只接受明确且在线的 TargetId；目标离线会结束对应 Session。
11. **互发表情**：一方发送 `expression.*` 后，Host 验证稳定 PlayerId、当前允许状态和递增 sequence，再广播语义 Cue；双方只播放自己的本地表现，不打开对方 UI、不切换本机 Camera、不锁本机移动。占位模型的 `wave` 应左右摆动，`thanks` 应鞠躬；按 Q 播放后立刻移动并转动探索视角，人物朝向不得持续拉回触发表情前的固定角度，动作恢复完成后 `SnowTravelerVisual` 应回到父级下的 authored local pose；late-join 不得补播任一动作。
12. **互喂棉花糖**：烤好后选择目标并点击“赠给伙伴”。Host 验证 `marshmallow.give(targetId)` 的发起者、目标在线状态、产物归属和余火结算；重复 EventId、旧 revision、无效目标和资源不足不得产生部分写入。发送者收到 `marshmallow.give`，接收者收到 `marshmallow.receive` 社交事实及动画/VFX Cue；接收者不需要正在参加棉花活动。让接收者先进入 Fishing 或 Guitar 再收取一次，原活动 UI/Camera/移动锁必须保持，退出后移动正常；拒绝时发送方棉花糖窗体必须显示 Host 的最终原因。
13. **晚加入与断线**（PC 最小门槛已通过）：活动、Rest、篝火、树和余火源均直接落到当前快照；不重放历史一次性音效/动作；断线后输入、相机、活动锁和目标目录无残留。协议 7 已实测第三个 Client 晚加入可恢复既有 SmallFire 且不补播 Q，断线日志也确认 Peer 与场景服务被正常注销；更完整的活动中断矩阵留到玩法内容稳定后再做。
14. **PC / Android 互通**：相同协议版本和 Network Prefab 列表下，PC Host 与 Android Client 完成上述核心路径；公网测试只更换 endpoint，不修改 Gameplay。

## 5. 当前尚未宣称完成

- 最小直连窗体已有，最终视觉皮肤、二维码/房间码和产品级错误提示尚未完成；
- Lobby、Relay、匹配和断线重连产品流程；
- 正式 Animator、活动美术、VFX、吉他音频及最终活动轮盘；
- 旧 `ColorSource / RestorableNode` 实验视觉的共享状态。
- 实时/异步共用互动事实协议、互喂棉花糖和远端表情的代码链路已完成；仍需按第 3 节在 Unity Play Mode/构建中逐项验收，未验收前不得宣称社交闭环完成。

这些项目不应混入 Host 权威 Gameplay 代码；连接界面走 Bootstrap，表现资源走现有 Activity Presentation / Visuals 边界。
