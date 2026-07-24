# FirePlay 技术基线与约定

本文档是首个可玩纵向切片的技术护栏。它只约定会影响后续扩展和协作的事项；具体玩法实现见《FirePlay_纵向切片技术设计》。

## 1. 已确认的工程基线

| 项目 | 约定 |
|---|---|
| Unity 编辑器 | Unity `6000.5.5f1` |
| 渲染管线 | URP `17.5.0` |
| 输入 | Input System `1.19.0` |
| 目标平台（原型） | Windows PC，键鼠优先 |
| 根命名空间 | `DemonViglu` |
| 版本控制 | 已配置 Unity `.gitignore`；保留所有 `.meta` 文件 |

首个原型不承诺移动端性能，也不加入联网、Lobby 或 Relay。网络化应在单人体验与异步足迹闭环被验证之后再设计。

## 2. 命名空间与命名

新建的 FirePlay 代码统一使用以下命名空间：

```csharp
namespace DemonViglu.FirePlay.Flame
{
    public sealed class FlameBrush : MonoBehaviour
    {
    }
}
```

推荐子命名空间：

```text
DemonViglu.FirePlay.Core
DemonViglu.FirePlay.Player
DemonViglu.FirePlay.Flame
DemonViglu.FirePlay.World
DemonViglu.FirePlay.Rendering
DemonViglu.FirePlay.Data
DemonViglu.FirePlay.Events
DemonViglu.FirePlay.Editor
```

大众 C# 命名规则：

- 类、方法、属性、公开字段：`PascalCase`；
- 私有字段：`_camelCase`；
- 接口：`I` 前缀，例如 `IFlameBrushService`；
- ScriptableObject 类型以 `Config` 或 `Definition` 结尾；
- MonoBehaviour 脚本名与主类名一致；
- `SerializeField` 私有字段不暴露无必要的 setter。

当前已有框架代码尚未使用命名空间。先不做全局迁移；新 FirePlay 模块从现在开始采用命名空间即可，避免无关的大范围改动。

## 3. 程序集定义（asmdef）

当前工程没有 `.asmdef`，而已有 Core、Events 与 Flow 代码都编译在 Unity 默认的 `Assembly-CSharp` 中。程序集定义不能反向引用默认程序集，因此**首个切片暂不创建 FirePlay 的 asmdef**；新脚本仍编译在 `Assembly-CSharp`，但从第一天开始使用 `DemonViglu.FirePlay.*` 命名空间。

等第一条完整玩法链路稳定后，再统一迁移为：

```text
Assets/Foundation/Runtime/DemonViglu.Foundation.Runtime.asmdef
Assets/FirePlay/Runtime/DemonViglu.FirePlay.Runtime.asmdef
Assets/FirePlay/Editor/DemonViglu.FirePlay.Editor.asmdef
```

迁移规则：

- 先把现有 Core、Events 与 Flow 的可运行时代码移入 `Foundation.Runtime`；
- `FirePlay.Runtime` 只引用 `Foundation.Runtime`，不引用 Editor；
- `Editor` 可以引用 Runtime，反向禁止；
- 仅在实际使用对应 API 时才显式引用 Input System 或 URP 程序集。

这样避免在原型阶段引入程序集依赖问题，也为后期编译优化保留清晰的迁移路径。

## 4. 现有框架的接入边界

工程已有 `GameInstanceSubsystem`、`GameEventBus` 与 FlowGraph。第一阶段按以下方式使用：

### 使用 GameInstanceSubsystem

仅注册跨场景、无 Unity GameObject 所有权的服务，例如：

- `IFlameStateService`：火苗颜色、强度等运行时状态；
- 将来的 `ISaveService`：存档读写。

### 使用 GameEventBus

只发布“已经发生的游戏事实”，不把它当作普通函数调用的替代品：

```text
FlameColorChangedEvent
RestorableNodeCompletedEvent
```

局部对象间的直接协作（如 `RestorableNode` 调用自身 `WorldBloom`）保持直接引用；不要为了“解耦”把每一个动画播放都改成事件。

### 暂不使用 FlowGraph

第一条“花 → 古树”的原型不接入 FlowGraph。等出现教程、任务步骤或可复用剧情流程时，再用 FlowGraph 编排高层流程。

## 5. 数据所有权

