using System;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DemonViglu.FirePlay.Network
{
    public struct FirePlayCampfireSnapshot : INetworkSerializable, IEquatable<FirePlayCampfireSnapshot>
    {
        public int Level;
        public float TotalContribution;
        public float Warmth;
        public uint CommandVersion;
        public bool IsRuntimeCreated;
        public bool IsRetired;
        public FixedString128Bytes SourceSmallFireId;

        public FirePlayCampfireSnapshot(Campfire campfire)
        {
            var state = campfire.CreateAuthoritySnapshot();
            Level = state.Level;
            TotalContribution = state.TotalContribution;
            Warmth = state.Warmth;
            CommandVersion = campfire.CommandVersion;
            IsRuntimeCreated = campfire.IsRuntimeCreated;
            IsRetired = campfire.IsRetired;
            SourceSmallFireId = campfire.SourceSmallFireId ?? string.Empty;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Level);
            serializer.SerializeValue(ref TotalContribution);
            serializer.SerializeValue(ref Warmth);
            serializer.SerializeValue(ref CommandVersion);
            serializer.SerializeValue(ref IsRuntimeCreated);
            serializer.SerializeValue(ref IsRetired);
            serializer.SerializeValue(ref SourceSmallFireId);
        }

        public bool Equals(FirePlayCampfireSnapshot other) =>
            Level == other.Level
            && TotalContribution.Equals(other.TotalContribution)
            && Warmth.Equals(other.Warmth)
            && CommandVersion == other.CommandVersion
            && IsRuntimeCreated == other.IsRuntimeCreated
            && IsRetired == other.IsRetired
            && SourceSmallFireId.Equals(other.SourceSmallFireId);

        public override bool Equals(object obj) => obj is FirePlayCampfireSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(
            Level,
            TotalContribution,
            Warmth,
            CommandVersion,
            IsRuntimeCreated,
            IsRetired,
            SourceSmallFireId);
    }

    /// <summary>
    /// Transport adapter for a shared Campfire. Campfire owns the gameplay
    /// state; this component only publishes Host snapshots and applies mirrors.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(Campfire), typeof(StableSceneId))]
    public sealed class FirePlayNetworkCampfire : NetworkBehaviour
    {
        [SerializeField] private Campfire _campfire;
        [SerializeField, Min(0.05f)] private float _snapshotInterval = 0.2f;
        [SerializeField, Min(0.001f)] private float _warmthEpsilon = 0.05f;

        private float _nextSnapshotTime;
        private readonly NetworkVariable<FixedString128Bytes> _stableId = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<FirePlayCampfireSnapshot> _snapshot = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _campfire ??= GetComponent<Campfire>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _stableId.OnValueChanged += OnStableIdChanged;
            _snapshot.OnValueChanged += OnSnapshotChanged;

            if (IsServer)
            {
                _campfire.ConfigureSimulation(true);
                _campfire.RetirementRequested += OnRetirementRequested;
                PublishIdentity();
                PublishSnapshot(force: true);
            }
            else
            {
                _campfire.ConfigureSimulation(false);
                ApplyStableId(_stableId.Value);
                ApplySnapshot(_snapshot.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            _stableId.OnValueChanged -= OnStableIdChanged;
            _snapshot.OnValueChanged -= OnSnapshotChanged;
            if (_campfire != null)
                _campfire.RetirementRequested -= OnRetirementRequested;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || Time.unscaledTime < _nextSnapshotTime)
                return;

            _nextSnapshotTime = Time.unscaledTime + _snapshotInterval;
            PublishSnapshot(force: false);
        }

        public void PublishAuthorityStateNow()
        {
            if (IsSpawned && IsServer)
                PublishSnapshot(force: true);
        }

        private void PublishIdentity()
        {
            var stableSceneId = GetComponent<StableSceneId>();
            if (stableSceneId == null || !stableSceneId.IsValid)
            {
                Debug.LogError("[FirePlayNetworkCampfire] Host Campfire requires a stable ID before spawn.", this);
                return;
            }

            _stableId.Value = stableSceneId.Value;
        }

        private void PublishSnapshot(bool force)
        {
            if (_campfire == null)
                return;

            var next = new FirePlayCampfireSnapshot(_campfire);
            var previous = _snapshot.Value;
            if (!force
                && next.Level == previous.Level
                && next.CommandVersion == previous.CommandVersion
                && next.IsRuntimeCreated == previous.IsRuntimeCreated
                && next.IsRetired == previous.IsRetired
                && next.SourceSmallFireId.Equals(previous.SourceSmallFireId)
                && Mathf.Abs(next.TotalContribution - previous.TotalContribution) < 0.001f
                && Mathf.Abs(next.Warmth - previous.Warmth) < _warmthEpsilon)
            {
                return;
            }

            _snapshot.Value = next;
        }

        private void OnStableIdChanged(FixedString128Bytes previous, FixedString128Bytes current)
        {
            if (!IsServer)
                ApplyStableId(current);
        }

        private void OnSnapshotChanged(FirePlayCampfireSnapshot previous, FirePlayCampfireSnapshot current)
        {
            if (!IsServer)
                ApplySnapshot(current);
        }

        private void ApplyStableId(FixedString128Bytes value)
        {
            if (value.IsEmpty)
                return;

            var text = value.ToString();
            var stableSceneId = GetComponent<StableSceneId>();
            if (stableSceneId == null || stableSceneId.Value == text)
                return;
            if (!stableSceneId.IsValid && stableSceneId.TryAssignRuntimeSpawnValue(text))
                return;

            Debug.LogError($"[FirePlayNetworkCampfire] Stable ID mismatch: local={stableSceneId.Value}, host={text}.", this);
        }

        private void ApplySnapshot(FirePlayCampfireSnapshot value)
        {
            if (_campfire == null)
                return;

            _campfire.ApplyNetworkSnapshot(
                new CampfireAuthoritySnapshot(value.Level, value.TotalContribution, value.Warmth),
                value.IsRuntimeCreated,
                value.IsRetired,
                value.SourceSmallFireId.ToString(),
                value.CommandVersion);
        }

        private void OnRetirementRequested(Campfire campfire)
        {
            if (IsServer && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }
    }
}
