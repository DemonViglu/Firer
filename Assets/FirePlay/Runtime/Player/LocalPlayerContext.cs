using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.UI;
using DemonViglu.FirePlay.Debugging;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    [DisallowMultipleComponent]
    public sealed class LocalPlayerContext : MonoBehaviour
    {
        [SerializeField] private string _playerId = "local.player";
        [SerializeField] private Camera _localCamera;
        public static LocalPlayerContext Current { get; private set; }
        public string PlayerId => Identity != null ? Identity.LocalPlayerId : _playerId;
        public IPlayerIdentityService Identity { get; private set; }
        public Camera LocalCamera => _localCamera;
        public FirePlayPlayerInput Input { get; private set; }
        public PlayerInteraction Interaction { get; private set; }
        public PlayerAnimationController Animation { get; private set; }
        public RestInteraction RestInteraction { get; private set; }
        public MarshmallowInteraction MarshmallowInteraction { get; private set; }
        public FishingInteraction FishingInteraction { get; private set; }
        public FlameResourceController FlameResource { get; private set; }
        public CampfirePlacement CampfirePlacement { get; private set; }
        public InteractionRouter InteractionRouter { get; private set; }
        public WorldCommandExecutor CommandExecutor { get; private set; }
        public RitualInteractionCoordinator RitualCoordinator { get; private set; }
        public PlayerSharedStateAdapter SharedStateAdapter { get; private set; }
        public PlayerExpressionController Expressions { get; private set; }
        public PlayerProximityEffects ProximityEffects { get; private set; }
        public PlayerActivityController Activities { get; private set; }
        public ActivityUIOrchestrator ActivityUI { get; private set; }
        public PlayerCampfireComfortController CampfireComfort { get; private set; }
        public PlayerRestPoseController RestPose { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public ActivitySelectionProbe ActivitySelectionProbe { get; private set; }
        public FishingActivityPresenterProbe FishingActivityPresenterProbe { get; private set; }
#endif

        public static LocalPlayerContext EnsureFor(Component component)
        {
            if (component == null) return Current;
            var context = component.GetComponent<LocalPlayerContext>() ?? component.gameObject.AddComponent<LocalPlayerContext>();
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
            MarshmallowInteraction = GetComponent<MarshmallowInteraction>();
            FishingInteraction = GetComponent<FishingInteraction>();
            FlameResource = GetComponent<FlameResourceController>();
            CampfirePlacement = GetComponent<CampfirePlacement>();
        }

        private void EnsureCoreServices()
        {
            Identity = GameInstanceSubsystem.GetOrCreate<IPlayerIdentityService>(() => new LocalPlayerIdentityService(_playerId));
            GameInstanceSubsystem.GetOrCreate<IWorldObjectRegistry>(() => new StableIdWorldObjectRegistry());
            RitualCoordinator = GetComponent<RitualInteractionCoordinator>() ?? gameObject.AddComponent<RitualInteractionCoordinator>();
            RitualCoordinator.Initialize(this);
            SharedStateAdapter = GetComponent<PlayerSharedStateAdapter>() ?? gameObject.AddComponent<PlayerSharedStateAdapter>();
            SharedStateAdapter.Initialize(this);
            Expressions = GetComponent<PlayerExpressionController>() ?? gameObject.AddComponent<PlayerExpressionController>();
            Expressions.Initialize(this);
            ProximityEffects = GetComponent<PlayerProximityEffects>() ?? gameObject.AddComponent<PlayerProximityEffects>();
            ProximityEffects.Initialize(Interaction);
            Activities = GetComponent<PlayerActivityController>() ?? gameObject.AddComponent<PlayerActivityController>();
            Activities.Initialize(this);
            RestInteraction?.InitializeActivitySupport();
            ActivityUI = GetComponent<ActivityUIOrchestrator>() ?? gameObject.AddComponent<ActivityUIOrchestrator>();
            ActivityUI.Initialize(this);
            CampfireComfort = GetComponent<PlayerCampfireComfortController>() ?? gameObject.AddComponent<PlayerCampfireComfortController>();
            CampfireComfort.Initialize();
            RestPose = GetComponent<PlayerRestPoseController>() ?? gameObject.AddComponent<PlayerRestPoseController>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ActivitySelectionProbe = GetComponent<ActivitySelectionProbe>() ?? gameObject.AddComponent<ActivitySelectionProbe>();
#endif
            CommandExecutor = GetComponent<WorldCommandExecutor>() ?? gameObject.AddComponent<WorldCommandExecutor>();
            CommandExecutor.Initialize(this);
            InteractionRouter = GetComponent<InteractionRouter>() ?? gameObject.AddComponent<InteractionRouter>();
            InteractionRouter.Initialize(this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            FishingActivityPresenterProbe = GetComponent<FishingActivityPresenterProbe>() ?? gameObject.AddComponent<FishingActivityPresenterProbe>();
            ActivityUI?.RegisterPresenter(FishingActivityPresenterProbe);
#endif
        }
    }
}
