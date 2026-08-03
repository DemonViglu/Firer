using System;

namespace DemonViglu.FirePlay.Player
{
    [Serializable]
    public struct PlayerSharedStateSnapshot
    {
        public PlayerMode Mode;
        public string RitualStateId;
        public string RitualId;
        public uint Revision;

        public PlayerSharedStateSnapshot(PlayerMode mode, string ritualStateId, string ritualId, uint revision)
        {
            Mode = mode;
            RitualStateId = ritualStateId ?? string.Empty;
            RitualId = ritualId ?? string.Empty;
            Revision = revision;
        }
    }

    /// <summary>不依赖场景对象的玩家共享语义状态；本地输入与网络快照共用。</summary>
    public sealed class PlayerSharedState
    {
        public PlayerSharedStateSnapshot Snapshot { get; private set; } =
            new(PlayerMode.Exploring, string.Empty, string.Empty, 0);

        public bool Set(PlayerMode mode, string ritualStateId, string ritualId)
        {
            ritualStateId ??= string.Empty;
            ritualId ??= string.Empty;
            if (Snapshot.Mode == mode && Snapshot.RitualStateId == ritualStateId && Snapshot.RitualId == ritualId)
                return false;

            Snapshot = new PlayerSharedStateSnapshot(mode, ritualStateId, ritualId, Snapshot.Revision + 1);
            return true;
        }

        public bool Apply(PlayerSharedStateSnapshot snapshot)
        {
            if (snapshot.Revision < Snapshot.Revision) return false;
            snapshot.RitualStateId ??= string.Empty;
            snapshot.RitualId ??= string.Empty;
            Snapshot = snapshot;
            return true;
        }
    }

    /// <summary>
    /// Runtime owner for the player's shared semantic state. It is a plain C#
    /// service so the state lifecycle does not require a dedicated Unity
    /// MonoBehaviour. PlayerSharedStateAdapter remains as a compatibility
    /// facade while callers migrate to PlayerCoreHost.
    /// </summary>
    public sealed class PlayerSharedStateService
    {
        private PlayerModeController _mode;
        private PlayerAnimationController _animation;
        private LocalPlayerContext _context;

        public PlayerSharedState State { get; } = new();
        public PlayerSharedStateSnapshot Snapshot => State.Snapshot;
        public event Action<PlayerSharedStateSnapshot> Changed;

        public void Initialize(LocalPlayerContext context)
        {
            _context = context;
            _mode = context != null ? context.GetComponent<PlayerModeController>() : null;
            _animation = context != null ? context.Animation : null;
        }

        public void Tick()
        {
            if (_context == null || !_context.IsLocalPlayer) return;

            var mode = _mode != null ? _mode.CurrentMode : PlayerMode.Exploring;
            // Activity sessions have their own semantic/network path. This
            // shared snapshot mirrors Player mode and Resting animation state
            // until the realtime DTO replaces it.
            var ritualStateId = mode == PlayerMode.Resting ? PlayerAnimationStateIds.Resting : string.Empty;
            if (State.Set(mode, ritualStateId, string.Empty))
                Changed?.Invoke(State.Snapshot);
            _animation?.ApplySharedState(State.Snapshot);
        }

        public bool ApplyRemoteSnapshot(PlayerSharedStateSnapshot snapshot)
        {
            if (!State.Apply(snapshot)) return false;
            _animation?.ApplySharedState(State.Snapshot);
            Changed?.Invoke(State.Snapshot);
            return true;
        }
    }
}
