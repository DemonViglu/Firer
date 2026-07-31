using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Core;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    [Serializable]
    public sealed class ActivityActionDescriptor
    {
        public string actionId;
        public string displayName;
        public string presentationId;
        public bool enabled = true;

        public ActivityActionDescriptor()
        {
        }

        public ActivityActionDescriptor(string actionId, string displayName, string presentationId = "button")
        {
            this.actionId = actionId;
            this.displayName = displayName;
            this.presentationId = presentationId;
        }
    }

    [Serializable]
    public sealed class ActivityOfferDescriptor
    {
        public string activityId;
        public string displayName;
        public string presentationId;
        public bool enabled = true;
        public bool locksLookInput;
        public ActivityActionDescriptor[] actions = Array.Empty<ActivityActionDescriptor>();

        public ActivityOfferDescriptor()
        {
        }

        public ActivityOfferDescriptor(string activityId, string displayName, string presentationId)
        {
            this.activityId = activityId;
            this.displayName = displayName;
            this.presentationId = presentationId;
        }
    }

    /// <summary>活动地点提供者；玩法 Session 不依附具体 UI 或玩家对象。</summary>
    public interface IActivityOfferProvider
    {
        ActivityOfferDescriptor CreateActivityOffer();
    }

    /// <summary>兼容 RestSpot 的活动发现层；当前只负责列出候选，不启动活动。</summary>
    [RequireComponent(typeof(RestSpot))]
    [DisallowMultipleComponent]
    public sealed class ActivityAnchor : MonoBehaviour
    {
        [SerializeField] private string _anchorId;
        [Tooltip("引用可复用的活动定义。玩法配置只存在于 ActivityDefinition 资产中，地点只决定是否提供该玩法。")]
        [SerializeField] private ActivityDefinition[] _activityDefinitions = Array.Empty<ActivityDefinition>();
        [Tooltip("兼容现有场景的内嵌 Offer；新内容请优先使用 ActivityDefinition。")]
        [SerializeField] private ActivityOfferDescriptor[] _offers = Array.Empty<ActivityOfferDescriptor>();
        [HideInInspector, SerializeField] private ActivityOfferDescriptor[] _legacyOffers = Array.Empty<ActivityOfferDescriptor>();
        [HideInInspector, SerializeField] private ActivityOfferDescriptor[] _additionalOffers = Array.Empty<ActivityOfferDescriptor>();

        private RestSpot _restSpot;
        private readonly List<ActivityOfferDescriptor> _runtimeOffers = new();

        public string AnchorId => _anchorId;
        public RestSpot RestSpot => _restSpot;
        public IReadOnlyList<ActivityOfferDescriptor> Offers => _runtimeOffers;

        public bool TryGetOffer(string activityId, out ActivityOfferDescriptor offer)
        {
            offer = null;
            if (string.IsNullOrWhiteSpace(activityId)) return false;

            foreach (var candidate in _runtimeOffers)
            {
                if (candidate != null && candidate.enabled && candidate.activityId == activityId)
                {
                    offer = candidate;
                    return true;
                }
            }

            return false;
        }

        public bool IsSingleLegacyActivity(string activityId)
        {
            if (string.IsNullOrWhiteSpace(activityId)) return false;

            var nonRestCount = 0;
            var onlyActivityId = string.Empty;
            foreach (var offer in _runtimeOffers)
            {
                if (offer == null || !offer.enabled || string.IsNullOrWhiteSpace(offer.activityId) || offer.activityId == "rest")
                    continue;
                nonRestCount++;
                onlyActivityId = offer.activityId;
            }

            return nonRestCount == 1 && onlyActivityId == activityId;
        }

        public bool TryGetSingleLegacyActivityOffer(out ActivityOfferDescriptor offer)
        {
            offer = null;
            var nonRestCount = 0;
            foreach (var candidate in _runtimeOffers)
            {
                if (candidate == null || !candidate.enabled || string.IsNullOrWhiteSpace(candidate.activityId) || candidate.activityId == "rest")
                    continue;
                nonRestCount++;
                offer = candidate;
            }

            if (nonRestCount == 1 && offer != null) return true;
            offer = null;
            return false;
        }

        public void Initialize(RestSpot restSpot)
        {
            _restSpot = restSpot != null ? restSpot : GetComponent<RestSpot>();
            RefreshOffers();
        }

        private void Awake()
        {
            _restSpot = GetComponent<RestSpot>();
            if (string.IsNullOrWhiteSpace(_anchorId))
            {
                var stableId = GetComponent<StableSceneId>();
                _anchorId = stableId != null && stableId.IsValid ? stableId.Value : gameObject.name;
            }
            RefreshOffers();
        }

        public void RefreshOffers()
        {
            _runtimeOffers.Clear();
            // 兼容未配置 Anchor 的旧 RestSpot：基础坐下仍可用；其它玩法必须显式配置。
            AddOffer(new ActivityOfferDescriptor("rest", "坐下", "none"));
            if (_activityDefinitions != null)
            {
                foreach (var definition in _activityDefinitions)
                {
                    if (definition == null) continue;
                    AddOffer(definition.CreateOffer());
                }
            }

            AddOffers(_offers);
            AddOffers(_legacyOffers);
            AddOffers(_additionalOffers);

            // RestSpotRitual remains the location-specific presentation/behavior
            // provider. For example, StargazingRitual carries the LookTarget that
            // belongs to this particular RestPot and cannot live in a global
            // ActivityDefinition. Explicit definitions/offers win by activityId;
            // providers only fill missing activities for legacy and scene-authored
            // ritual components.
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour is IActivityOfferProvider provider)
                {
                    AddOffer(provider.CreateActivityOffer());
                }
            }
        }

        private void AddOffers(IEnumerable<ActivityOfferDescriptor> offers)
        {
            if (offers == null) return;
            foreach (var offer in offers)
                AddOffer(offer);
        }

        private void AddOffer(ActivityOfferDescriptor offer)
        {
            if (offer == null || !offer.enabled || string.IsNullOrWhiteSpace(offer.activityId)) return;
            foreach (var existing in _runtimeOffers)
            {
                if (existing != null && existing.activityId == offer.activityId) return;
            }

            _runtimeOffers.Add(offer);
        }
    }
}
