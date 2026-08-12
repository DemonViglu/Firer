using DemonViglu.FirePlay.Activity;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Player-side presentation for Fishing. Host/single-player reads the
    /// typed Logic; network Owner/Observer mirrors read Fishing's own opaque
    /// state payload. Fuel, timing and validation remain in the Logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingActivityVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerActivityHost _activityHost;
        [SerializeField] private PlayerAnimationController _animationController;
        [SerializeField] private Transform _fishingRodProp;
        [SerializeField] private Transform _fishingLineProp;
        [SerializeField] private Transform _biteIndicator;

        private bool _hasActivityState;
        private bool _wasLineCast;
        private bool _loggedMissingHost;

        private void Awake()
        {
            ResolveReferences();
            ResetPresentation();
        }

        private void Update()
        {
            ResolveReferences();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_activityHost == null && !_loggedMissingHost)
            {
                Debug.LogWarning("[FishingActivityVisuals] 未找到 PlayerActivityHost，无法读取钓鱼状态。", this);
                _loggedMissingHost = true;
            }
#endif
            if (_activityHost == null) return;

            if (!TryReadState(out var state))
            {
                if (_hasActivityState)
                    ResetPresentation();
                return;
            }

            var firstState = !_hasActivityState;
            _hasActivityState = true;
            if (!firstState && !_wasLineCast && state.IsLineCast)
                _animationController?.Play(PlayerAnimationCueIds.FishingCast);

            _animationController?.SetState(PlayerAnimationStateIds.Fishing, state.HasRod);
            SetVisible(state.HasRod);
            SetLineVisible(state.HasRod && state.IsLineCast);
            SetBiteVisible(state.HasRod && state.IsFishBiting);
            _wasLineCast = state.IsLineCast;
        }

        private bool TryReadState(out FishingActivityStateSnapshot state)
        {
            state = default;
            var session = _activityHost?.ActiveSession;
            if (session?.Definition?.ActivityId != FishingActivityLogic.ActivityId)
                return false;

            if (session.Logic is FishingActivityLogic logic)
            {
                state = new FishingActivityStateSnapshot(
                    logic.HasRod,
                    logic.IsLineCast,
                    logic.IsFishBiting,
                    logic.IsFighting,
                    logic.IsCatchReady,
                    logic.Catches,
                    logic.CatchesPerRod,
                    logic.FishPosition01,
                    logic.CatchBarPosition01,
                    logic.CatchZoneSize01,
                    logic.CatchProgress01,
                    logic.IsLiftHeld,
                    logic.Status);
                return true;
            }

            return _activityHost.TryGetActiveStatePayload(
                       FishingActivityLogic.ActivityId,
                       out var payload)
                   && FishingActivityStateSnapshot.TryParse(payload, out state);
        }

        private void ResolveReferences()
        {
            _activityHost ??= GetComponentInParent<PlayerActivityHost>();
            _activityHost ??= PlayerActivityHost.Local;
            _animationController ??= GetComponentInParent<PlayerAnimationController>();
            _fishingRodProp ??= transform.root.Find("Hand/FishingRodPop");
        }

        private void ResetPresentation()
        {
            _hasActivityState = false;
            _wasLineCast = false;
            _animationController?.SetState(PlayerAnimationStateIds.Fishing, false);
            SetBiteVisible(false);
            SetLineVisible(false);
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_fishingRodProp != null && _fishingRodProp.gameObject.activeSelf != visible)
                _fishingRodProp.gameObject.SetActive(visible);
        }

        private void SetLineVisible(bool visible)
        {
            if (_fishingLineProp != null && _fishingLineProp.gameObject.activeSelf != visible)
                _fishingLineProp.gameObject.SetActive(visible);
        }

        private void SetBiteVisible(bool visible)
        {
            if (_biteIndicator != null && _biteIndicator.gameObject.activeSelf != visible)
                _biteIndicator.gameObject.SetActive(visible);
        }
    }
}
