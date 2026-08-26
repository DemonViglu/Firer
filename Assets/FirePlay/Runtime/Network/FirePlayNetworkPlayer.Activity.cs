using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.Player;
using Unity.Netcode;
using UnityEngine;

namespace DemonViglu.FirePlay.Network
{
    public sealed partial class FirePlayNetworkPlayer
    {
        private readonly System.Collections.Generic.HashSet<string>
            _receivedActivityActionEventIds = new(System.StringComparer.Ordinal);
        private IEventPublisher _expressionEvents;
        private bool _expressionEventsAttached;
        private uint _lastAcceptedExpressionSequence;
        private const float PersistentActivityStateInterval = 0.5f;
        private ActivityFactDto _pendingPersistentActivityState;
        private float _nextPersistentActivityStateTime;

        public ActivityStartResult RequestSelection(ActivitySelectionRequestDto request)
        {
            if (!CanSubmitOwnedActivityRequest(
                    request.PlayerId,
                    request.AnchorId,
                    request.ActivityId,
                    out var reason))
                return ActivityStartResult.Reject(reason);
            if (!IsValidOptionalStableId(request.TargetId))
                return ActivityStartResult.Reject("Activity target contains an invalid stable ID");

            if (IsServer)
                return HandleAuthoritySelection(request.AnchorId, request.ActivityId, request.TargetId);

            SubmitActivitySelectionRpc(request.AnchorId, request.ActivityId, request.TargetId);
            return new ActivityStartResult(
                true,
                "Activity selection submitted to Host authority",
                rules: null,
                session: null);
        }

        public ActivityActionResult RequestAction(ActivityActionRequestDto request)
        {
            if (!CanSubmitOwnedActivityRequest(
                    request.PlayerId,
                    request.AnchorId,
                    request.ActivityId,
                    out var reason)
                || !IsValidStableId(request.ActionId)
                || request.SessionRevision == 0
                || !System.Enum.IsDefined(typeof(ActivityTargetKind), request.TargetKind)
                || !IsValidOptionalStableId(request.TargetId)
                || (request.TargetKind == ActivityTargetKind.None
                    ? !string.IsNullOrWhiteSpace(request.TargetId)
                    : string.IsNullOrWhiteSpace(request.TargetId))
                || !IsValidStableId(request.EventId)
                || (request.Payload?.Length ?? 0) > MaximumActivityPayloadLength)
            {
                return ActivityActionResult.Reject(string.IsNullOrWhiteSpace(reason)
                    ? "Activity action request is invalid"
                    : reason);
            }

            if (IsServer)
            {
                return HandleAuthorityAction(
                    request.AnchorId,
                    request.ActivityId,
                    request.ActionId,
                    request.Payload,
                    request.SessionRevision,
                    request.TargetKind,
                    request.TargetId,
                    request.EventId);
            }

            SubmitActivityActionRpc(
                request.AnchorId,
                request.ActivityId,
                request.ActionId,
                request.Payload,
                request.SessionRevision,
                (int)request.TargetKind,
                request.TargetId,
                request.EventId);
            return ActivityActionResult.Consume("Activity action submitted to Host authority");
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitActivitySelectionRpc(
            string anchorId,
            string activityId,
            string targetId)
        {
            var result = HandleAuthoritySelection(anchorId, activityId, targetId);
            if (!result.Success)
            {
                Debug.LogWarning(
                    $"[FirePlayNetworkPlayer] Host rejected activity selection: player={PlayerId}, anchor={anchorId}, activity={activityId}, target={targetId}, reason={result.Reason}",
                    this);
            }
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitActivityActionRpc(
            string anchorId,
            string activityId,
            string actionId,
            string payload,
            uint sessionRevision,
            int targetKind,
            string targetId,
            string eventId)
        {
            var result = HandleAuthorityAction(
                anchorId,
                activityId,
                actionId,
                payload,
                sessionRevision,
                (ActivityTargetKind)targetKind,
                targetId,
                eventId);
            ReceiveActivityActionResultRpc(
                result.Consumed,
                result.Reason,
                activityId,
                actionId,
                eventId,
                RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
            if (!result.Consumed)
            {
                Debug.LogWarning(
                    $"[FirePlayNetworkPlayer] Host rejected activity action: player={PlayerId}, activity={activityId}, action={actionId}, reason={result.Reason}",
                    this);
            }
        }

        [Rpc(
            SendTo.NotServer,
            Delivery = RpcDelivery.Reliable,
            AllowTargetOverride = true)]
        private void ReceiveActivityActionResultRpc(
            bool accepted,
            string reason,
            string activityId,
            string actionId,
            string eventId,
            RpcParams rpcParams = default)
        {
            if (IsServer
                || !IsOwner
                || !HasLocalGameplayControl
                || !IsValidStableId(activityId)
                || !IsValidStableId(actionId)
                || !IsValidStableId(eventId)
                || (reason?.Length ?? 0) > MaximumActivityPayloadLength)
            {
                return;
            }

            var result = new ActivityActionAuthorityResolved(
                PlayerId,
                activityId,
                actionId,
                eventId,
                accepted,
                reason);
            GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus()).Publish(result);
            if (!accepted)
            {
                Debug.LogWarning(
                    $"[FirePlayNetworkPlayer] Host rejected activity action: activity={activityId}, action={actionId}, reason={reason}",
                    this);
            }
        }

        private ActivityStartResult HandleAuthoritySelection(
            string anchorId,
            string activityId,
            string targetId)
        {
            if (!IsServer || _activityHost == null)
                return ActivityStartResult.Reject("Activity authority is unavailable");
            if (!IsValidOptionalStableId(anchorId)
                || !IsValidStableId(activityId)
                || !IsValidOptionalStableId(targetId))
                return ActivityStartResult.Reject("Activity selection contains an invalid stable ID");

            var result = _activityHost.HandleSelection(new ActivitySelectionRequestDto(
                PlayerId,
                anchorId,
                activityId,
                targetId));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkPlayer] Host authority selection: player={PlayerId}, anchor={anchorId}, activity={activityId}, target={targetId}, accepted={result.Success}, revision={result.Session?.Revision}, reason={result.Reason}",

                this);
#endif
            return result;
        }

