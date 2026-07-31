using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Player;
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

        private PlayerActivityController _activities;
        private string _shownFormName;
        private readonly Dictionary<string, string> _formsByActivity = new(StringComparer.Ordinal);

        public void Initialize(LocalPlayerContext context)
        {
            Detach();
            _activities = context != null ? context.Activities : GetComponent<PlayerActivityController>();
            RebuildBindingCache();
            Attach();
        }

        public void RegisterBinding(string activityId, string uiFormName)
        {
            if (string.IsNullOrWhiteSpace(activityId) || string.IsNullOrWhiteSpace(uiFormName)) return;
            _formsByActivity[activityId] = uiFormName;
        }

        private void Awake()
        {
            RebuildBindingCache();
            _activities = GetComponent<PlayerActivityController>();
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

        private void OnSessionChanged(ActivitySessionSnapshot snapshot)
        {
            var nextFormName = snapshot.IsActive && _formsByActivity.TryGetValue(snapshot.ActivityId, out var mappedForm)
                ? mappedForm
                : string.Empty;

            if (_shownFormName == nextFormName) return;

            if (!string.IsNullOrWhiteSpace(_shownFormName))
                UIManager.GetInstance().CloseOrReturnUIForms(_shownFormName);

            _shownFormName = nextFormName;
            if (!string.IsNullOrWhiteSpace(_shownFormName))
                UIManager.GetInstance().ShowUIForms(_shownFormName);
        }

        private void OnDisable()
        {
            Detach();
            if (!string.IsNullOrWhiteSpace(_shownFormName))
            {
                UIManager.GetInstance().CloseOrReturnUIForms(_shownFormName);
                _shownFormName = string.Empty;
            }
        }
    }
}
