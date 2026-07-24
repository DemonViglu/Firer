# FlowGraph 技术文档与使用指南

## 概述

FlowGraph 是一个轻量级的 Unity 可视化流程编排插件，类似 UE 的 Flow 系统。它允许设计者通过节点图来编排游戏逻辑流程（对话、过场动画、AI 行为序列等），无需编写代码即可组合已有的功能节点。

---

## 一、架构设计

### 核心分层

```
┌─────────────────────────────────────────────┐
│             Editor Layer                     │
│  FlowEditorWindow / FlowGraphView / ...     │
├─────────────────────────────────────────────┤
│             Runtime Layer                    │
│  FlowGraphRunner / FlowExecutionContext     │
├─────────────────────────────────────────────┤
│             Data Layer                       │
│  FlowGraph (ScriptableObject)               │
│  FlowNode / FlowPort / FlowConnection       │
└─────────────────────────────────────────────┘
```

### 关键设计决策

| 项目 | 方案 | 理由 |
|------|------|------|
| 节点序列化 | `[SerializeReference]` | 支持多态，单文件存储整个图 |
| 数据传输 | Pull-based（懒求值） | 消费节点按需拉取，无冗余计算 |
| 执行模型 | 帧驱动队列 | 支持持续性节点，不阻塞主线程 |
| 场景绑定 | Runner Bindings + Key | 解耦图资产与场景实例 |

---

## 二、数据模型

### FlowGraph（ScriptableObject）

图资产，存储所有节点和连线。通过 `Create > Flow > Flow Graph` 创建。

```
FlowGraph
├── m_Nodes: List<FlowNode>         （[SerializeReference] 多态列表）
├── m_Connections: List<FlowConnection>（连线数据）
└── m_GraphId: string               （唯一标识）
```

### FlowNode（抽象基类）

所有节点的基类。每个节点拥有：

- **nodeId** — 唯一标识（GUID）
- **position** — 编辑器中的位置
- **Ports** — 输入/输出端口列表
- **state** — 运行时状态（Idle / Active / Completed / Failed）

### FlowPort

端口定义：

- **portId** — 唯一标识
- **portName** — 显示名
- **direction** — Input / Output
- **portType** — Execution（执行流）/ Data（数据流）
- **dataType** — 数据端口的类型（仅 Data 类型有效）

### FlowConnection

连线数据，记录：`outputNodeId + outputPortId → inputNodeId + inputPortId`

---

## 三、执行引擎

### FlowGraphRunner（MonoBehaviour）

挂载在场景 GameObject 上，负责驱动图的执行。

**执行循环（每帧 Update）：**

```
1. ProcessPendingExecutions()  — 处理队列中等待执行的节点
2. TickActiveNodes()           — Tick 所有 Active 状态的持续性节点
3. CheckCompletion()           — 检查图是否完成
```

**生命周期：**

```
Run() → 找到 StartNode → ExecuteNode → 节点返回状态
  ├── Completed → OnExit，继续下游
  ├── Active   → 加入 ActiveNodes，每帧 OnTick
  └── Failed   → OnExit，记录失败
```

### FlowExecutionContext

执行上下文，为节点提供运行时 API：

- `GetInputValue<T>(node, portName, defaultValue)` — 拉取上游数据
- `GetBinding<T>(key)` — 获取场景绑定对象
- `GetGameObject(key)` — 获取绑定的 GameObject
- `TriggerPort(node, port)` — 触发下游节点
- `Blackboard` — 图级变量存储

### 数据传输机制（Pull-based）

数据端口不会主动推送值。当下游节点需要数据时：

```
下游节点调用 context.GetInputValue<T>(this, "PortName", defaultValue)
    → Context 查找连线 → 找到上游节点
    → 调用上游节点的 GetOutputValue<T>(portId)
    → 返回值
```

如果数据端口没有连线，返回节点面板上配置的默认值（defaultValue 参数）。

---

## 四、场景绑定系统

### 工作原理

FlowGraph 是 ScriptableObject 资产，不能直接引用场景对象。通过 **Key-Binding** 机制间接引用：

