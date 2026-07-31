using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.Core;
using SUIFW;
using UnityEngine;

namespace DemonViglu.FirePlay.UI
{
    [Serializable]
    public sealed class ActivityUIBinding
    {
        public string activityId;
        public string uiFormName;
        public bool enabled = true;
    }

    /// <summary>
    /// 活动 Session 与 SUIFW UIManager 的薄映射层；不把 UI 引用或窗口栈放入玩法 Session。
    /// UIForm 的 Normal／ReverseChange／HideOther 行为仍由 SUIFW Prefab 配置决定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityUIOrchestrator : MonoBehaviour
    {
        [SerializeField] private ActivityUIBinding[] _bindings = Array.Empty<ActivityUIBinding>();
        [SerializeField] private string _defaultActivityFormName = "ActivityForms";

        private PlayerActivityController _activities;
        private IEventPublisher _events;
        private string _shownFormName;
        private readonly Dictionary<string, string> _formsByActivity = new(StringComparer.Ordinal);
        private readonly List<IActivityPresenter> _presenters = new();

        public bool HasShownForm => !string.IsNullOrWhiteSpace(_shownFormName);

        public void Initialize(LocalPlayerContext context)
        {
            Detach();
            _activities = context != null ? context.Activities : GetComponent<PlayerActivityController>();
            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            RebuildBindingCache();
            RebuildPresenterCache();
            Attach();
        }

        public void RegisterBinding(string activityId, string uiFormName)
        {
            if (string.IsNullOrWhiteSpace(activityId) || string.IsNullOrWhiteSpace(uiFormName)) return;
            _formsByActivity[activityId] = uiFormName;
        }

        public void RegisterPresenter(IActivityPresenter presenter)
        {
            if (presenter != null && !_presenters.Contains(presenter)) _presenters.Add(presenter);
        }

        public void RequestAction(string actionId, string payload = null)
        {
            if (_activities == null || !_activities.Session.IsActive || string.IsNullOrWhiteSpace(actionId)) return;
            var snapshot = _activities.Session.Snapshot;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ActivityUIOrchestrator] 发布动作：{snapshot.ActivityId}/{actionId}", this);
#endif
            _events?.Publish(new ActivityActionRequested(
                snapshot.PlayerId,
                snapshot.AnchorId,
                snapshot.ActivityId,
                actionId,
                payload));
        }

        private void Awake()
        {
            RebuildBindingCache();
            _activities = GetComponent<PlayerActivityController>();
            RebuildPresenterCache();
        }

        private void OnEnable() => Attach();

        private void Attach()
        {
            if (_activities != null) _activities.SessionChanged += OnSessionChanged;
        }

        private void Detach()
        {
            if (_activities != null) _activities.SessionChanged -= OnSessionChanged;
        }

        private void RebuildBindingCache()
        {
            _formsByActivity.Clear();
            if (_bindings == null) return;

            foreach (var binding in _bindings)
            {
                if (binding == null || !binding.enabled || string.IsNullOrWhiteSpace(binding.activityId) || string.IsNullOrWhiteSpace(binding.uiFormName))
                    continue;
                _formsByActivity[binding.activityId] = binding.uiFormName;
            }
        }

        private void RebuildPresenterCache()
        {
            _presenters.Clear();
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour is IActivityPresenter presenter) _presenters.Add(presenter);
            }
        }

        private void OnSessionChanged(ActivitySessionSnapshot snapshot)
        {
            foreach (var presenter in _presenters)
            {
                if (!snapshot.IsActive || presenter.ActivityId != snapshot.ActivityId) presenter.Close();
                else presenter.Present(snapshot, this);
            }

            var nextFormName = ResolveFormName(snapshot);

            if (_shownFormName == nextFormName) return;

            if (!string.IsNullOrWhiteSpace(_shownFormName))
                UIManager.GetInstance().CloseOrReturnUIForms(_shownFormName);

            _shownFormName = nextFormName;
            if (!string.IsNullOrWhiteSpace(_shownFormName))
                UIManager.GetInstance().ShowUIForms(_shownFormName);
        }

        private string ResolveFormName(ActivitySessionSnapshot snapshot)
        {
            if (!snapshot.IsActive) return string.Empty;
            if (_formsByActivity.TryGetValue(snapshot.ActivityId, out var mappedForm)) return mappedForm;
            if (string.IsNullOrWhiteSpace(_defaultActivityFormName) || _activities == null || _activities.ActiveAnchor == null)
                return string.Empty;
            if (!_activities.ActiveAnchor.TryGetOffer(snapshot.ActivityId, out var offer) || offer.actions == null)
                return string.Empty;
            foreach (var action in offer.actions)
            {
                if (action != null && action.enabled && !string.IsNullOrWhiteSpace(action.actionId))
                    return _defaultActivityFormName;
            }
            return string.Empty;
        }

        private void OnDisable()
        {
            Detach();
            var uiManager = UIManager.TryGetInstance();
            if (uiManager != null && !string.IsNullOrWhiteSpace(_shownFormName))
            {
                uiManager.CloseOrReturnUIForms(_shownFormName);
            }
            _shownFormName = string.Empty;
        }
    }
}
