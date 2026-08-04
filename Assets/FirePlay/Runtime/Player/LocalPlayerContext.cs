using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using DemonViglu.FirePlay.Core;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    [DisallowMultipleComponent]
    public sealed class LocalPlayerContext : MonoBehaviour
    {
        [SerializeField] private string _playerId = "local.player";
        [SerializeField] private bool _isLocalPlayer = true;
        [SerializeField] private Camera _localCamera;
        [SerializeField] private PlayerCameraTargetSet _cameraTargets;
        [Header("Explicit Player Services")]
        [SerializeField] private PlayerSharedStateAdapter _sharedStateAdapter;
        [SerializeField] private PlayerExpressionController _expressions;
        [SerializeField] private PlayerProximityEffects _proximityEffects;
        [SerializeField] private WorldCommandExecutor _commandExecutor;
        [SerializeField] private InteractionRouter _interactionRouter;

        public static LocalPlayerContext Current { get; private set; }
        public bool IsLocalPlayer => _isLocalPlayer;
        public bool CoreServicesReady { get; private set; }
        public PlayerCoreHost CoreHost { get; private set; }
        public string PlayerId => Identity != null ? Identity.LocalPlayerId : _playerId;
        public IPlayerIdentityService Identity { get; private set; }
        public Camera LocalCamera => _localCamera;
        public FirePlayPlayerInput Input { get; private set; }
        public PlayerMovement Movement { get; private set; }
        public PlayerLook Look { get; private set; }
        public PlayerCameraTargetSet CameraTargets { get; private set; }
        public PlayerInteraction Interaction { get; private set; }
        public PlayerAnimationController Animation { get; private set; }
        public RestInteraction RestInteraction { get; private set; }
        public FlameResourceController FlameResource { get; private set; }
        public CampfirePlacement CampfirePlacement { get; private set; }
        public CampfireUpgradeController CampfireUpgrade { get; private set; }
        public FlameModule FlameModule { get; private set; }
        public ActivityModule ActivityModule { get; private set; }
        public InteractionRouter InteractionRouter { get; private set; }
        public WorldCommandExecutor CommandExecutor { get; private set; }
        public PlayerSharedStateAdapter SharedStateAdapter { get; private set; }
        public PlayerExpressionController Expressions { get; private set; }
        public PlayerProximityEffects ProximityEffects { get; private set; }
        public static LocalPlayerContext EnsureFor(Component component)
        {
            if (component == null) return Current;
            var context = component.GetComponent<LocalPlayerContext>() ?? component.GetComponentInParent<LocalPlayerContext>();
            if (context == null)
            {
                Debug.LogError(
                    $"[LocalPlayerContext] {component.gameObject.name} 缺少显式挂载的 LocalPlayerContext。请在 Player Prefab 上配置完整的 Player Core Services。",
                    component);
                return null;
            }

            context.ResolveReferences();
            context.EnsureCoreServices();
            return context;
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureCoreServices();
        }
        private void OnEnable()
        {
            if (!_isLocalPlayer) return;

            if (Current != null && Current != this)
            {
                Debug.LogError("[LocalPlayerContext] 同一客户端只能激活一个本地玩家上下文。", this);
                enabled = false;
                return;
            }
            Current = this;
        }
        private void OnDisable() { if (Current == this) Current = null; }
        private void LateUpdate() => CoreHost?.Tick();
        private void OnValidate() => ResolveReferences(createCoreHost: false);
        private void ResolveReferences(bool createCoreHost = true)
        {
            _localCamera ??= GetComponentInChildren<Camera>(true);
            Input = GetComponent<FirePlayPlayerInput>();
            Movement = GetComponent<PlayerMovement>();
            Look = GetComponent<PlayerLook>();
            _cameraTargets ??= GetComponent<PlayerCameraTargetSet>();
            CameraTargets = _cameraTargets;
            Interaction = GetComponent<PlayerInteraction>() ?? GetComponentInChildren<PlayerInteraction>(true);
            Animation = GetComponent<PlayerAnimationController>();
            RestInteraction = GetComponent<RestInteraction>();
            FlameResource = GetComponent<FlameResourceController>() ?? GetComponentInChildren<FlameResourceController>(true);
            CampfirePlacement = GetComponent<CampfirePlacement>() ?? GetComponentInChildren<CampfirePlacement>(true);
            CampfireUpgrade = GetComponent<CampfireUpgradeController>() ?? GetComponentInChildren<CampfireUpgradeController>(true);
            FlameModule = GetComponentInChildren<FlameModule>(true);
            ActivityModule = GetComponentInChildren<ActivityModule>(true);

            // These services are intentionally serialized on the Player
            // prefab. Resolve only fills inspector references for older
            // prefabs; it never creates components at runtime.
            _sharedStateAdapter ??= GetComponent<PlayerSharedStateAdapter>();
            _expressions ??= GetComponent<PlayerExpressionController>();
            _proximityEffects ??= GetComponent<PlayerProximityEffects>();
            _commandExecutor ??= GetComponent<WorldCommandExecutor>();
            _interactionRouter ??= GetComponent<InteractionRouter>() ?? GetComponentInChildren<InteractionRouter>(true);

            SharedStateAdapter = _sharedStateAdapter;
            Expressions = _expressions;
            ProximityEffects = _proximityEffects;
            CommandExecutor = _commandExecutor;
            InteractionRouter = _interactionRouter;
            if (createCoreHost)
            {
                CoreHost ??= new PlayerCoreHost(
                    this,
                    _sharedStateAdapter,
                    _expressions,
                    _proximityEffects,
                    _commandExecutor,
                    _interactionRouter);
            }
        }

        private void EnsureCoreServices()
        {
            CoreServicesReady = false;

            // Remote Player objects may initialize modules/presentation, but
            // must not register local identity, input or world command globals.
            if (_isLocalPlayer)
            {
                Identity = GameInstanceSubsystem.GetOrCreate<IPlayerIdentityService>(() => new LocalPlayerIdentityService(_playerId));
                GameInstanceSubsystem.GetOrCreate<IWorldObjectRegistry>(() => new StableIdWorldObjectRegistry());
            }

            CoreHost ??= new PlayerCoreHost(
                this,
                _sharedStateAdapter,
                _expressions,
                _proximityEffects,
                _commandExecutor,
                _interactionRouter);
            CoreHost.Initialize();
            CoreServicesReady = CoreHost.IsReady;
        }
    }

    /// <summary>
    /// Non-Unity composition boundary for the Player's generic services.
    /// This first slice centralizes validation and initialization without
    /// changing the existing service components or their public contracts.
    /// Later slices can move individual implementations behind this host and
    /// remove their MonoBehaviour lifecycle one service at a time.
    /// </summary>
    public sealed class PlayerCoreHost
    {
        private readonly LocalPlayerContext _context;
        private readonly PlayerSharedStateAdapter _sharedStateAdapter;
        private readonly PlayerExpressionController _expressions;
        private readonly PlayerProximityEffects _proximityEffects;
        private readonly WorldCommandExecutor _commandExecutor;
        private readonly InteractionRouter _interactionRouter;
        private readonly List<IPlayerModule> _modules = new();

        public bool IsReady { get; private set; }
        public IReadOnlyList<IPlayerModule> Modules => _modules;
        public PlayerSharedStateAdapter SharedStateAdapter => _sharedStateAdapter;
        public PlayerSharedStateService SharedStateService { get; } = new();
        public PlayerExpressionController Expressions => _expressions;
        public PlayerProximityEffects ProximityEffects => _proximityEffects;
        public WorldCommandExecutor CommandExecutor => _commandExecutor;
        public InteractionRouter InteractionRouter => _interactionRouter;

        public PlayerCoreHost(
            LocalPlayerContext context,
            PlayerSharedStateAdapter sharedStateAdapter,
            PlayerExpressionController expressions,
            PlayerProximityEffects proximityEffects,
            WorldCommandExecutor commandExecutor,
            InteractionRouter interactionRouter)
        {
            _context = context;
            _sharedStateAdapter = sharedStateAdapter;
            _expressions = expressions;
            _proximityEffects = proximityEffects;
            _commandExecutor = commandExecutor;
            _interactionRouter = interactionRouter;
        }

        public void Initialize()
        {
            IsReady = false;

            if (_context == null)
                return;

            RegisterModulesFromHierarchy();

            SharedStateService.Initialize(_context);
            _sharedStateAdapter?.Initialize(_context, SharedStateService);

            // These are optional feature services. A base Player with only
            // movement/input does not need any of them to become ready.
            if (_context.IsLocalPlayer)
            {
                _expressions?.Initialize(_context);
                _proximityEffects?.Initialize(_context.Interaction);
                _commandExecutor?.Initialize(_context);
                _interactionRouter?.Initialize(_context);
            }

            IsReady = true;
            InitializeModules();
        }

        private void RegisterModulesFromHierarchy()
        {
            var components = _context.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var component in components)
            {
                if (component is IPlayerModule module)
                    RegisterModule(module);
            }
        }

        public void Tick()
        {
            if (IsReady)
                SharedStateService.Tick();
        }

        public bool RegisterModule(IPlayerModule module)
        {
            if (module == null || string.IsNullOrWhiteSpace(module.ModuleId) || _modules.Contains(module))
                return false;
            _modules.Add(module);
            if (IsReady)
                module.Initialize(new PlayerModuleContext(_context, this));
            return true;
        }

        public bool TryGetModule<T>(out T module) where T : class
        {
            foreach (var candidate in _modules)
            {
                if (candidate is T typed)
                {
                    module = typed;
                    return true;
                }
            }

            module = null;
            return false;
        }

        public bool UnregisterModule(IPlayerModule module)
        {
            if (module == null || !_modules.Remove(module)) return false;
            if (module.IsReady)
                module.Shutdown();
            return true;
        }

        private void InitializeModules()
        {
            var moduleContext = new PlayerModuleContext(_context, this);
            foreach (var module in _modules)
            {
                if (module != null && !module.IsReady)
                    module.Initialize(moduleContext);
            }
        }
    }

    /// <summary>
    /// Optional feature module contract. Flame, Activity and future systems
    /// attach through this boundary instead of becoming Player Core duties.
    /// </summary>
    public interface IPlayerModule
    {
        string ModuleId { get; }
        bool IsReady { get; }
        void Initialize(PlayerModuleContext context);
        void Shutdown();
    }

    public sealed class PlayerModuleContext
    {
        public LocalPlayerContext Player { get; }
        public PlayerCoreHost Core { get; }
        public string PlayerId => Player != null ? Player.PlayerId : string.Empty;
        public bool IsLocalPlayer => Player != null && Player.IsLocalPlayer;
        public FirePlayPlayerInput Input => Player?.Input;
        public PlayerMovement Movement => Player?.Movement;
        public PlayerLook Look => Player?.Look;
        public PlayerCameraTargetSet CameraTargets => Player?.CameraTargets;

        public PlayerModuleContext(LocalPlayerContext player, PlayerCoreHost core)
        {
            Player = player;
            Core = core;
        }

        public T GetComponent<T>() where T : Component => Player != null
            ? Player.GetComponent<T>() ?? Player.GetComponentInChildren<T>(true)
            : null;

        public bool TryGetModule<T>(out T module) where T : class => Core != null
            ? Core.TryGetModule(out module)
            : SetMissing(out module);

        private static bool SetMissing<T>(out T module) where T : class
        {
            module = null;
            return false;
        }
    }
}
