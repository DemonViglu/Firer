# FirePlay UI 接入

正式场景：`Assets/Scenes/SnowValley_Playable.unity`。

## 现有配置

`Gameplay_UI/FirePlayUiBootstrap` 显式引用场景内 `FirePlayUiRoot` 和 `FirePlayUiCatalog.asset`。Root 引用 Canvas、GraphicRaycaster、HUD/Screen/Modal 三层、透明 ModalInputBlocker，以及唯一 EventSystem/InputSystemUIInputModule。所有组件在编辑器内配置，不在运行时补齐。

Catalog 的 UiId 是稳定语义键，不能通过 Prefab 名称或资源路径推断。当前活动定义的 UiPrefabKey 对应 Catalog 的 UiId；这是目录键，不是 Resources 路径。改 Prefab 名称不需要改 UiId。

| 字段 | 配置方式 |
|---|---|
| Prefab | 根节点挂一个 FirePlayUiView 派生组件，并显式绑定 CanvasGroup |
| Layer | HUD 是持续信息/探索操作，Screen 是活动内容，Modal 是上层交互 |
| Modal / BlocksInput | Modal 需要遮罩时两项开启；BlocksInput 控制底层 View 的输入焦点 |
| StackPolicy | Normal 为持续 UI；Stack 支持返回；HideOther 暂时隐藏已有非 HUD 窗体，关闭后恢复 |

## 添加一个活动 UI

1. 创建自己的 FirePlayUiView 派生组件及 Prefab。按钮、文字、进度图及 CanvasGroup 全部序列化引用，禁止按名称自动发现。
2. 在 OnShow 绑定按钮和状态事件，在 OnHide 解除绑定。持续输入只在 IsFocused 时处理；OnBlur 释放已经提交的持续输入，例如 fishing.lift.stop。
3. 按钮调用当前 PlayerActivityHost 的 RequestAction；活动退出提交 activity.exit，等待活动宿主关闭 UI 并恢复镜头/控制。不要在按钮里直接扣余火或控制 Camera。
4. 重写 OnBackRequested，提交与关闭按钮相同的 activity.exit。服务的 CloseOrReturn 是关闭已经裁决的 View；用户 Back 必须经过活动退出请求。
5. 在 Catalog 添加唯一 UiId、Prefab、Layer 和栈策略；在 ActivityDefinitionAsset 的 UiPrefabKey 填写相同 UiId。Stargazing 沿用共享休息入口，不需要新建专属面板。
6. 单机验证进入、动作、退出；网络验证本机 Owner 打开窗体，Observer 只执行表现事实。覆盖网络窗 HideOther 后恢复、非栈顶关闭、键鼠和触摸。

```csharp
// 示例片段，放入自己的 View 类；玩法动作仍由现有 Activity Logic 实现。
protected override void OnShow() => _closeButton.onClick.AddListener(Exit);
protected override void OnHide() => _closeButton.onClick.RemoveListener(Exit);
protected override void OnBackRequested() => Exit();
private void Exit() => PlayerActivityHost.Local?.RequestAction("activity.exit");
```

## 验证范围

`FirePlayUiServiceContractVerifier.Run()` 是编辑器内存契约检查，覆盖服务生命周期、栈、指定关闭、返回、HideOther 和 Modal 焦点。它不是实际触屏、多端网络或完整活动玩法验收的替代品。

七个正式 View 为 HUD、ActivitySelection、Marshmallow、Fishing、Guitar、Emote 和 NetworkConnection。后者是开发期直连入口，不负责账号、房间匹配或正式后端。
