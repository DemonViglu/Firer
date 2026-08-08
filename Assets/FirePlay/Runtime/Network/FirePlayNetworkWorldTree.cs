using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Save;
using DemonViglu.FirePlay.World;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DemonViglu.FirePlay.Network
{
    public struct FirePlayWorldTreeSummary : INetworkSerializable, IEquatable<FirePlayWorldTreeSummary>
    {
        public float TotalContribution;
        public int ContributionCount;
        public uint CommandVersion;

        public FirePlayWorldTreeSummary(WorldTreeContribution tree)
        {
            TotalContribution = tree.TotalContribution;
            ContributionCount = tree.ContributionCount;
            CommandVersion = tree.CommandVersion;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TotalContribution);
            serializer.SerializeValue(ref ContributionCount);
            serializer.SerializeValue(ref CommandVersion);
        }

        public bool Equals(FirePlayWorldTreeSummary other) =>
            TotalContribution.Equals(other.TotalContribution)
            && ContributionCount == other.ContributionCount
            && CommandVersion == other.CommandVersion;

        public override bool Equals(object obj) => obj is FirePlayWorldTreeSummary other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(TotalContribution, ContributionCount, CommandVersion);
    }

    public struct FirePlayTreeLightEntry : INetworkSerializable, IEquatable<FirePlayTreeLightEntry>
    {
        public FixedString128Bytes PlayerId;
        public Color Color;
        public float PositionSeed;

        public FirePlayTreeLightEntry(TreePersonalLightRecord record)
        {
            PlayerId = record.playerId ?? string.Empty;
            Color = record.color;
            PositionSeed = record.positionSeed;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref PlayerId);
            serializer.SerializeValue(ref Color);
            serializer.SerializeValue(ref PositionSeed);
        }

        public bool Equals(FirePlayTreeLightEntry other) =>
            PlayerId.Equals(other.PlayerId)
            && Color.Equals(other.Color)
            && PositionSeed.Equals(other.PositionSeed);

        public override bool Equals(object obj) => obj is FirePlayTreeLightEntry other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(PlayerId, Color, PositionSeed);
    }

    /// <summary>
    /// Shared-state adapter for the authored WorldTree. The gameplay component
    /// owns contribution rules; this component mirrors summary and per-player lights.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject), typeof(WorldTreeContribution))]
    public sealed class FirePlayNetworkWorldTree : NetworkBehaviour
    {
        [SerializeField] private WorldTreeContribution _tree;
        [SerializeField, Min(0.1f)] private float _fallbackPollInterval = 0.5f;

        private float _nextPollTime;
        private readonly NetworkVariable<FirePlayWorldTreeSummary> _summary = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);
        private readonly NetworkList<FirePlayTreeLightEntry> _lights = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _tree ??= GetComponent<WorldTreeContribution>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _summary.OnValueChanged += OnSummaryChanged;
            _lights.OnListChanged += OnLightsChanged;
            if (IsServer)
            {
                _tree.ConfigureSimulation(true);
                _tree.Contributed += OnAuthorityContributed;
                PublishAuthorityStateNow();
            }
            else
            {
                _tree.ConfigureSimulation(false);
                ApplyMirror();
            }
        }

        public override void OnNetworkDespawn()
        {
            _summary.OnValueChanged -= OnSummaryChanged;
            _lights.OnListChanged -= OnLightsChanged;
            if (_tree != null)
                _tree.Contributed -= OnAuthorityContributed;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || Time.unscaledTime < _nextPollTime)
                return;
            _nextPollTime = Time.unscaledTime + _fallbackPollInterval;
            if (_tree != null && _summary.Value.CommandVersion != _tree.CommandVersion)
                PublishAuthorityStateNow();
        }

        public void PublishAuthorityStateNow()
        {
            if (!IsSpawned || !IsServer || _tree == null)
                return;

            _summary.Value = new FirePlayWorldTreeSummary(_tree);
            _lights.Clear();
            foreach (var record in _tree.PersonalLights)
            {
                if (record != null && !string.IsNullOrWhiteSpace(record.playerId))
                    _lights.Add(new FirePlayTreeLightEntry(record));
            }
        }

        private void OnAuthorityContributed(WorldTreeContribution tree, Color color)
        {
            PublishAuthorityStateNow();
        }

        private void OnSummaryChanged(FirePlayWorldTreeSummary previous, FirePlayWorldTreeSummary current)
        {
            if (!IsServer)
                ApplyMirror();
        }

        private void OnLightsChanged(NetworkListEvent<FirePlayTreeLightEntry> change)
        {
            if (!IsServer)
                ApplyMirror();
        }

        private void ApplyMirror()
        {
            if (_tree == null)
                return;

            var records = new List<TreePersonalLightRecord>(_lights.Count);
            foreach (var entry in _lights)
            {
                records.Add(new TreePersonalLightRecord
                {
                    playerId = entry.PlayerId.ToString(),
                    color = entry.Color,
                    positionSeed = entry.PositionSeed
                });
            }
            var summary = _summary.Value;
            _tree.ApplyNetworkSnapshot(
                summary.TotalContribution,
                summary.ContributionCount,
                records,
                summary.CommandVersion);
        }
    }
}