        private ActivityActionResult HandleAuthorityAction(
            string anchorId,
            string activityId,
            string actionId,
            string payload,
            uint sessionRevision,
            ActivityTargetKind targetKind,
            string targetId,
            string eventId)
        {
            if (!IsServer || _activityHost == null)
                return ActivityActionResult.Reject("Activity authority is unavailable");
            if (!IsValidStableId(eventId))
                return ActivityActionResult.Reject("Activity action EventId is invalid");
            if (!_receivedActivityActionEventIds.Add(eventId))
                return ActivityActionResult.Reject("Activity action EventId was already handled");
            if (!IsValidOptionalStableId(anchorId)
                || !IsValidStableId(activityId)
                || !IsValidStableId(actionId)
                || sessionRevision == 0
                || !System.Enum.IsDefined(typeof(ActivityTargetKind), targetKind)
                || !IsValidOptionalStableId(targetId)
                || (targetKind == ActivityTargetKind.None
                    ? !string.IsNullOrWhiteSpace(targetId)
                    : string.IsNullOrWhiteSpace(targetId))
                || (payload?.Length ?? 0) > MaximumActivityPayloadLength)
            {
                return ActivityActionResult.Reject("Activity action contains invalid network data");
            }

            var result = _activityHost.HandleAction(new ActivityActionRequestDto(
                PlayerId,
                anchorId,
                activityId,
                actionId,
                payload,
                sessionRevision,
                targetKind,
                targetId,
                eventId));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkPlayer] Host authority action: player={PlayerId}, activity={activityId}, action={actionId}, revision={sessionRevision}, consumed={result.Consumed}, reason={result.Reason}",
                this);
#endif
            return result;
        }

        private void AttachActivityFactEvents()
        {
            if (_activityFactsAttached || !IsServer || _activityHost == null)
                return;

            _activityEvents = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _activityEvents.Subscribe<ActivitySessionStarted>(OnAuthoritySessionStarted);
            _activityEvents.Subscribe<ActivityInteractionOccurred>(OnAuthorityInteractionOccurred);
            _activityEvents.Subscribe<ActivitySessionEnded>(OnAuthoritySessionEnded);
            _activityEvents.Subscribe<ActivityStateChanged>(OnAuthorityStateChanged);
            _activityEvents.Subscribe<ActivityPlayerPresentationRequested>(OnAuthorityPresentationRequested);
            _activityFactsAttached = true;
        }

        private void AttachExpressionEvents()
        {
            if (_expressionEventsAttached
                || !IsLocallyOwned
                || !HasLocalGameplayControl)
            {
                return;
            }

            _expressionEvents = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(
                () => new GameEventBus());
            _expressionEvents.Subscribe<PlayerExpressionPlayed>(OnLocalExpressionPlayed);
            _expressionEventsAttached = true;
        }

        private void DetachExpressionEvents()
        {
            if (!_expressionEventsAttached || _expressionEvents == null)
                return;

            _expressionEvents.Unsubscribe<PlayerExpressionPlayed>(OnLocalExpressionPlayed);
            _expressionEventsAttached = false;
            _expressionEvents = null;
            _lastAcceptedExpressionSequence = 0;
        }

        private void OnLocalExpressionPlayed(PlayerExpressionPlayed expression)
        {
            if (expression == null
                || !IsLocallyOwned
                || !HasLocalGameplayControl
                || expression.PlayerId != PlayerId)
            {
                return;
            }

            if (IsServer)
            {
                HandleAuthorityExpression(
                    expression.ExpressionId,
                    expression.CueId,
                    expression.Sequence);
                return;
            }

            SubmitExpressionRpc(
                expression.ExpressionId,
                expression.CueId,
                expression.Sequence);
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitExpressionRpc(
            string expressionId,
            string cueId,
            uint sequence)
        {
            HandleAuthorityExpression(expressionId, cueId, sequence);
        }

        private bool HandleAuthorityExpression(
            string expressionId,
            string cueId,
            uint sequence)
        {
            if (!IsServer
                || !IsValidStableId(expressionId)
                || !IsValidStableId(cueId)
                || sequence == 0
                || sequence <= _lastAcceptedExpressionSequence
                || _modeController == null
                || !_modeController.IsExploring
                || !IsSupportedRemoteExpression(expressionId, cueId))
            {
                return false;
            }

            _lastAcceptedExpressionSequence = sequence;
            var request = new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationCue,
                PlayerId,
                EmoteActivityLogic.ActivityId,
                string.Empty,
                cueId,
                active: true,
                sessionRevision: 1u);
            BroadcastRemoteExpression(request);
            return true;
        }

        private void BroadcastRemoteExpression(ActivityPlayerRequest request)
        {
            // The local owner already played the expression. The Host still
            // executes it on its own observer mirror when this Player belongs
            // to a remote client, then sends the one-shot cue to every other
            // client. No UI, camera, movement lock, or late-join replay is
            // involved.
            if (!IsOwner)
                _presentationHost?.ExecuteObserver(request);

            if (!IsServer || NetworkManager == null)
                return;

            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == OwnerClientId)
                    continue;

                ReceiveActivityPresentationRpc(
                    (int)request.Kind,
                    request.ActivityId,
                    request.TargetId,
                    request.CueId,
                    request.Active,
                    request.SessionRevision,
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkPlayer] Remote expression cue broadcast: player={PlayerId}, cue={request.CueId}",
                this);
