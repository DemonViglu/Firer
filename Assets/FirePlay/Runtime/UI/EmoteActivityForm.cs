using System;
using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Independent emote wheel. The prefab owns the available cue list and
    /// layout; the form only submits semantic emote actions to the local host.
    /// </summary>
    public sealed class EmoteActivityForm : BaseUIForms
    {
        [Serializable]
        private sealed class EmoteEntry
        {
            public string displayName = string.Empty;
            public string cueId = string.Empty;
        }

        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button[] _emoteButtons;
        [SerializeField] private EmoteEntry[] _entries;

        private UnityAction[] _emoteHandlers;
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

        private void ResolveRequester()
        {
            _requester = PlayerActivityHost.Local;
        }

        private void ResolveControls()
        {
            _titleText ??= FindText("Title");
            _statusText ??= FindText("Status");
            _closeButton ??= FindButton("CloseButton");

            var expectedCount = _entries?.Length ?? 0;
            if (expectedCount > 0
                && (_emoteButtons == null || _emoteButtons.Length != expectedCount))
            {
                _emoteButtons = new Button[expectedCount];
            }

            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == "CloseButton")
                {
                    _closeButton ??= button;
                    continue;
                }

                if (_emoteButtons == null
                    || !button.gameObject.name.StartsWith("EmoteButton", StringComparison.Ordinal)
                    || !int.TryParse(button.gameObject.name.Substring("EmoteButton".Length), out var index)
                    || index < 1
                    || index > _emoteButtons.Length)
                {
                    continue;
                }

                _emoteButtons[index - 1] = button;
            }

            if (_titleText != null)
                _titleText.text = "表情";
            SetButtonLabels();
        }

        private void SetButtonLabels()
        {
            if (_emoteButtons == null || _entries == null)
                return;

            for (var index = 0; index < _emoteButtons.Length && index < _entries.Length; index++)
            {
                var label = _emoteButtons[index]?.GetComponentInChildren<Text>(true);
                if (label != null)
                    label.text = _entries[index]?.displayName ?? string.Empty;
            }

            var closeLabel = _closeButton?.GetComponentInChildren<Text>(true);
            if (closeLabel != null)
                closeLabel.text = "关闭";
        }

        private void BindButtons()
        {
            UnbindButtons();
            if (_emoteButtons != null)
            {
                _emoteHandlers = new UnityAction[_emoteButtons.Length];
                for (var index = 0; index < _emoteButtons.Length; index++)
                {
                    var button = _emoteButtons[index];
                    if (button == null) continue;

                    var capturedIndex = index;
                    UnityAction handler = () => SubmitEntry(capturedIndex);
                    _emoteHandlers[index] = handler;
                    button.onClick.AddListener(handler);
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
            if (_emoteButtons != null && _emoteHandlers != null)
            {
                for (var index = 0; index < _emoteButtons.Length && index < _emoteHandlers.Length; index++)
                {
                    if (_emoteButtons[index] != null && _emoteHandlers[index] != null)
                        _emoteButtons[index].onClick.RemoveListener(_emoteHandlers[index]);
                }
            }

            if (_closeButton != null && _closeHandler != null)
                _closeButton.onClick.RemoveListener(_closeHandler);
            _emoteHandlers = null;
            _closeHandler = null;
        }

        private void Refresh()
        {
            ResolveRequester();
            var host = _requester as PlayerActivityHost;
            var active = host?.ActiveActivityId == EmoteActivityLogic.ActivityId;

            if (_statusText != null)
                _statusText.text = active ? "选择一个表情 · 不会限制移动" : "表情活动未开始";

            if (_emoteButtons == null) return;
            foreach (var button in _emoteButtons)
                if (button != null) button.interactable = active;
        }

        private void SubmitEntry(int index)
        {
            if (_entries == null || index < 0 || index >= _entries.Length)
                return;

            var cueId = _entries[index]?.cueId?.Trim();
            if (string.IsNullOrWhiteSpace(cueId))
            {
                SetStatus("该表情未配置动画 Cue");
                return;
            }

            Submit("emote.play", cueId);
        }

        private void OnCloseClicked() => Submit("activity.exit");

        private void Submit(string actionId, string payload = null)
        {
            ResolveRequester();
            if (_requester == null)
            {
                SetStatus("活动宿主未就绪");
                return;
            }

            var result = _requester.RequestAction(actionId, payload);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[EmoteActivityForm] 提交动作：{actionId}/{payload}，consumed={result.Consumed}，reason={result.Reason}", this);
#endif
            SetStatus(result.Consumed ? FriendlyResult(payload) : $"未执行：{result.Reason}");
        }

        private string FriendlyResult(string cueId)
        {
            if (string.IsNullOrWhiteSpace(cueId) || _entries == null)
                return "已关闭";

            foreach (var entry in _entries)
                if (entry != null && entry.cueId == cueId)
                    return $"已发送：{entry.displayName}";
            return "表情已发送";
        }

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
