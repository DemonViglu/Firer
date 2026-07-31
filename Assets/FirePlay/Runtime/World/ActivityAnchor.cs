using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Core;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    [Serializable]
    public sealed class ActivityOfferDescriptor
    {
        public string activityId;
        public string displayName;
        public string presentationId;
        public bool enabled = true;

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
        [SerializeField] private ActivityOfferDescriptor[] _additionalOffers = Array.Empty<ActivityOfferDescriptor>();

        private RestSpot _restSpot;
        private readonly List<ActivityOfferDescriptor> _offers = new();

        public string AnchorId => _anchorId;
        public RestSpot RestSpot => _restSpot;
        public IReadOnlyList<ActivityOfferDescriptor> Offers => _offers;

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
            _offers.Clear();
            AddOffer(new ActivityOfferDescriptor("rest", "坐下", "none"));
            foreach (var provider in GetComponents<MonoBehaviour>())
            {
                if (provider is IActivityOfferProvider offerProvider)
                    AddOffer(offerProvider.CreateActivityOffer());
            }
            if (_additionalOffers != null)
                foreach (var offer in _additionalOffers) AddOffer(offer);
        }

        private void AddOffer(ActivityOfferDescriptor offer)
        {
            if (offer == null || !offer.enabled || string.IsNullOrWhiteSpace(offer.activityId)) return;
            foreach (var existing in _offers)
                if (existing.activityId == offer.activityId) return;
            _offers.Add(offer);
        }
    }
}
