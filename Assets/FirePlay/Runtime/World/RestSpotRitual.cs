using System;
using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 挂在 RestSpot 同一物体上的停留仪式表现基类。
    /// 例如观星镜头引导、烤棉花道具或吉他音频都各自实现本类，
    /// 不直接修改玩家的停留状态。
    /// </summary>
    public abstract class RestSpotRitual : MonoBehaviour, IActivityOfferProvider
    {
        public virtual string ActivityId => string.Empty;
        public virtual string ActivityDisplayName => "停留";
        public virtual string ActivityPresentationId => "compact";
        public virtual bool LocksLookInput => false;
        public virtual ActivityActionDescriptor[] ActivityActions => Array.Empty<ActivityActionDescriptor>();
        public virtual string RestHint => "这里可以坐下，安静歇一会儿";
        public virtual string ShortRestHint => " · 这里可以坐下歇一会儿";

        public ActivityOfferDescriptor CreateActivityOffer()
        {
            if (string.IsNullOrWhiteSpace(ActivityId)) return null;
            var offer = new ActivityOfferDescriptor(ActivityId, ActivityDisplayName, ActivityPresentationId)
            {
                locksLookInput = LocksLookInput,
                actions = ActivityActions ?? Array.Empty<ActivityActionDescriptor>()
            };
            return offer;
        }

        public bool IsSelectedFor(RestInteraction interaction)
        {
            if (interaction == null || !interaction.IsResting)
            {
                return false;
            }

            var spot = GetComponent<RestSpot>();
            var anchor = spot != null ? spot.ActivityAnchor : null;
            return interaction.ActiveRestSpot == spot
                && anchor != null
                && anchor.IsSingleLegacyActivity(ActivityId);
        }

        public virtual void OnRestStarted(RestInteraction interaction)
        {
        }

        public virtual void OnRestEnded(RestInteraction interaction)
        {
        }
    }
}
