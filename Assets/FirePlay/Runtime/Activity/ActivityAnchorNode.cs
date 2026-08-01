using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Core;
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
        private StableSceneId _stableSceneId;
        private string _resolvedAnchorId;
        private AnchorLocationView _locationView;

        public string AnchorId
        {
            get
            {
                RefreshIdentityIfNeeded();
                return _resolvedAnchorId;
            }
        }
        public string RegionId => _regionId;
        public IReadOnlyList<ActivityDefinitionAsset> Activities => _activities ?? Array.Empty<ActivityDefinitionAsset>();
        public IActivityLocationView Location
        {
            get
            {
                RefreshIdentityIfNeeded();
                return _locationView;
            }
        }
        public IReadOnlyList<IActivityRuleProvider> RuleProviders => _ruleProviders;

        private void Awake()
        {
            _stableSceneId = GetComponent<StableSceneId>();
            _regionId = _regionId?.Trim() ?? string.Empty;
            RefreshIdentity();
            RefreshRuleProviders();
        }

        private void OnEnable()
        {
            RefreshIdentity();
            if (!ActiveNodes.Contains(this)) ActiveNodes.Add(this);
            ValidateUniqueIdentity();
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

        /// <summary>
        /// Rebuilds the read-only location view after a runtime StableSceneId
        /// has been assigned. Generated Campfires can therefore keep their
        /// ActivityAnchorNode in the prefab and assign identity afterwards.
        /// </summary>
        public void RefreshIdentity()
        {
            var nextAnchorId = ResolveAnchorId();
            if (string.Equals(_resolvedAnchorId, nextAnchorId, StringComparison.Ordinal)
                && _locationView != null)
            {
                return;
            }

            _resolvedAnchorId = nextAnchorId;
            _locationView = new AnchorLocationView(_resolvedAnchorId, _regionId, _tags);
            if (isActiveAndEnabled)
                ValidateUniqueIdentity();
        }

        private void RefreshIdentityIfNeeded()
        {
            if (_stableSceneId == null)
                _stableSceneId = GetComponent<StableSceneId>();

            var nextAnchorId = ResolveAnchorId();
            if (!string.Equals(_resolvedAnchorId, nextAnchorId, StringComparison.Ordinal)
                || _locationView == null)
            {
                RefreshIdentity();
            }
        }

        private string ResolveAnchorId()
        {
            if (_stableSceneId != null && _stableSceneId.IsValid)
                return _stableSceneId.Value.Trim();

            if (!string.IsNullOrWhiteSpace(_anchorId))
                return _anchorId.Trim();

            return gameObject.name;
        }

        private void ValidateUniqueIdentity()
        {
            // A generated prefab may briefly share its clone name before its
            // StableSceneId is assigned. Only stable identities are validated
            // here; authoring-only fallback names remain legacy-local IDs.
            if (_stableSceneId == null
                || !_stableSceneId.IsValid
                || string.IsNullOrWhiteSpace(_resolvedAnchorId))
                return;

            foreach (var node in ActiveNodes)
            {
                if (node != null
                    && node != this
                    && string.Equals(node.AnchorId, _resolvedAnchorId, StringComparison.Ordinal))
                {
                    Debug.LogError(
                        $"[ActivityAnchorNode] 活动 AnchorId 重复：{_resolvedAnchorId}。请确保每个地点使用唯一 StableSceneId。",
                        this);
                    return;
                }
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
