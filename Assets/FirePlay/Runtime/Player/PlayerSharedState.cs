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
}
