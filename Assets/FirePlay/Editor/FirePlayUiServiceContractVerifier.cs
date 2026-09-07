using System;
using DemonViglu.FirePlay.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.Editor
{
    public sealed class FirePlayUiProbeView : FirePlayUiView
    {
        public int ShowCount { get; private set; }
        public int HideCount { get; private set; }
        public int FocusCount { get; private set; }
        public int BlurCount { get; private set; }
        public int CloseCount { get; private set; }

        protected override void OnShow() => ShowCount++;
        protected override void OnHide() => HideCount++;
        protected override void OnFocus() => FocusCount++;
        protected override void OnBlur() => BlurCount++;
        protected override void OnClose(bool wasVisible) => CloseCount++;
    }

    public static class FirePlayUiServiceContractVerifier
    {
        public static void Run()
        {
            var rootObject = new GameObject("UiContractRoot", typeof(RectTransform));
            var catalog = ScriptableObject.CreateInstance<FirePlayUiCatalog>();
            var inputActions = ScriptableObject.CreateInstance<InputActionAsset>();
            GameObject hudPrefab = null;
            GameObject screenPrefab = null;
            GameObject modalPrefab = null;
            GameObject hideOtherPrefab = null;

            try
            {
                var canvas = rootObject.AddComponent<Canvas>();
                var raycaster = rootObject.AddComponent<GraphicRaycaster>();
                var root = rootObject.AddComponent<FirePlayUiRoot>();
                var hudLayer = CreateLayer(rootObject.transform, "HudLayer");
                var screenLayer = CreateLayer(rootObject.transform, "ScreenLayer");
                var modalLayer = CreateLayer(rootObject.transform, "ModalLayer");
                var blocker = new GameObject("ModalBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                blocker.transform.SetParent(modalLayer, false);
                blocker.GetComponent<Image>().raycastTarget = true;

                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystemObject.transform.SetParent(rootObject.transform, false);
                var eventSystem = eventSystemObject.GetComponent<EventSystem>();
                var inputModule = eventSystemObject.GetComponent<InputSystemUIInputModule>();
                inputModule.actionsAsset = inputActions;

                hudPrefab = CreateViewPrototype("HudProbe");
                screenPrefab = CreateViewPrototype("ScreenProbe");
                modalPrefab = CreateViewPrototype("ModalProbe");
                hideOtherPrefab = CreateViewPrototype("HideOtherProbe");
                ConfigureCatalog(catalog, hudPrefab, screenPrefab, modalPrefab, hideOtherPrefab);
                ConfigureRoot(root, canvas, raycaster, hudLayer, screenLayer, modalLayer, blocker, eventSystem, inputModule);

                Require(root.Initialize(catalog), "Root 应成功初始化");
                Require(root.Show("hud"), "HUD 应成功显示");
                var hud = GetProbe(root, "hud");
                Require(hud.IsVisible && hud.IsFocused, "HUD 初始应可见且聚焦");

                Require(root.Show("screen"), "Screen 应成功入栈");
                var screen = GetProbe(root, "screen");
                Require(hud.IsVisible && !hud.IsFocused, "阻挡输入的 Screen 应 Blur HUD 但不隐藏 HUD");
                Require(screen.IsVisible && screen.IsFocused, "栈顶 Screen 应可见且聚焦");

                Require(root.Show("modal"), "Modal 应成功入栈");
                var modal = GetProbe(root, "modal");
                Require(screen.IsVisible && !screen.IsFocused, "Modal 入栈后 Screen 应保持可见但失焦");
                Require(modal.IsVisible && modal.IsFocused && blocker.activeSelf, "Modal 应聚焦并启用输入遮罩");

                Require(root.CloseOrReturn("screen"), "应能关闭非栈顶指定 View");
                Require(!screen.IsVisible && modal.IsFocused, "关闭非栈顶 View 不应干扰栈顶 Modal");
                Require(root.CloseOrReturn("modal"), "应能关闭栈顶 Modal");
                Require(!blocker.activeSelf && hud.IsFocused, "关闭 Modal 后遮罩应关闭且 HUD 恢复焦点");

                Require(root.Show("screen"), "Screen 应可重新打开");
                Require(root.Show("hide-other"), "HideOther 应成功显示");
                var hideOther = GetProbe(root, "hide-other");
                Require(!screen.IsVisible && hideOther.IsVisible && hideOther.IsFocused,
                    "HideOther 应隐藏现有动态 View 并取得焦点");
                Require(root.CloseOrReturn("hide-other"), "HideOther 应成功关闭");
                Require(screen.IsVisible && screen.IsFocused, "关闭 HideOther 后应恢复先前 Screen");

                Require(root.Back(), "Back 应关闭当前栈顶");
                Require(!screen.IsVisible && hud.IsFocused, "Back 后 HUD 应恢复焦点");

                Debug.Log("[FirePlayUiServiceContractVerifier] PASS: lifecycle/stack/back/HideOther/modal/input focus");
            }
            finally
            {
                Destroy(rootObject);
                Destroy(hudPrefab);
                Destroy(screenPrefab);
                Destroy(modalPrefab);
                Destroy(hideOtherPrefab);
                Destroy(catalog);
                Destroy(inputActions);
            }
        }

        private static Transform CreateLayer(Transform parent, string name)
        {
            var layer = new GameObject(name, typeof(RectTransform));
            layer.transform.SetParent(parent, false);
            return layer.transform;
        }

        private static GameObject CreateViewPrototype(string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(FirePlayUiProbeView));
            var view = gameObject.GetComponent<FirePlayUiProbeView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_canvasGroup").objectReferenceValue = gameObject.GetComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            gameObject.SetActive(false);
            return gameObject;
        }

        private static void ConfigureCatalog(
            FirePlayUiCatalog catalog,
            GameObject hud,
            GameObject screen,
            GameObject modal,
            GameObject hideOther)
        {
            var data = new[]
            {
                ("hud", hud, FirePlayUiLayer.Hud, false, false, FirePlayUiStackPolicy.Normal),
                ("screen", screen, FirePlayUiLayer.Screen, false, true, FirePlayUiStackPolicy.Stack),
                ("modal", modal, FirePlayUiLayer.Modal, true, true, FirePlayUiStackPolicy.Stack),
                ("hide-other", hideOther, FirePlayUiLayer.Modal, true, true, FirePlayUiStackPolicy.HideOther)
            };

            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            entries.arraySize = data.Length;
            for (var index = 0; index < data.Length; index++)
            {
                var target = entries.GetArrayElementAtIndex(index);
                target.FindPropertyRelative("_uiId").stringValue = data[index].Item1;
                target.FindPropertyRelative("_prefab").objectReferenceValue = data[index].Item2;
                target.FindPropertyRelative("_layer").enumValueIndex = (int)data[index].Item3;
                target.FindPropertyRelative("_modal").boolValue = data[index].Item4;
                target.FindPropertyRelative("_blocksInput").boolValue = data[index].Item5;
                target.FindPropertyRelative("_stackPolicy").enumValueIndex = (int)data[index].Item6;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureRoot(
            FirePlayUiRoot root,
            Canvas canvas,
            GraphicRaycaster raycaster,
            Transform hudLayer,
            Transform screenLayer,
            Transform modalLayer,
            GameObject blocker,
            EventSystem eventSystem,
            InputSystemUIInputModule inputModule)
        {
            var serialized = new SerializedObject(root);
            serialized.FindProperty("_canvas").objectReferenceValue = canvas;
            serialized.FindProperty("_raycaster").objectReferenceValue = raycaster;
            serialized.FindProperty("_hudLayer").objectReferenceValue = hudLayer;
            serialized.FindProperty("_screenLayer").objectReferenceValue = screenLayer;
            serialized.FindProperty("_modalLayer").objectReferenceValue = modalLayer;
            serialized.FindProperty("_modalInputBlocker").objectReferenceValue = blocker;
            serialized.FindProperty("_eventSystem").objectReferenceValue = eventSystem;
            serialized.FindProperty("_inputModule").objectReferenceValue = inputModule;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static FirePlayUiProbeView GetProbe(FirePlayUiRoot root, string uiId)
        {
            Require(root.TryGetView(uiId, out var view), $"应能取得 {uiId} View");
            return (FirePlayUiProbeView)view;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("UI Service contract failed: " + message);
        }

        private static void Destroy(UnityEngine.Object target)
        {
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