1. 节点中声明 string 类型的 key 字段，标记 `[FlowBindingKey]`
2. FlowGraphRunner Inspector 自动扫描图中所有带此标记的字段
3. Inspector 显示 Object 槽位，用户拖入场景对象
4. 运行时通过 `context.GetBinding<T>(key)` 获取

### FlowGraphRunner Inspector

自动行为：
- 赋值 Graph 后，自动列出所有需要的 binding key
- 未绑定的 key 显示黄色高亮
- 图中不再使用的 key 显示为 "Unused"，可一键清除

### 向后兼容

如果场景中存在旧的 FlowOwner 组件，Runner 会自动 fallback：
`Runner.m_Bindings → 找不到 → FlowOwner.GetBinding(key)`

---

## 五、节点状态与可视化

### 状态枚举

| 状态 | 含义 | 编辑器颜色 |
|------|------|-----------|
| Idle | 未执行 | 无特殊样式 |
| Active | 持续执行中 | 绿色边框 |
| Completed | 已完成 | 半透明（opacity:0.7）|
| Failed | 执行失败 | 红色边框 |

### 节点分类颜色

节点标题栏颜色由 `[FlowNode("Category/Name")]` 的 Category 决定：

| Category | 颜色 |
|----------|------|
| Flow | 深蓝 |
| Events | 蓝色 |
| Logic | 绿色 |
| Data | 深绿 |
| Action | 橙色 |
| Debug | 红色 |
| AI | 紫色 |
| Dialog | 青色 |
| Owner | 棕色 |

---

## 六、编辑器功能

### 打开方式

菜单 `Window > Flow > Flow Graph Editor`

### 操作

| 操作 | 方式 |
|------|------|
| 创建节点 | 右键空白区域 / 空格键 |
| 连线 | 从端口拖拽到另一个端口 |
| 删除 | 选中后 Delete 键 |
| 复制粘贴 | Ctrl+C / Ctrl+V（自动偏移位置） |
| 撤销 | Ctrl+Z |
| 框选 | 鼠标拖拽空白区域 |
| 缩放 | 滚轮 |
| 平移 | Alt+拖拽 / 中键拖拽 |

### Inspector 面板

选中节点后右侧 Inspector 显示节点的 SerializeField 字段，可直接编辑参数。

---

## 七、内置节点一览

### Flow 控制

| 节点 | 功能 |
|------|------|
| Start | 图的入口点（每个图必须有且只有一个） |
| Finish | 标记图完成（Success/Failure） |
| Sequence | 按顺序执行各输出分支，等待每个分支完成后再执行下一个 |
| Parallel | 同时执行所有输出分支 |
| Wait | 等待指定时间后继续 |
| SubGraph | 运行另一个 FlowGraph 作为子图 |

### Logic 逻辑

| 节点 | 功能 |
|------|------|
| Branch | 根据 bool 条件走 True/False 分支 |

### Data 数据

| 节点 | 功能 |
|------|------|
| Bool Value | 输出一个 bool 常量 |
| Float Value | 输出一个 float 常量 |
| String Value | 输出一个 string 常量 |
| Blackboard Get Bool | 从黑板读取 bool |
| Blackboard Set | 向黑板写入 bool |

### Owner 场景交互

| 节点 | 功能 |
|------|------|
| Get GameObject | 通过 key 获取绑定的 GameObject |
| Set Active | 设置绑定对象的 active 状态 |
| Get Component | 获取绑定对象上的 Component |
| Send Message | 对绑定对象调用 SendMessage |
| Invoke Method | 通过反射调用绑定 Component 的方法 |

### Events 事件

| 节点 | 功能 |
|------|------|
| Publish Event | 通过 EventBus 发布自定义事件 |
| Wait For Event | 等待某个事件触发后继续 |

### Debug

| 节点 | 功能 |
|------|------|
| Log | 输出日志到 Console |

---

## 八、自定义节点开发指南

### 基本模板

