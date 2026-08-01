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
        [Header("Explicit Player Services")]
        [SerializeField] private PlayerSharedStateAdapter _sharedStateAdapter;
        [SerializeField] private PlayerExpressionController _expressions;
        [SerializeField] private PlayerProximityEffects _proximityEffects;
        [SerializeField] private WorldCommandExecutor _commandExecutor;
        [SerializeField] private InteractionRouter _interactionRouter;

        public static LocalPlayerContext Current { get; private set; }
        public bool IsLocalPlayer => _isLocalPlayer;
        public bool CoreServicesReady { get; private set; }
        public string PlayerId => Identity != null ? Identity.LocalPlayerId : _playerId;
        public IPlayerIdentityService Identity { get; private set; }
        public Camera LocalCamera => _localCamera;
        public FirePlayPlayerInput Input { get; private set; }
        public PlayerInteraction Interaction { get; private set; }
        public PlayerAnimationController Animation { get; private set; }
        public RestInteraction RestInteraction { get; private set; }
        public FlameResourceController FlameResource { get; private set; }
        public CampfirePlacement CampfirePlacement { get; private set; }
        public InteractionRouter InteractionRouter { get; private set; }
        public WorldCommandExecutor CommandExecutor { get; private set; }
        public PlayerSharedStateAdapter SharedStateAdapter { get; private set; }
        public PlayerExpressionController Expressions { get; private set; }
        public PlayerProximityEffects ProximityEffects { get; private set; }
        public static LocalPlayerContext EnsureFor(Component component)
        {
            if (component == null) return Current;
            var context = component.GetComponent<LocalPlayerContext>();
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
        private void OnValidate() => ResolveReferences();
        private void ResolveReferences()
        {
            _localCamera ??= GetComponentInChildren<Camera>(true);
            Input = GetComponent<FirePlayPlayerInput>();
            Interaction = GetComponent<PlayerInteraction>();
            Animation = GetComponent<PlayerAnimationController>();
            RestInteraction = GetComponent<RestInteraction>();
            FlameResource = GetComponent<FlameResourceController>();
            CampfirePlacement = GetComponent<CampfirePlacement>();

            // These services are intentionally serialized on the Player
            // prefab. Resolve only fills inspector references for older
            // prefabs; it never creates components at runtime.
            _sharedStateAdapter ??= GetComponent<PlayerSharedStateAdapter>();
            _expressions ??= GetComponent<PlayerExpressionController>();
            _proximityEffects ??= GetComponent<PlayerProximityEffects>();
            _commandExecutor ??= GetComponent<WorldCommandExecutor>();
            _interactionRouter ??= GetComponent<InteractionRouter>();

            SharedStateAdapter = _sharedStateAdapter;
            Expressions = _expressions;
            ProximityEffects = _proximityEffects;
            CommandExecutor = _commandExecutor;
            InteractionRouter = _interactionRouter;
        }

        private void EnsureCoreServices()
        {
            CoreServicesReady = false;

            // Remote Player objects keep presentation/state components but
            // must not create or register local input/world services.
            if (!_isLocalPlayer) return;

            if (_sharedStateAdapter == null
                || _expressions == null
                || _proximityEffects == null
                || _commandExecutor == null
                || _interactionRouter == null)
            {
                Debug.LogError(
                    "[LocalPlayerContext] Player Core Services 配置不完整。请在 Player Prefab 显式挂载并绑定 PlayerSharedStateAdapter、PlayerExpressionController、PlayerProximityEffects、WorldCommandExecutor、InteractionRouter。",
                    this);
                return;
            }

            Identity = GameInstanceSubsystem.GetOrCreate<IPlayerIdentityService>(() => new LocalPlayerIdentityService(_playerId));
            GameInstanceSubsystem.GetOrCreate<IWorldObjectRegistry>(() => new StableIdWorldObjectRegistry());
            SharedStateAdapter.Initialize(this);
            Expressions.Initialize(this);
            ProximityEffects.Initialize(Interaction);
            CommandExecutor.Initialize(this);
            InteractionRouter.Initialize(this);
            CoreServicesReady = true;
        }
    }
}
