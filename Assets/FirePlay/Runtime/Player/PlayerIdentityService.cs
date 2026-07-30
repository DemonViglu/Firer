using System;

namespace DemonViglu.FirePlay.Player
{
    public interface IPlayerIdentityService
    {
        string LocalPlayerId { get; }
        bool IsLocalPlayer(string playerId);
    }

    /// <summary>单机身份实现；联网时替换为 NGO ClientId／账号身份映射。</summary>
    public sealed class LocalPlayerIdentityService : IPlayerIdentityService
    {
        public string LocalPlayerId { get; }

        public LocalPlayerIdentityService(string playerId)
        {
            LocalPlayerId = string.IsNullOrWhiteSpace(playerId) ? "local.player" : playerId.Trim();
        }

        public bool IsLocalPlayer(string playerId)
        {
            return string.Equals(LocalPlayerId, playerId, StringComparison.Ordinal);
        }
    }
}
