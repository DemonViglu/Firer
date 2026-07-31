namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Canonical cross-module request for starting an activity. Input,
    /// roulette and future network adapters publish this event; the player
    /// host resolves the anchor and performs the synchronous preflight.
    /// </summary>
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

    /// <summary>Canonical cross-module request for an action in an active session.</summary>
    public sealed class ActivityActionRequested : IGameEvent
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public string ActionId { get; }
        public string Payload { get; }

        public ActivityActionRequested(
            string playerId,
            string anchorId,
            string activityId,
            string actionId,
            string payload = null)
        {
            PlayerId = playerId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Payload = payload ?? string.Empty;
        }
    }
}
