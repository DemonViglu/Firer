using System;
using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Dedicated 21-key guitar form. The prefab owns the layout; this form
    /// only binds static buttons and submits semantic guitar actions.
    /// </summary>
    public sealed class GuitarActivityForm : BaseUIForms
    {
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button[] _keyButtons;

        private UnityAction[] _keyHandlers;
        private UnityAction _closeHandler;
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

        private void ResolveControls()
        {
            _statusText ??= FindText("Status");
            _closeButton ??= FindButton("CloseButton");

            if (_keyButtons == null || _keyButtons.Length != GuitarActivityLogic.KeyCount)
                _keyButtons = new Button[GuitarActivityLogic.KeyCount];

            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == "CloseButton")
                {
                    _closeButton ??= button;
                    continue;
                }

                if (!button.gameObject.name.StartsWith("KeyButton", StringComparison.Ordinal)
                    || !int.TryParse(button.gameObject.name.Substring("KeyButton".Length), out var keyIndex)
                    || keyIndex < 1
                    || keyIndex > GuitarActivityLogic.KeyCount)
                    continue;

                _keyButtons[keyIndex - 1] = button;
                var label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = keyIndex.ToString("00");
            }

            var closeLabel = _closeButton != null
                ? _closeButton.GetComponentInChildren<Text>(true)
                : null;
            if (closeLabel != null)
                closeLabel.text = "关闭";
        }

        private void ResolveRequester()
        {
            _requester = PlayerActivityHost.Local;
            if (_requester == null)
                _requester = FindAnyObjectByType<PlayerActivityHost>();
        }

        private void BindButtons()
        {
            UnbindButtons();
            _keyHandlers = new UnityAction[GuitarActivityLogic.KeyCount];

            for (var i = 0; i < _keyButtons.Length; i++)
            {
                var button = _keyButtons[i];
                if (button == null) continue;

                var keyIndex = i + 1;
                UnityAction handler = () => Submit(GuitarActivityLogic.GetKeyActionId(keyIndex));
                _keyHandlers[i] = handler;
                button.onClick.AddListener(handler);
            }

            if (_closeButton != null)
            {
                _closeHandler = OnCloseClicked;
                _closeButton.onClick.AddListener(_closeHandler);
            }
        }

        private void UnbindButtons()
        {
            if (_keyButtons != null && _keyHandlers != null)
            {
                for (var i = 0; i < _keyButtons.Length && i < _keyHandlers.Length; i++)
                {
                    if (_keyButtons[i] != null && _keyHandlers[i] != null)
                        _keyButtons[i].onClick.RemoveListener(_keyHandlers[i]);
                }
            }

            if (_closeButton != null && _closeHandler != null)
                _closeButton.onClick.RemoveListener(_closeHandler);
            _closeHandler = null;
        }

        private void Refresh()
        {
            ResolveRequester();
            var host = _requester as PlayerActivityHost;
            var logic = host?.ActiveSession?.Logic as GuitarActivityLogic;
            var active = logic != null;

            SetStatus(active
                ? $"已演奏 {logic.PlayedKeyCount}/{GuitarActivityLogic.KeyCount}"
                : "吉他活动未开始");

            if (_keyButtons == null) return;
            foreach (var button in _keyButtons)
                if (button != null) button.interactable = active;
        }

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
            Debug.Log($"[GuitarActivityForm] 提交动作：{actionId}，consumed={result.Consumed}，reason={result.Reason}", this);
#endif
            SetStatus(result.Consumed ? result.Reason : $"未执行：{result.Reason}");
        }

        private void OnCloseClicked() => Submit("activity.exit");

        private void SetStatus(string value)
        {
            if (_statusText != null)
                _statusText.text = value ?? string.Empty;
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

        private void OnDisable()
        {
            UnbindButtons();
        }
    }
}
