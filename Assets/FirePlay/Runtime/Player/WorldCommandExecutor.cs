using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public interface IWorldCommandExecutor
    {
        bool Execute(PlayerIntentRequested intent);
    }

    /// <summary>
    /// 单机权威的世界命令入口。未来网络版只替换本接口实现，
    /// Router、UI 与世界对象不需要知道命令是在本地还是 Host 执行。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldCommandExecutor : MonoBehaviour, IWorldCommandExecutor
    {
        private LocalPlayerContext _context;
        private IEventPublisher _events;
        private PlayerFlameController _flame;
        private CampfireUpgradeController _campfireUpgrade;
        private bool _subscribed;
        private IWorldObjectRegistry _registry;
        private readonly WorldCommandRateLimiter _rateLimiter = new();
        private const double DuplicateCommandCooldownSeconds = 0.12d;
        private bool _localExecutionEnabled = true;

        public void Initialize(LocalPlayerContext context)
        {
            _context = context;
            if (!_localExecutionEnabled || (_context != null && !_context.IsLocalPlayer)) return;

            _flame ??= GetComponentInChildren<PlayerFlameController>(true);
            _campfireUpgrade ??= GetComponentInChildren<CampfireUpgradeController>(true);
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _registry ??= GameInstanceSubsystem.GetOrCreate<IWorldObjectRegistry>(() => new StableIdWorldObjectRegistry());

            var registered = GameInstanceSubsystem.TryGet<IWorldCommandExecutor>();
            if (registered == null)
                GameInstanceSubsystem.Register<IWorldCommandExecutor>(this);
            else if (!ReferenceEquals(registered, this))
            {
                Debug.LogError("[WorldCommandExecutor] 已存在另一个世界命令执行器。", this);
                enabled = false;
                return;
            }

            Subscribe();
        }

        private void OnEnable()
        {
            Initialize(GetComponent<LocalPlayerContext>() ?? GetComponentInParent<LocalPlayerContext>());
        }

        private void OnDisable()
        {
            DetachLocalExecution();
        }

        /// <summary>
        /// A network Player routes the same semantic intents through its NGO
        /// authority boundary. Disabling this local executor prevents the Host
        /// owner from also mutating the world through the single-player path.
        /// </summary>
        public void ConfigureLocalExecution(bool enabled)
        {
            if (_localExecutionEnabled == enabled)
                return;

            _localExecutionEnabled = enabled;
            if (enabled)
                Initialize(_context ?? GetComponent<LocalPlayerContext>() ?? GetComponentInParent<LocalPlayerContext>());
            else
                DetachLocalExecution();
        }

        private void Subscribe()
        {
            if (_subscribed || _events == null) return;
            _events.Subscribe<PlayerIntentRequested>(OnIntentRequested);
            _subscribed = true;
        }

        private void DetachLocalExecution()
        {
            if (_subscribed && _events != null)
            {
                _events.Unsubscribe<PlayerIntentRequested>(OnIntentRequested);
                _subscribed = false;
            }
            if (ReferenceEquals(GameInstanceSubsystem.TryGet<IWorldCommandExecutor>(), this))
                GameInstanceSubsystem.Unregister<IWorldCommandExecutor>();
            _rateLimiter.Clear();
        }

        private void OnIntentRequested(PlayerIntentRequested intent)
        {
            Execute(intent);
        }

        public bool Execute(PlayerIntentRequested intent)
        {
            if (!_localExecutionEnabled || !CanExecute(intent)) return false;

            var resource = _context.FlameResource;
            var activeFlame = _flame != null ? _flame.ActiveFlame : null;
            var now = Time.realtimeSinceStartupAsDouble;
            if (!_rateLimiter.CanAccept(intent.PlayerId, intent.Kind, intent.TargetId, now, DuplicateCommandCooldownSeconds))
                return false;

            var accepted = intent.Kind switch
            {
                PlayerIntentKind.Interact => ExecuteContextInteract(intent, resource, activeFlame),
                PlayerIntentKind.AddFire => ExecuteAddFire(intent, resource),
                PlayerIntentKind.TendFire => TryResolveCurrent(intent, out Campfire tendFire) && CanAfford(resource, tendFire.TendFuelCost) && tendFire.TryTend(resource),
                PlayerIntentKind.GatherEmber => TryResolveCurrent(intent, out FlameSource source) && source.TryRestore(resource),
                PlayerIntentKind.StartPublicFire => TryResolveCurrent(intent, out SmallFire startFire) && CanStartPublicFire(resource) && _campfireUpgrade.TryTendSmallFire(startFire),
                PlayerIntentKind.DrawFire => TryResolveCurrent(intent, out Campfire drawFire) && drawFire.TryWithdrawEmergencyFuel(resource),
                PlayerIntentKind.ReclaimSmallFire => TryResolveCurrent(intent, out SmallFire reclaimFire) && reclaimFire.TryReclaim(resource),
                PlayerIntentKind.ContributeWorldTree => TryResolveCurrent(intent, out WorldTreeContribution tree) && CanAfford(resource, tree.ContributionCost) && tree.TryContribute(intent.PlayerId, resource, activeFlame),
                PlayerIntentKind.LegacyWithdrawOrReclaim => ExecuteLegacyWithdrawOrReclaim(intent, resource),
                _ => false
            };
            if (accepted)
                _rateLimiter.RecordAccepted(intent.PlayerId, intent.Kind, intent.TargetId, now);
            return accepted;
        }

        private bool CanExecute(PlayerIntentRequested intent)
        {
            if (intent == null || _context == null || _context.Interaction == null) return false;
            if (intent.PlayerId != _context.PlayerId || intent.Mode != PlayerMode.Exploring) return false;
            return intent.TargetKind == PlayerInteractTargetKind.None
                || !string.IsNullOrWhiteSpace(intent.TargetId);
        }

        private bool ExecuteContextInteract(PlayerIntentRequested intent, FlameResourceController resource, FlameBrush flame)
        {
            return intent.TargetKind switch
            {
                PlayerInteractTargetKind.WorldTree => TryResolveCurrent(intent, out WorldTreeContribution tree) && CanAfford(resource, tree.ContributionCost) && tree.TryContribute(intent.PlayerId, resource, flame),
                PlayerInteractTargetKind.FlameSource => TryResolveCurrent(intent, out FlameSource source) && source.TryRestore(resource),
                PlayerInteractTargetKind.Campfire => TryResolveCurrent(intent, out Campfire campfire) && CanAfford(resource, campfire.TendFuelCost) && campfire.TryTend(resource),
                PlayerInteractTargetKind.SmallFire => TryResolveCurrent(intent, out SmallFire fire) && CanStartPublicFire(resource) && _campfireUpgrade.TryTendSmallFire(fire),
                _ => false
            };
        }

        private bool ExecuteAddFire(PlayerIntentRequested intent, FlameResourceController resource)
        {
            if (intent.TargetKind == PlayerInteractTargetKind.Campfire)
                return TryResolveCurrent(intent, out Campfire campfire) && CanAfford(resource, campfire.TendFuelCost) && campfire.TryTend(resource);
            return TryResolveCurrent(intent, out SmallFire fire) && CanStartPublicFire(resource) && _campfireUpgrade.TryTendSmallFire(fire);
        }

        private bool ExecuteLegacyWithdrawOrReclaim(PlayerIntentRequested intent, FlameResourceController resource)
        {
            if (intent.TargetKind == PlayerInteractTargetKind.Campfire)
                return TryResolveCurrent(intent, out Campfire campfire) && campfire.TryWithdrawEmergencyFuel(resource);
            return TryResolveCurrent(intent, out SmallFire fire) && fire.TryReclaim(resource);
        }

        private bool TryResolveCurrent<T>(PlayerIntentRequested intent, out T target) where T : Component
        {
            target = null;
            if (_registry == null || !_registry.TryResolve(intent.TargetId, out target)) return false;
            if (intent.HasExpectedTargetVersion &&
                (!_registry.TryGetCommandVersion(intent.TargetId, out var currentVersion) || currentVersion != intent.ExpectedTargetVersion))
                return false;

            _context.Interaction.GetIntentTarget(intent.Kind, out var currentKind, out var currentId);
            return currentKind == intent.TargetKind && currentId == intent.TargetId;
        }

        private bool CanStartPublicFire(FlameResourceController resource) =>
            _campfireUpgrade != null && _campfireUpgrade.CanStartPublicFire && CanAfford(resource, _campfireUpgrade.TendFuelCost);

        private static bool CanAfford(FlameResourceController resource, float cost) =>
            cost > 0f && resource != null && resource.State != null && resource.State.CurrentFuel >= cost;
    }
}