```csharp
using System;
using Flow.Runtime;
using UnityEngine;

[Serializable]
[FlowNode("Category/NodeName", "节点描述")]
public class MyCustomNode : FlowNode
{
    [SerializeField] private float m_MyParam = 1.0f;

    protected override void DefinePortLayout()
    {
        AddExecutionInput("In");
        AddExecutionOutput("Out");
    }

    public override FlowNodeState OnExecute(FlowExecutionContext context)
    {
        // 执行逻辑
        TriggerOutput(context, "Out");
        return FlowNodeState.Completed;
    }
}
```

### 持续性节点（Active）

返回 `FlowNodeState.Active` 使节点持续工作，每帧接收 `OnTick` 调用：

```csharp
public override FlowNodeState OnExecute(FlowExecutionContext context)
{
    m_Elapsed = 0f;
    return FlowNodeState.Active;  // 进入持续模式
}

public override void OnTick(FlowExecutionContext context, float deltaTime)
{
    m_Elapsed += deltaTime;
    if (m_Elapsed >= m_Duration)
    {
        TriggerOutput(context, "Out");
        state = FlowNodeState.Completed;  // 手动标记完成
    }
}

public override void OnExit(FlowExecutionContext context)
{
    // 清理资源
}
```

### 使用数据端口

**输出数据：**

```csharp
protected override void DefinePortLayout()
{
    AddDataOutput<float>("Value");
}

public override T GetOutputValue<T>(string portId)
{
    if (typeof(T) == typeof(float))
    {
        return (T)(object)m_ComputedValue;
    }
    return default;
}
```

**读取数据：**

```csharp
protected override void DefinePortLayout()
{
    AddDataInput<float>("Speed");
}

public override FlowNodeState OnExecute(FlowExecutionContext context)
{
    // 有连线用连线值，无连线用面板配置的 m_Speed
    float speed = context.GetInputValue<float>(this, "Speed", m_Speed);
}
```

### 访问场景对象

```csharp
[FlowBindingKey]
[SerializeField] private string m_TargetKey = "Player";

public override FlowNodeState OnExecute(FlowExecutionContext context)
{
    // 获取 GameObject
    GameObject go = context.GetGameObject(m_TargetKey);

    // 获取 Component（自动从 GameObject 上获取）
    Rigidbody rb = context.GetBinding<Rigidbody>(m_TargetKey);

    // 直接绑定 Component 类型也支持
    MyComponent comp = context.GetBinding<MyComponent>(m_TargetKey);
}
```

### 多输入判断触发源

```csharp
protected override void DefinePortLayout()
{
    AddExecutionInput("Execute");
    AddExecutionInput("Reset");
    AddExecutionOutput("Out");
}

public override FlowNodeState OnExecute(FlowExecutionContext context)
{
    var port = GetPort(triggeredPortId);
    string portName = port != null ? port.portName : "Execute";

    switch (portName)
    {
        case "Execute":
            // 正常执行
            break;
        case "Reset":
            // 重置逻辑
            break;
    }
    return FlowNodeState.Completed;
}
```

### 多输出分支

```csharp
protected override void DefinePortLayout()
{
    AddExecutionInput("In");
    AddExecutionOutput("High");
    AddExecutionOutput("Medium");
    AddExecutionOutput("Low");
}

public override FlowNodeState OnExecute(FlowExecutionContext context)
{
    float health = context.GetInputValue<float>(this, "Health", 1.0f);

    if (health >= 0.7f)
    {
        TriggerOutput(context, "High");
    }
    else if (health >= 0.3f)
    {
        TriggerOutput(context, "Medium");
    }
    else
    {
        TriggerOutput(context, "Low");
    }
    return FlowNodeState.Completed;
}
```

---

## 九、使用流程

### 1. 创建流程图

1. Project 面板右键 → `Create > Flow > Flow Graph`
2. 双击打开 Flow Graph Editor 窗口
3. 图中默认有一个 Start 节点

### 2. 编辑流程图

1. 右键空白区域添加节点
2. 从输出端口拖线到输入端口建立连接
3. 选中节点在右侧 Inspector 配置参数
4. 需要访问场景对象的节点填写 key 名称

### 3. 运行流程图

