namespace DemonViglu.FirePlay.Player
{
    /// <summary>活动自己的动作语义；不限制为 RitualPrimary／RitualSecondary。</summary>
    public sealed class ActivityActionRequested : IGameEvent
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public string ActionId { get; }
        public string Payload { get; }

        public ActivityActionRequested(string playerId, string anchorId, string activityId, string actionId, string payload = null)
        {
            PlayerId = playerId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Payload = payload ?? string.Empty;
        }
    }
}
