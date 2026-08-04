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

            var localContext = LocalPlayerContext.Current;
            if (localContext == null || localContext.RestInteraction != interaction)
                return;

            var host = PlayerActivityHost.Local;
            if (host == null)
            {
                Debug.LogWarning("[StargazingActivityTrigger] 本地 PlayerActivityHost 尚未就绪。", this);
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
            var localContext = LocalPlayerContext.Current;
            if (localContext == null || localContext.RestInteraction != interaction)
                return;

            var host = PlayerActivityHost.Local;
            if (host != null && host.HasActiveActivity
                && host.ActiveActivityId == StargazingActivityLogic.ActivityId)
            {
                host.End(ActivityEndReason.StateChanged);
            }
        }
    }
}
