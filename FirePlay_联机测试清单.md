# FirePlay 实时联机启动与测试清单

> 本文只描述当前 NGO + UTP 直连测试入口和最终验收顺序。开发完成情况以 `FirePlay_开发状态.md` 为准。

## 1. 唯一启动入口

DemoScene 只保留一个 `NetworkBootstrap`：

- `NetworkManager`：Player Prefab 与 Network Prefab 注册；
- `UnityTransport`：传输实现；
- `FirePlayNetworkBootstrap`：唯一 Host / Server / Client 启停入口；
- `ConnectionApproval`：必须显式开启；Bootstrap 独占准入回调；
- 当前 `DemoScene` 开发配置：`Client + 127.0.0.1:7777` 自动尝试连接，连接窗体默认隐藏；
- Gameplay、Activity 和 World 脚本不得直接读取 `NetworkManager` 来启动会话。

PC 构建无需修改场景即可通过启动参数覆盖：

```text
FirePlay.exe --fireplay-mode host --fireplay-port 7777
FirePlay.exe --fireplay-mode client --fireplay-address 127.0.0.1 --fireplay-port 7777
FirePlay.exe --fireplay-mode client --fireplay-address 公网或内网穿透地址 --fireplay-port 映射端口
FirePlay.exe --fireplay-mode manual
```

同时支持 `--key=value` 写法。合法 mode 只有 `manual / host / server / client`；端口必须为 `1..65535`，无效参数会明确拒绝启动。

Android 不依赖命令行。`NetworkConnectionForms` 只调用：

```text
FirePlayNetworkBootstrap.ConfigureEndpoint(address, port)
FirePlayNetworkBootstrap.StartClient()
```

连接界面不持有 `NetworkManager` 或 `UnityTransport`。它目前只是开发期 Client 直连入口，支持填写地址/端口、连接和断开，不负责启动 Host，也不是正式房间 UI。制作 Android Client 测试包时，将唯一 Bootstrap 的 `Auto Start` 设为 `Manual`，并在场景 `FirePlayUiBootstrap` 开启 `Show Network Connection On Start`；玩家在窗体内填写地址与端口后连接。PC Host 使用命令行或在专用测试场景中明确设为 Host。

## 2. 连接准入边界

- PC 与 Android 都发送同一个 `fireplay.realtime` 协议版本；当前版本为 `1`；
- 握手只判断实时联机兼容性，不包含账号、身份或玩法状态；
- DemoScene 的 `Maximum Players` 默认为 `4`，Host 本机也占一个玩家名额；
- 协议不匹配、缺少握手或房间已满时，Host 不生成 Player，并把明确原因返回连接窗体；
- 修改活动、UI 或美术不需要递增协议版本；只有破坏网络 DTO / Prefab / 状态语义兼容性时才递增。

## 3. 最终验收顺序

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
11. **晚加入与断线**：活动、Rest、篝火、树和余火源均直接落到当前快照；不重放历史一次性音效/动作；断线后输入、相机、活动锁和目标目录无残留。
12. **PC / Android 互通**：相同协议版本和 Network Prefab 列表下，PC Host 与 Android Client 完成上述核心路径；公网测试只更换 endpoint，不修改 Gameplay。

## 4. 当前尚未宣称完成

- 最小直连窗体已有，最终视觉皮肤、二维码/房间码和产品级错误提示尚未完成；
- Lobby、Relay、匹配和断线重连产品流程；
- 正式 Animator、活动美术、VFX、吉他音频及最终活动轮盘；
- 旧 `ColorSource / RestorableNode` 实验视觉的共享状态。

这些项目不应混入 Host 权威 Gameplay 代码；连接界面走 Bootstrap，表现资源走现有 Activity Presentation / Visuals 边界。
