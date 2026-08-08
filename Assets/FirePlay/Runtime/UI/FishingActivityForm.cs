using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
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
        [SerializeField] private Button _easeButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private GameObject _fightPanel;
        [SerializeField] private Image _tensionFill;
        [SerializeField] private Image _progressFill;

        private Text _primaryLabel;
        private Text _reelLabel;
        private IActivityActionRequester _requester;

        private void Awake()
        {
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
            UnbindButtons();
            base.Hiding();
        }

        private void Update()
        {
            if (gameObject.activeInHierarchy)
                Refresh();
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
                    logic.Catches,
                    logic.CatchesPerRod,
                    logic.Tension01,
                    logic.CatchProgress01,
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
            SetInteractable(_easeButton, false);
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
                    : state.IsFishBiting
                        ? "提竿"
                        : "抛竿";
            if (_reelLabel != null)
                _reelLabel.text = "收线";
            SetInteractable(_primaryButton, !state.IsLineCast || state.IsFishBiting);
            SetInteractable(_reelButton, state.IsFighting);
            SetInteractable(_easeButton, state.IsFighting);
            SetFightPanel(state.IsFighting);
            if (_tensionFill != null)
                _tensionFill.fillAmount = state.TensionPercent / 100f;
            if (_progressFill != null)
                _progressFill.fillAmount = state.CatchProgressPercent / 100f;
        }

        private void ResolveControls()
        {
            _statusText ??= FindText("Status");
            _primaryButton ??= FindButton("PrimaryButton");
            _reelButton ??= FindButton("ReelButton");
            _easeButton ??= FindButton("EaseButton");
            _closeButton ??= FindButton("CloseButton");
            _fightPanel ??= FindChild("FightPanel");
            _tensionFill ??= FindImage("TensionFill");
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
            _reelButton?.onClick.AddListener(OnReelClicked);
            _easeButton?.onClick.AddListener(OnEaseClicked);
            _closeButton?.onClick.AddListener(OnCloseClicked);
        }

        private void UnbindButtons()
        {
            _primaryButton?.onClick.RemoveListener(OnPrimaryClicked);
            _reelButton?.onClick.RemoveListener(OnReelClicked);
            _easeButton?.onClick.RemoveListener(OnEaseClicked);
            _closeButton?.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnPrimaryClicked() => Submit("fishing.primary");
        private void OnReelClicked() => Submit("fishing.reel");
        private void OnEaseClicked() => Submit("fishing.ease");
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

        private void SetStatus(string value)
        {
            if (_statusText != null)
                _statusText.text = value ?? string.Empty;
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null) button.interactable = value;
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

        private static Text FindButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<Text>(true) : null;
        }

        private void OnDisable()
        {
            UnbindButtons();
        }
    }
}
