using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Player-side composition boundary for the Flame feature set.
    ///
    /// The module owns the sprint policy and the player-owned world fire
    /// operations. The resource authority remains a single component inside
    /// this module; visual and persistence bridges may still live elsewhere
    /// but resolve it through the Player hierarchy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlameModule : MonoBehaviour, IPlayerModule, IPlayerSprintPolicy
    {
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private PlayerFlameController _playerFlameController;
        [SerializeField] private CampfirePlacement _campfirePlacement;
        [SerializeField] private CampfireUpgradeController _campfireUpgrade;

        private PlayerModuleContext _context;

        public string ModuleId => "flame";
        public bool IsReady { get; private set; }
        public FlameResourceController ResourceController => _resourceController;
        public PlayerFlameController PlayerFlameController => _playerFlameController;
        public CampfirePlacement CampfirePlacement => _campfirePlacement;
        public CampfireUpgradeController CampfireUpgrade => _campfireUpgrade;

        public void Initialize(PlayerModuleContext context)
        {
            _context = context;
            ResolveReferences();

            // Movement only knows about the neutral sprint policy contract.
            // It no longer needs a direct FlameResourceController reference.
            _context?.Movement?.BindSprintPolicy(this);
            IsReady = _context != null;
        }

        public void Shutdown()
        {
            if (_context?.Movement != null)
                _context.Movement.BindSprintPolicy(null);

            _context = null;
            IsReady = false;
        }

        public bool TryConsumeSprint(float deltaTime)
        {
            ResolveReferences();
            // A partially configured optional module must not soft-lock base
            // movement. The full Flame module supplies the resource policy.
            return _resourceController == null || _resourceController.TryConsumeSprint(deltaTime);
        }

        private void ResolveReferences()
        {
            if (_context == null) return;

            _resourceController ??= _context.GetComponent<FlameResourceController>();
            _playerFlameController ??= _context.GetComponent<PlayerFlameController>();
            _campfirePlacement ??= _context.GetComponent<CampfirePlacement>();
            _campfireUpgrade ??= _context.GetComponent<CampfireUpgradeController>();
        }

        private void OnValidate()
        {
            _resourceController ??= GetComponentInParent<FlameResourceController>();
            _playerFlameController ??= GetComponentInParent<PlayerFlameController>();
            _campfirePlacement ??= GetComponentInParent<CampfirePlacement>();
            _campfireUpgrade ??= GetComponentInParent<CampfireUpgradeController>();
        }
    }
}
