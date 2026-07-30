# FirePlay 实时联机接入评估

## 结论

当前工程不需要重写，但必须先完成输入、意图、命令、本地玩家和共享状态边界收口。完成内容扩展能力 Review 后，直接进入中心广场实时联机；异步只保存实时权威数据中需要跨会话保留的部分。

## 可保留结构

| 模块 | 联机角色 |
|---|---|
| `FlameResourceState` / `FlameResourceController` | 本地资源展示基础；共享区域中的奖励与消耗由 Host 校验。 |
| `Campfire` / `CampfireConfig` / `CampfireRecord` | 演进为 Host 权威中心篝火状态。 |
| `RestSpotRitual` | 继续作为地点仪式扩展点，通过统一 Ritual 操作接口接入。 |
| `PlayerRitualAnimationController` | 本地和远端玩家共用的语义动画出口。 |
| Visuals / Audio / VFX 组件 | 客户端读取同步状态做表现，不参与权威判定。 |
| `StableSceneId` | 网络命令与持久化记录的场景对象地址。 |

## 联机前必须完成

1. `LocalPlayerContext`：UI、相机和输入只绑定本地 Player；
2. `InteractionRouter`：原始离散输入只有一个语义裁决点；
3. `CommandExecutor`／`WorldActionService`：共享状态只有一个写入口；
4. `InteractionScanner`：只发现、排序和描述 Stable ID 目标；
5. Ritual 统一操作描述：UI 不判断钓鱼、棉花糖、吉他等具体类型；
6. 权威最小 POCO：中心篝火、共享玩家模式和共享仪式状态；
7. 内容扩展能力 Review：表情轮盘、吉他仪式与动画接入不污染核心 Router、HUD、CommandExecutor 或网络层。

## 首版实时范围

- 中心广场单场景；
- 2–4 人；
- Multiplayer Services SDK + Netcode for GameObjects + Relay；
- Host 权威，Host 断开即结束会话，首版不做 Host Migration；
- 无语音、无自由文本、无排行榜。

### 同步

- 玩家位置、朝向、移动／停留模式；
- 仪式和表情的语义 ID／Cue；
- 火焰颜色、强度档位和手持道具状态；
- 中心篝火热量、等级及必要版本号；
- 加入、离开和有限会话成员信息。

### 不同步

- 本地相机、输入设备和 UI；
- Animator 参数细节；
- 音频混音、粒子随机数和全局 Canvas；
- 中心广场之外尚未声明共享的钓鱼、棉花糖等本地细节。

## 权威命令模型

客户端提交操作意图和 Stable ID；Host 校验身份、模式、距离、成本、冷却与目标版本，再修改状态并广播结果。Client 不能直接上传热量、奖励或任意最终数值。

Move／Look 和 NetworkTransform 不经过 `GameEventBus`；事件总线只承载低频语义意图和领域事件。

## 实时测试准入线

- 单机 PC 与移动端完整流程零回归；
- `LocalPlayerContext` 已验收；
- 共享状态写入已收口；
- 所有同步目标具备稳定 ID；
- 表情轮盘、吉他仪式和动画 Review 通过；
- 编辑器 + Build 或 ParrelSync 可同时运行两个实例；
- 接受 2–4 人上限和 Host 断线结束会话。

## 实时后的持久化

实时链路稳定后，从同一权威状态模型保存中心篝火快照、投火记录、有限表情痕迹和回流计数。不创建独立异步状态机，不额外设计兼容层；持久化不可用时，实时会话和单机基础体验仍可运行。
