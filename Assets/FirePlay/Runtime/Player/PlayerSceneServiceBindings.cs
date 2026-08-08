using System.Collections.Generic;
using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.CameraSystem;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.UI;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public interface IPlayerSceneServiceBindings
    {
        bool IsReady { get; }
        bool TryBind(PlayerActivityHost activityHost, PlayerActivityPresentationHost presentationHost);
        bool TryBindLocalPlayerCamera(PlayerCameraTargetSet targets);
        void ReleaseLocalPlayerCamera(PlayerCameraTargetSet targets);
        bool TryGetNetworkPlayerSpawnPose(
            ulong ownerClientId,
            out Vector3 position,
            out Quaternion rotation);
        bool TryEnsurePlayerFlame(string playerId, FlameModule flameModule, out FlameBrush flame);
        void ReleasePlayerFlame(string playerId);
    }

    /// <summary>
    /// Explicit scene-to-Player composition boundary. Runtime-spawned Player
    /// prefabs cannot serialize references to scene camera rigs or activity
    /// factories, so the scene owns those references and exposes them through
    /// GameInstanceSubsystem.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class PlayerSceneServiceBindings : MonoBehaviour, IPlayerSceneServiceBindings
    {
        [Header("Scene Activity Services")]
        [SerializeField] private ActivityLogicRegistryBehaviour _activityLogicFactory;
        [SerializeField] private ActivityCameraRigExecutor _activityCameraExecutor;
        [SerializeField] private MonoBehaviour _activityVfxExecutor;

        [Header("Player Flame Factory")]
        [SerializeField] private FlameBrush _playerFlamePrefab;

        [Header("Network Player Spawn")]
        [SerializeField] private Transform _networkPlayerSpawnPoint;
        [SerializeField, Min(0f)] private float _networkPlayerSpawnSpacing = 1.5f;

        private readonly Dictionary<string, FlameAllocation> _flamesByPlayerId = new();
        private PlayerCameraTargetSet _localCameraTargets;

        public bool IsReady => _activityLogicFactory != null
            && _activityCameraExecutor != null
            && _activityCameraExecutor.HasValidSetup
            && _playerFlamePrefab != null
            && _networkPlayerSpawnPoint != null;

        private void OnEnable()
        {
            var existing = GameInstanceSubsystem.TryGet<IPlayerSceneServiceBindings>();
            if (existing != null && !ReferenceEquals(existing, this))
            {
                Debug.LogError(
                    "[PlayerSceneServiceBindings] A scene may only register one Player scene-service binding.",
                    this);
                enabled = false;
                return;
            }

            GameInstanceSubsystem.Register<IPlayerSceneServiceBindings>(this);
        }

        private void Start()
        {
            if (!IsReady)
            {
                Debug.LogError(
                    "[PlayerSceneServiceBindings] Activity services, Player Flame prefab, or Network Player spawn point are not ready.",
                    this);
                return;
            }

            var currentTargets = LocalPlayerContext.Current?.CameraTargets;
            if (currentTargets != null && !TryBindLocalPlayerCamera(currentTargets))
            {
                Debug.LogError(
                    "[PlayerSceneServiceBindings] Could not bind the current scene Player camera targets.",
                    this);
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                "[PlayerSceneServiceBindings] Ready: runtime-spawned Players can bind Activity services and receive an owned Flame.",
                this);
#endif
        }

        private void OnDisable()
        {
            if (ReferenceEquals(GameInstanceSubsystem.TryGet<IPlayerSceneServiceBindings>(), this))
                GameInstanceSubsystem.Unregister<IPlayerSceneServiceBindings>();
        }

        public bool TryBind(
            PlayerActivityHost activityHost,
            PlayerActivityPresentationHost presentationHost)
        {
            if (!IsReady || activityHost == null || presentationHost == null)
                return false;

            if (!presentationHost.ConfigureSceneExecutors(
                    _activityCameraExecutor,
                    _activityVfxExecutor))
            {
                return false;
            }

            return activityHost.ConfigureSceneServices(
                _activityLogicFactory,
                presentationHost);
        }

        public bool TryBindLocalPlayerCamera(PlayerCameraTargetSet targets)
        {
            if (_activityCameraExecutor == null || targets == null)
                return false;

            if (_localCameraTargets != null && _localCameraTargets != targets)
                return false;

            if (!_activityCameraExecutor.BindLocalPlayerTargets(targets))
                return false;

            _localCameraTargets = targets;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[PlayerSceneServiceBindings] Bound local Player camera targets: {targets.gameObject.name}.",
                targets);
#endif
            return true;
        }

        public void ReleaseLocalPlayerCamera(PlayerCameraTargetSet targets)
        {
            if (_localCameraTargets == null || _localCameraTargets != targets)
                return;

            _activityCameraExecutor?.ReleaseLocalPlayerTargets(targets);
            _localCameraTargets = null;
        }

        public bool TryGetNetworkPlayerSpawnPose(
            ulong ownerClientId,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (_networkPlayerSpawnPoint == null)
                return false;

            position = _networkPlayerSpawnPoint.position;
            rotation = _networkPlayerSpawnPoint.rotation;
            if (ownerClientId == 0 || _networkPlayerSpawnSpacing <= 0f)
                return true;

            // The host occupies the authored point. Later clients receive a
            // deterministic ring offset so simultaneous spawns do not overlap.
            const ulong slotsPerRing = 8;
            var zeroBasedClientIndex = ownerClientId - 1;
            var ring = 1f + (float)(zeroBasedClientIndex / slotsPerRing);
            var slot = zeroBasedClientIndex % slotsPerRing;
            var angle = slot / (float)slotsPerRing * Mathf.PI * 2f;
            var offset = (_networkPlayerSpawnPoint.right * Mathf.Cos(angle)
                + _networkPlayerSpawnPoint.forward * Mathf.Sin(angle))
                * (_networkPlayerSpawnSpacing * ring);
            position += offset;
            return true;
        }

        public bool TryEnsurePlayerFlame(
            string playerId,
            FlameModule flameModule,
            out FlameBrush flame)
        {
            flame = null;
            if (string.IsNullOrWhiteSpace(playerId)
                || flameModule == null
                || flameModule.PlayerFlameController == null
                || flameModule.FlameAnchor == null
                || _playerFlamePrefab == null)
            {
                return false;
            }

            if (_flamesByPlayerId.TryGetValue(playerId, out var existing))
            {
                if (existing.Flame != null && existing.Owner == flameModule.PlayerFlameController)
                {
                    flame = existing.Flame;
                    return true;
                }

                Debug.LogError(
                    $"[PlayerSceneServiceBindings] Flame allocation collision for playerId={playerId}.",
                    this);
                return false;
            }

            // A scene-authored Player may already own a Flame. Never adopt it
            // into this factory, because this service must not destroy objects
            // it did not create when a network Player despawns.
            if (flameModule.PlayerFlameController.ActiveFlame != null)
            {
                flame = flameModule.PlayerFlameController.ActiveFlame;
                return true;
            }

            flame = Instantiate(
                _playerFlamePrefab,
                flameModule.FlameAnchor.position,
                Quaternion.identity);
            flame.name = $"Flame[{playerId}]";
            flame.SetFollowAnchor(flameModule.FlameAnchor);
            flameModule.PlayerFlameController.AddFlame(flame, makeActive: true);
            _flamesByPlayerId.Add(
                playerId,
                new FlameAllocation(flameModule.PlayerFlameController, flame));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[PlayerSceneServiceBindings] Allocated Flame for playerId={playerId}.",
                flame);
#endif
            return true;
        }

        public void ReleasePlayerFlame(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || !_flamesByPlayerId.Remove(playerId, out var allocation))
            {
                return;
            }

            if (allocation.Owner != null && allocation.Flame != null)
                allocation.Owner.RemoveFlame(allocation.Flame);
            if (allocation.Flame != null)
                Destroy(allocation.Flame.gameObject);
        }

        private readonly struct FlameAllocation
        {
            public FlameAllocation(PlayerFlameController owner, FlameBrush flame)
            {
                Owner = owner;
                Flame = flame;
            }

            public PlayerFlameController Owner { get; }
            public FlameBrush Flame { get; }
        }
    }
}
