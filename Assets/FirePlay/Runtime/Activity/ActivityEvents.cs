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

    /// <summary>
    /// Fact emitted after an ActivitySession has committed locally. A future
    /// realtime transport can serialize this event using stable IDs without
    /// depending on ActivitySession, Unity objects or a concrete activity type.
    /// </summary>
    public sealed class ActivitySessionStarted : IGameEvent
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public ActivityParticipationMode ParticipationMode { get; }
        public uint SessionRevision { get; }

        public ActivitySessionStarted(
            string playerId,
            string anchorId,
            string activityId,
            ActivityParticipationMode participationMode,
            uint sessionRevision)
        {
            PlayerId = playerId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            ParticipationMode = participationMode;
            SessionRevision = sessionRevision;
        }
    }

    /// <summary>
    /// Fact emitted after an active Session consumes an action. The payload is
    /// intentionally semantic so remote presentation and persistence can
    /// observe it without sharing the local Logic instance.
    /// </summary>
    public sealed class ActivityInteractionOccurred : IGameEvent
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public string ActionId { get; }
        public string Payload { get; }
        public uint SessionRevision { get; }
        public bool EndsSession { get; }
        public ActivityEndReason EndReason { get; }
        public string Reason { get; }

        public ActivityInteractionOccurred(
            string playerId,
            string anchorId,
            string activityId,
            string actionId,
            string payload,
            uint sessionRevision,
            bool endsSession,
            ActivityEndReason endReason,
            string reason)
        {
            PlayerId = playerId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Payload = payload ?? string.Empty;
            SessionRevision = sessionRevision;
            EndsSession = endsSession;
            EndReason = endReason;
            Reason = reason ?? string.Empty;
        }
    }

    /// <summary>
    /// Fact emitted once when a Session leaves the running set. It is the
    /// transport-neutral lifecycle boundary for future remote players.
    /// </summary>
    public sealed class ActivitySessionEnded : IGameEvent
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public uint SessionRevision { get; }
        public ActivityEndReason Reason { get; }

        public ActivitySessionEnded(
            string playerId,
            string anchorId,
            string activityId,
            uint sessionRevision,
            ActivityEndReason reason)
        {
            PlayerId = playerId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            SessionRevision = sessionRevision;
            Reason = reason;
        }
    }
}
