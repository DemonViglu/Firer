using System;
using DemonViglu.FirePlay.Activity;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Independent emote wheel. The prefab owns the available cue list and
    /// layout; the form only submits semantic emote actions to the local host.
    /// </summary>
    public sealed class EmoteActivityForm : FirePlayUiView
    {
        protected override void OnBackRequested() => OnCloseClicked();
        [Serializable]
        private sealed class EmoteEntry
        {
            public string displayName = string.Empty;
            public string cueId = string.Empty;
        }

        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _closeButtonLabel;
        [SerializeField] private Button[] _emoteButtons;
        [SerializeField] private Text[] _emoteLabels;
        [SerializeField] private EmoteEntry[] _entries;

        private UnityAction[] _emoteHandlers;
        private UnityAction _closeHandler;
        private IActivityActionRequester _requester;

        private void Awake()
        {
            FirePlayMinimalUiTheme.Apply(gameObject);
        }

        protected override void OnShow()
        {
            ResolveRequester();
            BindButtons();
            Refresh();
        }

        protected override void OnHide()
        {
            UnbindButtons();
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

        private void OnValidate()
        {
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
                var label = _emoteLabels != null && index < _emoteLabels.Length
                    ? _emoteLabels[index]
                    : null;
                if (label != null)
                    label.text = _entries[index]?.displayName ?? string.Empty;
            }

            if (_closeButtonLabel != null)
                _closeButtonLabel.text = "关闭";
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

        private void OnDisable()
        {
            UnbindButtons();
        }
    }
}
