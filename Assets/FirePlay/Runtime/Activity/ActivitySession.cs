using System;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>活动事实状态；UI、Camera 和网络只读取快照，不直接修改 Session。</summary>
    public sealed class ActivitySession
    {
        private readonly IActivityLogic _logic;

        public ActivityDefinition Definition { get; }
        public IActivityContext Context { get; private set; }
        public ActivityRuleSnapshot Rules { get; private set; }
        /// <summary>
        /// Read-only access for presentation adapters. Presentation may
        /// inspect typed activity state, but it cannot mutate the Session.
        /// </summary>
        public IActivityLogic Logic => _logic;
        public ActivitySessionPhase Phase { get; private set; }
        public ActivityEndReason? EndReason { get; private set; }
        public uint Revision { get; }
        public bool IsActive => Phase is ActivitySessionPhase.Running or ActivitySessionPhase.Suspended;
        public bool IsSwitching => Phase == ActivitySessionPhase.Switching;

        internal ActivitySession(
            ActivityDefinition definition,
            IActivityContext context,
            ActivityRuleSnapshot rules,
            IActivityLogic logic,
            uint revision)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _logic = logic ?? throw new ArgumentNullException(nameof(logic));
            Revision = revision;
            Phase = ActivitySessionPhase.Running;
        }

        public ActivityActionResult HandleAction(ActivityActionRequest request)
        {
            if (!IsActive || request.PlayerId != Context.PlayerId || request.ActivityId != Definition.ActivityId || request.SessionRevision != Revision)
            {
                return ActivityActionResult.Reject("Activity session is not the action owner");
            }

            var result = _logic.HandleAction(Context, request);
            if (result.Consumed && result.EndsSession)
                End(result.EndReason);
            return result;
        }

        internal void Tick(float deltaTime)
        {
            if (!IsActive || deltaTime <= 0f) return;
            if (_logic is IActivityTickable tickable)
                tickable.Tick(Context, deltaTime);
        }

        internal ActivityStartCheck CheckRuntimeState(IActivityContext context)
        {
            if (!IsActive || context == null) return ActivityStartCheck.Deny("Activity session is not active");
            return _logic.CheckStart(context);
        }

        public void Suspend()
        {
            if (Phase == ActivitySessionPhase.Running) Phase = ActivitySessionPhase.Suspended;
        }

        public void Resume(IActivityContext context)
        {
            if (Phase != ActivitySessionPhase.Suspended) return;
            Context = context ?? Context;
            Phase = ActivitySessionPhase.Running;
        }

        internal bool UpdateRuntimeContext(IActivityContext context, ActivityRuleSnapshot rules)
        {
            if (!IsActive || context == null || rules == null) return false;
            if (context.PlayerId != Context.PlayerId || context.ActivityId != Definition.ActivityId)
                return false;

            Context = context;
            Rules = rules;
            return true;
        }

        internal bool BeginSwitch()
        {
            if (!IsActive) return false;
            Phase = ActivitySessionPhase.Switching;
            return true;
        }

        internal void RestoreAfterSwitch()
        {
            if (Phase == ActivitySessionPhase.Switching)
                Phase = ActivitySessionPhase.Running;
        }

        internal void End(ActivityEndReason reason)
        {
            if (Phase == ActivitySessionPhase.Ended) return;
            Phase = reason == ActivityEndReason.Completed
                ? ActivitySessionPhase.Completed
                : ActivitySessionPhase.Interrupted;
            EndReason = reason;
            _logic.End(Context, reason);
            Phase = ActivitySessionPhase.Ended;
        }
    }
}
