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

        private UnityAction _closeHandler;
        private IActivityActionRequester _requester;
        private bool _acceptInput;

        private void Awake()
        {
            // Keep the instrument above the persistent Fixed HUD. Otherwise the
            // keys are visible and keyboard input works, but HUD graphics win the
            // pointer raycast before the piano-key Buttons receive OnClick.
            CurrentUIType = new UIType
            {
                UIForms_Type = UIFormsType.PopUp,
                UIForms_ShowMode = UIFormsShowMode.ReverseChange,
                UIForms_LucencyType = UIFormsLucencyType.Lucency
            };

            FirePlayMinimalUiTheme.Apply(gameObject);
            ResolveControls();
        }

        public override void Display()
        {
            base.Display();
            transform.SetAsLastSibling();
            ResolveControls();
            EnsurePointerTargets();
            ResolveRequester();
            BindButtons();
            Refresh();
            _acceptInput = true;
        }

        public override void Hiding()
        {
            _acceptInput = false;
            UnbindButtons();
            base.Hiding();
        }

        public override void Freeze()
        {
            _acceptInput = false;
            base.Freeze();
        }

        public override void Redisplay()
        {
            base.Redisplay();
            transform.SetAsLastSibling();
            EnsurePointerTargets();
            _acceptInput = true;
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy || !_acceptInput)
                return;

            ProcessPointerInput();
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
                    label.text = $"{KeyboardKeys[keyIndex - 1]}\n{GuitarActivityLogic.GetNoteLabel(keyIndex)}";
            }

            var closeLabel = _closeButton != null
                ? _closeButton.GetComponentInChildren<Text>(true)
                : null;
            if (closeLabel != null)
                closeLabel.text = "关闭";
        }

        private void EnsurePointerTargets()
        {
            if (TryGetComponent<Image>(out var rootImage))
                rootImage.raycastTarget = false;

            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.targetGraphic != null)
                    button.targetGraphic.raycastTarget = true;

                foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic != button.targetGraphic)
                        graphic.raycastTarget = false;
                }
            }
        }

        private void ResolveRequester()
        {
            _requester = PlayerActivityHost.Local;
        }

        private void BindButtons()
        {
            UnbindButtons();

            if (_closeButton != null)
            {
                _closeHandler = OnCloseClicked;
                _closeButton.onClick.AddListener(_closeHandler);
            }
        }

        private void UnbindButtons()
        {
            if (_closeButton != null && _closeHandler != null)
                _closeButton.onClick.RemoveListener(_closeHandler);
            _closeHandler = null;
        }

        private void ProcessPointerInput()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame || !TryGetActiveState(out _))
                return;

            var screenPoint = pointer.position.ReadValue();
            for (var index = 0; index < _keyButtons.Length; index++)
            {
                var button = _keyButtons[index];
                if (button == null
                    || !button.interactable
                    || button.transform is not RectTransform keyRect
                    || !RectTransformUtility.RectangleContainsScreenPoint(keyRect, screenPoint, null))
                {
                    continue;
                }

                Submit(GuitarActivityLogic.GetKeyActionId(index + 1));
                return;
            }
        }

        private void Refresh()
        {
            var stateReady = TryGetActiveState(out var playedKeyCount);
            var active = (_requester as PlayerActivityHost)?.ActiveActivityId
                         == GuitarActivityLogic.ActivityId;

            SetStatus(!active
                ? "吉他活动未开始"
                : stateReady
                    ? $"已演奏 {playedKeyCount} 次 · 三组 Do–Re–Mi 自然音阶"
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
