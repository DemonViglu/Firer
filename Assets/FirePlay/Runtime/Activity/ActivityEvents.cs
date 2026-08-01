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

    /// <summary>
    /// Transport-neutral request sent to the activity authority. These DTOs
    /// contain only stable IDs and semantic payloads; they never contain
    /// Unity objects, ActivityLogic instances or Session references.
    /// </summary>
    public readonly struct ActivitySelectionRequestDto
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(PlayerId)
            && !string.IsNullOrWhiteSpace(ActivityId);

        public ActivitySelectionRequestDto(string playerId, string anchorId, string activityId)
        {
            PlayerId = playerId?.Trim() ?? string.Empty;
            AnchorId = anchorId?.Trim() ?? string.Empty;
            ActivityId = activityId?.Trim() ?? string.Empty;
        }
    }

    /// <summary>Action requests carry the Session revision returned at start.</summary>
    public readonly struct ActivityActionRequestDto
    {
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public string ActionId { get; }
        public string Payload { get; }
        public uint SessionRevision { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(PlayerId)
            && !string.IsNullOrWhiteSpace(ActivityId)
            && !string.IsNullOrWhiteSpace(ActionId)
            && SessionRevision > 0;

        public ActivityActionRequestDto(
            string playerId,
            string anchorId,
            string activityId,
            string actionId,
            string payload,
            uint sessionRevision)
        {
            PlayerId = playerId?.Trim() ?? string.Empty;
            AnchorId = anchorId?.Trim() ?? string.Empty;
            ActivityId = activityId?.Trim() ?? string.Empty;
            ActionId = actionId?.Trim() ?? string.Empty;
            Payload = payload ?? string.Empty;
            SessionRevision = sessionRevision;
        }
    }

    /// <summary>
    /// The only authority entry point a future network adapter needs. Local
    /// EventBus requests and network requests use the same host implementation.
    /// </summary>
    public interface IActivityAuthority
    {
        ActivityStartResult HandleSelection(ActivitySelectionRequestDto request);
        ActivityActionResult HandleAction(ActivityActionRequestDto request);
    }

    public enum ActivityNetworkFactKind
    {
        SessionStarted,
        InteractionOccurred,
        SessionEnded
    }

    /// <summary>
    /// Serializable lifecycle fact. A transport can encode this struct as
    /// JSON, binary data or SDK-native messages without changing the domain.
    /// </summary>
    public readonly struct ActivityFactDto
    {
        public ActivityNetworkFactKind Kind { get; }
        public string PlayerId { get; }
        public string AnchorId { get; }
        public string ActivityId { get; }
        public ActivityParticipationMode ParticipationMode { get; }
        public string ActionId { get; }
        public string Payload { get; }
        public uint SessionRevision { get; }
        public bool EndsSession { get; }
        public ActivityEndReason EndReason { get; }
        public string Reason { get; }

        private ActivityFactDto(
            ActivityNetworkFactKind kind,
            string playerId,
            string anchorId,
            string activityId,
            ActivityParticipationMode participationMode,
            string actionId,
            string payload,
            uint sessionRevision,
            bool endsSession,
            ActivityEndReason endReason,
            string reason)
        {
            Kind = kind;
            PlayerId = playerId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            ParticipationMode = participationMode;
            ActionId = actionId ?? string.Empty;
            Payload = payload ?? string.Empty;
            SessionRevision = sessionRevision;
            EndsSession = endsSession;
            EndReason = endReason;
            Reason = reason ?? string.Empty;
        }

        public static ActivityFactDto From(ActivitySessionStarted fact) => fact == null
            ? default
            : new(
                ActivityNetworkFactKind.SessionStarted,
                fact.PlayerId,
                fact.AnchorId,
                fact.ActivityId,
                fact.ParticipationMode,
                string.Empty,
                string.Empty,
                fact.SessionRevision,
                false,
                ActivityEndReason.Requested,
                string.Empty);

        public static ActivityFactDto From(ActivityInteractionOccurred fact) => fact == null
            ? default
            : new(
                ActivityNetworkFactKind.InteractionOccurred,
                fact.PlayerId,
                fact.AnchorId,
                fact.ActivityId,
                ActivityParticipationMode.Independent,
                fact.ActionId,
                fact.Payload,
                fact.SessionRevision,
                fact.EndsSession,
                fact.EndReason,
                fact.Reason);

        public static ActivityFactDto From(ActivitySessionEnded fact) => fact == null
            ? default
            : new(
                ActivityNetworkFactKind.SessionEnded,
                fact.PlayerId,
                fact.AnchorId,
                fact.ActivityId,
                ActivityParticipationMode.Independent,
                string.Empty,
                string.Empty,
                fact.SessionRevision,
                true,
                fact.Reason,
                string.Empty);
    }

    public static class ActivityNetworkMapper
    {
        public static ActivitySelectionRequestDto ToDto(ActivitySelectionRequested request) => request == null
            ? default
            : new(request.PlayerId, request.AnchorId, request.ActivityId);

        public static ActivityActionRequestDto ToDto(ActivityActionRequested request, uint sessionRevision) => request == null
            ? default
            : new(
                request.PlayerId,
                request.AnchorId,
                request.ActivityId,
                request.ActionId,
                request.Payload,
                sessionRevision);
    }
}
