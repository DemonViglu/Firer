using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.Save;
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
    public sealed class PlayerActivityHost : MonoBehaviour,
        IActivityActionRequester,
        IActivityAuthority,
        IActivityTargetGiftReceiver
    {
        private const int MaximumActionPayloadLength = 512;
        private const string MarshmallowReceiveActionId = "marshmallow.receive";
        public static PlayerActivityHost Local { get; private set; }
        [SerializeField] private string _playerId = "local.player";
        [SerializeField] private bool _isLocalPlayer = true;
        [SerializeField] private ActivityCatalogAsset _catalogAsset;
        [SerializeField] private MonoBehaviour _logicFactoryBehaviour;
        [SerializeField] private MonoBehaviour _presentationBehaviour;
        [SerializeField] private MonoBehaviour _flameBehaviour;
        [SerializeField] private MonoBehaviour _playerStateBehaviour;
        [SerializeField, Min(0.1f)] private float _nearestAnchorDistance = 3f;

        private ActivityRuntime _runtime;
        private IActivityLogicFactory _logicFactory;
        private IActivityPresentationRequests _presentation;
        private IActivityPresentationRequests _localPresentation;
        private AuthorityPresentationRelay _authorityPresentation;
        private IActivityFlameResource _flame;
        private IActivityPlayerStateView _playerState;
        private IActivityParticipationDirectory _participationDirectory;
        private IActivityTargetDirectory _targetDirectory;
        private IEventPublisher _events;
        private IAsyncInteractionFactStore _asyncFactStore;
        private IActivityRequestTransport _requestTransport;
        private ActivityCatalog _catalog;
        private ActivityDefinition _mirroredDefinition;
        private string _mirroredAnchorId = string.Empty;
        private string _mirroredTargetId = string.Empty;
        private uint _mirroredRevision;
        private uint _mirroredStateRevision;
        private string _mirroredStatePayload = string.Empty;
        private uint _publishedStateSessionRevision;
        private uint _publishedLogicStateRevision;
        private uint _nextFactRevision;
        private readonly string _eventRunId = Guid.NewGuid().ToString("N");
        private readonly HashSet<string> _consumedEventIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _appliedFactEventIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _receivedGiftEventIds = new(StringComparer.Ordinal);
        private uint _lastAppliedFactRevision;
        private bool _eventsAttached;
        private bool _participationEventsAttached;
        private bool _targetEventsAttached;
        private bool _hasAuthority;

        public ActivityRuntime Runtime => _runtime;
        public ActivitySystem System => _runtime?.System;
        public ActivityCatalog Catalog => _runtime?.Catalog ?? _catalog;
        public bool IsLocalPlayer => _isLocalPlayer;
        public bool HasAuthority => _hasAuthority;
        public bool IsReady => _runtime != null
            || (_isLocalPlayer && _requestTransport != null && _catalog != null);
        public string PlayerId => _playerId;

        public bool TryReceiveMarshmallow(MarshmallowGift gift, out string reason)
        {
            if (!_hasAuthority || _flame == null)
            {
                reason = "Target Player authority is unavailable";
                return false;
            }
            if (!gift.IsValid || gift.SourcePlayerId == _playerId)
            {
                reason = "Marshmallow gift is invalid";
                return false;
            }
            var receiveEventId = $"{gift.EventId}:receive";
            if (!IsValidStableText(receiveEventId))
            {
                reason = "Marshmallow gift EventId is too long to derive a receive fact";
                return false;
            }
            if (!_receivedGiftEventIds.Add(gift.EventId))
            {
                reason = "Marshmallow gift EventId was already received";
                return false;
            }
            if (gift.FuelValue > 0f
                && _flame.CurrentFuel + gift.FuelValue > _flame.MaximumFuel + 0.0001f)
            {
                reason = "Target Player does not have room for the full marshmallow value";
                return false;
            }
            if (gift.FuelValue > 0f && !_flame.Restore(gift.FuelValue))
            {
                reason = "Target Player cannot receive the marshmallow value";
                return false;
            }

            // Receiving a gift is a standalone social fact. The receiver does
            // not need to be inside a marshmallow Session, and this fact is
            // intentionally excluded from late-join snapshots.
            var receiveFact = new ActivityInteractionOccurred(
                _playerId,
                string.Empty,
                MarshmallowActivityLogic.ActivityId,
                MarshmallowReceiveActionId,
                $"{(int)gift.Quality}|{gift.FuelValue:R}",
                ActiveSession?.Revision ?? 1u,
                endsSession: false,
                ActivityEndReason.Requested,
                "Marshmallow received",
                ActivityTargetKind.Player,
                _playerId,
                CreateFactMetadata(gift.SourcePlayerId, receiveEventId),
                isSessionBound: false);
            _events?.Publish(receiveFact);
            _asyncFactStore?.AppendActivity(ActivityFactDto.From(receiveFact), out _);

            var request = new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationCue,
                _playerId,
                MarshmallowActivityLogic.ActivityId,
                gift.SourcePlayerId,
                PlayerAnimationCueIds.MarshmallowReceive,
                active: true,
                sessionRevision: ActiveSession?.Revision ?? 1u);
            if (_isLocalPlayer)
                _presentation?.RequestPlayer(request);
            _events?.Publish(new ActivityPlayerPresentationRequested(request));
            var vfxRequest = new ActivityPlayerRequest(
                ActivityPlayerRequestKind.VfxCue,
                _playerId,
                MarshmallowActivityLogic.ActivityId,
                gift.SourcePlayerId,
                MarshmallowActivityLogic.ReceiveVfxCueId,
                active: true,
                sessionRevision: ActiveSession?.Revision ?? 1u);
            if (_isLocalPlayer)
                _presentation?.RequestPlayer(vfxRequest);
            _events?.Publish(new ActivityPlayerPresentationRequested(vfxRequest));
            reason = "Marshmallow received";
            return true;
        }
        public ActivitySession ActiveSession => _runtime != null
            && _runtime.System.TryGetSession(_playerId, out var session)
            ? session
            : null;
        public bool HasActiveActivity => (_runtime != null
                && _runtime.System.TryGetSession(_playerId, out _))
            || _mirroredRevision > 0;
        public string ActiveActivityId
        {
            get
            {
                if (_runtime != null && _runtime.System.TryGetSession(_playerId, out var session))
                    return session.Definition.ActivityId;
                return _mirroredDefinition?.ActivityId ?? string.Empty;
            }
        }
        public uint ActiveStateRevision => _mirroredStateRevision;
        public string ActiveTargetId
        {
            get
            {
                if (_runtime != null
                    && _runtime.System.TryGetSession(_playerId, out var session))
                {
                    return session.Context.Target?.TargetId ?? string.Empty;
                }
                return _mirroredTargetId;
            }
        }
        public IReadOnlyList<string> AvailableTargetPlayerIds =>
            _targetDirectory?.GetAvailablePlayerIds(_playerId) ?? Array.Empty<string>();
        public IReadOnlyList<ActivityParticipant> ActiveParticipants
        {
            get
            {
                if (_runtime != null
                    && _runtime.System.TryGetSession(_playerId, out var session))
                {
                    return session.Context.Participants;
                }
                if (_mirroredDefinition != null && _mirroredRevision > 0)
                {
                    return ResolveParticipants(
                        _mirroredDefinition,
                        _mirroredAnchorId,
                        _mirroredTargetId);
                }
                return Array.Empty<ActivityParticipant>();
            }
        }

        public bool ConfigureSceneServices(
            MonoBehaviour logicFactoryBehaviour,
            MonoBehaviour presentationBehaviour)
        {
            if (logicFactoryBehaviour is not IActivityLogicFactory
                || presentationBehaviour is not IActivityPresentationRequests)
            {
                return false;
            }

            _logicFactoryBehaviour = logicFactoryBehaviour;
            _presentationBehaviour = presentationBehaviour;
            _logicFactory = (IActivityLogicFactory)logicFactoryBehaviour;
            _localPresentation = (IActivityPresentationRequests)presentationBehaviour;
            if (!_hasAuthority)
                _presentation = _localPresentation;
            return true;
        }

        /// <summary>
        /// Applies local presentation ownership after NGO has spawned the
        /// object. Kept for scene/single-player callers where local ownership
        /// and authority are the same role.
        /// </summary>
        public void ConfigureNetworkRole(bool isLocalPlayer, string playerId)
        {
            ConfigureNetworkRole(isLocalPlayer, isLocalPlayer, playerId);
        }

        /// <summary>
        /// Separates local input/presentation ownership from Activity
        /// authority. A Server owns Runtime/Logic for every network Player,
        /// while only this device's owned Player subscribes to local UI input.
        /// </summary>
        public void ConfigureNetworkRole(bool isLocalPlayer, bool hasAuthority, string playerId)
        {
            if (!string.IsNullOrWhiteSpace(playerId))
                _playerId = playerId;

            if (_isLocalPlayer == isLocalPlayer
                && _hasAuthority == hasAuthority
                && (_runtime != null || !hasAuthority)
                && (hasAuthority || isLocalPlayer || _mirroredRevision == 0))
                return;

            if (_isLocalPlayer && !isLocalPlayer && Local == this)
                Local = null;

            DetachEvents();
            End(ActivityEndReason.OwnerDisabled);
            ClearMirroredSession(closePresentation: true, ActivityEndReason.OwnerDisabled);
            _appliedFactEventIds.Clear();
            _lastAppliedFactRevision = 0;
            _consumedEventIds.Clear();
            _receivedGiftEventIds.Clear();
            _isLocalPlayer = isLocalPlayer;
            _hasAuthority = hasAuthority;
            if (isLocalPlayer)
                Local = this;
            BuildRuntime();
            AttachEvents();
        }

        public void ConfigureRequestTransport(IActivityRequestTransport requestTransport)
        {
            _requestTransport = requestTransport;
        }

        private void Awake()
        {
            _hasAuthority = _isLocalPlayer;
            if (_isLocalPlayer && Local != null && Local != this)
                Debug.LogWarning("[PlayerActivityHost] 场景中存在多个本地 ActivityHost，UI 将使用最后启用的宿主。", this);
            if (_isLocalPlayer)
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
            ClearMirroredSession(
                closePresentation: _isLocalPlayer,
                ActivityEndReason.OwnerDisabled);
            _participationDirectory?.RemovePlayer(_playerId);
            if (_isLocalPlayer && Local == this) Local = null;
        }

        private void Update()
        {
            if (HasActiveActivity)
            {
                _runtime?.Tick(_playerId, Time.deltaTime);
                if (_runtime != null && _runtime.System.TryGetSession(_playerId, out var session))
                    PublishStateIfChanged(session);
            }
        }

        public void BuildRuntime()
        {
            _catalog = _catalogAsset != null
                ? _catalogAsset.CreateRuntimeCatalog()
                : new ActivityCatalog();
            _localPresentation = _presentationBehaviour as IActivityPresentationRequests;
            _presentation = _localPresentation;
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _participationDirectory ??=
                GameInstanceSubsystem.GetOrCreate<IActivityParticipationDirectory>(
                    () => new ActivityParticipationDirectory(_events));
            _targetDirectory ??=
                GameInstanceSubsystem.GetOrCreate<IActivityTargetDirectory>(
                    () => new ActivityTargetDirectory(_events));

            // A non-authority Player is a presentation/request endpoint only.
            // It must never consume flame or advance Activity Logic locally.
            if (!_hasAuthority)
            {
                _runtime = null;
                return;
            }

            _logicFactory = _logicFactoryBehaviour as IActivityLogicFactory;
            // Authority always receives a semantic presentation endpoint.
            // It forwards locally for this device's Player and only publishes
            // facts for a remote Server-side Player.
            _authorityPresentation ??= new AuthorityPresentationRelay(this);
            _presentation = _authorityPresentation;
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

            _runtime = new ActivityRuntime(_catalog, _logicFactory);
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
                presentation: _presentation,
                participants: ResolveParticipants(definition, string.Empty));
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
                _presentation,
                ResolveParticipants(definition, anchor.AnchorId));
            return TryStart(definition, context, anchor.RuleProviders);
        }

        public ActivityStartResult TryStartTargetedActivity(
            string activityId,
            string targetId,
            ActivityAnchorNode anchor = null)
        {
            ActivityDefinition definition;
            if (anchor != null)
            {
                if (!anchor.TryGetDefinition(activityId, out var asset) || asset == null)
                    return ActivityStartResult.Reject("Anchor does not provide this activity");
                definition = asset.CreateRuntimeDefinition();
            }
            else if (_runtime == null || !_runtime.Catalog.TryGet(activityId, out definition))
            {
                return ActivityStartResult.Reject("Targeted activity is not registered");
            }

            if (definition.Scope != ActivityScope.Targeted
                || definition.ParticipationMode != ActivityParticipationMode.TargetedInteraction)
            {
                return ActivityStartResult.Reject(
                    "Targeted activity must use Targeted scope and TargetedInteraction participation");
            }
            if (string.IsNullOrWhiteSpace(targetId) || targetId == _playerId)
                return ActivityStartResult.Reject("Targeted activity needs another Player target");
            if (_targetDirectory == null
                || !_targetDirectory.TryResolve(targetId, out var target)
                || target == null
                || !target.IsAvailable
                || !target.HasTag("player"))
            {
                return ActivityStartResult.Reject("Target Player is unavailable");
            }

            var anchorId = anchor != null ? anchor.AnchorId : string.Empty;
            var context = new ActivityContext(
                _playerId,
                definition.ActivityId,
                anchor?.Location,
                _playerState,
                target,
                _flame,
                _presentation,
                ResolveParticipants(definition, anchorId, target.TargetId));
            return TryStart(definition, context, anchor?.RuleProviders);
        }

        /// <summary>
        /// Single authority entry point for both local selection events and a
        /// future network transport. The DTO contains only stable IDs, so no
        /// transport adapter needs to know about Unity objects or Logic.
        /// </summary>
        public ActivityStartResult HandleSelection(ActivitySelectionRequestDto request)
        {
            if (!request.IsValid || request.PlayerId != _playerId)
                return ActivityStartResult.Reject("Activity selection request is not owned by this Player");

            ActivityAnchorNode anchor = null;
            ActivityDefinition requestedDefinition = null;
            if (!string.IsNullOrWhiteSpace(request.AnchorId))
            {
                anchor = ActivityAnchorNode.FindById(request.AnchorId);
                if (anchor == null)
                    return ActivityStartResult.Reject("Activity anchor does not exist");
                if ((anchor.transform.position - transform.position).sqrMagnitude
                    > _nearestAnchorDistance * _nearestAnchorDistance)
                {
                    return ActivityStartResult.Reject("Activity anchor is out of range");
                }
                if (!anchor.TryGetDefinition(request.ActivityId, out var asset) || asset == null)
                    return ActivityStartResult.Reject("Anchor does not provide this activity");
                requestedDefinition = asset.CreateRuntimeDefinition();
            }
            else
            {
                _runtime?.Catalog.TryGet(request.ActivityId, out requestedDefinition);
            }

            if (requestedDefinition?.Scope == ActivityScope.Targeted)
            {
                return TryStartTargetedActivity(
                    request.ActivityId,
                    request.TargetId,
                    anchor);
            }
            if (!string.IsNullOrWhiteSpace(request.TargetId))
                return ActivityStartResult.Reject("Only a Targeted activity accepts TargetId");

            if (string.IsNullOrWhiteSpace(request.AnchorId))
                return TryStartAnywhereActivity(request.ActivityId);

            return TryStartAtAnchor(anchor, request.ActivityId);
        }

        /// <summary>
        /// Single authority entry point for an action in the active Session.
        /// Anchor and revision checks happen here before Logic receives the
        /// action, which makes stale or misrouted network messages harmless.
        /// </summary>
        public ActivityActionResult HandleAction(ActivityActionRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.PlayerId)
                || string.IsNullOrWhiteSpace(request.ActivityId)
                || string.IsNullOrWhiteSpace(request.ActionId)
                || request.SessionRevision == 0
                || request.PlayerId != _playerId)
                return ActivityActionResult.Reject("Activity action request is not owned by this Player");
            var eventId = string.IsNullOrWhiteSpace(request.EventId)
                ? CreateActionEventId()
                : request.EventId;
            if (!IsValidStableText(eventId))
                return ActivityActionResult.Reject("Activity action EventId is invalid");
            if (!_consumedEventIds.Add(eventId))
                return ActivityActionResult.Reject("Activity action EventId was already consumed");
            if ((request.Payload?.Length ?? 0) > MaximumActionPayloadLength)
                return ActivityActionResult.Reject("Activity action payload is too large");
            if (!ValidateActionTarget(request.TargetKind, request.TargetId, out var targetReason))
                return ActivityActionResult.Reject(targetReason);
            if (_runtime == null)
                return ActivityActionResult.Reject("PlayerActivityHost is not ready");
            if (!_runtime.System.TryGetSession(_playerId, out var session))
                return ActivityActionResult.Reject("No active activity session");
            if (request.ActivityId != session.Definition.ActivityId)
                return ActivityActionResult.Reject("Activity action does not match the active activity");
            if (!string.IsNullOrWhiteSpace(request.AnchorId)
                && request.AnchorId != session.Context.AnchorId)
                return ActivityActionResult.Reject("Activity action does not match the active anchor");
            if (request.SessionRevision != session.Revision)
                return ActivityActionResult.Reject("Activity action uses a stale session revision");

            var activityRequest = new ActivityActionRequest(
                _playerId,
                session.Definition.ActivityId,
                request.ActionId,
                request.Payload,
                session.Revision,
                request.TargetKind,
                request.TargetId,
                eventId);
            var result = request.ActionId == MarshmallowActivityLogic.GiveActionId
                ? SubmitMarshmallowGive(session, activityRequest)
                : SubmitAction(activityRequest);
            return result;
        }

        private ActivityActionResult SubmitMarshmallowGive(
            ActivitySession session,
            ActivityActionRequest request)
        {
            if (session.Logic is not MarshmallowActivityLogic logic)
                return ActivityActionResult.Reject("Marshmallow logic is unavailable");
            if (request.TargetKind != ActivityTargetKind.Player
                || string.IsNullOrWhiteSpace(request.TargetId))
            {
                return ActivityActionResult.Reject("Marshmallow give needs a Player target");
            }
            var accepted = logic.TryGive(
                session.Context.PlayerId,
                request.EventId,
                gift =>
                {
                    if (_targetDirectory == null)
                        return "Target Player directory is unavailable";
                    if (!_targetDirectory.TryDeliverMarshmallow(
                            request.TargetId,
                            gift,
                            out var receiverReason))
                        return receiverReason;
                    return string.Empty;
                },
                out var reason);
            if (!accepted)
                return ActivityActionResult.Reject(reason);

            PublishInteraction(session, request, ActivityActionResult.Consume("Marshmallow given"));
            PublishStateIfChanged(session);
            return ActivityActionResult.Consume("Marshmallow given");
        }

        public ActivityStartResult TryStart(
            string activityId,
            IActivityContext context,
            IEnumerable<IActivityRuleProvider> ruleProviders = null)
        {
            var previous = ActiveSession;
            var result = _runtime == null
                ? ActivityStartResult.Reject("PlayerActivityHost is not ready")
                : _runtime.TryStart(activityId, context, ruleProviders);
            if (result.Success)
            {
                if (previous != null && previous != result.Session)
                {
                    RequestPresentationClose(previous);
                    PublishSessionEnded(previous, ActivityEndReason.Switched);
                }
                PublishSessionStarted(result.Session);
                RequestPresentationOpen(result.Session);
                PublishStateIfChanged(result.Session, force: true);
            }
            return result;
        }

        public ActivityStartResult TryStart(
            ActivityDefinition definition,
            IActivityContext context,
            IEnumerable<IActivityRuleProvider> ruleProviders = null)
        {
            var previous = ActiveSession;
            var result = _runtime == null
                ? ActivityStartResult.Reject("PlayerActivityHost is not ready")
                : _runtime.TryStart(definition, context, ruleProviders);
            if (result.Success)
            {
                if (previous != null && previous != result.Session)
                {
                    RequestPresentationClose(previous);
                    PublishSessionEnded(previous, ActivityEndReason.Switched);
                }
                PublishSessionStarted(result.Session);
                RequestPresentationOpen(result.Session);
                PublishStateIfChanged(result.Session, force: true);
            }
            return result;
        }

        private ActivityActionResult SubmitAction(ActivityActionRequest request)
        {
            var session = _runtime?.System != null && _runtime.System.TryGetSession(request.PlayerId, out var activeSession)
                ? activeSession
                : null;
            var result = _runtime == null
                ? ActivityActionResult.Reject("PlayerActivityHost is not ready")
                : _runtime.SubmitAction(request);
            if (result.Consumed && session != null)
            {
                PublishInteraction(session, request, result);
                if (!result.EndsSession)
                    PublishStateIfChanged(session);
            }
            if (result.EndsSession && session != null)
            {
                RequestPresentationClose(session);
                PublishSessionEnded(session, result.EndReason);
            }
            return result;
        }

        public ActivityActionResult RequestAction(
            string actionId,
            string payload = null,
            ActivityTargetReference target = default)
        {
            ActivityActionRequestDto request;
            if (_runtime != null && _runtime.System.TryGetSession(_playerId, out var session))
            {
                request = new ActivityActionRequestDto(
                    _playerId,
                    session.Context.AnchorId,
                    session.Definition.ActivityId,
                    actionId,
                    payload,
                    session.Revision,
                    target.Kind,
                    target.Id,
                    CreateActionEventId());
            }
            else if (_mirroredDefinition != null && _mirroredRevision > 0)
            {
                request = new ActivityActionRequestDto(
                    _playerId,
                    _mirroredAnchorId,
                    _mirroredDefinition.ActivityId,
                    actionId,
                    payload,
                    _mirroredRevision,
                    target.Kind,
                    target.Id,
                    CreateActionEventId());
            }
            else
            {
                return ActivityActionResult.Reject("No active activity session");
            }

            return _requestTransport != null
                ? _requestTransport.RequestAction(request)
                : HandleAction(request);
        }

        /// <summary>
        /// Applies an authority fact to a non-authority Player mirror. The
        /// locally-owned mirror also opens its UI/Camera; a remote observer
        /// mirror stores only lifecycle/state for that Player's Visuals. No
        /// Client mirror creates ActivityLogic or consumes Flame.
        /// </summary>
        public bool ApplyNetworkFact(ActivityFactDto fact)
        {
            if (_hasAuthority
                || fact.PlayerId != _playerId
                || string.IsNullOrWhiteSpace(fact.ActivityId)
                || fact.SessionRevision == 0
                || !fact.Metadata.IsValid
                || (fact.Kind != ActivityNetworkFactKind.StateChanged
                    && fact.Metadata.FactRevision <= _lastAppliedFactRevision)
                || !_appliedFactEventIds.Add(fact.Metadata.EventId))
            {
                return false;
            }

            switch (fact.Kind)
            {
                case ActivityNetworkFactKind.SessionStarted:
                    if (_mirroredRevision == fact.SessionRevision)
                        return MatchesMirroredSession(fact)
                            && _mirroredTargetId == fact.TargetId
                            && AcceptAppliedFactRevision(fact);
                    if (_mirroredRevision > fact.SessionRevision
                        || _catalog == null
                        || !_catalog.TryGet(fact.ActivityId, out var definition)
                        || definition.ParticipationMode != fact.ParticipationMode
                        || !IsTargetShapeValid(fact.TargetKind, fact.TargetId)
                        || (definition.Scope == ActivityScope.Targeted)
                            != !string.IsNullOrWhiteSpace(fact.TargetId)
                        || (definition.Scope == ActivityScope.Targeted
                            && (definition.ParticipationMode
                                    != ActivityParticipationMode.TargetedInteraction
                                || fact.TargetId == _playerId)))
                    {
                        return false;
                    }

                    if (_mirroredRevision > 0)
                        ClearMirroredSession(
                            closePresentation: _isLocalPlayer,
                            ActivityEndReason.Switched);
                    _mirroredDefinition = definition;
                    _mirroredAnchorId = fact.AnchorId;
                    _mirroredTargetId = fact.TargetId;
                    _mirroredRevision = fact.SessionRevision;
                    _participationDirectory?.Register(new ActivitySessionStarted(
                        fact.PlayerId,
                        fact.AnchorId,
                        fact.ActivityId,
                        fact.ParticipationMode,
                        fact.SessionRevision,
                        fact.TargetId,
                        fact.TargetKind,
                        fact.Metadata));
                    if (_isLocalPlayer)
                    {
                        RequestMirroredPresentation(
                            ActivityUiRequestKind.Open,
                            ActivityCameraRequestKind.Enter);
                    }
                    PublishMirroredFact(fact);
                    return AcceptAppliedFactRevision(fact);

                case ActivityNetworkFactKind.InteractionOccurred:
                    if (!MatchesMirroredSession(fact)) return false;
                    PublishMirroredFact(fact);
                    return AcceptAppliedFactRevision(fact);

                case ActivityNetworkFactKind.SocialInteractionOccurred:
                    if (!IsTargetShapeValid(fact.TargetKind, fact.TargetId)
                        || fact.TargetKind != ActivityTargetKind.Player
                        || fact.TargetId != _playerId)
                    {
                        return false;
                    }
                    PublishMirroredFact(fact);
                    return AcceptAppliedFactRevision(fact);

                case ActivityNetworkFactKind.StateChanged:
                    if (!MatchesMirroredSession(fact)
                        || fact.StateRevision == 0
                        || fact.StateRevision <= _mirroredStateRevision)
                    {
                        return false;
                    }
                    _mirroredStateRevision = fact.StateRevision;
                    _mirroredStatePayload = fact.Payload ?? string.Empty;
                    PublishMirroredFact(fact);
                    return AcceptAppliedFactRevision(fact);

                case ActivityNetworkFactKind.SessionEnded:
                    if (!MatchesMirroredSession(fact)) return false;
                    PublishMirroredFact(fact);
                    ClearMirroredSession(
                        closePresentation: _isLocalPlayer,
                        fact.EndReason);
                    return AcceptAppliedFactRevision(fact);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Captures only the current authority truth for a late-joining
        /// observer. Historical interactions are intentionally not replayed.
        /// </summary>
        public bool TryCaptureAuthorityNetworkSnapshot(
            out ActivityFactDto sessionFact,
            out ActivityFactDto stateFact)
        {
            sessionFact = default;
            stateFact = default;
            var session = ActiveSession;
            if (!_hasAuthority || session == null || session.Revision == 0)
                return false;

            sessionFact = ActivityFactDto.From(new ActivitySessionStarted(
                session.Context.PlayerId,
                session.Context.AnchorId,
                session.Definition.ActivityId,
                session.Definition.ParticipationMode,
                session.Revision,
                session.Context.Target?.TargetId,
                session.Context.Target == null
                    ? ActivityTargetKind.None
                    : ActivityTargetKind.Player,
                CreateFactMetadata()));

            if (session.Logic is IActivityNetworkStateProvider provider
                && provider.NetworkStateRevision > 0)
            {
                stateFact = ActivityFactDto.From(new ActivityStateChanged(
                    session.Context.PlayerId,
                    session.Context.AnchorId,
                    session.Definition.ActivityId,
                    session.Revision,
                    provider.NetworkStateRevision,
                    provider.CaptureNetworkState(),
                    CreateFactMetadata()));
            }

            return true;
        }

        public bool ApplyNetworkPresentation(ActivityPlayerRequest request)
        {
            if (!_isLocalPlayer
                || _hasAuthority
                || _presentation == null
                || request.PlayerId != _playerId)
            {
                return false;
            }

            if (IsOutOfSessionPresentation(request))
                return _presentation.RequestPlayer(request);
            if (_mirroredDefinition == null)
                return false;
            if (request.ActivityId != _mirroredDefinition.ActivityId
                || request.SessionRevision != _mirroredRevision)
                return false;

            return _presentation.RequestPlayer(request);
        }

        public bool ApplyNetworkObserverPresentation(ActivityPlayerRequest request)
        {
            if (_hasAuthority
                || request.PlayerId != _playerId
                || _presentationBehaviour is not IActivityObserverPlayerRequestExecutor observer)
            {
                return false;
            }

            if (IsOutOfSessionPresentation(request))
                return observer.ExecuteObserver(request);
            if (_mirroredDefinition == null)
                return false;
            if (request.ActivityId != _mirroredDefinition.ActivityId
                || request.SessionRevision != _mirroredRevision)
                return false;

            return observer.ExecuteObserver(request);
        }

        private static bool IsOutOfSessionPresentation(ActivityPlayerRequest request) =>
            (request.ActivityId == EmoteActivityLogic.ActivityId
                && request.Kind == ActivityPlayerRequestKind.AnimationCue
                && (request.CueId == PlayerAnimationCueIds.EmoteWave
                    || request.CueId == PlayerAnimationCueIds.EmoteThanks))
            || (request.ActivityId == MarshmallowActivityLogic.ActivityId
                && ((request.Kind == ActivityPlayerRequestKind.AnimationCue
                        && request.CueId == PlayerAnimationCueIds.MarshmallowReceive)
                    || (request.Kind == ActivityPlayerRequestKind.VfxCue
                        && request.CueId == MarshmallowActivityLogic.ReceiveVfxCueId)));

        public bool End(ActivityEndReason reason = ActivityEndReason.Requested)
        {
            if (_runtime == null || string.IsNullOrWhiteSpace(_playerId)) return false;
            if (_runtime.System.TryGetSession(_playerId, out var session))
            {
                RequestPresentationClose(session, reason);
                var ended = _runtime.End(_playerId, reason);
                if (ended)
                    PublishSessionEnded(session, reason);
                return ended;
            }

            return false;
        }

        private void PublishSessionStarted(ActivitySession session)
        {
            if (_events == null || session == null) return;
            var fact = new ActivitySessionStarted(
                session.Context.PlayerId,
                session.Context.AnchorId,
                session.Definition.ActivityId,
                session.Definition.ParticipationMode,
                session.Revision,
                session.Context.Target?.TargetId,
                session.Context.Target == null
                    ? ActivityTargetKind.None
                    : ActivityTargetKind.Player,
                CreateFactMetadata());
            _participationDirectory?.Register(fact);
            _events.Publish(fact);
        }

        private void PublishInteraction(
            ActivitySession session,
            ActivityActionRequest request,
            ActivityActionResult result)
        {
            if (_events == null || session == null) return;
            var fact = new ActivityInteractionOccurred(
                session.Context.PlayerId,
                session.Context.AnchorId,
                session.Definition.ActivityId,
                request.ActionId,
                request.Payload,
                session.Revision,
                result.EndsSession,
                result.EndReason,
                result.Reason,
                request.TargetKind,
                request.TargetId,
                CreateFactMetadata(request.EventId));
            _events.Publish(fact);
            _asyncFactStore?.AppendActivity(ActivityFactDto.From(fact), out _);
        }

        private void PublishSessionEnded(ActivitySession session, ActivityEndReason reason)
        {
            if (_events == null || session == null) return;
            var fact = new ActivitySessionEnded(
                session.Context.PlayerId,
                session.Context.AnchorId,
                session.Definition.ActivityId,
                session.Revision,
                reason,
                CreateFactMetadata());
            _participationDirectory?.Remove(fact);
            _events.Publish(fact);
            if (_publishedStateSessionRevision == session.Revision)
            {
                _publishedStateSessionRevision = 0;
                _publishedLogicStateRevision = 0;
            }
        }

        public bool TryGetActiveStatePayload(string activityId, out string payload)
        {
            if (_mirroredDefinition != null
                && _mirroredDefinition.ActivityId == activityId
                && _mirroredRevision > 0
                && _mirroredStateRevision > 0)
            {
                payload = _mirroredStatePayload;
                return true;
            }

            payload = string.Empty;
            return false;
        }

        private void PublishStateIfChanged(ActivitySession session, bool force = false)
        {
            if (_events == null
                || session?.Logic is not IActivityNetworkStateProvider provider
                || provider.NetworkStateRevision == 0)
            {
                return;
            }
            if (!force
                && _publishedStateSessionRevision == session.Revision
                && _publishedLogicStateRevision == provider.NetworkStateRevision)
            {
                return;
            }

            var payload = provider.CaptureNetworkState() ?? string.Empty;
            _publishedStateSessionRevision = session.Revision;
            _publishedLogicStateRevision = provider.NetworkStateRevision;
            _events.Publish(new ActivityStateChanged(
                session.Context.PlayerId,
                session.Context.AnchorId,
                session.Definition.ActivityId,
                session.Revision,
                provider.NetworkStateRevision,
                payload,
                CreateFactMetadata()));
        }

        private void AttachEvents()
        {
            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _asyncFactStore = GameInstanceSubsystem.GetOrCreate<IAsyncInteractionFactStore>(
                () => new LocalAsyncInteractionFactStore());
            if (_isLocalPlayer && !_eventsAttached)
            {
                _events.Subscribe<ActivitySelectionRequested>(OnActivitySelectionRequested);
                _events.Subscribe<ActivityActionRequested>(OnActivityActionRequested);
                _eventsAttached = true;
            }
            if (_hasAuthority && !_participationEventsAttached)
            {
                _events.Subscribe<ActivityParticipantsChanged>(OnActivityParticipantsChanged);
                _participationEventsAttached = true;
            }
            if (_hasAuthority && !_targetEventsAttached)
            {
                _events.Subscribe<ActivityTargetAvailabilityChanged>(OnActivityTargetAvailabilityChanged);
                _targetEventsAttached = true;
            }
        }

        private void DetachEvents()
        {
            if (_events == null) return;

            if (_eventsAttached)
            {
                _events.Unsubscribe<ActivitySelectionRequested>(OnActivitySelectionRequested);
                _events.Unsubscribe<ActivityActionRequested>(OnActivityActionRequested);
                _eventsAttached = false;
            }
            if (_participationEventsAttached)
            {
                _events.Unsubscribe<ActivityParticipantsChanged>(OnActivityParticipantsChanged);
                _participationEventsAttached = false;
            }
            if (_targetEventsAttached)
            {
                _events.Unsubscribe<ActivityTargetAvailabilityChanged>(OnActivityTargetAvailabilityChanged);
                _targetEventsAttached = false;
            }
        }

        private void OnActivityTargetAvailabilityChanged(ActivityTargetAvailabilityChanged change)
        {
            if (!_hasAuthority
                || change == null
                || change.IsAvailable
                || _runtime == null
                || !_runtime.System.TryGetSession(_playerId, out var session)
                || session.Context.Target == null
                || session.Context.Target.TargetId != change.TargetId)
            {
                return;
            }

            End(ActivityEndReason.TargetUnavailable);
        }

        private void OnActivityParticipantsChanged(ActivityParticipantsChanged change)
        {
            if (!_hasAuthority
                || change == null
                || change.ParticipationMode != ActivityParticipationMode.SharedGroup
                || _runtime == null
                || !_runtime.System.TryGetSession(_playerId, out var session)
                || session.Definition.ParticipationMode != ActivityParticipationMode.SharedGroup
                || session.Definition.ActivityId != change.ActivityId
                || session.Context.AnchorId != change.AnchorId)
            {
                return;
            }

            var participants = ResolveParticipants(
                session.Definition,
                session.Context.AnchorId);
            if (ParticipantsMatch(session.Context.Participants, participants))
                return;

            var context = new ActivityContext(
                session.Context.PlayerId,
                session.Context.ActivityId,
                session.Context.Location,
                session.Context.PlayerState,
                session.Context.Target,
                session.Context.Flame,
                session.Context.Presentation,
                participants,
                session.Context.Rules);
            var anchor = string.IsNullOrWhiteSpace(session.Context.AnchorId)
                ? null
                : ActivityAnchorNode.FindById(session.Context.AnchorId);
            if (!_runtime.System.Revalidate(
                    _playerId,
                    context,
                    anchor?.RuleProviders,
                    ActivityEndReason.StateChanged))
            {
                RequestPresentationClose(session, ActivityEndReason.StateChanged);
                PublishSessionEnded(session, ActivityEndReason.StateChanged);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[PlayerActivityHost] Shared group updated: anchor={change.AnchorId}, activity={change.ActivityId}, owner={_playerId}, participants={participants.Count}, revision={change.Revision}",
                this);
#endif
        }

        private void OnActivitySelectionRequested(ActivitySelectionRequested request)
        {
            if (request == null) return;

            var dto = ActivityNetworkMapper.ToDto(request);
            var result = _requestTransport != null
                ? _requestTransport.RequestSelection(dto)
                : HandleSelection(dto);

            _events.Publish(new ActivitySelectionResolved(
                request.PlayerId,
                request.AnchorId,
                request.ActivityId,
                result.Success,
                result.Reason));

            if (!result.Success)
                Debug.LogWarning($"[PlayerActivityHost] Activity selection rejected: {request.ActivityId}; {result.Reason}", this);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
                Debug.Log($"[PlayerActivityHost] Activity selection accepted: {request.AnchorId}/{request.ActivityId}, revision={result.Session?.Revision}", this);
#endif
        }

        private void OnActivityActionRequested(ActivityActionRequested request)
        {
            if (request == null) return;

            uint revision;
            if (_runtime != null && _runtime.System.TryGetSession(_playerId, out var session))
                revision = session.Revision;
            else if (_mirroredDefinition != null && _mirroredRevision > 0)
                revision = _mirroredRevision;
            else
                return;

            var dto = ActivityNetworkMapper.ToDto(request, revision);
            if (string.IsNullOrWhiteSpace(dto.EventId))
            {
                dto = new ActivityActionRequestDto(
                    dto.PlayerId,
                    dto.AnchorId,
                    dto.ActivityId,
                    dto.ActionId,
                    dto.Payload,
                    dto.SessionRevision,
                    dto.TargetKind,
                    dto.TargetId,
                    CreateActionEventId());
            }
            var result = _requestTransport != null
                ? _requestTransport.RequestAction(dto)
                : HandleAction(dto);
            if (!result.Consumed)
                Debug.LogWarning($"[PlayerActivityHost] Activity action rejected: {request.ActivityId}/{request.ActionId}; {result.Reason}", this);
        }

        private void RequestPresentationOpen(ActivitySession session)
        {
            if (_presentation == null || session == null) return;
            if (session.Logic is IActivityPresentationLifecycle lifecycle)
                lifecycle.OnPresentationStarted(session.Context, session.Revision);
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

        private void RequestPresentationClose(ActivitySession session, ActivityEndReason? reasonOverride = null)
        {
            if (_presentation == null || session == null) return;
            if (session.Logic is IActivityPresentationLifecycle lifecycle)
                lifecycle.OnPresentationEnded(
                    session.Context,
                    session.Revision,
                    reasonOverride ?? session.EndReason ?? ActivityEndReason.Requested);
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

        private bool MatchesMirroredSession(ActivityFactDto fact) =>
            _mirroredDefinition != null
            && _mirroredRevision == fact.SessionRevision
            && _mirroredDefinition.ActivityId == fact.ActivityId
            && _mirroredAnchorId == fact.AnchorId;

        private void RequestMirroredPresentation(
            ActivityUiRequestKind uiKind,
            ActivityCameraRequestKind cameraKind)
        {
            if (_presentation == null || _mirroredDefinition == null || _mirroredRevision == 0)
                return;

            _presentation.RequestUi(new ActivityUiRequest(
                uiKind,
                _playerId,
                _mirroredDefinition.ActivityId,
                _mirroredDefinition.UiPrefabKey,
                _mirroredRevision));
            _presentation.RequestCamera(new ActivityCameraRequest(
                cameraKind,
                _playerId,
                _mirroredDefinition.ActivityId,
                _mirroredDefinition.CameraProfileId,
                _mirroredAnchorId,
                _mirroredRevision));
        }

        private void ClearMirroredSession(bool closePresentation, ActivityEndReason reason)
        {
            if (_mirroredDefinition == null || _mirroredRevision == 0)
                return;

            if (closePresentation)
                RequestMirroredPresentation(ActivityUiRequestKind.Close, ActivityCameraRequestKind.Exit);
            _participationDirectory?.Remove(new ActivitySessionEnded(
                _playerId,
                _mirroredAnchorId,
                _mirroredDefinition.ActivityId,
                _mirroredRevision,
                reason));
            _mirroredDefinition = null;
            _mirroredAnchorId = string.Empty;
            _mirroredTargetId = string.Empty;
            _mirroredRevision = 0;
            _mirroredStateRevision = 0;
            _mirroredStatePayload = string.Empty;
        }

        private IReadOnlyList<ActivityParticipant> ResolveParticipants(
            ActivityDefinition definition,
            string anchorId,
            string targetId = null)
        {
            if (definition == null)
                return Array.Empty<ActivityParticipant>();

            return _participationDirectory?.Resolve(
                    _playerId,
                    anchorId,
                    definition.ActivityId,
                    definition.ParticipationMode,
                    targetId)
                ?? new[] { new ActivityParticipant(_playerId, isOwner: true) };
        }

        private static bool ParticipantsMatch(
            IReadOnlyList<ActivityParticipant> left,
            IReadOnlyList<ActivityParticipant> right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Count != right.Count)
                return false;

            for (var index = 0; index < left.Count; index++)
            {
                if (left[index].PlayerId != right[index].PlayerId
                    || left[index].IsOwner != right[index].IsOwner)
                {
                    return false;
                }
            }
            return true;
        }

        private void PublishMirroredFact(ActivityFactDto fact)
        {
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            switch (fact.Kind)
            {
                case ActivityNetworkFactKind.SessionStarted:
                    _events.Publish(new ActivitySessionStarted(
                        fact.PlayerId,
                        fact.AnchorId,
                        fact.ActivityId,
                        fact.ParticipationMode,
                        fact.SessionRevision,
                        fact.TargetId,
                        fact.TargetKind,
                        fact.Metadata));
                    break;
                case ActivityNetworkFactKind.InteractionOccurred:
                case ActivityNetworkFactKind.SocialInteractionOccurred:
                    _events.Publish(new ActivityInteractionOccurred(
                        fact.PlayerId,
                        fact.AnchorId,
                        fact.ActivityId,
                        fact.ActionId,
                        fact.Payload,
                        fact.SessionRevision,
                        fact.EndsSession,
                        fact.EndReason,
                        fact.Reason,
                        fact.TargetKind,
                        fact.TargetId,
                        fact.Metadata,
                        isSessionBound: fact.Kind
                            == ActivityNetworkFactKind.InteractionOccurred));
                    break;
                case ActivityNetworkFactKind.SessionEnded:
                    _events.Publish(new ActivitySessionEnded(
                        fact.PlayerId,
                        fact.AnchorId,
                        fact.ActivityId,
                        fact.SessionRevision,
                        fact.EndReason,
                        fact.Metadata));
                    break;
                case ActivityNetworkFactKind.StateChanged:
                    _events.Publish(new ActivityStateChanged(
                        fact.PlayerId,
                        fact.AnchorId,
                        fact.ActivityId,
                        fact.SessionRevision,
                        fact.StateRevision,
                        fact.Payload,
                        fact.Metadata));
                    break;
            }
        }

        private void PublishAuthorityPresentation(ActivityPlayerRequest request)
        {
            if (_events == null
                || request.PlayerId != _playerId
                || string.IsNullOrWhiteSpace(request.ActivityId)
                || request.SessionRevision == 0)
            {
                return;
            }

            _events.Publish(new ActivityPlayerPresentationRequested(request));
        }

        private ActivityFactMetadata CreateFactMetadata(string eventId = null)
        {
            var revision = ++_nextFactRevision;
            if (revision == 0)
                revision = ++_nextFactRevision;
            return ActivityFactMetadata.Create(_playerId, revision, eventId);
        }

        private ActivityFactMetadata CreateFactMetadata(
            string actorId,
            string eventId)
        {
            var revision = ++_nextFactRevision;
            if (revision == 0)
                revision = ++_nextFactRevision;
            return ActivityFactMetadata.Create(actorId, revision, eventId);
        }

        private bool AcceptAppliedFactRevision(ActivityFactDto fact)
        {
            // Continuous StateChanged snapshots use an unreliable live lane
            // and may overtake an older reliable interaction. Their own
            // StateRevision orders them; only the reliable fact lane advances
            // the cross-fact revision watermark.
            if (fact.Kind != ActivityNetworkFactKind.StateChanged)
                _lastAppliedFactRevision = fact.Metadata.FactRevision;
            return true;
        }

        private string CreateActionEventId()
        {
            var revision = ++_nextFactRevision;
            if (revision == 0)
                revision = ++_nextFactRevision;
            return $"activity.action:{_eventRunId}:{revision}";
        }

        private bool ValidateActionTarget(
            ActivityTargetKind targetKind,
            string targetId,
            out string reason)
        {
            if (!IsTargetShapeValid(targetKind, targetId))
            {
                reason = "Activity action target shape is invalid";
                return false;
            }
            if (targetKind == ActivityTargetKind.None)
            {
                reason = string.Empty;
                return true;
            }
            if (targetKind != ActivityTargetKind.Player)
            {
                // Place/instance targets are valid protocol values. Their
                // concrete authority rules are supplied by the owning logic.
                reason = string.Empty;
                return true;
            }
            if (targetId == _playerId)
            {
                reason = "Activity action cannot target the acting Player";
                return false;
            }
            if (_targetDirectory == null
                || !_targetDirectory.TryResolve(targetId, out var target)
                || target == null
                || !target.IsAvailable
                || !target.HasTag("player"))
            {
                reason = "Activity action target Player is unavailable";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static bool IsTargetShapeValid(
            ActivityTargetKind targetKind,
            string targetId)
        {
            if (!Enum.IsDefined(typeof(ActivityTargetKind), targetKind))
                return false;
            return targetKind == ActivityTargetKind.None
                ? string.IsNullOrWhiteSpace(targetId)
                : !string.IsNullOrWhiteSpace(targetId);
        }

        private static bool IsValidStableText(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= 128;

        private sealed class AuthorityPresentationRelay : IActivityPresentationRequests
        {
            private readonly PlayerActivityHost _owner;

            public AuthorityPresentationRelay(PlayerActivityHost owner)
            {
                _owner = owner;
            }

            private IActivityPresentationRequests LocalTarget =>
                _owner._isLocalPlayer ? _owner._localPresentation : null;

            public bool RequestUi(ActivityUiRequest request) =>
                LocalTarget?.RequestUi(request) ?? true;

            public bool RequestCamera(ActivityCameraRequest request) =>
                LocalTarget?.RequestCamera(request) ?? true;

            public bool RequestPlayer(ActivityPlayerRequest request)
            {
                _owner.PublishAuthorityPresentation(request);
                return LocalTarget?.RequestPlayer(request) ?? true;
            }
        }
    }
}