#endif
        }

        private static bool IsSupportedRemoteExpression(
            string expressionId,
            string cueId)
        {
            return (expressionId == "wave"
                    && cueId == PlayerAnimationCueIds.EmoteWave)
                || (expressionId == "thanks"
                    && cueId == PlayerAnimationCueIds.EmoteThanks);
        }

        private void DetachActivityFactEvents()
        {
            if (!_activityFactsAttached || _activityEvents == null)
                return;

            _activityEvents.Unsubscribe<ActivitySessionStarted>(OnAuthoritySessionStarted);
            _activityEvents.Unsubscribe<ActivityInteractionOccurred>(OnAuthorityInteractionOccurred);
            _activityEvents.Unsubscribe<ActivitySessionEnded>(OnAuthoritySessionEnded);
            _activityEvents.Unsubscribe<ActivityStateChanged>(OnAuthorityStateChanged);
            _activityEvents.Unsubscribe<ActivityPlayerPresentationRequested>(OnAuthorityPresentationRequested);
            _activityFactsAttached = false;
            _activityEvents = null;
        }

        private void OnAuthoritySessionStarted(ActivitySessionStarted fact)
        {
            if (fact?.PlayerId != PlayerId)
                return;

            _activeObserverAnimationStates.Clear();
            _pendingPersistentActivityState = default;
            SendActivityFactToClients(ActivityFactDto.From(fact));
        }

        private void OnAuthorityInteractionOccurred(ActivityInteractionOccurred fact)
        {
            if (fact?.PlayerId == PlayerId)
                SendActivityFactToClients(ActivityFactDto.From(fact));
        }

        private void OnAuthoritySessionEnded(ActivitySessionEnded fact)
        {
            if (fact?.PlayerId != PlayerId)
                return;

            SendActivityFactToClients(ActivityFactDto.From(fact));
            _activeObserverAnimationStates.Clear();
            _pendingPersistentActivityState = default;
        }

        private void OnAuthorityStateChanged(ActivityStateChanged fact)
        {
            if (fact?.PlayerId != PlayerId)
                return;

            var dto = ActivityFactDto.From(fact);
            SendTransientActivityStateToClients(dto);
            _pendingPersistentActivityState = dto;
            FlushPersistentActivityStateIfDue();
        }

        private void OnAuthorityPresentationRequested(ActivityPlayerPresentationRequested fact)
        {
            var request = fact?.Request ?? default;
            if (request.PlayerId != PlayerId || request.SessionRevision == 0)
                return;

            CacheAuthorityAnimationState(request);
            if (!IsOwner)
                _presentationHost?.ExecuteObserver(request);

            ReceiveActivityPresentationRpc(
                (int)request.Kind,
                request.ActivityId,
                request.TargetId,
                request.CueId,
                request.Active,
                request.SessionRevision);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkPlayer] Host presentation sent: player={PlayerId}, kind={request.Kind}, activity={request.ActivityId}, active={request.Active}, revision={request.SessionRevision}",
                this);
