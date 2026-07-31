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
        [SerializeField] private Button _closeButton;

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
            if (_requester == null)
                _requester = FindAnyObjectByType<PlayerActivityHost>();
        }

        private void Refresh()
        {
            ResolveRequester();
            var host = _requester as PlayerActivityHost;
            var logic = host?.ActiveSession?.Logic as FishingActivityLogic;
            if (logic == null)
            {
                SetStatus("钓鱼活动未开始");
                SetInteractable(_primaryButton, false);
                SetInteractable(_reelButton, false);
                return;
            }

            SetStatus(logic.Status);
            if (_primaryLabel != null)
                _primaryLabel.text = logic.HasRod ? "抛竿" : "拟造鱼竿";
            if (_reelLabel != null)
                _reelLabel.text = "收线";
            SetInteractable(_primaryButton, !logic.IsLineCast);
            SetInteractable(_reelButton, logic.IsFishBiting);
        }

        private void ResolveControls()
        {
            _statusText ??= FindText("Status");
            _primaryButton ??= FindButton("PrimaryButton");
            _reelButton ??= FindButton("ReelButton");
            _closeButton ??= FindButton("CloseButton");
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
            _closeButton?.onClick.AddListener(OnCloseClicked);
        }

        private void UnbindButtons()
        {
            _primaryButton?.onClick.RemoveListener(OnPrimaryClicked);
            _reelButton?.onClick.RemoveListener(OnReelClicked);
            _closeButton?.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnPrimaryClicked() => Submit("fishing.primary");
        private void OnReelClicked() => Submit("fishing.reel");
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
