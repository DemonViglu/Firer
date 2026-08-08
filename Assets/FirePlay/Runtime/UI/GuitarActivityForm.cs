using System;
using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Dedicated 21-key guitar form. The prefab owns the layout; this form
    /// only binds static buttons and submits semantic guitar actions.
    /// </summary>
    public sealed class GuitarActivityForm : BaseUIForms
    {
        private static readonly Key[] KeyboardKeys =
        {
            Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U,
            Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J,
            Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M
        };

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
            if (!gameObject.activeInHierarchy)
                return;

            ProcessKeyboardInput();
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
                    label.text = $"{KeyboardKeys[keyIndex - 1]}\n{keyIndex:00}";
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
            var stateReady = TryGetActiveState(out var playedKeyCount);
            var active = (_requester as PlayerActivityHost)?.ActiveActivityId
                         == GuitarActivityLogic.ActivityId;

            SetStatus(!active
                ? "吉他活动未开始"
                : stateReady
                    ? $"已演奏 {playedKeyCount} 次 · 21 个音位"
                    : "等待主机同步");

            if (_keyButtons == null) return;
            foreach (var button in _keyButtons)
                if (button != null) button.interactable = active && stateReady;
        }

        private void ProcessKeyboardInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !TryGetActiveState(out _))
                return;

            for (var i = 0; i < KeyboardKeys.Length; i++)
            {
                if (keyboard[KeyboardKeys[i]].wasPressedThisFrame)
                    Submit(GuitarActivityLogic.GetKeyActionId(i + 1));
            }
        }

        private bool TryGetActiveState(out int playedKeyCount)
        {
            playedKeyCount = 0;
            ResolveRequester();
            var host = _requester as PlayerActivityHost;
            if (host?.ActiveActivityId != GuitarActivityLogic.ActivityId)
                return false;

            if (host.ActiveSession?.Logic is GuitarActivityLogic logic)
            {
                playedKeyCount = logic.PlayedKeyCount;
                return true;
            }

            if (!host.TryGetActiveStatePayload(
                    GuitarActivityLogic.ActivityId,
                    out var payload)
                || !GuitarActivityStateSnapshot.TryParse(payload, out var state))
                return false;

            playedKeyCount = state.PlayedKeyCount;
            return true;
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
