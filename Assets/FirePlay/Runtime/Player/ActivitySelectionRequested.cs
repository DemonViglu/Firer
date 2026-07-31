namespace DemonViglu.FirePlay.Player
{
    /// <summary>活动轮盘、通用 Form 与未来网络输入共用的活动选择语义。</summary>
    public sealed class ActivitySelectionRequested : IGameEvent
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }

        public ActivitySelectionRequested(string playerId, string anchorId, string activityId)
        {
            PlayerId = playerId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
        }
    }
}
