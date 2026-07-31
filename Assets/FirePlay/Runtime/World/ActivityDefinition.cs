using System;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 可复用的活动玩法定义。它描述活动的稳定身份、展示方案和动作契约，
    /// 不绑定某个 RestSpot、相机目标或场景对象。
    /// </summary>
    [CreateAssetMenu(fileName = "ActivityDefinition", menuName = "FirePlay/Activities/Activity Definition")]
    public sealed class ActivityDefinition : ScriptableObject
    {
        [SerializeField] private string _activityId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _presentationId = "none";
        [SerializeField] private bool _enabled = true;
        [SerializeField] private bool _locksLookInput;
        [SerializeField] private ActivityActionDescriptor[] _actions = Array.Empty<ActivityActionDescriptor>();

        public string ActivityId => _activityId;
        public string DisplayName => _displayName;
        public string PresentationId => _presentationId;
        public bool Enabled => _enabled;
        public bool LocksLookInput => _locksLookInput;
        public ActivityActionDescriptor[] Actions => _actions ?? Array.Empty<ActivityActionDescriptor>();

        public ActivityOfferDescriptor CreateOffer()
        {
            if (!_enabled || string.IsNullOrWhiteSpace(_activityId)) return null;

            var actions = Actions;
            var copiedActions = new ActivityActionDescriptor[actions.Length];
            for (var index = 0; index < actions.Length; index++)
            {
                var action = actions[index];
                copiedActions[index] = action == null
                    ? null
                    : new ActivityActionDescriptor(action.actionId, action.displayName, action.presentationId)
                    {
                        enabled = action.enabled
                    };
            }

            return new ActivityOfferDescriptor(_activityId, _displayName, _presentationId)
            {
                locksLookInput = _locksLookInput,
                actions = copiedActions
            };
        }

        private void OnValidate()
        {
            _activityId = _activityId?.Trim();
            _displayName = _displayName?.Trim();
            _presentationId = string.IsNullOrWhiteSpace(_presentationId) ? "none" : _presentationId.Trim();
        }
    }
}
