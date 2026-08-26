using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DemonViglu.FirePlay.Network
{
    public struct FirePlayFlameSourceSnapshot : INetworkSerializable, IEquatable<FirePlayFlameSourceSnapshot>
    {
        public FixedString128Bytes SourceId;
        public bool IsAvailable;
        public uint CommandVersion;

        public FirePlayFlameSourceSnapshot(FlameSource source)
        {
            SourceId = source.SourceId ?? string.Empty;
            IsAvailable = source.IsAvailable;
            CommandVersion = source.CommandVersion;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref SourceId);
            serializer.SerializeValue(ref IsAvailable);
            serializer.SerializeValue(ref CommandVersion);
        }

        public bool Equals(FirePlayFlameSourceSnapshot other) =>
            SourceId.Equals(other.SourceId)
            && IsAvailable == other.IsAvailable
            && CommandVersion == other.CommandVersion;

        public override bool Equals(object obj) => obj is FirePlayFlameSourceSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(SourceId, IsAvailable, CommandVersion);
    }

    public struct FirePlayAuthoredCampfireSnapshot : INetworkSerializable, IEquatable<FirePlayAuthoredCampfireSnapshot>
    {
        public FixedString128Bytes CampfireId;
        public FirePlayCampfireSnapshot State;

        public FirePlayAuthoredCampfireSnapshot(Campfire campfire)
        {
            CampfireId = campfire.CampfireId ?? string.Empty;
            State = new FirePlayCampfireSnapshot(campfire);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CampfireId);
            serializer.SerializeValue(ref State);
        }

        public bool Equals(FirePlayAuthoredCampfireSnapshot other) =>
            CampfireId.Equals(other.CampfireId) && State.Equals(other.State);

        public override bool Equals(object obj) => obj is FirePlayAuthoredCampfireSnapshot other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CampfireId, State);
    }

    /// <summary>
    /// Scene-level network boundary for authored FlameSources and Campfires. One
    /// visible scene component synchronizes stable authored objects; individual
    /// scene props remain small domain objects and do not each require a NetworkObject.
    /// Runtime-created Campfires keep using FirePlayNetworkCampfire.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public sealed class FirePlayNetworkWorldState : NetworkBehaviour
    {
        [SerializeField, Min(0.1f)] private float _sourceDiscoveryInterval = 0.5f;

        private readonly HashSet<FlameSource> _subscribedSources = new();
        private readonly NetworkList<FirePlayFlameSourceSnapshot> _flameSources = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<FirePlayAuthoredCampfireSnapshot> _authoredCampfires = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private float _nextDiscoveryTime;
        private bool _mirrorInitialized;
        private bool _campfireEventsAttached;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _flameSources.OnListChanged += OnFlameSourceListChanged;
            _authoredCampfires.OnListChanged += OnAuthoredCampfireListChanged;
            RefreshSources();

            if (IsServer)
            {
                Campfire.StateChanged += OnAuthorityCampfireChanged;
                _campfireEventsAttached = true;
                ConfigureAllSources(simulateAuthority: true);
                ConfigureAllAuthoredCampfires(simulateAuthority: true);
                PublishAllAuthorityStates();
            }
            else
            {
                ConfigureAllSources(simulateAuthority: false);
                ConfigureAllAuthoredCampfires(simulateAuthority: false);
                ApplyAllMirrorStates(playCollectedFeedback: false);
                ApplyAllCampfireMirrorStates();
                _mirrorInitialized = true;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[FirePlayNetworkWorldState] Spawned: authority={IsServer}, " +
                $"flameSources={_flameSources.Count}, authoredCampfires={_authoredCampfires.Count}.",
                this);
#endif
        }

        public override void OnNetworkDespawn()
        {
            _flameSources.OnListChanged -= OnFlameSourceListChanged;
            _authoredCampfires.OnListChanged -= OnAuthoredCampfireListChanged;
            if (_campfireEventsAttached)
            {
                Campfire.StateChanged -= OnAuthorityCampfireChanged;
                _campfireEventsAttached = false;
            }
            UnsubscribeAll();
            _mirrorInitialized = false;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || Time.unscaledTime < _nextDiscoveryTime)
                return;

            _nextDiscoveryTime = Time.unscaledTime + _sourceDiscoveryInterval;
            RefreshSources();
            ConfigureAllSources(IsServer);
            ConfigureAllAuthoredCampfires(IsServer);
            if (IsServer)
                PublishAllAuthorityStates();
            else
            {
                ApplyAllMirrorStates(playCollectedFeedback: false);
                ApplyAllCampfireMirrorStates();
            }
        }

        private void RefreshSources()
        {
            if (!IsServer)
                return;

            var activeSet = new HashSet<FlameSource>();
            foreach (var source in FlameSource.ActiveInstances)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.SourceId))
                    continue;

                activeSet.Add(source);
                if (_subscribedSources.Add(source))
                    source.StateChanged += OnAuthoritySourceChanged;
            }

            var stale = new List<FlameSource>();
            foreach (var source in _subscribedSources)
            {
                if (source == null || !activeSet.Contains(source))
                    stale.Add(source);
            }
            foreach (var source in stale)
            {
                if (source != null)
                    source.StateChanged -= OnAuthoritySourceChanged;
                _subscribedSources.Remove(source);
            }
        }

        private void ConfigureAllSources(bool simulateAuthority)
        {
            foreach (var source in FlameSource.ActiveInstances)
            {
                if (source != null)
                    source.ConfigureSimulation(simulateAuthority);
            }
        }

        private static void ConfigureAllAuthoredCampfires(bool simulateAuthority)
        {
            foreach (var campfire in Campfire.ActiveInstances)
            {
                if (IsAuthoredCampfire(campfire))
                    campfire.ConfigureSimulation(simulateAuthority);
            }
        }

        private void PublishAllAuthorityStates()
        {
            if (!IsServer)
                return;

            var liveIds = new HashSet<FixedString128Bytes>();
            foreach (var source in _subscribedSources)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.SourceId))
                    continue;

                var snapshot = new FirePlayFlameSourceSnapshot(source);
                liveIds.Add(snapshot.SourceId);
                Upsert(snapshot);
            }

            for (var index = _flameSources.Count - 1; index >= 0; index--)
            {
                if (!liveIds.Contains(_flameSources[index].SourceId))
                    _flameSources.RemoveAt(index);
            }

            PublishAllAuthoredCampfireStates();
        }

        private void PublishAllAuthoredCampfireStates()
        {
            var liveIds = new HashSet<FixedString128Bytes>();
            foreach (var campfire in Campfire.ActiveInstances)
            {
                if (!IsAuthoredCampfire(campfire))
                    continue;

                var snapshot = new FirePlayAuthoredCampfireSnapshot(campfire);
                liveIds.Add(snapshot.CampfireId);
                Upsert(snapshot);
            }

            for (var index = _authoredCampfires.Count - 1; index >= 0; index--)
            {
                if (!liveIds.Contains(_authoredCampfires[index].CampfireId))
                    _authoredCampfires.RemoveAt(index);
            }
        }

        private void OnAuthoritySourceChanged(FlameSource source)
        {
            if (IsServer && source != null && !string.IsNullOrWhiteSpace(source.SourceId))
                Upsert(new FirePlayFlameSourceSnapshot(source));
        }

        private void Upsert(FirePlayFlameSourceSnapshot snapshot)
        {
            for (var index = 0; index < _flameSources.Count; index++)
            {
                if (!_flameSources[index].SourceId.Equals(snapshot.SourceId))
                    continue;
                if (!_flameSources[index].Equals(snapshot))
                    _flameSources[index] = snapshot;
                return;
            }
            _flameSources.Add(snapshot);
        }

        private void OnAuthorityCampfireChanged(Campfire campfire)
        {
            if (IsServer && IsAuthoredCampfire(campfire))
                Upsert(new FirePlayAuthoredCampfireSnapshot(campfire));
        }

        private void Upsert(FirePlayAuthoredCampfireSnapshot snapshot)
        {
            for (var index = 0; index < _authoredCampfires.Count; index++)
            {
                if (!_authoredCampfires[index].CampfireId.Equals(snapshot.CampfireId))
                    continue;
                if (!_authoredCampfires[index].Equals(snapshot))
                    _authoredCampfires[index] = snapshot;
                return;
            }
            _authoredCampfires.Add(snapshot);
        }

        private void OnFlameSourceListChanged(NetworkListEvent<FirePlayFlameSourceSnapshot> change)
        {
            if (IsServer)
                return;

            if (change.Type == NetworkListEvent<FirePlayFlameSourceSnapshot>.EventType.Add
                || change.Type == NetworkListEvent<FirePlayFlameSourceSnapshot>.EventType.Value)
            {
                ApplyMirrorState(change.Value, _mirrorInitialized);
                return;
            }

            ApplyAllMirrorStates(playCollectedFeedback: false);
        }

        private void OnAuthoredCampfireListChanged(NetworkListEvent<FirePlayAuthoredCampfireSnapshot> change)
        {
            if (IsServer)
                return;

            if (change.Type == NetworkListEvent<FirePlayAuthoredCampfireSnapshot>.EventType.Add
                || change.Type == NetworkListEvent<FirePlayAuthoredCampfireSnapshot>.EventType.Value)
            {
                ApplyCampfireMirrorState(change.Value);
                return;
            }

            ApplyAllCampfireMirrorStates();
        }

        private void ApplyAllMirrorStates(bool playCollectedFeedback)
        {
            foreach (var snapshot in _flameSources)
                ApplyMirrorState(snapshot, playCollectedFeedback);
        }

        private static void ApplyMirrorState(FirePlayFlameSourceSnapshot snapshot, bool playCollectedFeedback)
        {
            foreach (var source in FlameSource.ActiveInstances)
            {
                if (source != null && string.Equals(source.SourceId, snapshot.SourceId.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    source.ApplyNetworkSnapshot(snapshot.IsAvailable, snapshot.CommandVersion, playCollectedFeedback);
                    return;
                }
            }
        }

        private void ApplyAllCampfireMirrorStates()
        {
            foreach (var snapshot in _authoredCampfires)
                ApplyCampfireMirrorState(snapshot);
        }

        private static void ApplyCampfireMirrorState(FirePlayAuthoredCampfireSnapshot snapshot)
        {
            foreach (var campfire in Campfire.ActiveInstances)
            {
                if (!IsAuthoredCampfire(campfire)
                    || !string.Equals(
                        campfire.CampfireId,
                        snapshot.CampfireId.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var state = snapshot.State;
                campfire.ApplyNetworkSnapshot(
                    new DemonViglu.FirePlay.World.CampfireAuthoritySnapshot(
                        state.Level,
                        state.TotalContribution,
                        state.Warmth),
                    state.IsRuntimeCreated,
                    state.IsRetired,
                    state.SourceSmallFireId.ToString(),
                    state.CommandVersion);
                return;
            }
        }

        private static bool IsAuthoredCampfire(Campfire campfire) =>
            campfire != null
            && !campfire.IsRuntimeCreated
            && !string.IsNullOrWhiteSpace(campfire.CampfireId);

        private void UnsubscribeAll()
        {
            foreach (var source in _subscribedSources)
            {
                if (source != null)
                    source.StateChanged -= OnAuthoritySourceChanged;
            }
            _subscribedSources.Clear();
        }
    }
}
