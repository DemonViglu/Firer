namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Minimal Anywhere activity for emotes. It has no location, camera or
    /// dedicated UI requirement; an emote wheel can start the session and
    /// submit semantic cue actions while the Player executes the animation.
    /// </summary>
    public sealed class EmoteActivityLogic : IActivityLogic
    {
        public const string ActivityId = "emote";

        public string LastCueId { get; private set; } = string.Empty;
        public int PlayCount { get; private set; }

        public ActivityStartCheck CheckStart(IActivityContext context)
        {
            return context != null && context.ActivityId == ActivityId
                ? ActivityStartCheck.Allow()
                : ActivityStartCheck.Deny("Emote context is invalid");
        }

        public IActivityStartOperation PrepareStart(IActivityContext context)
        {
            return new ActivityStartOperation(
                () =>
                {
                    LastCueId = string.Empty;
                    PlayCount = 0;
                    return true;
                },
                () =>
                {
                    LastCueId = string.Empty;
                    PlayCount = 0;
                });
        }

        public ActivityActionResult HandleAction(IActivityContext context, ActivityActionRequest request)
        {
            if (context == null || context.ActivityId != ActivityId)
                return ActivityActionResult.Reject("Emote context is invalid");

            return request.ActionId switch
            {
                "emote.play" => Play(context, request),
                "activity.exit" => ActivityActionResult.End(ActivityEndReason.Requested, "Emote activity exited"),
                _ => ActivityActionResult.Reject("Unknown emote action")
            };
        }

        public void End(IActivityContext context, ActivityEndReason reason)
        {
            LastCueId = string.Empty;
            PlayCount = 0;
        }

        private ActivityActionResult Play(IActivityContext context, ActivityActionRequest request)
        {
            var cueId = request.Payload?.Trim();
            if (string.IsNullOrWhiteSpace(cueId))
                return ActivityActionResult.Reject("Emote cue is empty");

            LastCueId = cueId;
            PlayCount++;
            context.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationCue,
                context.PlayerId,
                ActivityId,
                string.Empty,
                cueId,
                active: true,
                sessionRevision: request.SessionRevision));
            return ActivityActionResult.Consume($"Emote played: {cueId}");
        }
    }
}