#endif
        }

        private void CacheAuthorityAnimationState(ActivityPlayerRequest request)
        {
            if (request.Kind != ActivityPlayerRequestKind.AnimationState
                || string.IsNullOrWhiteSpace(request.CueId))
            {
                return;
            }

            var key = $"{request.ActivityId}\n{request.CueId}";
            if (request.Active)
                _activeObserverAnimationStates[key] = request;
            else
                _activeObserverAnimationStates.Remove(key);
        }

        private void SendActivityFactToClients(ActivityFactDto fact)
        {
            if (!IsServer || fact.SessionRevision == 0)
                return;

            ReceiveActivityFactRpc(
                (int)fact.Kind,
                fact.Metadata.ActorId,
                fact.Metadata.EventId,
                fact.Metadata.OccurredAtUnixMs,
                fact.Metadata.FactRevision,
                fact.AnchorId,
                (int)fact.TargetKind,
                fact.TargetId,
                fact.ActivityId,
                (int)fact.ParticipationMode,
                fact.ActionId,
                fact.Payload,
                fact.SessionRevision,
                fact.StateRevision,
                fact.EndsSession,
                (int)fact.EndReason,
                fact.Reason);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkPlayer] Host fact broadcast: player={PlayerId}, kind={fact.Kind}, activity={fact.ActivityId}, revision={fact.SessionRevision}",
                this);
