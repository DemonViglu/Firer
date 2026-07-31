using System;
using System.Collections.Generic;

namespace DemonViglu.FirePlay.Activity
{
    public readonly struct ActivityPreflightResult
    {
        public bool Allowed { get; }
        public string Reason { get; }
        public ActivityRuleSnapshot Rules { get; }
        internal IActivityContext Context { get; }
        internal IActivityLogic Logic { get; }

        internal ActivityPreflightResult(
            bool allowed,
            string reason,
            ActivityRuleSnapshot rules,
            IActivityContext context,
            IActivityLogic logic)
        {
            Allowed = allowed;
            Reason = reason ?? string.Empty;
            Rules = rules;
            Context = context;
            Logic = logic;
        }

        public static ActivityPreflightResult Reject(string reason, ActivityRuleSnapshot rules = null) =>
            new(false, reason, rules, null, null);

        internal static ActivityPreflightResult Accept(
            IActivityContext context,
            IActivityLogic logic,
            ActivityRuleSnapshot rules) =>
            new(true, string.Empty, rules, context, logic);
    }

    public readonly struct ActivityStartResult
    {
        public bool Success { get; }
        public string Reason { get; }
        public ActivityRuleSnapshot Rules { get; }
        public ActivitySession Session { get; }

        public ActivityStartResult(bool success, string reason, ActivityRuleSnapshot rules, ActivitySession session)
        {
            Success = success;
            Reason = reason ?? string.Empty;
            Rules = rules;
            Session = session;
        }

        public static ActivityStartResult Reject(string reason, ActivityRuleSnapshot rules = null) =>
            new(false, reason, rules, null);

        public static ActivityStartResult Accepted(ActivitySession session, ActivityRuleSnapshot rules) =>
            new(true, string.Empty, rules, session);
    }

    /// <summary>
    /// 无 Unity 依赖的活动事实系统。它只负责预检、Session 生命周期和动作路由。
    /// UI、Camera、Anchor MonoBehaviour 和网络适配器在外层接入。
    /// </summary>
    public sealed class ActivitySystem
    {
        private readonly Dictionary<string, ActivitySession> _sessions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, uint> _revisions = new(StringComparer.Ordinal);

        public bool TryGetSession(string playerId, out ActivitySession session)
        {
            if (!string.IsNullOrWhiteSpace(playerId) && _sessions.TryGetValue(playerId, out session) && session.IsActive)
            {
                return true;
            }

            session = null;
            return false;
        }

        public ActivityPreflightResult Preflight(
            ActivityDefinition definition,
            IActivityContext context,
            IActivityLogic logic,
            IEnumerable<IActivityRuleProvider> ruleProviders)
        {
            if (definition == null || !definition.IsValid) return ActivityPreflightResult.Reject("Invalid activity definition");
            if (context == null || context.PlayerId.Length == 0 || context.ActivityId != definition.ActivityId)
                return ActivityPreflightResult.Reject("Activity context does not match definition");
            if (definition.Scope == ActivityScope.Anchor && context.AnchorId.Length == 0)
                return ActivityPreflightResult.Reject("This activity needs an anchor");
            if (definition.Scope == ActivityScope.Targeted && (context.Target == null || !context.Target.IsAvailable))
                return ActivityPreflightResult.Reject("This activity needs an available target");
            if (logic == null) return ActivityPreflightResult.Reject("Activity logic is missing");

            var providers = CollectProviders(definition, definition.DefaultRules, ruleProviders);
            var rules = ActivityRuleResolver.Resolve(context, providers);
            var ruledContext = new ActivityContext(
                context.PlayerId,
                context.ActivityId,
                context.Location,
                context.PlayerState,
                context.Target,
                context.Flame,
                context.Presentation,
                context.Participants,
                rules);
            if (!rules.IsAllowed)
                return ActivityPreflightResult.Reject(rules.Reason, rules);

            var startCheck = logic.CheckStart(ruledContext);
            if (!startCheck.Allowed)
                return ActivityPreflightResult.Reject(startCheck.Reason, rules);

            return ActivityPreflightResult.Accept(ruledContext, logic, rules);
        }

