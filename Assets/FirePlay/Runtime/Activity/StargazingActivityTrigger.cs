using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Composes RestSpot with the Activity system. This is a location rule,
    /// not part of StargazingActivityLogic, so another spot can choose a
    /// different trigger policy without changing the activity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StargazingActivityTrigger : MonoBehaviour
    {
        [SerializeField] private RestSpot _restSpot;
        [SerializeField] private ActivityAnchorNode _anchor;

        private void Awake()
        {
            _restSpot ??= GetComponent<RestSpot>();
            _anchor ??= GetComponent<ActivityAnchorNode>();
        }

        private void OnEnable()
        {
            if (_restSpot == null)
                _restSpot = GetComponent<RestSpot>();
            if (_restSpot != null)
            {
                _restSpot.RestStarted += OnRestStarted;
                _restSpot.RestEnded += OnRestEnded;
            }
        }

        private void OnDisable()
        {
            if (_restSpot != null)
            {
                _restSpot.RestStarted -= OnRestStarted;
                _restSpot.RestEnded -= OnRestEnded;
            }

            var host = PlayerActivityHost.Local;
            if (host != null && host.HasActiveActivity
                && host.ActiveActivityId == StargazingActivityLogic.ActivityId)
            {
                host.End(ActivityEndReason.StateChanged);
            }
        }

        private void OnRestStarted(RestInteraction interaction)
        {
            if (interaction == null || _anchor == null)
                return;

            var host = ResolveAuthorityHost(interaction);
            if (host == null)
            {
                Debug.LogWarning("[StargazingActivityTrigger] Rest Player 的权威 ActivityHost 尚未就绪。", this);
                return;
            }

            var result = host.TryStartAtAnchor(_anchor, StargazingActivityLogic.ActivityId);
            if (!result.Success)
            {
                Debug.LogWarning($"[StargazingActivityTrigger] 观星活动未启动：{result.Reason}", this);
            }
        }

        private void OnRestEnded(RestInteraction interaction)
        {
            var host = ResolveAuthorityHost(interaction);
            if (host != null && host.HasActiveActivity
                && host.ActiveActivityId == StargazingActivityLogic.ActivityId)
            {
                host.End(ActivityEndReason.StateChanged);
            }
        }

        private static PlayerActivityHost ResolveAuthorityHost(RestInteraction interaction)
        {
            if (interaction == null) return null;
            var context = interaction.GetComponentInParent<LocalPlayerContext>();
            var host = context?.ActivityModule?.ActivityHost
                ?? interaction.GetComponentInParent<PlayerActivityHost>()
                ?? interaction.GetComponentInChildren<PlayerActivityHost>(true);
            return host != null && host.HasAuthority ? host : null;
        }
    }
}
