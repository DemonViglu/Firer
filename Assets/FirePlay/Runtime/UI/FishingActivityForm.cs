using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Fishing owns its own presentation. The prefab contains the complete
    /// form layout; this component only binds controls and submits semantic
    /// actions to PlayerActivityHost.
    /// </summary>
    public sealed class FishingActivityForm : BaseUIForms
    {
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _primaryButton;
        [SerializeField] private Button _reelButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private GameObject _fightPanel;
        [SerializeField] private Image _catchZone;
        [SerializeField] private RectTransform _fishMarker;
        [SerializeField] private Image _progressFill;

        private Text _primaryLabel;
        private Text _reelLabel;
        private IActivityActionRequester _requester;
        private bool _isFighting;
        private bool _liftInputSent;

        private void Awake()
        {
            FirePlayMinimalUiTheme.Apply(gameObject);
            ResolveControls();
        }

        public override void Display()
        {
            base.Display();
            ResolveControls();
            ResolveRequester();
            BindButtons();
            Refresh();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FishingActivityForm] 已显示，buttons primary={_primaryButton != null}, reel={_reelButton != null}, close={_closeButton != null}", this);
#endif
        }

        public override void Hiding()
        {
            _isFighting = false;
            _liftInputSent = false;
            UnbindButtons();
            base.Hiding();
        }

        private void Update()
        {
            if (gameObject.activeInHierarchy)
            {
                Refresh();
                UpdateLiftInput();
            }
        }

        private void ResolveRequester()
        {
            _requester = PlayerActivityHost.Local;
        }

        private void Refresh()
        {
            ResolveRequester();
            var host = _requester as PlayerActivityHost;
            var logic = host?.ActiveSession?.Logic as FishingActivityLogic;
            if (logic != null)
            {
                ApplyState(new FishingActivityStateSnapshot(
                    logic.HasRod,
                    logic.IsLineCast,
                    logic.IsFishBiting,
                    logic.IsFighting,
                    logic.IsCatchReady,
                    logic.Catches,
                    logic.CatchesPerRod,
                    logic.FishPosition01,
                    logic.CatchBarPosition01,
                    logic.CatchZoneSize01,
                    logic.CatchProgress01,
                    logic.IsLiftHeld,
                    logic.Status));
                return;
            }

            if (host != null
                && host.TryGetActiveStatePayload(FishingActivityLogic.ActivityId, out var payload)
                && FishingActivityStateSnapshot.TryParse(payload, out var snapshot))
            {
                ApplyState(snapshot);
                return;
            }

            SetStatus("等待主机同步钓鱼状态");
            SetInteractable(_primaryButton, false);
            SetInteractable(_reelButton, false);
            SetVisible(_reelButton != null ? _reelButton.gameObject : null, false);
            _isFighting = false;
            _liftInputSent = false;
            SetFightPanel(false);
        }

        private void ApplyState(FishingActivityStateSnapshot state)
        {
            SetStatus(state.HasRod
                ? $"{state.Status}\n本轮收获 {state.Catches}/{state.CatchesPerRod}"
                : state.Status);
            if (_primaryLabel != null)
                _primaryLabel.text = !state.HasRod
                    ? "拟造鱼竿"
                    : state.IsCatchReady
                        ? "收获"
                    : state.IsFishBiting
                        ? "提竿"
                        : "抛竿";
            if (_reelLabel != null)
                _reelLabel.text = "按住上浮（Space）";
            SetInteractable(_primaryButton, !state.IsLineCast || state.IsFishBiting || state.IsCatchReady);
            SetInteractable(_reelButton, state.IsFighting);
            SetVisible(_reelButton != null ? _reelButton.gameObject : null, state.IsFighting);
            SetFightPanel(state.IsFighting || state.IsCatchReady);
            _isFighting = state.IsFighting;
            ApplyCatchTrack(state);
            if (_progressFill != null)
                _progressFill.fillAmount = state.CatchProgressPercent / 100f;
        }

        private void ResolveControls()
        {
            _statusText ??= FindText("Status");
            _primaryButton ??= FindButton("PrimaryButton");
            _reelButton ??= FindButton("ReelButton");
            _closeButton ??= FindButton("CloseButton");
            _fightPanel ??= FindChild("FightPanel");
            _catchZone ??= FindImage("CatchZone");
            _fishMarker ??= FindRectTransform("FishMarker");
            _progressFill ??= FindImage("ProgressFill");
            _primaryLabel ??= FindButtonLabel(_primaryButton);
            _reelLabel ??= FindButtonLabel(_reelButton);
            var closeLabel = FindButtonLabel(_closeButton);
            if (closeLabel != null) closeLabel.text = "关闭";
        }

        private void BindButtons()
        {
            UnbindButtons();
            _primaryButton?.onClick.AddListener(OnPrimaryClicked);
            _closeButton?.onClick.AddListener(OnCloseClicked);
        }

        private void UnbindButtons()
        {
            _primaryButton?.onClick.RemoveListener(OnPrimaryClicked);
            _closeButton?.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnPrimaryClicked() => Submit("fishing.primary");
        private void OnCloseClicked() => Submit("activity.exit");

        private void Submit(string actionId)
        {
            ResolveRequester();
            if (_requester == null)
            {
                SetStatus("活动宿主未就绪");
                return;
            }

            var result = _requester.RequestAction(actionId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FishingActivityForm] 提交动作：{actionId}，consumed={result.Consumed}，reason={result.Reason}", this);
#endif
            SetStatus(result.Consumed ? result.Reason : $"未执行：{result.Reason}");
        }

        private void UpdateLiftInput()
        {
            if (!_isFighting)
            {
                _liftInputSent = false;
                return;
            }

            var held = IsLiftControlHeld();
            if (held == _liftInputSent)
                return;

            ResolveRequester();
            if (_requester == null)
                return;

            var result = _requester.RequestAction(
                held ? "fishing.lift.start" : "fishing.lift.stop");
            if (result.Consumed)
                _liftInputSent = held;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!result.Consumed)
            {
                Debug.LogWarning(
                    $"[FishingActivityForm] 升降输入未消费：held={held}, reason={result.Reason}",
                    this);
            }
#endif
        }

        private bool IsLiftControlHeld()
        {
            if (_reelButton == null || !_reelButton.gameObject.activeInHierarchy)
                return false;

            if (Keyboard.current?.spaceKey.isPressed == true)
                return true;

            var rect = _reelButton.transform as RectTransform;
            if (rect == null)
                return false;

            var canvas = _reelButton.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            if (Mouse.current?.leftButton.isPressed == true
                && RectTransformUtility.RectangleContainsScreenPoint(
                    rect,
                    Mouse.current.position.ReadValue(),
                    eventCamera))
            {
                return true;
            }

            var touchscreen = Touchscreen.current;
            return touchscreen != null
                   && touchscreen.primaryTouch.press.isPressed
                   && RectTransformUtility.RectangleContainsScreenPoint(
                       rect,
                       touchscreen.primaryTouch.position.ReadValue(),
                       eventCamera);
        }

        private void ApplyCatchTrack(FishingActivityStateSnapshot state)
        {
            var catchBarPosition = Mathf.Clamp01(state.CatchBarPositionPercent / 100f);
            var zoneSize = Mathf.Clamp(state.CatchZoneSizePercent / 100f, 0.1f, 0.75f);
            if (_catchZone != null)
            {
                var zoneRect = _catchZone.rectTransform;
                var minimum = Mathf.Clamp01(catchBarPosition - zoneSize * 0.5f);
                var maximum = Mathf.Clamp01(catchBarPosition + zoneSize * 0.5f);
                zoneRect.anchorMin = new Vector2(0.08f, minimum);
                zoneRect.anchorMax = new Vector2(0.92f, maximum);
                zoneRect.anchoredPosition = Vector2.zero;
                zoneRect.sizeDelta = Vector2.zero;
                _catchZone.color = state.IsFishInsideCatchBar
                    ? new Color(0.28f, 0.84f, 0.55f, 0.88f)
                    : new Color(0.17f, 0.58f, 0.52f, 0.72f);
            }

            if (_fishMarker != null)
            {
                var fishPosition = Mathf.Clamp01(state.FishPositionPercent / 100f);
                _fishMarker.anchorMin = new Vector2(0.5f, fishPosition);
                _fishMarker.anchorMax = new Vector2(0.5f, fishPosition);
                _fishMarker.anchoredPosition = Vector2.zero;
            }
        }

        private void SetStatus(string value)
        {
            if (_statusText != null)
                _statusText.text = value ?? string.Empty;
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
        }

        private static void SetVisible(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
                target.SetActive(value);
        }

        private void SetFightPanel(bool visible)
        {
            if (_fightPanel != null && _fightPanel.activeSelf != visible)
                _fightPanel.SetActive(visible);
        }

        private Text FindText(string childName)
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.gameObject.name == childName) return text;
            return null;
        }

        private Button FindButton(string childName)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
                if (button.gameObject.name == childName) return button;
            return null;
        }

        private GameObject FindChild(string childName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child.gameObject.name == childName) return child.gameObject;
            return null;
        }

        private Image FindImage(string childName)
        {
            foreach (var image in GetComponentsInChildren<Image>(true))
                if (image.gameObject.name == childName) return image;
            return null;
        }

        private RectTransform FindRectTransform(string childName)
        {
            foreach (var child in GetComponentsInChildren<RectTransform>(true))
                if (child.gameObject.name == childName) return child;
            return null;
        }

        private static Text FindButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<Text>(true) : null;
        }

        private void OnDisable()
        {
            _isFighting = false;
            _liftInputSent = false;
            UnbindButtons();
        }
    }
}