#endif
        }

        private void SendActivityFactToClient(ActivityFactDto fact, ulong clientId)
        {
            if (!IsServer || fact.SessionRevision == 0)
                return;

            ReceiveActivityFactRpc(
                (int)fact.Kind,
                fact.Metadata.ActorId,
                fact.Metadata.EventId,
                fact.Metadata.OccurredAtUnixMs,
                fact.Metadata.FactRevision,
                fact.AnchorId,
                (int)fact.TargetKind,
                fact.TargetId,
                fact.ActivityId,
                (int)fact.ParticipationMode,
                fact.ActionId,
                fact.Payload,
                fact.SessionRevision,
                fact.StateRevision,
                fact.EndsSession,
                (int)fact.EndReason,
                fact.Reason,
                RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// Continuously changing activity gauges are transient presentation
        /// snapshots. They use an unreliable stream so fishing/marshmallow
        /// motion cannot queue in front of lifecycle or social facts.
        /// </summary>
        private void SendTransientActivityStateToClients(ActivityFactDto fact)
        {
            if (!IsServer
                || fact.Kind != ActivityNetworkFactKind.StateChanged
                || fact.SessionRevision == 0
                || fact.StateRevision == 0)
            {
                return;
            }

            ReceiveActivityStateFactRpc(
                fact.Metadata.ActorId,
                fact.Metadata.EventId,
                fact.Metadata.OccurredAtUnixMs,
                fact.Metadata.FactRevision,
                fact.AnchorId,
                fact.ActivityId,
                fact.Payload,
                fact.SessionRevision,
                fact.StateRevision);
        }

        /// <summary>
        /// The newest transient state is also checkpointed reliably at a low
        /// rate. This guarantees a terminal gauge/state is eventually seen
        /// even if the last unreliable packet was dropped.
        /// </summary>
        private void FlushPersistentActivityStateIfDue()
        {
            if (!IsServer
                || _pendingPersistentActivityState.SessionRevision == 0
                || Time.unscaledTime < _nextPersistentActivityStateTime)
            {
                return;
            }

            SendActivityFactToClients(_pendingPersistentActivityState);
            _pendingPersistentActivityState = default;
            _nextPersistentActivityStateTime =
                Time.unscaledTime + PersistentActivityStateInterval;
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestActivitySnapshotRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || _activityHost == null)
                return;

            var requesterClientId = rpcParams.Receive.SenderClientId;
            if (requesterClientId == NetworkManager.ServerClientId
                || !NetworkManager.ConnectedClients.ContainsKey(requesterClientId)
                || !_activityHost.TryCaptureAuthorityNetworkSnapshot(
                    out var sessionFact,
                    out var stateFact))
            {
                return;
            }


            SendActivityFactToClient(sessionFact, requesterClientId);
            if (stateFact.SessionRevision > 0 && stateFact.StateRevision > 0)
                SendActivityFactToClient(stateFact, requesterClientId);
            foreach (var request in _activeObserverAnimationStates.Values)
            {
                if (request.ActivityId == sessionFact.ActivityId
                    && request.SessionRevision == sessionFact.SessionRevision)
                {
                    SendActivityPresentationToClient(request, requesterClientId);
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkPlayer] Late-join snapshot sent: player={PlayerId}, clientId={requesterClientId}, activity={sessionFact.ActivityId}, revision={sessionFact.SessionRevision}, stateRevision={stateFact.StateRevision}",
                this);
#endif
        }

        [Rpc(
            SendTo.NotServer,
            Delivery = RpcDelivery.Reliable,
            AllowTargetOverride = true)]
        private void ReceiveActivityFactRpc(
            int kind,
            string actorId,
            string eventId,
            long occurredAtUnixMs,
            uint factRevision,
            string anchorId,
            int targetKind,
            string targetId,
            string activityId,
            int participationMode,
            string actionId,
            string payload,
            uint sessionRevision,
            uint stateRevision,
            bool endsSession,
            int endReason,
            string reason,
            RpcParams rpcParams = default)
        {
            // A Host already owns authority Logic. Every pure Client keeps a
            // mirror for this NetworkObject: the owner uses UI/Camera while
            // remote observers expose state only to the object's Visuals.
            if (IsServer
                || _activityHost == null
                || (IsOwner && !HasLocalGameplayControl))
                return;
            if (!System.Enum.IsDefined(typeof(ActivityNetworkFactKind), kind)
                || !System.Enum.IsDefined(typeof(ActivityParticipationMode), participationMode)
                || !System.Enum.IsDefined(typeof(ActivityEndReason), endReason)
                || !System.Enum.IsDefined(typeof(ActivityTargetKind), targetKind)
                || !IsValidStableId(actorId)
                || !IsValidStableId(eventId)
                || occurredAtUnixMs <= 0
                || factRevision == 0
                || !IsValidOptionalStableId(anchorId)
                || !IsValidOptionalStableId(targetId)
                || ((ActivityTargetKind)targetKind == ActivityTargetKind.None
                    ? !string.IsNullOrWhiteSpace(targetId)
                    : string.IsNullOrWhiteSpace(targetId))
                || !IsValidStableId(activityId)
                || !IsValidOptionalStableId(actionId)
                || (payload?.Length ?? 0) > MaximumActivityPayloadLength
                || (reason?.Length ?? 0) > MaximumActivityPayloadLength
                || sessionRevision == 0)
            {
                Debug.LogWarning("[FirePlayNetworkPlayer] Client rejected malformed Activity fact.", this);
                return;
            }
            if ((ActivityNetworkFactKind)kind == ActivityNetworkFactKind.SessionStarted
                && ((ActivityParticipationMode)participationMode
                        == ActivityParticipationMode.TargetedInteraction)
                    != !string.IsNullOrWhiteSpace(targetId))
            {
                Debug.LogWarning("[FirePlayNetworkPlayer] Client rejected inconsistent Activity target fact.", this);
                return;
            }

            var fact = ActivityFactDto.FromTransport(
                (ActivityNetworkFactKind)kind,
                PlayerId,
                anchorId,
                (ActivityTargetKind)targetKind,
                targetId,
                activityId,
                (ActivityParticipationMode)participationMode,
                actionId,
                payload,
                sessionRevision,
                stateRevision,
                endsSession,
                (ActivityEndReason)endReason,
                reason,
                actorId,
                eventId,
                occurredAtUnixMs,
                factRevision);
            ApplyReceivedActivityFact(fact);
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void ReceiveActivityStateFactRpc(
            string actorId,
            string eventId,
            long occurredAtUnixMs,
            uint factRevision,
            string anchorId,
            string activityId,
            string payload,
            uint sessionRevision,
            uint stateRevision)
        {
            if (IsServer
                || _activityHost == null
                || (IsOwner && !HasLocalGameplayControl))
            {
                return;
            }
            if (!IsValidStableId(actorId)
                || !IsValidStableId(eventId)
                || occurredAtUnixMs <= 0
                || factRevision == 0
                || !IsValidOptionalStableId(anchorId)
                || !IsValidStableId(activityId)
                || (payload?.Length ?? 0) > MaximumActivityPayloadLength
                || sessionRevision == 0
                || stateRevision == 0)
            {
                return;
            }

            ApplyReceivedActivityFact(ActivityFactDto.FromTransport(
                ActivityNetworkFactKind.StateChanged,
                PlayerId,
                anchorId,
                ActivityTargetKind.None,
                string.Empty,
                activityId,
                ActivityParticipationMode.Independent,
                string.Empty,
                payload,
                sessionRevision,
                stateRevision,
                endsSession: false,
                ActivityEndReason.Requested,
                string.Empty,
                actorId,
                eventId,
                occurredAtUnixMs,
                factRevision));
        }

        private void ApplyReceivedActivityFact(ActivityFactDto fact)
        {
            var applied = _activityHost != null && _activityHost.ApplyNetworkFact(fact);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkPlayer] Client fact applied: player={PlayerId}, role={(IsOwner ? "Owner" : "Observer")}, kind={fact.Kind}, activity={fact.ActivityId}, revision={fact.SessionRevision}, applied={applied}",
                this);
#endif
        }

        private void SendActivityPresentationToClient(
            ActivityPlayerRequest request,
            ulong clientId)
        {
            if (!IsServer || request.SessionRevision == 0)
                return;

            ReceiveActivityPresentationRpc(
                (int)request.Kind,
                request.ActivityId,
                request.TargetId,
                request.CueId,
                request.Active,
                request.SessionRevision,
                RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }

        [Rpc(
            SendTo.NotServer,
            Delivery = RpcDelivery.Reliable,
            AllowTargetOverride = true)]
        private void ReceiveActivityPresentationRpc(
            int kind,
            string activityId,
            string targetId,
            string cueId,
            bool active,
            uint sessionRevision,
            RpcParams rpcParams = default)
        {
            if (IsServer || _activityHost == null || (IsOwner && !HasLocalGameplayControl))
                return;
            if (!System.Enum.IsDefined(typeof(ActivityPlayerRequestKind), kind)
                || !IsValidStableId(activityId)
                || !IsValidOptionalStableId(targetId)
                || !IsValidOptionalStableId(cueId)
                || sessionRevision == 0)
            {
                Debug.LogWarning("[FirePlayNetworkPlayer] Client rejected malformed Activity presentation fact.", this);
                return;
            }

            var request = new ActivityPlayerRequest(
                (ActivityPlayerRequestKind)kind,
                PlayerId,
                activityId,
                targetId,
                cueId,
                active,
                sessionRevision);
            var applied = IsOwner
                ? _activityHost.ApplyNetworkPresentation(request)
                : _activityHost.ApplyNetworkObserverPresentation(request);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkPlayer] Client presentation applied: player={PlayerId}, role={(IsOwner ? "Owner" : "Observer")}, kind={request.Kind}, activity={request.ActivityId}, active={request.Active}, revision={request.SessionRevision}, applied={applied}",
                this);
#endif
        }

        private bool CanSubmitOwnedActivityRequest(
            string playerId,
            string anchorId,
            string activityId,
            out string reason)
        {
            if (!IsSpawned || !IsOwner || !HasLocalGameplayControl)
            {
                reason = "Only the locally controlled network Player can submit activity requests";
                return false;
            }
            if (playerId != PlayerId)
            {
                reason = "Activity request PlayerId does not match network ownership";
                return false;
            }
            if (!IsValidOptionalStableId(anchorId) || !IsValidStableId(activityId))
            {
                reason = "Activity request contains an invalid stable ID";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool IsValidStableId(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumStableIdLength;

        private static bool IsValidOptionalStableId(string value) =>
            string.IsNullOrWhiteSpace(value) || value.Length <= MaximumStableIdLength;

    }
}
