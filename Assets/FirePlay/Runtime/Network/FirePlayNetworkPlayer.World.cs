using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.Save;
using DemonViglu.FirePlay.World;
using Unity.Netcode;
using UnityEngine;

namespace DemonViglu.FirePlay.Network
{
    public sealed partial class FirePlayNetworkPlayer
    {
        private uint _nextAsyncWorldFactRevision;
        private readonly string _asyncWorldRunId = System.Guid.NewGuid().ToString("N");

        public SmallFirePlacementRequestResult RequestSmallFirePlacement(Vector3 requestedPoint)
        {
            var placement = _flameModule != null ? _flameModule.CampfirePlacement : null;
            if (!IsSpawned || !IsOwner || !HasLocalGameplayControl || placement == null)
                return SmallFirePlacementRequestResult.Reject("SmallFire placement transport is unavailable");
            if (!IsFinite(requestedPoint))
                return SmallFirePlacementRequestResult.Reject("Invalid placement coordinates");

            if (IsServer)
                return HandleAuthoritySmallFirePlacement(requestedPoint);

            SubmitSmallFirePlacementRpc(requestedPoint);
            return SmallFirePlacementRequestResult.Accept("Placement submitted to Host authority");
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitSmallFirePlacementRpc(Vector3 requestedPoint)
        {
            var result = HandleAuthoritySmallFirePlacement(requestedPoint);
            ReceiveSmallFirePlacementResultRpc(
                result.Accepted,
                result.Reason,
                RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(
            SendTo.NotServer,
            Delivery = RpcDelivery.Reliable,
            AllowTargetOverride = true)]
        private void ReceiveSmallFirePlacementResultRpc(
            bool accepted,
            string reason,
            RpcParams rpcParams = default)
        {
            if (IsServer || !IsOwner || (reason?.Length ?? 0) > MaximumActivityPayloadLength)
                return;

            _flameModule?.CampfirePlacement?.ApplyAuthorityPlacementResult(accepted, reason);
        }

        private SmallFirePlacementRequestResult HandleAuthoritySmallFirePlacement(Vector3 requestedPoint)
        {
            var placement = _flameModule != null ? _flameModule.CampfirePlacement : null;

            if (!IsServer || placement == null)
                return SmallFirePlacementRequestResult.Reject("SmallFire placement authority is unavailable");
            if (!IsFinite(requestedPoint))
                return SmallFirePlacementRequestResult.Reject("Invalid placement coordinates");
            if (!placement.TryPrepareAuthorityPlacement(
                    transform.position,
                    requestedPoint,
                    out var smallFire,
                    out var reason))
            {
                return SmallFirePlacementRequestResult.Reject(reason);
            }

            try
            {
                var networkObject = smallFire.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    smallFire.ReleaseAuthorityObject();
                    return SmallFirePlacementRequestResult.Reject("SmallFire prefab is missing NetworkObject");
                }

                networkObject.Spawn();
                if (!smallFire.TryAssignNetworkStableId(out reason)
                    || !placement.TryCommitPreparedPlacement(smallFire, out reason))
                {
                    smallFire.ReleaseAuthorityObject();
                    return SmallFirePlacementRequestResult.Reject(reason);
                }

                var resource = _flameModule.ResourceController;
                if (resource != null)
                    _fuelSnapshot.Value = resource.CurrentFuel;
                RecordAsyncSmallFirePlacement(
                    smallFire.GetComponent<StableSceneId>()?.Value);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log(
                    $"[FirePlayNetworkPlayer] Host placed SmallFire: player={PlayerId}, id={smallFire.GetComponent<DemonViglu.FirePlay.Core.StableSceneId>()?.Value}, position={smallFire.transform.position}, fuel={_fuelSnapshot.Value:0.00}.",
                    this);
#endif
                return SmallFirePlacementRequestResult.Accept("Placed");
            }
            catch (System.Exception exception)
            {
                if (smallFire != null)
                    smallFire.ReleaseAuthorityObject();
                Debug.LogException(exception, this);
                return SmallFirePlacementRequestResult.Reject("SmallFire network spawn failed");
            }
        }

        private void AttachWorldCommandEvents()
        {
            if (_worldCommandsAttached || !HasLocalGameplayControl)
                return;

            _worldEvents = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _worldEvents.Subscribe<PlayerIntentRequested>(OnOwnedWorldIntentRequested);
            _worldCommandsAttached = true;
        }

        private void DetachWorldCommandEvents()
        {
            if (!_worldCommandsAttached || _worldEvents == null)
                return;

            _worldEvents.Unsubscribe<PlayerIntentRequested>(OnOwnedWorldIntentRequested);
            _worldCommandsAttached = false;
            _worldEvents = null;
        }

        private void OnOwnedWorldIntentRequested(PlayerIntentRequested intent)
        {
            if (intent == null || intent.PlayerId != PlayerId || !IsNetworkWorldCommand(intent.Kind))
                return;

            if (IsServer)
            {
                if (!HandleAuthorityWorldCommand(
                    intent.Kind,
                    intent.TargetKind,
                    intent.TargetId,
                    intent.HasExpectedTargetVersion,
                    intent.ExpectedTargetVersion,
                    GetRequestedWorldCommandColor(intent),
                    out var reason))
                {
                    Debug.LogWarning(
                        $"[FirePlayNetworkPlayer] Host rejected local world command: kind={intent.Kind}, target={intent.TargetId}, reason={reason}",
                        this);
                }
                return;
            }

            SubmitWorldCommandRpc(
                (int)intent.Kind,
                (int)intent.TargetKind,
                intent.TargetId,
                intent.HasExpectedTargetVersion,
                intent.ExpectedTargetVersion,
                GetRequestedWorldCommandColor(intent));
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitWorldCommandRpc(
            int intentKind,
            int targetKind,
            string targetId,
            bool hasExpectedVersion,
            uint expectedVersion,
            Color selectedTreeColor)
        {
            var reason = string.Empty;
            var accepted = System.Enum.IsDefined(typeof(PlayerIntentKind), intentKind)
                && System.Enum.IsDefined(typeof(PlayerInteractTargetKind), targetKind)
                && HandleAuthorityWorldCommand(
                    (PlayerIntentKind)intentKind,
                    (PlayerInteractTargetKind)targetKind,
                    targetId,
                    hasExpectedVersion,
                    expectedVersion,
                    selectedTreeColor,
                    out reason);
            if (!accepted && string.IsNullOrWhiteSpace(reason))
                reason = "Malformed world command";

            ReceiveWorldCommandResultRpc(
                accepted,
                reason,
                RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));
        }

        [Rpc(
            SendTo.NotServer,
            Delivery = RpcDelivery.Reliable,
            AllowTargetOverride = true)]
        private void ReceiveWorldCommandResultRpc(
            bool accepted,
            string reason,
            RpcParams rpcParams = default)
        {
            if (IsServer || !IsOwner || (reason?.Length ?? 0) > MaximumActivityPayloadLength)
                return;
            if (!accepted)
                Debug.LogWarning($"[FirePlayNetworkPlayer] Host rejected world command: {reason}", this);
        }

        private bool HandleAuthorityWorldCommand(
            PlayerIntentKind intentKind,
            PlayerInteractTargetKind targetKind,
            string targetId,
            bool hasExpectedVersion,
            uint expectedVersion,
            Color selectedTreeColor,
            out string reason)
        {
            reason = string.Empty;
            if (!IsServer || !IsNetworkWorldCommand(intentKind))
            {
                reason = "World command authority is unavailable";
                return false;
            }
            if (_modeController != null && !_modeController.IsExploring)
            {
                reason = "Player is not exploring";
                return false;
            }
            if (!IsValidStableId(targetId) || targetKind == PlayerInteractTargetKind.None)
            {
                reason = "World command target is invalid";
                return false;
            }
            if (intentKind == PlayerIntentKind.ContributeWorldTree && !IsFinite(selectedTreeColor))
            {
                reason = "WorldTree color is invalid";
                return false;
            }

            _worldRegistry ??= GameInstanceSubsystem.GetOrCreate<IWorldObjectRegistry>(
                () => new StableIdWorldObjectRegistry());
            if (!TryResolveWorldTarget(targetKind, targetId, out var target) || target == null)
            {
                reason = "World command target is unavailable";
                return false;
            }
            if (!TargetKindMatches(targetKind, target))
            {
                reason = "World command target type mismatch";
                return false;
            }
            if (hasExpectedVersion
                && (!_worldRegistry.TryGetCommandVersion(targetId, out var currentVersion)
                    || currentVersion != expectedVersion))
            {
                reason = "World command target revision is stale";
                return false;
            }

            var activeFlame = _flameModule?.PlayerFlameController?.ActiveFlame;
            var interactionRadius = activeFlame != null ? activeFlame.InteractionRadius : 3f;
            // Discovery uses collider overlap, so authority must validate the
            // same authored interaction volume. Transform pivots are often at
            // the centre or base of large props (the WorldTree is the clearest
            // example) and can be several metres from a legitimately nearby
            // player even when its interaction collider is touching them.
            if (DistanceToWorldTarget(transform.position, target) > interactionRadius + 0.5f)
            {
                reason = "World command target is out of range";
                return false;

            }

            var now = Time.realtimeSinceStartupAsDouble;
            if (!_worldCommandRateLimiter.CanAccept(
                    PlayerId,
                    intentKind,
                    targetId,
                    now,
                    WorldCommandCooldownSeconds))
            {
                reason = "World command rate limited";
                return false;
            }

            var resource = _flameModule?.ResourceController;
            var accepted = ExecuteAuthorityWorldCommand(
                intentKind,
                targetKind,
                target,
                resource,
                activeFlame,
                selectedTreeColor,
                out reason);
            if (!accepted)
                return false;

            _worldCommandRateLimiter.RecordAccepted(PlayerId, intentKind, targetId, now);
            if (resource != null)
                _fuelSnapshot.Value = resource.CurrentFuel;
            RecordAsyncWorldFact(intentKind, targetKind, targetId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var appliedVersion = target is IWorldCommandVersioned versioned
                ? versioned.CommandVersion
                : 0u;
            Debug.Log(
                $"[FirePlayNetworkPlayer] Host applied world command: player={PlayerId}, " +
                $"kind={intentKind}, target={targetId}, revision={appliedVersion}, " +
                $"fuel={_fuelSnapshot.Value:0.00}.",
                this);
#endif
            return true;
        }

        private void RecordAsyncWorldFact(
            PlayerIntentKind intentKind,
            PlayerInteractTargetKind targetKind,
            string targetId)
        {
            var factKind = targetKind switch
            {
                PlayerInteractTargetKind.Campfire => FirePlayAsyncFactKind.Campfire,
                PlayerInteractTargetKind.SmallFire => FirePlayAsyncFactKind.SmallFire,
                PlayerInteractTargetKind.WorldTree => FirePlayAsyncFactKind.WorldTree,
                _ => (FirePlayAsyncFactKind?)null
            };
            if (!factKind.HasValue)
                return;

            var revision = ++_nextAsyncWorldFactRevision;
            if (revision == 0)
                revision = ++_nextAsyncWorldFactRevision;
            var metadata = ActivityFactMetadata.Create(
                PlayerId,
                revision,
                $"world:{_asyncWorldRunId}:{revision}");
            var target = targetKind switch
            {
                PlayerInteractTargetKind.Campfire => new ActivityTargetReference(
                    ActivityTargetKind.Instance,
                    targetId),
                PlayerInteractTargetKind.SmallFire => new ActivityTargetReference(
                    ActivityTargetKind.Instance,
                    targetId),
                PlayerInteractTargetKind.WorldTree => new ActivityTargetReference(
                    ActivityTargetKind.Instance,
                    targetId),
                _ => default
            };
            var store = GameInstanceSubsystem.GetOrCreate<IAsyncInteractionFactStore>(
                () => new LocalAsyncInteractionFactStore());
            store.AppendWorld(
                factKind.Value,
                metadata,
                target,
                intentKind.ToString(),
                string.Empty,
                out _);
        }

        private void RecordAsyncSmallFirePlacement(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
                return;
            var revision = ++_nextAsyncWorldFactRevision;
            if (revision == 0)
                revision = ++_nextAsyncWorldFactRevision;
            var metadata = ActivityFactMetadata.Create(
                PlayerId,
                revision,
                $"smallfire:{_asyncWorldRunId}:{revision}");
            var store = GameInstanceSubsystem.GetOrCreate<IAsyncInteractionFactStore>(
                () => new LocalAsyncInteractionFactStore());
            store.AppendWorld(
                FirePlayAsyncFactKind.SmallFire,
                metadata,
                ActivityTargetReference.Instance(stableId),
                PlayerIntentKind.PlaceFire.ToString(),
                string.Empty,
                out _);
        }

        private bool ExecuteAuthorityWorldCommand(
            PlayerIntentKind intentKind,
            PlayerInteractTargetKind targetKind,
            Component target,
            DemonViglu.FirePlay.Flame.FlameResourceController resource,
            DemonViglu.FirePlay.Flame.FlameBrush activeFlame,
            Color selectedTreeColor,
            out string reason)
        {
            reason = string.Empty;
            if (intentKind == PlayerIntentKind.Interact)
            {
                intentKind = targetKind switch
                {
                    PlayerInteractTargetKind.Campfire => PlayerIntentKind.TendFire,
                    PlayerInteractTargetKind.SmallFire => PlayerIntentKind.StartPublicFire,
                    PlayerInteractTargetKind.FlameSource => PlayerIntentKind.GatherEmber,
                    PlayerInteractTargetKind.WorldTree => PlayerIntentKind.ContributeWorldTree,
                    _ => intentKind
                };
            }
            else if (intentKind == PlayerIntentKind.AddFire)
            {
                intentKind = targetKind == PlayerInteractTargetKind.Campfire
                    ? PlayerIntentKind.TendFire
                    : PlayerIntentKind.StartPublicFire;
            }
            else if (intentKind == PlayerIntentKind.LegacyWithdrawOrReclaim)
            {
                intentKind = targetKind == PlayerInteractTargetKind.Campfire
                    ? PlayerIntentKind.DrawFire
                    : PlayerIntentKind.ReclaimSmallFire;
            }

            var accepted = intentKind switch
            {
                PlayerIntentKind.TendFire when target is Campfire campfire => campfire.TryTend(resource),
                PlayerIntentKind.StartPublicFire when target is SmallFire smallFire =>
                    TryUpgradeSmallFireAuthority(smallFire, out reason),
                PlayerIntentKind.DrawFire when target is Campfire campfire =>
                    campfire.TryWithdrawEmergencyFuel(resource),
                PlayerIntentKind.ReclaimSmallFire when target is SmallFire smallFire =>
                    smallFire.TryReclaim(resource),
                PlayerIntentKind.GatherEmber when target is FlameSource source => source.TryRestore(resource),
                PlayerIntentKind.ContributeWorldTree when target is WorldTreeContribution tree =>
                    tree.TryContribute(PlayerId, resource, activeFlame, selectedTreeColor),
                _ => false
            };

            if (!accepted && string.IsNullOrWhiteSpace(reason))
            {
                reason = target switch
                {
                    Campfire campfire => campfire.LastUpgradeStatus,
                    WorldTreeContribution tree => tree.LastContributionStatus,
                    _ => "World command was rejected"
                };
            }
            else if (accepted && target is Campfire changedCampfire)
            {
                changedCampfire.GetComponent<FirePlayNetworkCampfire>()?.PublishAuthorityStateNow();
            }
            else if (accepted && target is WorldTreeContribution changedTree)
            {
                changedTree.GetComponent<FirePlayNetworkWorldTree>()?.PublishAuthorityStateNow();
            }

            return accepted;
        }

        private bool TryUpgradeSmallFireAuthority(SmallFire smallFire, out string reason)
        {
            var upgrade = _flameModule != null ? _flameModule.CampfireUpgrade : null;
            if (upgrade == null)
            {
                reason = "Campfire upgrade authority is unavailable";
                return false;
            }
            if (!upgrade.TryPrepareAuthorityUpgrade(smallFire, out var campfire, out reason))
                return false;

            try
            {
                var networkObject = campfire.GetComponent<NetworkObject>();
                if (networkObject == null)
                {
                    Destroy(campfire.gameObject);
                    reason = "Campfire prefab is missing NetworkObject";
                    return false;
                }

                networkObject.Spawn();
                if (!upgrade.TryCommitPreparedUpgrade(campfire, smallFire, out reason))
                {
                    networkObject.Despawn(true);
                    return false;
                }

                campfire.GetComponent<FirePlayNetworkCampfire>()?.PublishAuthorityStateNow();
                return true;
            }
            catch (System.Exception exception)
            {
                var networkObject = campfire != null ? campfire.GetComponent<NetworkObject>() : null;
                if (networkObject != null && networkObject.IsSpawned)
                    networkObject.Despawn(true);
                else if (campfire != null)
                    Destroy(campfire.gameObject);
                Debug.LogException(exception, this);
                reason = "Campfire network spawn failed";
                return false;
            }
        }

        private static bool IsNetworkWorldCommand(PlayerIntentKind kind) => kind is
            PlayerIntentKind.Interact
            or PlayerIntentKind.AddFire
            or PlayerIntentKind.TendFire
            or PlayerIntentKind.GatherEmber
            or PlayerIntentKind.StartPublicFire
            or PlayerIntentKind.DrawFire
            or PlayerIntentKind.ReclaimSmallFire
            or PlayerIntentKind.ContributeWorldTree
            or PlayerIntentKind.LegacyWithdrawOrReclaim;

        private static bool TargetKindMatches(PlayerInteractTargetKind kind, Component target) => kind switch
        {
            PlayerInteractTargetKind.Campfire => target is Campfire,
            PlayerInteractTargetKind.SmallFire => target is SmallFire,
            PlayerInteractTargetKind.FlameSource => target is FlameSource,
            PlayerInteractTargetKind.WorldTree => target is WorldTreeContribution,
            _ => false
        };

        private bool TryResolveWorldTarget(
            PlayerInteractTargetKind kind,
            string targetId,
            out Component target)
        {
            target = null;
            switch (kind)
            {
                case PlayerInteractTargetKind.Campfire:
                    if (_worldRegistry.TryResolve(targetId, out Campfire campfire))
                        target = campfire;
                    break;
                case PlayerInteractTargetKind.SmallFire:
                    if (_worldRegistry.TryResolve(targetId, out SmallFire smallFire))
                        target = smallFire;
                    break;
                case PlayerInteractTargetKind.FlameSource:
                    if (_worldRegistry.TryResolve(targetId, out FlameSource source))
                        target = source;
                    break;
                case PlayerInteractTargetKind.WorldTree:
                    if (_worldRegistry.TryResolve(targetId, out WorldTreeContribution tree))
                        target = tree;
                    break;
            }

            return target != null;
        }

        private static float DistanceToWorldTarget(Vector3 playerPosition, Component target)
        {
            if (target == null)
                return float.PositiveInfinity;

            var closestSqrDistance = float.PositiveInfinity;
            var colliders = target.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled)
                    continue;

                var closestPoint = collider.ClosestPoint(playerPosition);
                closestSqrDistance = Mathf.Min(
                    closestSqrDistance,
                    (closestPoint - playerPosition).sqrMagnitude);
            }

            return float.IsPositiveInfinity(closestSqrDistance)
                ? Vector3.Distance(playerPosition, target.transform.position)
                : Mathf.Sqrt(closestSqrDistance);
        }

        private Color GetRequestedWorldCommandColor(PlayerIntentRequested intent)
        {
            if (intent == null || intent.TargetKind != PlayerInteractTargetKind.WorldTree)
                return Color.white;


            var tree = _playerInteraction != null ? _playerInteraction.NearestWorldTree : null;
            if (tree != null)
            {
                var stableId = tree.GetComponent<DemonViglu.FirePlay.Core.StableSceneId>();
                if (stableId != null && stableId.Value == intent.TargetId)
                    return tree.SelectedLightColor;
            }

            return StableSceneId.TryFind(intent.TargetId, out var targetId)
                && targetId != null
                && targetId.TryGetComponent<WorldTreeContribution>(out var targetTree)
                    ? targetTree.SelectedLightColor
                    : Color.white;
        }

        private static bool IsFinite(Color value) =>
            IsFinite(value.r) && IsFinite(value.g) && IsFinite(value.b) && IsFinite(value.a);

    }
}
