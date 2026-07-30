using System;
using System.Collections.Generic;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>纯 C# 权威命令限频器；网络 Host 可复用同一规则，不依赖 MonoBehaviour。</summary>
    public sealed class WorldCommandRateLimiter
    {
        private readonly Dictionary<CommandKey, double> _lastAcceptedAt = new();

        public bool CanAccept(string playerId, PlayerIntentKind kind, string targetId, double now, double cooldownSeconds)
        {
            if (cooldownSeconds <= 0d) return true;
            var key = new CommandKey(playerId, kind, targetId);
            return !_lastAcceptedAt.TryGetValue(key, out var acceptedAt) || now - acceptedAt >= cooldownSeconds;
        }

        public void RecordAccepted(string playerId, PlayerIntentKind kind, string targetId, double now)
        {
            _lastAcceptedAt[new CommandKey(playerId, kind, targetId)] = now;
        }

        public void Clear() => _lastAcceptedAt.Clear();

        private readonly struct CommandKey : IEquatable<CommandKey>
        {
            private readonly string _playerId;
            private readonly PlayerIntentKind _kind;
            private readonly string _targetId;

            public CommandKey(string playerId, PlayerIntentKind kind, string targetId)
            {
                _playerId = playerId ?? string.Empty;
                _kind = kind;
                _targetId = targetId ?? string.Empty;
            }

            public bool Equals(CommandKey other) =>
                _kind == other._kind && _playerId == other._playerId && _targetId == other._targetId;

            public override bool Equals(object obj) => obj is CommandKey other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(_playerId, (int)_kind, _targetId);
        }
    }
}