1. 场景中创建 GameObject
2. 添加 `FlowGraphRunner` 组件
3. 将 FlowGraph 资产拖入 Graph 字段
4. Inspector 自动显示需要的 Binding 槽位
5. 拖入对应的场景对象
6. 勾选 AutoStart 或代码调用 `runner.Run()`

### 4. 代码启动

```csharp
var runner = GetComponent<FlowGraphRunner>();
runner.Run();

// 监听完成
runner.OnGraphCompleted += (success) =>
{
    Debug.Log($"Graph completed, success={success}");
};
```

### 5. 运行时调试

Play 模式下打开 Flow Graph Editor：
- 自动高亮当前运行的节点（绿色=Active，灰色=Completed，红色=Failed）
- 选择场景中的 FlowGraphRunner 对象会自动跟踪其图

---

## 十、Blackboard（黑板）

图级别的临时变量存储，生命周期与图执行周期一致（Stop/Complete 后清空）。

```csharp
// 写入
context.Blackboard.Set("enemyCount", 5);

// 读取
int count = context.Blackboard.Get<int>("enemyCount", 0);

// 检查
bool has = context.Blackboard.Has("enemyCount");
```

适合在同一个图的不同节点之间传递需要暂存的状态。

---

## 十一、SubGraph（子图）

将复杂逻辑拆分为多个 FlowGraph 资产：

- SubGraph 节点引用另一个 FlowGraph
- 运行时自动创建子 Runner，继承父 Runner 的所有 Bindings
- 子图完成后触发 Success/Failure 输出
- 父图中 Sequence 等节点会正确等待子图完成

---

## 十二、Sequence 节点行为详解

Sequence 节点按顺序执行各分支，**等待当前分支完全完成后**再执行下一个：

1. 触发第一个输出端口
2. 通过 BFS 收集该分支所有可达节点
3. 每帧检查这些节点是否全部完成（非 Active 且不在执行队列中）
4. 全部完成后触发下一个输出端口
5. 所有分支完成后自身完成

这意味着 Sequence 可以正确处理包含 Wait、动画等待等持续性节点的分支。

---

## 十三、端口连线规则

- **执行输入端口**：单连接（一个输入只能被一根线连入，新连线替换旧连线）
- **执行输出端口**：多连接（一个输出可以连到多个下游节点，但会并行触发）
- **数据输入端口**：单连接
- **数据输出端口**：多连接（一个数据源可以被多个下游节点拉取）

---

## 十四、打包为独立插件

`Assets/Game/Scripts/Flow/` 目录可以独立打包，不依赖项目其它代码。

需要处理的事项：
- `CustomEventNode.cs` 和 `WaitForEventNode.cs` 引用了 `IEventPublisher` / `GameInstanceSubsystem`，属于项目特定集成，打包时需移除或提供接口适配
- 其余所有 Runtime 和 Editor 代码无外部依赖

---

## 十五、常见问题

**Q: 数据端口如何处理自定义类型？**

A: `AddDataInput<T>` / `AddDataOutput<T>` 的 T 可以是任何类型。上游节点通过 `GetOutputValue<T>` 返回值，下游通过 `GetInputValue<T>` 拉取。类型转换由节点自行负责。

**Q: 面板值和数据连线值怎么选择？**

A: `context.GetInputValue<T>(this, "PortName", defaultValue)` 内部逻辑：有连线 → 拉取上游值；无连线 → 返回 defaultValue（即面板配置值）。

**Q: 两条路径合并到一个节点会执行两次吗？**

A: 不会。执行输入端口是 Single Capacity，新连线会替换旧连线，因此一个输入只能有一个来源。

**Q: 节点的 OnExit 什么时候被调用？**

A: 三种情况：
1. OnExecute 返回 Completed/Failed 后立即调用
2. Active 节点在 OnTick 中将 state 设为 Completed/Failed 后调用
3. Runner.Stop() 强制停止时对所有 Active 节点调用

**Q: FlowGraphRunner 可以同时运行多个图吗？**

A: 一个 Runner 实例同一时间只运行一个图。如需并行多个流程，使用多个 Runner 或使用 Parallel 节点。
