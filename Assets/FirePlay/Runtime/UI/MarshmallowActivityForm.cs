using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Dedicated marshmallow UI form. It owns no roasting rules and no Flame
    /// reference; every button submits a semantic action to PlayerActivityHost.
    /// </summary>
    public sealed class MarshmallowActivityForm : BaseUIForms
    {
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _materializeButton;
        [SerializeField] private Button _turnButton;
        [SerializeField] private Button _eatButton;
        [SerializeField] private Button _closeButton;

        private IActivityActionRequester _requester;

        private void Awake()
        {
            // Inspector references remain supported, but a dedicated prefab
            // only needs stable child names and the form script.
            _statusText ??= FindText("Status");
            _materializeButton ??= FindButton("MaterializeButton");
            _turnButton ??= FindButton("TurnButton");
            _eatButton ??= FindButton("EatButton");
            _closeButton ??= FindButton("CloseButton");
        }

        public override void Display()
        {
            base.Display();
            ResolveRequester();
            BindButtons();
            SetStatus("准备好烤棉花糖");
        }

        public override void Hiding()
        {
            UnbindButtons();
            base.Hiding();
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
            _materializeButton?.onClick.AddListener(OnMaterializeClicked);
            _turnButton?.onClick.AddListener(OnTurnClicked);
            _eatButton?.onClick.AddListener(OnEatClicked);
            _closeButton?.onClick.AddListener(OnCloseClicked);
        }

        private void UnbindButtons()
        {
            _materializeButton?.onClick.RemoveListener(OnMaterializeClicked);
            _turnButton?.onClick.RemoveListener(OnTurnClicked);
            _eatButton?.onClick.RemoveListener(OnEatClicked);
            _closeButton?.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnMaterializeClicked() => Submit("marshmallow.materialize");
        private void OnTurnClicked() => Submit("marshmallow.turn");
        private void OnEatClicked() => Submit("marshmallow.eat");
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
            Debug.Log($"[MarshmallowActivityForm] 提交动作：{actionId}，consumed={result.Consumed}，reason={result.Reason}", this);
#endif
            SetStatus(result.Consumed ? result.Reason : $"未执行：{result.Reason}");
        }

        private void SetStatus(string value)
        {
            if (_statusText != null)
                _statusText.text = value ?? string.Empty;
        }

        private Text FindText(string childName)
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
            {
                if (text.gameObject.name == childName)
                    return text;
            }

            return null;
        }

        private Button FindButton(string childName)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == childName)
                    return button;
            }

            return null;
        }

        private void OnDisable()
        {
            UnbindButtons();
        }
    }
}
