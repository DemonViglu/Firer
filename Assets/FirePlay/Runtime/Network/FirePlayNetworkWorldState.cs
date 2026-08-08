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

    /// <summary>
    /// Scene-level network boundary for authored FlameSources. One visible scene
    /// component synchronizes every stable source; individual collectibles remain
    /// small domain objects and do not each require a NetworkObject.
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
        private float _nextDiscoveryTime;
        private bool _mirrorInitialized;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _flameSources.OnListChanged += OnFlameSourceListChanged;
            RefreshSources();

            if (IsServer)
            {
                ConfigureAllSources(simulateAuthority: true);
                PublishAllAuthorityStates();
            }
            else
            {
                ConfigureAllSources(simulateAuthority: false);
                ApplyAllMirrorStates(playCollectedFeedback: false);
                _mirrorInitialized = true;
            }
        }

        public override void OnNetworkDespawn()
        {
            _flameSources.OnListChanged -= OnFlameSourceListChanged;
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
            if (IsServer)
                PublishAllAuthorityStates();
            else
                ApplyAllMirrorStates(playCollectedFeedback: false);
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
