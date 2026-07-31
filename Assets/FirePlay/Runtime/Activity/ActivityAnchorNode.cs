using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// 新活动系统的地点节点。只保存可复用 ActivityDefinition 和地点身份，
    /// 不复制动作、UI 或玩家状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityAnchorNode : MonoBehaviour
    {
        private static readonly List<ActivityAnchorNode> ActiveNodes = new();

        [SerializeField] private string _anchorId;
        [SerializeField] private string _regionId;
        [SerializeField] private string[] _tags = Array.Empty<string>();
        [SerializeField] private ActivityDefinitionAsset[] _activities = Array.Empty<ActivityDefinitionAsset>();
        [Tooltip("显式指定此地点的规则组件。规则组件必须实现 IActivityRuleProvider。")]
        [SerializeField] private MonoBehaviour[] _ruleProviderBehaviours = Array.Empty<MonoBehaviour>();

        private readonly List<IActivityRuleProvider> _ruleProviders = new();
        private AnchorLocationView _locationView;

        public string AnchorId => _anchorId;
        public string RegionId => _regionId;
        public IReadOnlyList<ActivityDefinitionAsset> Activities => _activities ?? Array.Empty<ActivityDefinitionAsset>();
        public IActivityLocationView Location => _locationView;
        public IReadOnlyList<IActivityRuleProvider> RuleProviders => _ruleProviders;

        private void Awake()
        {
            _anchorId = string.IsNullOrWhiteSpace(_anchorId) ? gameObject.name : _anchorId.Trim();
            _regionId = _regionId?.Trim() ?? string.Empty;
            _locationView = new AnchorLocationView(_anchorId, _regionId, _tags);
            RefreshRuleProviders();
        }

        private void OnEnable()
        {
            if (!ActiveNodes.Contains(this)) ActiveNodes.Add(this);
        }

        private void OnDisable() => ActiveNodes.Remove(this);

        public void RefreshRuleProviders()
        {
            _ruleProviders.Clear();
            if (_ruleProviderBehaviours == null) return;

            foreach (var behaviour in _ruleProviderBehaviours)
            {
                if (behaviour is IActivityRuleProvider provider && behaviour != this)
                    _ruleProviders.Add(provider);
            }
        }

        public bool Provides(string activityId)
        {
            if (string.IsNullOrWhiteSpace(activityId)) return false;
            foreach (var definition in Activities)
            {
                if (definition != null && definition.ActivityId == activityId) return true;
            }

            return false;
        }

        public bool TryGetDefinition(string activityId, out ActivityDefinitionAsset definition)
        {
            if (!string.IsNullOrWhiteSpace(activityId))
            {
                foreach (var candidate in Activities)
                {
                    if (candidate != null && candidate.ActivityId == activityId)
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public static ActivityAnchorNode FindNearest(Vector3 position, float maximumDistance)
        {
            ActivityAnchorNode nearest = null;
            var nearestDistance = maximumDistance * maximumDistance;
            for (var index = ActiveNodes.Count - 1; index >= 0; index--)
            {
                var node = ActiveNodes[index];
                if (node == null)
                {
                    ActiveNodes.RemoveAt(index);
                    continue;
                }

                var distance = (node.transform.position - position).sqrMagnitude;
                if (distance <= nearestDistance)
                {
                    nearest = node;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        public static ActivityAnchorNode FindById(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId)) return null;

            for (var index = ActiveNodes.Count - 1; index >= 0; index--)
            {
                var node = ActiveNodes[index];
                if (node == null)
                {
                    ActiveNodes.RemoveAt(index);
                    continue;
                }

                if (string.Equals(node.AnchorId, anchorId, StringComparison.Ordinal))
                    return node;
            }

            return null;
        }

        private sealed class AnchorLocationView : IActivityLocationView
        {
            private readonly string[] _tags;

            public string AnchorId { get; }
            public string RegionId { get; }

            public AnchorLocationView(string anchorId, string regionId, IReadOnlyList<string> tags)
            {
                AnchorId = anchorId ?? string.Empty;
                RegionId = regionId ?? string.Empty;
                _tags = tags == null ? Array.Empty<string>() : new List<string>(tags).ToArray();
            }

            public bool HasTag(string tag)
            {
                if (string.IsNullOrWhiteSpace(tag)) return false;
                foreach (var candidate in _tags)
                {
                    if (string.Equals(candidate, tag, StringComparison.OrdinalIgnoreCase)) return true;
                }

                return false;
            }
        }
    }
}