        public ActivityStartResult TryStart(
            ActivityDefinition definition,
            IActivityContext context,
            IActivityLogic logic,
            IEnumerable<IActivityRuleProvider> ruleProviders)
        {
            var preflight = Preflight(definition, context, logic, ruleProviders);
            if (!preflight.Allowed)
                return ActivityStartResult.Reject(preflight.Reason, preflight.Rules);

            var operation = preflight.Logic.PrepareStart(preflight.Context);
            if (operation == null)
                return ActivityStartResult.Reject("Activity start operation is missing", preflight.Rules);

            var previous = _sessions.TryGetValue(preflight.Context.PlayerId, out var previousSession)
                && previousSession.IsActive
                ? previousSession
                : null;
            previous?.BeginSwitch();

            // Commit is the only point where the activity may consume fuel or
            // reserve a target. The previous Session is put into a reversible
            // Switching phase first, so a rejected switch restores it intact.
            bool committed;
            string commitReason;
            try
            {
                committed = operation.Commit(out commitReason);
            }
            catch (Exception exception)
            {
                operation.Rollback();
                previous?.RestoreAfterSwitch();
                return ActivityStartResult.Reject($"Activity start failed: {exception.Message}", preflight.Rules);
            }

            if (!committed)
            {
                operation.Rollback();
                previous?.RestoreAfterSwitch();
                return ActivityStartResult.Reject(commitReason, preflight.Rules);
            }

            previous?.End(ActivityEndReason.Switched);

            var revision = NextRevision(preflight.Context.PlayerId);
            var session = new ActivitySession(definition, preflight.Context, preflight.Rules, preflight.Logic, revision);
            _sessions[preflight.Context.PlayerId] = session;
            return ActivityStartResult.Accepted(session, preflight.Rules);
        }

        public ActivityActionResult HandleAction(ActivityActionRequest request)
        {
            if (request.PlayerId == null || !_sessions.TryGetValue(request.PlayerId, out var session) || !session.IsActive)
                return ActivityActionResult.Reject("No active activity session");

            return session.HandleAction(request);
        }

        public bool Tick(string playerId, float deltaTime)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || !_sessions.TryGetValue(playerId, out var session)
                || !session.IsActive)
            {
                return false;
            }

            session.Tick(deltaTime);
            return true;
        }

        /// <summary>
        /// Re-evaluates the active session without creating a second session or
        /// performing any side effect. A denied result ends the session with the
        /// supplied interruption reason; an allowed result replaces the readonly
        /// context/rule snapshot used by the activity.
        /// </summary>
        public bool Revalidate(
            string playerId,
            IActivityContext context,
            IEnumerable<IActivityRuleProvider> ruleProviders,
            ActivityEndReason deniedReason = ActivityEndReason.StateChanged)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || context == null
                || !_sessions.TryGetValue(playerId, out var session)
                || !session.IsActive
                || context.PlayerId != playerId
                || context.ActivityId != session.Definition.ActivityId)
            {
                return false;
            }

            var providers = CollectProviders(session.Definition, session.Definition.DefaultRules, ruleProviders);
            var rules = ActivityRuleResolver.Resolve(context, providers);
            if (!rules.IsAllowed)
            {
                session.End(deniedReason);
                _sessions.Remove(playerId);
                return false;
            }

            var ruledContext = new ActivityContext(
                context.PlayerId,
                context.ActivityId,
                context.Location,
                context.PlayerState,
                context.Target,
                context.Flame,
                context.Presentation,
                context.Participants,
                rules);
            var check = session.CheckRuntimeState(ruledContext);
            if (!check.Allowed)
            {
                session.End(deniedReason);
                _sessions.Remove(playerId);
                return false;
            }

            return session.UpdateRuntimeContext(ruledContext, rules);
        }

        public bool End(string playerId, ActivityEndReason reason = ActivityEndReason.Requested)
        {
            if (string.IsNullOrWhiteSpace(playerId) || !_sessions.TryGetValue(playerId, out var session) || !session.IsActive)
                return false;

            session.End(reason);
            _sessions.Remove(playerId);
            return true;
        }

        private uint NextRevision(string playerId)
        {
            var next = _revisions.TryGetValue(playerId, out var current) ? current + 1u : 1u;
            _revisions[playerId] = next;
            return next;
        }

        private static IEnumerable<IActivityRuleProvider> CollectProviders(
            ActivityDefinition definition,
            IActivityRuleProvider defaultProvider,
            IEnumerable<IActivityRuleProvider> providers)
        {
            // Every valid definition is an explicit opt-in. Additional
            // providers may deny it, but an omitted default provider must not
            // make a perfectly valid activity impossible to start.
            if (definition != null)
                yield return new DefinitionAllowRuleProvider(definition.ActivityId);
            if (defaultProvider != null) yield return defaultProvider;
            if (providers == null) yield break;

            foreach (var provider in providers)
            {
                if (provider != null) yield return provider;
            }
        }

        private sealed class DefinitionAllowRuleProvider : IActivityRuleProvider
        {
            private readonly string _activityId;

            public DefinitionAllowRuleProvider(string activityId)
            {
                _activityId = activityId ?? string.Empty;
            }

            public IEnumerable<ActivityRuleResult> Evaluate(IActivityContext context)
            {
                yield return ActivityRuleResult.Allow($"activity:{_activityId}", 0, "Activity definition enabled");
            }
        }
    }
}
