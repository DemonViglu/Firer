
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.UI;
using DemonViglu.FirePlay.World;

namespace DemonViglu.FirePlay.Network
{
    /// <summary>
    /// The only NGO-facing component on a network Player. It resolves
    /// ownership, applies the local/remote gates to existing Player services,
    /// publishes a stable identity event, replicates host-authoritative fuel,
    /// and carries the server-validated owner pose stream. Feature logic
    /// remains in Player modules rather than moving into the networking layer.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class FirePlayNetworkPlayer : NetworkBehaviour, IActivityRequestTransport,
        IRestRequestTransport, ISmallFirePlacementRequestTransport
    {
        private const int MaximumStableIdLength = 128;
        private const int MaximumActivityPayloadLength = 512;
        [SerializeField] private string _playerIdPrefix = "network.player";
        [SerializeField] private LocalPlayerContext _playerContext;
        [SerializeField] private DemonViglu.FirePlay.Activity.PlayerActivityHost _activityHost;
        [SerializeField] private PlayerActivityPresentationHost _presentationHost;
        [SerializeField] private FlameModule _flameModule;
        [SerializeField] private RestInteraction _restInteraction;
        [SerializeField] private PlayerModeController _modeController;
        [SerializeField] private PlayerInteraction _playerInteraction;
        [SerializeField, Min(0.05f)] private float _fuelSnapshotInterval = 0.1f;
        [SerializeField, Min(0.001f)] private float _fuelSnapshotEpsilon = 0.05f;

        [Header("Network Pose")]
        [SerializeField, Min(0.02f)] private float _poseSendInterval = 0.05f;
        [SerializeField, Min(1f)] private float _maximumAcceptedSpeed = 12f;
        [SerializeField, Min(1f)] private float _poseValidationSlack = 1.5f;
        [SerializeField, Min(1f)] private float _remotePoseLerpSpeed = 15f;
        [SerializeField, Min(0.1f)] private float _remotePoseSnapDistance = 6f;
        [SerializeField, Min(0.1f)] private float _ownerCorrectionDistance = 2f;

        private IPlayerSceneServiceBindings _sceneBindings;
        private IActivityTargetDirectory _activityTargetDirectory;
        private IEventPublisher _activityEvents;
        private IEventPublisher _worldEvents;
        private IWorldObjectRegistry _worldRegistry;
        private bool _worldCommandsAttached;
        private readonly WorldCommandRateLimiter _worldCommandRateLimiter = new();
        private const double WorldCommandCooldownSeconds = 0.12d;
        private float _nextFuelSnapshotTime;
        private float _nextPoseSendTime;
        private float _lastAcceptedPoseTime;
        private bool _ownsLocalCameraBinding;
        private bool _activityFactsAttached;
        private readonly Dictionary<string, ActivityPlayerRequest> _activeObserverAnimationStates = new();
        private uint _nextOwnerPoseRevision;
        private uint _lastAcceptedOwnerPoseRevision;
        private uint _serverPoseRevision;
        private readonly NetworkVariable<float> _fuelSnapshot = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FirePlayNetworkPoseSnapshot> _poseSnapshot = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool> _restingSnapshot = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public string PlayerId { get; private set; } = string.Empty;
        public bool IsLocallyOwned { get; private set; }
        public bool HasLocalGameplayControl { get; private set; }
        public float NetworkFuel => _fuelSnapshot.Value;
        public FirePlayNetworkPoseSnapshot NetworkPose => _poseSnapshot.Value;
        public bool NetworkResting => _restingSnapshot.Value;

        private void Awake()
        {
            _playerContext ??= GetComponent<LocalPlayerContext>();
            _activityHost ??= GetComponentInChildren<DemonViglu.FirePlay.Activity.PlayerActivityHost>(true);
            _presentationHost ??= GetComponentInChildren<PlayerActivityPresentationHost>(true);
            _flameModule ??= GetComponentInChildren<FlameModule>(true);
            _restInteraction ??= GetComponentInChildren<RestInteraction>(true);
            _modeController ??= GetComponentInChildren<PlayerModeController>(true);
            _playerInteraction ??= GetComponentInChildren<PlayerInteraction>(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            IsLocallyOwned = IsOwner;
            PlayerId = $"{_playerIdPrefix}.{OwnerClientId}";
            var targetEvents = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _activityTargetDirectory =
                GameInstanceSubsystem.GetOrCreate<IActivityTargetDirectory>(
                    () => new ActivityTargetDirectory(targetEvents));
            _activityTargetDirectory.RegisterPlayer(PlayerId);
            _sceneBindings = GameInstanceSubsystem.TryGet<IPlayerSceneServiceBindings>();
            var spawnReady = true;
            if (IsServer)
            {
                var spawnPosition = default(Vector3);
                var spawnRotation = Quaternion.identity;
                spawnReady = _sceneBindings != null
                    && _sceneBindings.TryGetNetworkPlayerSpawnPose(
                        OwnerClientId,
                        out spawnPosition,
                        out spawnRotation);
                if (spawnReady)
                {
                    transform.SetPositionAndRotation(spawnPosition, spawnRotation);
                }
                else
                {
                    Debug.LogError(
                        $"[FirePlayNetworkPlayer] No explicit spawn pose is available for player={PlayerId}.",
                        this);
                }
            }

            _fuelSnapshot.OnValueChanged += OnFuelSnapshotChanged;
            _restingSnapshot.OnValueChanged += OnRestingSnapshotChanged;
            if (IsServer)
            {
                _lastAcceptedPoseTime = Time.unscaledTime;
                PublishPoseSnapshot(transform.position, transform.rotation);
                _restingSnapshot.Value = _restInteraction != null && _restInteraction.IsResting;
            }
            else
            {
                ApplyPoseImmediately(_poseSnapshot.Value);
            }
            var anotherLocalPlayer = _playerContext != null
                && LocalPlayerContext.Current != null
                && LocalPlayerContext.Current != _playerContext;
            var canBindSceneServices = spawnReady;

            if (IsLocallyOwned && !anotherLocalPlayer)
            {
                var cameraReady = _playerContext != null
                    && _playerContext.CameraTargets != null
                    && _sceneBindings != null
                    && _sceneBindings.TryBindLocalPlayerCamera(_playerContext.CameraTargets);
                _ownsLocalCameraBinding = cameraReady;
                canBindSceneServices &= cameraReady;
                if (!cameraReady)
                {
                    Debug.LogError(
                        "[FirePlayNetworkPlayer] Local Player could not bind required scene Camera services.",
                        this);
                }
            }

            if (_flameModule != null)
            {
                // Player flame resources are host-authoritative. Clients keep
                // a readable state shell and apply replicated fuel snapshots.
                _flameModule.ResourceController?.ConfigureSimulation(IsServer);
                if (IsServer && _flameModule.ResourceController != null)
                {
                    _fuelSnapshot.Value = _flameModule.ResourceController.CurrentFuel;
                    _nextFuelSnapshotTime = Time.unscaledTime + _fuelSnapshotInterval;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log(
                        $"[FirePlayNetworkPlayer] Fuel authority initialized: player={PlayerId}, fuel={_fuelSnapshot.Value:0.00}.",
                        this);
#endif
                }
                else if (!IsServer)
                {
                    _flameModule.ResourceController?.ApplyFuelSnapshot(_fuelSnapshot.Value);
                }

                _sceneBindings ??= GameInstanceSubsystem.TryGet<IPlayerSceneServiceBindings>();
                var flameReady = _sceneBindings != null
                    && _sceneBindings.TryEnsurePlayerFlame(PlayerId, _flameModule, out _);
                if (!flameReady)
                {
                    Debug.LogError(
                        $"[FirePlayNetworkPlayer] Could not allocate an owned Flame for player={PlayerId}.",
                        this);
                }

                // A locally controlled full Player cannot run Flame gameplay
                // without a player-owned Flame. Remote instances remain valid
                // presentation targets while their state synchronization lands.
                if (IsLocallyOwned && !anotherLocalPlayer)
                    canBindSceneServices &= flameReady;
            }

            // Every mirror may execute restricted Observer Animation/VFX.
            // Only the local owner is allowed to use the configured Camera.
            var needsActivityServices = _activityHost != null || _presentationHost != null;
            if (needsActivityServices
                && (_activityHost != null || _presentationHost != null))

            {
                _sceneBindings ??= GameInstanceSubsystem.TryGet<IPlayerSceneServiceBindings>();
                var activityReady = _sceneBindings != null
                    && _sceneBindings.TryBind(_activityHost, _presentationHost);
                if (IsLocallyOwned && !anotherLocalPlayer)
                    canBindSceneServices &= activityReady;
                if (!activityReady)
                {
                    Debug.LogError(
                        $"[FirePlayNetworkPlayer] Player could not bind required scene Activity services: player={PlayerId}, authority={IsServer}, localOwner={IsLocallyOwned}.",
                        this);
                }
            }

            HasLocalGameplayControl = IsLocallyOwned
                && !anotherLocalPlayer
                && canBindSceneServices;
            // Network Players have one world mutation path: this component's
            // Host-authoritative RPC boundary. The standalone complete Player
            // may keep its local executor outside a network session.
            _playerContext?.CommandExecutor?.ConfigureLocalExecution(false);
            _playerContext?.ConfigureNetworkRole(HasLocalGameplayControl, PlayerId);
            _flameModule?.CampfirePlacement?.ConfigureRequestTransport(
                HasLocalGameplayControl ? this : null);
            _restInteraction?.ConfigureNetworkAccess(
                IsServer && HasLocalGameplayControl,
                HasLocalGameplayControl ? this : null);
            _activityHost?.ConfigureNetworkRole(HasLocalGameplayControl, IsServer, PlayerId);
            _activityHost?.ConfigureRequestTransport(HasLocalGameplayControl ? this : null);
            AttachWorldCommandEvents();
            AttachRestAuthorityEvents();
            if (!IsServer && _restInteraction != null)
            {
                _restInteraction.ApplyConfirmedNetworkState(
                    _restingSnapshot.Value,
                    IsOwner && HasLocalGameplayControl);
            }
            AttachActivityFactEvents();
            if (!IsServer && _activityHost != null)
                RequestActivitySnapshotRpc();

            Publish(new FirePlayNetworkPlayerRoleChanged(
                PlayerId,
                OwnerClientId,
                IsLocallyOwned,
                HasLocalGameplayControl,
                spawned: true));

            Debug.Log(
                $"[FirePlayNetworkPlayer] Spawned player={PlayerId}, owner={OwnerClientId}, networkOwned={IsLocallyOwned}, localControl={HasLocalGameplayControl}, position={transform.position}",
                this);
        }

        public override void OnNetworkDespawn()
        {
            DetachActivityFactEvents();
            DetachWorldCommandEvents();
            _worldCommandRateLimiter.Clear();
            _activeObserverAnimationStates.Clear();
            _activityTargetDirectory?.Remove(PlayerId);
            _activityTargetDirectory = null;
            DetachRestAuthorityEvents();
            _fuelSnapshot.OnValueChanged -= OnFuelSnapshotChanged;
            _restingSnapshot.OnValueChanged -= OnRestingSnapshotChanged;

            Publish(new FirePlayNetworkPlayerRoleChanged(
                PlayerId,
                OwnerClientId,
                IsLocallyOwned,
                HasLocalGameplayControl,
                spawned: false));

            if (IsLocallyOwned)
            {
                _playerContext?.ConfigureNetworkRole(false, PlayerId);
            }
            _restInteraction?.ConfigureNetworkAccess(false, null);
            _flameModule?.CampfirePlacement?.ConfigureRequestTransport(null);
            if (!IsServer)
                _restInteraction?.ApplyConfirmedNetworkState(false, IsOwner);
            _activityHost?.ConfigureRequestTransport(null);
            _activityHost?.ConfigureNetworkRole(false, false, PlayerId);

            _sceneBindings?.ReleasePlayerFlame(PlayerId);
            if (_ownsLocalCameraBinding && _playerContext != null)
                _sceneBindings?.ReleaseLocalPlayerCamera(_playerContext.CameraTargets);
            _ownsLocalCameraBinding = false;
            _sceneBindings = null;
            _flameModule?.ResourceController?.ConfigureSimulation(false);

            HasLocalGameplayControl = false;

            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned)
                return;

            UpdatePoseReplication();

            if (!IsServer || Time.unscaledTime < _nextFuelSnapshotTime)
                return;

            _nextFuelSnapshotTime = Time.unscaledTime + _fuelSnapshotInterval;
            var resource = _flameModule != null ? _flameModule.ResourceController : null;
            if (resource == null || resource.State == null)
                return;

            var fuel = resource.CurrentFuel;
            if (Mathf.Abs(_fuelSnapshot.Value - fuel) >= _fuelSnapshotEpsilon)
                _fuelSnapshot.Value = fuel;
        }

        private void UpdatePoseReplication()
        {
            if (IsOwner && HasLocalGameplayControl)
            {
                if (Time.unscaledTime < _nextPoseSendTime)
                    return;

                _nextPoseSendTime = Time.unscaledTime + _poseSendInterval;
                var revision = ++_nextOwnerPoseRevision;
                if (IsServer)
                {
                    AcceptOwnerPose(transform.position, transform.rotation, revision);
                }
                else
                {
                    SubmitPoseRpc(transform.position, transform.rotation, revision);
                    var correction = _poseSnapshot.Value;
                    if (Vector3.Distance(transform.position, correction.Position) >= _ownerCorrectionDistance)
                        ApplyPoseImmediately(correction);
                }
                return;
            }

            if (!IsOwner)
                ApplyRemotePose(_poseSnapshot.Value);
        }

        public bool RequestRestToggle()
        {
            if (!IsSpawned || !IsOwner || !HasLocalGameplayControl || _restInteraction == null)
                return false;

            if (IsServer)
                return HandleAuthorityRestToggle();

            SubmitRestToggleRpc();
            return true;
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Reliable,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitRestToggleRpc()
        {
            if (!HandleAuthorityRestToggle())
            {
                Debug.LogWarning(
                    $"[FirePlayNetworkPlayer] Host rejected Rest toggle: player={PlayerId}, position={transform.position}.",
                    this);
            }
        }

        private bool HandleAuthorityRestToggle()
        {
            if (!IsServer || _restInteraction == null)
                return false;

            if (_restInteraction.IsResting)
            {
                _restInteraction.EndRest();
                return true;
            }

            // The Server chooses the RestSpot from its validated Player pose;
            // the Client never uploads a scene object or Anchor identity.
            return _restInteraction.TryBeginRest();
        }

        private void AttachRestAuthorityEvents()
        {
            if (!IsServer || _restInteraction == null)
                return;

            _restInteraction.RestStarted -= OnAuthorityRestStarted;
            _restInteraction.RestEnded -= OnAuthorityRestEnded;
            _restInteraction.RestStarted += OnAuthorityRestStarted;
            _restInteraction.RestEnded += OnAuthorityRestEnded;
        }

        private void DetachRestAuthorityEvents()
        {
            if (_restInteraction == null)
                return;

            _restInteraction.RestStarted -= OnAuthorityRestStarted;
            _restInteraction.RestEnded -= OnAuthorityRestEnded;
        }

        private void OnAuthorityRestStarted(RestSpot spot)
        {
            if (IsServer)
                _restingSnapshot.Value = true;
        }

        private void OnAuthorityRestEnded(RestSpot spot)
        {
            if (IsServer)
                _restingSnapshot.Value = false;
        }

        [Rpc(
            SendTo.Server,
            Delivery = RpcDelivery.Unreliable,
            InvokePermission = RpcInvokePermission.Owner)]
        private void SubmitPoseRpc(Vector3 position, Quaternion rotation, uint ownerRevision)
        {
            if (IsServer)
                AcceptOwnerPose(position, rotation, ownerRevision);
        }

        private void AcceptOwnerPose(Vector3 position, Quaternion rotation, uint ownerRevision)
        {
            if (!IsServer
                || ownerRevision <= _lastAcceptedOwnerPoseRevision
                || !IsFinite(position)
                || !IsFinite(rotation))
            {
                return;
            }


            var now = Time.unscaledTime;
            var elapsed = Mathf.Max(_poseSendInterval, now - _lastAcceptedPoseTime);
            var allowedDistance = _maximumAcceptedSpeed * elapsed * _poseValidationSlack + 0.25f;
            if (Vector3.Distance(transform.position, position) > allowedDistance)
            {
                // Advance the server snapshot revision even when rejecting so
                // the owner receives an explicit correction fact.
                PublishPoseSnapshot(transform.position, transform.rotation);
                return;
            }

            _lastAcceptedOwnerPoseRevision = ownerRevision;
            _lastAcceptedPoseTime = now;
            rotation = Quaternion.Normalize(rotation);
            transform.SetPositionAndRotation(position, rotation);
            PublishPoseSnapshot(position, rotation);
        }

        private void PublishPoseSnapshot(Vector3 position, Quaternion rotation)
        {
            if (!IsServer)
                return;

            _poseSnapshot.Value = new FirePlayNetworkPoseSnapshot(
                position,
                Quaternion.Normalize(rotation),
                ++_serverPoseRevision);
        }

        private void ApplyRemotePose(FirePlayNetworkPoseSnapshot snapshot)
        {
            if (snapshot.Revision == 0)
                return;

            if (Vector3.Distance(transform.position, snapshot.Position) >= _remotePoseSnapDistance)
            {
                ApplyPoseImmediately(snapshot);
                return;
            }

            var blend = 1f - Mathf.Exp(-_remotePoseLerpSpeed * Time.deltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, snapshot.Position, blend),
                Quaternion.Slerp(transform.rotation, snapshot.Rotation, blend));
        }

        private void ApplyPoseImmediately(FirePlayNetworkPoseSnapshot snapshot)
        {
            if (snapshot.Revision != 0 && IsFinite(snapshot.Position) && IsFinite(snapshot.Rotation))
                transform.SetPositionAndRotation(snapshot.Position, Quaternion.Normalize(snapshot.Rotation));
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            IsFinite(value.x)
            && IsFinite(value.y)
            && IsFinite(value.z)
            && IsFinite(value.w)
            && value.x * value.x + value.y * value.y + value.z * value.z + value.w * value.w > 0.0001f;

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnFuelSnapshotChanged(float previousFuel, float currentFuel)
        {
            if (!IsServer)
                _flameModule?.ResourceController?.ApplyFuelSnapshot(currentFuel);
        }

        private void OnRestingSnapshotChanged(bool previousResting, bool currentResting)
        {
            if (IsServer || _restInteraction == null)
                return;

            _restInteraction.ApplyConfirmedNetworkState(
                currentResting,
                IsOwner && HasLocalGameplayControl);
        }

        private static void Publish(FirePlayNetworkPlayerRoleChanged change)
        {
            GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus()).Publish(change);
        }
    }
}
