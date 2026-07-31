using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Player 上唯一需要为 Activity 域挂载的 Unity 宿主。
    ///
    /// 它不动态 AddComponent，也不保存棉花糖、钓鱼或吉他的字段。
    /// 玩法逻辑由显式的 IActivityLogicFactory 组件/启动器注册，Player
    /// 只提供运行时入口。旧 PlayerActivityController、旧 Interaction 玩法
    /// 和动态活动服务已从 Player Prefab 移除。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerActivityHost : MonoBehaviour, IActivityActionRequester
    {
        public static PlayerActivityHost Local { get; private set; }
        [SerializeField] private string _playerId = "local.player";
        [SerializeField] private ActivityCatalogAsset _catalogAsset;
        [SerializeField] private MonoBehaviour _logicFactoryBehaviour;
        [SerializeField] private MonoBehaviour _presentationBehaviour;
        [SerializeField] private MonoBehaviour _flameBehaviour;
        [SerializeField] private MonoBehaviour _playerStateBehaviour;
        [SerializeField, Min(0.1f)] private float _nearestAnchorDistance = 3f;

        private ActivityRuntime _runtime;
        private IActivityLogicFactory _logicFactory;
        private IActivityPresentationRequests _presentation;
        private IActivityFlameResource _flame;
        private IActivityPlayerStateView _playerState;
        private IEventPublisher _events;
        private bool _eventsAttached;

        public ActivityRuntime Runtime => _runtime;
        public ActivitySystem System => _runtime?.System;
        public ActivityCatalog Catalog => _runtime?.Catalog;
        public bool IsReady => _runtime != null;
        public string PlayerId => _playerId;
        public ActivitySession ActiveSession => _runtime != null
            && _runtime.System.TryGetSession(_playerId, out var session)
            ? session
            : null;
        public bool HasActiveActivity => _runtime != null
            && _runtime.System.TryGetSession(_playerId, out _);
        public string ActiveActivityId => _runtime != null
            && _runtime.System.TryGetSession(_playerId, out var session)
            ? session.Definition.ActivityId
            : string.Empty;

        private void Awake()
        {
            if (Local != null && Local != this)
                Debug.LogWarning("[PlayerActivityHost] 场景中存在多个本地 ActivityHost，UI 将使用最后启用的宿主。", this);
            Local = this;
            BuildRuntime();
            AttachEvents();
        }

        private void OnEnable()
        {
            AttachEvents();
        }

        private void OnDisable()
        {
            DetachEvents();
            End(ActivityEndReason.OwnerDisabled);
            if (Local == this) Local = null;
        }

        private void Update()
        {
            if (HasActiveActivity)
                _runtime?.Tick(_playerId, Time.deltaTime);
        }

        public void BuildRuntime()
        {
            _logicFactory = _logicFactoryBehaviour as IActivityLogicFactory;
            _presentation = _presentationBehaviour as IActivityPresentationRequests;
            _flame = _flameBehaviour as IActivityFlameResource;
            _playerState = _playerStateBehaviour as IActivityPlayerStateView;
            if (_logicFactory == null)
            {
                var configuredType = _logicFactoryBehaviour == null
                    ? "<未配置>"
                    : $"{_logicFactoryBehaviour.GetType().FullName} on {_logicFactoryBehaviour.gameObject.name}";
                Debug.LogError(
                    $"[PlayerActivityHost] Logic Factory 无效：{configuredType}。请将 ActivityLogicRegistryBehaviour（或其他 IActivityLogicFactory 实现组件）拖入 Logic Factory Behaviour 字段。",
                    this);
                _runtime = null;
                return;
            }

            if (_catalogAsset == null)
                Debug.LogWarning("[PlayerActivityHost] Catalog Asset 未配置，当前 Runtime 将为空注册表。", this);
            if (_presentationBehaviour != null && _presentation == null)
                Debug.LogWarning($"[PlayerActivityHost] Presentation Behaviour 类型无效：{_presentationBehaviour.GetType().FullName}。", this);
            if (_flameBehaviour != null && _flame == null)
                Debug.LogWarning($"[PlayerActivityHost] Flame Behaviour 类型无效：{_flameBehaviour.GetType().FullName}。", this);
            if (_playerStateBehaviour != null && _playerState == null)
                Debug.LogWarning($"[PlayerActivityHost] Player State Behaviour 类型无效：{_playerStateBehaviour.GetType().FullName}。", this);

            var catalog = _catalogAsset != null
                ? _catalogAsset.CreateRuntimeCatalog()
                : new ActivityCatalog();
            _runtime = new ActivityRuntime(catalog, _logicFactory);
        }

        public ActivityStartResult TryStartNearestActivity(string activityId)
        {
            var anchor = ActivityAnchorNode.FindNearest(transform.position, _nearestAnchorDistance);
            return TryStartAtAnchor(anchor, activityId);
        }

        public ActivityStartResult TryStartAnywhereActivity(string activityId)
        {
            if (_runtime == null || !_runtime.Catalog.TryGet(activityId, out var definition))
                return ActivityStartResult.Reject("Anywhere activity is not registered");
            if (definition.Scope != ActivityScope.Anywhere)
                return ActivityStartResult.Reject("Activity is not configured for Anywhere scope");

            var context = new ActivityContext(
                _playerId,
                definition.ActivityId,
                location: null,
                playerState: _playerState,
                target: null,
                flame: _flame,
                presentation: _presentation);
            return TryStart(definition, context);
        }

        public ActivityStartResult TryStartAtAnchor(ActivityAnchorNode anchor, string activityId)
        {
            if (anchor == null)
                return ActivityStartResult.Reject("No activity anchor is in range");
            if (!anchor.TryGetDefinition(activityId, out var asset) || asset == null)
                return ActivityStartResult.Reject("Anchor does not provide this activity");

            var definition = asset.CreateRuntimeDefinition();
            var context = new ActivityContext(
                _playerId,
                definition.ActivityId,
                anchor.Location,
                _playerState,
                target: null,
                _flame,
                _presentation);
            return TryStart(definition, context, anchor.RuleProviders);
        }

        public ActivityStartResult TryStart(
            string activityId,
            IActivityContext context,
            IEnumerable<IActivityRuleProvider> ruleProviders = null)
        {
            var result = _runtime == null
                ? ActivityStartResult.Reject("PlayerActivityHost is not ready")
                : _runtime.TryStart(activityId, context, ruleProviders);
            if (result.Success)
                RequestPresentationOpen(result.Session);
            return result;
        }

        public ActivityStartResult TryStart(
            ActivityDefinition definition,
            IActivityContext context,
            IEnumerable<IActivityRuleProvider> ruleProviders = null)
        {
            var result = _runtime == null
                ? ActivityStartResult.Reject("PlayerActivityHost is not ready")
                : _runtime.TryStart(definition, context, ruleProviders);
            if (result.Success)
                RequestPresentationOpen(result.Session);
            return result;
        }

        public ActivityActionResult SubmitAction(ActivityActionRequest request)
        {
            var session = _runtime?.System != null && _runtime.System.TryGetSession(request.PlayerId, out var activeSession)
                ? activeSession
                : null;
            var result = _runtime == null
                ? ActivityActionResult.Reject("PlayerActivityHost is not ready")
                : _runtime.SubmitAction(request);
            if (result.EndsSession && session != null)
                RequestPresentationClose(session);
            return result;
        }

        public ActivityActionResult RequestAction(string actionId, string payload = null)
        {
            if (_runtime == null || !_runtime.System.TryGetSession(_playerId, out var session))
                return ActivityActionResult.Reject("No active activity session");

            return SubmitAction(new ActivityActionRequest(
                _playerId,
                session.Definition.ActivityId,
                actionId,
                payload,
                session.Revision));
        }

        public bool End(ActivityEndReason reason = ActivityEndReason.Requested)
        {
            if (_runtime == null || string.IsNullOrWhiteSpace(_playerId)) return false;
            if (_runtime.System.TryGetSession(_playerId, out var session))
                RequestPresentationClose(session);
            return _runtime.End(_playerId, reason);
        }

        private void AttachEvents()
        {
            if (_eventsAttached) return;

            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _events.Subscribe<ActivitySelectionRequested>(OnActivitySelectionRequested);
            _events.Subscribe<ActivityActionRequested>(OnActivityActionRequested);
            _eventsAttached = true;
        }

        private void DetachEvents()
        {
            if (!_eventsAttached || _events == null) return;

            _events.Unsubscribe<ActivitySelectionRequested>(OnActivitySelectionRequested);
            _events.Unsubscribe<ActivityActionRequested>(OnActivityActionRequested);
            _eventsAttached = false;
        }

        private void OnActivitySelectionRequested(ActivitySelectionRequested request)
        {
            if (request == null || request.PlayerId != _playerId || string.IsNullOrWhiteSpace(request.ActivityId))
                return;

            ActivityStartResult result;
            if (string.IsNullOrWhiteSpace(request.AnchorId))
            {
                result = TryStartAnywhereActivity(request.ActivityId);
            }
            else
            {
                result = TryStartAtAnchor(ActivityAnchorNode.FindById(request.AnchorId), request.ActivityId);
            }

            if (!result.Success)
                Debug.LogWarning($"[PlayerActivityHost] Activity selection rejected: {request.ActivityId}; {result.Reason}", this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
                Debug.Log($"[PlayerActivityHost] Activity selection accepted: {request.AnchorId}/{request.ActivityId}, revision={result.Session?.Revision}", this);
#endif
        }

        private void OnActivityActionRequested(ActivityActionRequested request)
        {
            if (request == null || request.PlayerId != _playerId || !HasActiveActivity)
                return;

            if (!string.IsNullOrWhiteSpace(request.ActivityId) && request.ActivityId != ActiveActivityId)
                return;

            if (!_runtime.System.TryGetSession(_playerId, out var session)) return;
            if (!string.IsNullOrWhiteSpace(request.AnchorId)
                && request.AnchorId != session.Context.AnchorId)
                return;

            var result = SubmitAction(new ActivityActionRequest(
                _playerId,
                session.Definition.ActivityId,
                request.ActionId,
                request.Payload,
                session.Revision));
            if (!result.Consumed)
                Debug.LogWarning($"[PlayerActivityHost] Activity action rejected: {request.ActivityId}/{request.ActionId}; {result.Reason}", this);
        }

        private void RequestPresentationOpen(ActivitySession session)
        {
            if (_presentation == null || session == null) return;
            _presentation.RequestUi(new ActivityUiRequest(
                ActivityUiRequestKind.Open,
                session.Context.PlayerId,
                session.Definition.ActivityId,
                session.Definition.UiPrefabKey,
                session.Revision));
            _presentation.RequestCamera(new ActivityCameraRequest(
                ActivityCameraRequestKind.Enter,
                session.Context.PlayerId,
                session.Definition.ActivityId,
                session.Definition.CameraProfileId,
                session.Context.AnchorId,
                session.Revision));
        }

        private void RequestPresentationClose(ActivitySession session)
        {
            if (_presentation == null || session == null) return;
            _presentation.RequestUi(new ActivityUiRequest(
                ActivityUiRequestKind.Close,
                session.Context.PlayerId,
                session.Definition.ActivityId,
                session.Definition.UiPrefabKey,
                session.Revision));
            _presentation.RequestCamera(new ActivityCameraRequest(
                ActivityCameraRequestKind.Exit,
                session.Context.PlayerId,
                session.Definition.ActivityId,
                session.Definition.CameraProfileId,
                session.Context.AnchorId,
                session.Revision));
        }
    }
}
