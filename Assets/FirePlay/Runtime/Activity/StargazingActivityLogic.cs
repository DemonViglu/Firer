namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Sitting-based stargazing activity. The location trigger owns the
    /// composition rule (which RestSpot starts it); this logic only validates
    /// the player state and owns the Session lifecycle.
    /// </summary>
    public sealed class StargazingActivityLogic : IActivityLogic, IActivityPresentationLifecycle
    {
        public const string ActivityId = "stargazing";

        public ActivityStartCheck CheckStart(IActivityContext context)
        {
            if (context == null || context.ActivityId != ActivityId)
                return ActivityStartCheck.Deny("Stargazing context is invalid");

            if (context.PlayerState == null || !context.PlayerState.HasState("resting"))
                return ActivityStartCheck.Deny("Stargazing requires a resting player");

            return ActivityStartCheck.Allow();
        }

        public IActivityStartOperation PrepareStart(IActivityContext context)
        {
            return new ActivityStartOperation(() => true);
        }

        public ActivityActionResult HandleAction(IActivityContext context, ActivityActionRequest request)
        {
            if (context == null || context.ActivityId != ActivityId)
                return ActivityActionResult.Reject("Stargazing context is invalid");

            return request.ActionId == "activity.exit"
                ? ActivityActionResult.End(ActivityEndReason.Requested, "Stargazing activity exited")
                : ActivityActionResult.Reject("Unknown stargazing action");
        }

        public void OnPresentationStarted(IActivityContext context, uint sessionRevision)
        {
            if (context?.Presentation == null)
                return;

            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.LookTarget,
                context.PlayerId,
                ActivityId,
                context.AnchorId,
                string.Empty,
                active: true,
                sessionRevision: sessionRevision));
        }

        public void OnPresentationEnded(IActivityContext context, uint sessionRevision, ActivityEndReason reason)
        {
            if (context?.Presentation == null)
                return;

            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.LookTarget,
                context.PlayerId,
                ActivityId,
                context.AnchorId,
                string.Empty,
                active: false,
                sessionRevision: sessionRevision));
        }

        public void End(IActivityContext context, ActivityEndReason reason)
        {
        }
    }
}
