namespace DemonViglu.FirePlay.Activity
{
    public enum ActivityUiRequestKind
    {
        Open,
        Close
    }

    public enum ActivityCameraRequestKind
    {
        Enter,
        Exit
    }

    public enum ActivityPlayerRequestKind
    {
        MovementLock,
        LookTarget,
        AnimationState,
        AnimationCue,
        VfxCue
    }

    public readonly struct ActivityUiRequest
    {
        public ActivityUiRequestKind Kind { get; }
        public string PlayerId { get; }
        public string ActivityId { get; }
        public string UiPrefabKey { get; }
        public uint SessionRevision { get; }

        public ActivityUiRequest(
            ActivityUiRequestKind kind,
            string playerId,
            string activityId,
            string uiPrefabKey,
            uint sessionRevision)
        {
            Kind = kind;
            PlayerId = playerId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            UiPrefabKey = uiPrefabKey ?? string.Empty;
            SessionRevision = sessionRevision;
        }
    }

    public readonly struct ActivityCameraRequest
    {
        public ActivityCameraRequestKind Kind { get; }
        public string PlayerId { get; }
        public string ActivityId { get; }
        public string CameraProfileId { get; }
        public string AnchorId { get; }
        public uint SessionRevision { get; }

        public ActivityCameraRequest(
            ActivityCameraRequestKind kind,
            string playerId,
            string activityId,
            string cameraProfileId,
            string anchorId,
            uint sessionRevision)
        {
            Kind = kind;
            PlayerId = playerId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            CameraProfileId = cameraProfileId ?? string.Empty;
            AnchorId = anchorId ?? string.Empty;
            SessionRevision = sessionRevision;
        }
    }

    /// <summary>
    /// A semantic request for a Player capability. The activity supplies
    /// stable IDs and cues; the Player-side executor owns movement, look,
    /// Animator and VFX components.
    /// </summary>
    public readonly struct ActivityPlayerRequest
    {
        public ActivityPlayerRequestKind Kind { get; }
        public string PlayerId { get; }
        public string ActivityId { get; }
        public string TargetId { get; }
        public string CueId { get; }
        public bool Active { get; }
        public uint SessionRevision { get; }

        public ActivityPlayerRequest(
            ActivityPlayerRequestKind kind,
            string playerId,
            string activityId,
            string targetId,
            string cueId,
            bool active,
            uint sessionRevision)
        {
            Kind = kind;
            PlayerId = playerId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            CueId = cueId ?? string.Empty;
            Active = active;
            SessionRevision = sessionRevision;
        }
    }

    /// <summary>
    /// Player-side camera executor. Activities only submit requests; the
    /// concrete executor owns Cinemachine/Camera references.
    /// </summary>
    public interface IActivityCameraRequestExecutor
    {
        bool Execute(ActivityCameraRequest request);
    }

    public interface IActivityPlayerRequestExecutor
    {
        bool Execute(ActivityPlayerRequest request);
    }

    public interface IActivityVfxRequestExecutor
    {
        bool Execute(ActivityPlayerRequest request);
    }
}
