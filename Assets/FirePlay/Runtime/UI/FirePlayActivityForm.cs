using System;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using SUIFW;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// SUIFW 活动窗体基类实现：只负责把 ActivityOfferDescriptor 映射到预制体按钮，
    /// 点击后通过 ActivityUIOrchestrator 提交语义动作，不直接调用玩法对象。
    /// </summary>
    public sealed class FirePlayActivityForm : BaseUIForms
    {
        [Header("Activity filter")]
        [SerializeField] private string _activityId;

        [Header("Optional labels")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;

        [Header("Action slots")]
        [SerializeField] private Button[] _actionButtons = Array.Empty<Button>();
        [SerializeField] private Text[] _actionLabels = Array.Empty<Text>();
        [SerializeField] private Button _closeButton;

        private LocalPlayerContext _context;
        private ActivityUIOrchestrator _orchestrator;
        private uint _lastRevision = uint.MaxValue;

        public override void Display()
        {
            base.Display();
            transform.SetAsLastSibling();
            ResolveContext();
            _lastRevision = uint.MaxValue;
            Refresh();
        }

        public override void Hiding()
        {
            ClearActionBindings();
            base.Hiding();
        }

        private void OnDisable()
        {
            ClearActionBindings();
        }

        private void Update()
        {
            ResolveContext();
            Refresh();
        }

        private void ResolveContext()
        {
            _context ??= LocalPlayerContext.Current;
            _orchestrator ??= _context != null ? _context.ActivityUI : null;
        }

        private void Refresh()
        {
            var activities = _context != null ? _context.Activities : null;
            var snapshot = activities != null ? activities.Session.Snapshot : default;
            if (!snapshot.IsActive || (!string.IsNullOrWhiteSpace(_activityId) && snapshot.ActivityId != _activityId))
            {
                if (_statusText != null) _statusText.text = string.Empty;
                SetActionSlotsVisible(false);
                return;
            }

            var view = _context.RitualCoordinator != null ? _context.RitualCoordinator.ViewState : default;
            if (_statusText != null) _statusText.text = view.RitualId == snapshot.ActivityId ? view.Status : string.Empty;
            if (_lastRevision == snapshot.Revision) return;
            _lastRevision = snapshot.Revision;

            var anchor = activities.ActiveAnchor;
            if (anchor == null || !anchor.TryGetOffer(snapshot.ActivityId, out var offer))
            {
                SetActionSlotsVisible(false);
                return;
            }

            if (_titleText != null) _titleText.text = offer.displayName;

            ClearActionBindings();
            var actions = offer.actions ?? Array.Empty<ActivityActionDescriptor>();
            for (var i = 0; i < _actionButtons.Length; i++)
            {
                var button = _actionButtons[i];
                if (button == null) continue;

                var hasAction = i < actions.Length && actions[i] != null && actions[i].enabled
                    && !string.IsNullOrWhiteSpace(actions[i].actionId);
                button.gameObject.SetActive(hasAction);
                if (!hasAction) continue;

                var action = actions[i];
                button.transform.SetAsLastSibling();
                button.enabled = true;
                button.interactable = true;
                button.onClick.AddListener(() => SubmitAction(action.actionId));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[FirePlayActivityForm] 槽位 {i + 1}：{action.actionId}，active={button.gameObject.activeSelf}，interactable={button.interactable}", this);
#endif
                var label = i < _actionLabels.Length && _actionLabels[i] != null
                    ? _actionLabels[i]
                    : button.GetComponentInChildren<Text>(true);
                if (label != null) label.text = action.displayName;
            }

            if (_closeButton != null)
            {
                _closeButton.gameObject.SetActive(true);
                _closeButton.onClick.AddListener(() => SubmitAction("activity.exit"));
            }
        }

        private void SubmitAction(string actionId)
        {
            if (_orchestrator == null)
            {
                Debug.LogWarning($"[FirePlayActivityForm] 无法提交动作 {actionId}：ActivityUIOrchestrator 尚未就绪。", this);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[FirePlayActivityForm] 提交动作：{actionId}", this);
#endif
            _orchestrator.RequestAction(actionId);
        }

        private void ClearActionBindings()
        {
            if (_actionButtons != null)
            {
                foreach (var button in _actionButtons)
                {
                    if (button == null) continue;
                    button.onClick.RemoveAllListeners();
                    button.gameObject.SetActive(false);
                }
            }

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveAllListeners();
                _closeButton.gameObject.SetActive(false);
            }
        }

        private void SetActionSlotsVisible(bool visible)
        {
            if (_actionButtons == null) return;
            foreach (var button in _actionButtons)
            {
                if (button != null) button.gameObject.SetActive(visible && button.gameObject.activeSelf);
            }
        }
    }
}
