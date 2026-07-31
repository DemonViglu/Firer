using System;

namespace DemonViglu.FirePlay.Player
{
    [Serializable]
    public readonly struct ActivitySessionSnapshot
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public string PresentationId { get; }
        public uint Revision { get; }
        public bool IsActive => !string.IsNullOrWhiteSpace(ActivityId);

        public ActivitySessionSnapshot(string playerId, string anchorId, string activityId, string presentationId, uint revision)
        {
            PlayerId = playerId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            PresentationId = presentationId ?? string.Empty;
            Revision = revision;
        }
    }

    /// <summary>玩家级活动事实状态；不引用 UI、输入设备或 Unity 场景对象。</summary>
    public sealed class ActivitySession
    {
        public ActivitySessionSnapshot Snapshot { get; private set; } =
            new(string.Empty, string.Empty, string.Empty, string.Empty, 0);

        public bool IsActive => Snapshot.IsActive;

        public bool TryBegin(string playerId, string anchorId, string activityId, string presentationId)
        {
            if (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(anchorId) || string.IsNullOrWhiteSpace(activityId))
                return false;
            if (IsActive) return false;

            Snapshot = new ActivitySessionSnapshot(playerId, anchorId, activityId, presentationId, Snapshot.Revision + 1);
            return true;
        }

        public bool TrySwitch(string playerId, string anchorId, string activityId, string presentationId)
        {
            if (!IsActive) return TryBegin(playerId, anchorId, activityId, presentationId);
            if (Snapshot.PlayerId != playerId || Snapshot.AnchorId != anchorId || string.IsNullOrWhiteSpace(activityId))
                return false;
            if (Snapshot.ActivityId == activityId && Snapshot.PresentationId == (presentationId ?? string.Empty))
                return true;

            Snapshot = new ActivitySessionSnapshot(playerId, anchorId, activityId, presentationId, Snapshot.Revision + 1);
            return true;
        }

        public bool End()
        {
            if (!IsActive) return false;

            Snapshot = new ActivitySessionSnapshot(
                Snapshot.PlayerId,
                string.Empty,
                string.Empty,
                string.Empty,
                Snapshot.Revision + 1);
            return true;
        }
    }
}
