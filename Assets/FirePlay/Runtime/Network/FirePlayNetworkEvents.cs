using System;
using Unity.Netcode;
using UnityEngine;

namespace DemonViglu.FirePlay.Network
{
    public struct FirePlayNetworkPoseSnapshot : INetworkSerializable, IEquatable<FirePlayNetworkPoseSnapshot>
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public uint Revision;

        public FirePlayNetworkPoseSnapshot(Vector3 position, Quaternion rotation, uint revision)
        {
            Position = position;
            Rotation = rotation;
            Revision = revision;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Position);
            serializer.SerializeValue(ref Rotation);
            serializer.SerializeValue(ref Revision);
        }

        public bool Equals(FirePlayNetworkPoseSnapshot other) =>
            Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation)
            && Revision == other.Revision;

        public override bool Equals(object obj) =>
            obj is FirePlayNetworkPoseSnapshot other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Position, Rotation, Revision);
    }

    public enum FirePlayNetworkMode
    {
        None,
        Host,
        Server,
        Client
    }

    public enum FirePlayNetworkState
    {
        Starting,
        Started,
        StartFailed,
        ClientConnected,
        ClientDisconnected,
        PeerConnected,
        PeerDisconnected,
        Stopped
    }

    /// <summary>
    /// Network bootstrap 对外发布的连接状态。只携带稳定的连接事实，
    /// 不把 NetworkManager 或 UnityTransport 引用泄漏到 gameplay 模块。
    /// </summary>
    public sealed class FirePlayNetworkStateChanged : IGameEvent
    {
        public FirePlayNetworkState State { get; }
        public FirePlayNetworkMode Mode { get; }
        public ulong ClientId { get; }
        public string Reason { get; }

        public FirePlayNetworkStateChanged(
            FirePlayNetworkState state,
            FirePlayNetworkMode mode,
            ulong clientId = 0,
            string reason = null)
        {
            State = state;
            Mode = mode;
            ClientId = clientId;
            Reason = reason ?? string.Empty;
        }
    }

    public sealed class FirePlayNetworkPlayerRoleChanged : IGameEvent
    {
        public string PlayerId { get; }
        public ulong OwnerClientId { get; }
        public bool IsLocallyOwned { get; }
        public bool HasLocalGameplayControl { get; }
        public bool Spawned { get; }

        public FirePlayNetworkPlayerRoleChanged(
            string playerId,
            ulong ownerClientId,
            bool isLocallyOwned,
            bool hasLocalGameplayControl,
            bool spawned)
        {
            PlayerId = playerId ?? string.Empty;
            OwnerClientId = ownerClientId;
            IsLocallyOwned = isLocallyOwned;
            HasLocalGameplayControl = hasLocalGameplayControl;
            Spawned = spawned;
        }
    }
}
