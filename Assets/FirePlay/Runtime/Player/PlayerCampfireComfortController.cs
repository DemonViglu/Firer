using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>只负责玩家在公共篝火旁停留时的余火舒适恢复。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCampfireComfortController : MonoBehaviour
    {
        private FlameResourceController _resourceController;
        private Campfire _activeCampfire;

        public Campfire ActiveCampfire => _activeCampfire;
        public bool IsRecovering => _activeCampfire != null;

        public void Initialize()
        {
            _resourceController ??= GetComponent<FlameResourceController>();
        }

        private void Awake() => Initialize();

        public bool TryBegin(RestSpot spot)
        {
            End();

            var campfire = spot != null ? spot.GetComponent<Campfire>() : null;
            if (campfire == null || campfire.IsExtinguished || campfire.Config == null)
                return false;

            _resourceController ??= GetComponent<FlameResourceController>();
            if (_resourceController == null) return false;

            _resourceController.EnterCampfireRest(campfire);
            _activeCampfire = campfire;
            return true;
        }

        public void End()
        {
            if (_resourceController != null && _activeCampfire != null)
                _resourceController.ExitCampfireRest();
            _activeCampfire = null;
        }

        private void OnDisable() => End();
    }
}
