using DemonViglu.FirePlay.Activity;
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
    public sealed class GuitarActivityForm : FirePlayUiView
    {
        protected override void OnBackRequested() => OnCloseClicked();
        private static readonly Key[] KeyboardKeys =
        {
            Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U,
            Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J,
            Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M
        };

        [SerializeField] private Text _statusText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _closeButtonLabel;
        [SerializeField] private Button[] _keyButtons;
        [SerializeField] private Text[] _keyLabels;

        private UnityAction _closeHandler;
        private UnityAction[] _keyHandlers;
        private IActivityActionRequester _requester;
        private bool _acceptInput;

        private void Awake()
        {
            FirePlayMinimalUiTheme.Apply(gameObject);
        }

        protected override void OnShow()
        {
            transform.SetAsLastSibling();
            EnsurePointerTargets();
            ResolveRequester();
            BindButtons();
            Refresh();
            _acceptInput = true;
        }

        protected override void OnHide()
        {
            _acceptInput = false;
            UnbindButtons();
        }

        protected override void OnBlur()
        {
            _acceptInput = false;
        }

        protected override void OnFocus()
        {
            transform.SetAsLastSibling();
            EnsurePointerTargets();
            _acceptInput = true;
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy || !_acceptInput)
                return;

            ProcessKeyboardInput();
            Refresh();
        }

        private void OnValidate()
        {
            if (_keyLabels != null)
            {
                for (var index = 0; index < _keyLabels.Length && index < GuitarActivityLogic.KeyCount; index++)
                {
                    if (_keyLabels[index] != null)
                        _keyLabels[index].text = $"{KeyboardKeys[index]}\n{GuitarActivityLogic.GetNoteLabel(index + 1)}";
                }
            }

            if (_closeButtonLabel != null)
                _closeButtonLabel.text = "关闭";
        }

        private void EnsurePointerTargets()
        {
            if (TryGetComponent<Image>(out var rootImage))
                rootImage.raycastTarget = false;

            ConfigurePointerTarget(_closeButton);
            if (_keyButtons == null) return;
            foreach (var button in _keyButtons)
            {
                ConfigurePointerTarget(button);
            }
        }

        private static void ConfigurePointerTarget(Button button)
        {
            if (button == null) return;
            if (button.targetGraphic != null)
                button.targetGraphic.raycastTarget = true;

            foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                if (graphic != button.targetGraphic)
                    graphic.raycastTarget = false;
        }

        private void ResolveRequester()
        {
            _requester = PlayerActivityHost.Local;
        }

        private void BindButtons()
        {
            UnbindButtons();

            if (_keyButtons != null)
            {
                _keyHandlers = new UnityAction[_keyButtons.Length];
                for (var index = 0; index < _keyButtons.Length; index++)
                {
                    var key = index + 1;
                    _keyHandlers[index] = () =>
                    {
                        if (IsFocused && TryGetActiveState(out _))
                            Submit(GuitarActivityLogic.GetKeyActionId(key));
                    };
                    _keyButtons[index]?.onClick.AddListener(_keyHandlers[index]);
                }
            }

            if (_closeButton != null)
            {
                _closeHandler = OnCloseClicked;
                _closeButton.onClick.AddListener(_closeHandler);
            }
        }

        private void UnbindButtons()
        {
            if (_keyHandlers != null && _keyButtons != null)
                for (var index = 0; index < _keyButtons.Length && index < _keyHandlers.Length; index++)
                    _keyButtons[index]?.onClick.RemoveListener(_keyHandlers[index]);
            _keyHandlers = null;
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

        private void OnDisable()
        {
            UnbindButtons();
        }
    }
}