| 数据 | 所有者 | 保存时机 |
|---|---|---|
| 当前火苗颜色与亮度 | `FlameColorState` / 状态服务 | 本原型仅内存 |
| 色彩源的展示状态 | `ColorSource` | 本原型仅场景内 |
| 节点复苏状态 | `RestorableNode` | 后续存档版本 |
| 火种路标 | `BeaconRecord` 数据模型 | 后续存档版本 |

所有需要持久化或共享的对象，首次制作时就分配稳定 ID：

- `nodeId`：如 `forest.tree.ancient_01`；
- `beaconId`：如 `forest.crossroad.north_01`；
- ID 存在于 Inspector 的序列化字段或配置资产中；
- 禁止使用 GameObject 名称、层级路径或 Unity Instance ID 作为存档键。

## 6. 事件约定

事件使用过去式命名，表示状态已改变：

```csharp
public readonly struct FlameColorChangedEvent : IGameEvent
{
    public Color PreviousColor { get; }
    public Color CurrentColor { get; }
}
```

规则：

- 事件载荷是不可变数据；
- 发布方不假定任何订阅者存在；
- 订阅必须在 `OnEnable`，取消订阅必须在 `OnDisable`；
- 不在 `Update` 中高频发布日志型事件；
- 视觉过渡由状态驱动，事件只用于启动、记录或跨系统响应。

## 7. TA 参数层级与 Shader 规范

FirePlay 的视觉参数按这一条链路流动：

```text
ScriptableObject 配置
      ↓
运行时玩法状态
      ↓
MaterialPropertyBlock
      ↓
Shader Graph / Particle System
```

禁止为了改变单个对象外观而实例化或直接写入共享材质。`WorldBloom` 等组件使用 `MaterialPropertyBlock`。

统一 Shader 属性名：

| 属性 | 类型 | 用途 |
|---|---|---|
| `_LitAmount` | Float，0–1 | 水墨到淡彩的复苏进度 |
| `_InkColor` | Color | 未复苏时的墨色 |
| `_BaseColor` | Color | 完整复苏后的基础色 |
| `_BloomColor` | Color | 过渡边缘的光色 |
| `_FlameColor` | Color | 火苗主色 |
| `_FlameIntensity` | Float | 火苗亮度 |
| `_NoiseTex` | Texture2D | 水墨/笔触噪声 |
| `_NoiseScale` | Float | 噪声尺度 |

每一种效果都先有一个可调的“干净版本”，再添加噪声、粒子和后处理；这样学习与排错都会更清楚。

## 8. 原型性能约束

首个切片采用保守规则：

- 每个可复苏节点最多一个 `WorldBloom` 与一个粒子系统；
- 不在 `Update` 中做全场景 `Find`、`GetComponent` 或材质实例化；
- 触发判定使用 LayerMask，火苗交互半径保持在 1–1.5 米；
- 尽量复用材质、粒子与音效预制体；
- 开发期间使用 Unity Profiler 确认没有明显 GC Alloc 尖峰；
- 第一版不用自定义 Renderer Feature，先用 Shader Graph 完成 80% 的视觉目标。

## 9. 输入与相机

创建 `FirePlay.inputactions`，第一版 Action Map 为 `Player`：

| Action | 类型 | 键鼠默认绑定 |
|---|---|---|
| Move | Vector2 | WASD |
| Look | Vector2 | Mouse Delta |
| Interact | Button | E |
| Emote | Button | Q |
| Pause | Button | Escape |

互动由 `PlayerInteraction` 负责；移动由 `PlayerMovement` 负责；两者不互相读取具体世界对象的状态。

## 10. 开发调试开关

在原型场景提供一个仅开发用的调试面板或 Gizmo 显示：

- 当前火苗颜色和亮度；
- 当前可互动目标与距离；
- `nodeId`；
- `_LitAmount`；
- 已发布的关键事件。

调试信息必须可关闭，且不得依赖正式 UI。

## 11. 首个实现顺序

1. 创建 `Assets/FirePlay` 和 `FirePlay.inputactions`；程序集定义留待 Foundation 迁移时统一创建；
2. 建立 `Prototype_Forest` 与玩家移动；
3. 做火苗状态、跟随和视觉；
4. 接入色彩源；
5. 接入可复苏古树和 `_LitAmount` Shader；
6. 仅在火苗颜色变化、古树完成复苏时发布事件；
7. 加入调试面板，反复调节参数；
8. 试玩验证后再扩展篝火和本地足迹。
