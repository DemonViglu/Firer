using DemonViglu.FirePlay.Activity;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Player-side presentation for FishingActivityLogic. It owns only the rod
    /// prop and animation cues; fuel, timing and action validation stay in the
    /// activity logic.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingActivityVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerActivityHost _activityHost;
        [SerializeField] private PlayerAnimationController _animationController;
        [SerializeField] private Transform _fishingRodProp;

        private FishingActivityLogic _logic;
        private bool _wasLineCast;
        private int _catches;
        private bool _loggedMissingHost;

        private void Awake()
        {
            ResolveReferences();
            SetVisible(false);
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

            var session = _activityHost.ActiveSession;
            var nextLogic = session?.Logic as FishingActivityLogic;
            if (nextLogic == null)
            {
                ResetPresentation();
                return;
            }

            _logic = nextLogic;
            if (!_wasLineCast && _logic.IsLineCast)
                _animationController?.Play(PlayerAnimationCueIds.FishingCast);
            if (_logic.Catches > _catches)
                _animationController?.Play(PlayerAnimationCueIds.FishingReel);

            _animationController?.SetState(PlayerAnimationStateIds.Fishing, _logic.HasRod);
            SetVisible(_logic.HasRod);
            _wasLineCast = _logic.IsLineCast;
            _catches = _logic.Catches;
        }

        private void ResolveReferences()
        {
            _activityHost ??= GetComponentInParent<PlayerActivityHost>();
            _activityHost ??= PlayerActivityHost.Local;
            _activityHost ??= FindAnyObjectByType<PlayerActivityHost>();
            _animationController ??= GetComponentInParent<PlayerAnimationController>();
            _fishingRodProp ??= transform.root.Find("Hand/FishingRodPop");
        }

        private void ResetPresentation()
        {
            _logic = null;
            _wasLineCast = false;
            _catches = 0;
            _animationController?.SetState(PlayerAnimationStateIds.Fishing, false);
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_fishingRodProp != null && _fishingRodProp.gameObject.activeSelf != visible)
                _fishingRodProp.gameObject.SetActive(visible);
        }
    }
}
