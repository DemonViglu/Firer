using System;
using System.Collections.Generic;
using System.Linq;

namespace DemonViglu.FirePlay.Activity
{
    public enum ActivityRuleDecision
    {
        Allow,
        Deny,
        OverrideAllow,
        OverrideDeny
    }

    public readonly struct ActivityRuleResult
    {
        public string SourceId { get; }
        public int Priority { get; }
        public ActivityRuleDecision Decision { get; }
        public string Reason { get; }
        public bool IsAllowed => Decision is ActivityRuleDecision.Allow or ActivityRuleDecision.OverrideAllow;
        public bool IsOverride => Decision is ActivityRuleDecision.OverrideAllow or ActivityRuleDecision.OverrideDeny;

        public ActivityRuleResult(string sourceId, int priority, ActivityRuleDecision decision, string reason)
        {
            SourceId = sourceId ?? string.Empty;
            Priority = priority;
            Decision = decision;
            Reason = reason ?? string.Empty;
        }

        public static ActivityRuleResult Allow(string sourceId, int priority, string reason = "Allowed") =>
            new(sourceId, priority, ActivityRuleDecision.Allow, reason);

        public static ActivityRuleResult Deny(string sourceId, int priority, string reason) =>
            new(sourceId, priority, ActivityRuleDecision.Deny, reason);

        public static ActivityRuleResult OverrideAllow(string sourceId, int priority, string reason) =>
            new(sourceId, priority, ActivityRuleDecision.OverrideAllow, reason);

        public static ActivityRuleResult OverrideDeny(string sourceId, int priority, string reason) =>
            new(sourceId, priority, ActivityRuleDecision.OverrideDeny, reason);
    }

    public interface IActivityRuleProvider
    {
        IEnumerable<ActivityRuleResult> Evaluate(IActivityContext context);
    }

    public sealed class ActivityRuleSnapshot : IActivityRuleSnapshot
    {
        private readonly ActivityRuleResult[] _decisions;

        public bool IsAllowed { get; }
        public int WinningPriority { get; }
        public string Reason { get; }
        public IReadOnlyList<ActivityRuleResult> Decisions => _decisions;

        public ActivityRuleSnapshot(
            bool isAllowed,
            int winningPriority,
            string reason,
            IReadOnlyList<ActivityRuleResult> decisions)
        {
            IsAllowed = isAllowed;
            WinningPriority = winningPriority;
            Reason = reason ?? string.Empty;
            _decisions = decisions == null
                ? Array.Empty<ActivityRuleResult>()
                : new List<ActivityRuleResult>(decisions).ToArray();
        }
    }

    /// <summary>
    /// 纯规则裁决器。没有脚本执行顺序和最后写入覆盖，结果完全由优先级决定。
    /// </summary>
    public static class ActivityRuleResolver
    {
        public const int AuthorityPriority = 2000;

        public static ActivityRuleSnapshot Resolve(
            IActivityContext context,
            IEnumerable<IActivityRuleProvider> providers)
        {
            var decisions = new List<ActivityRuleResult>();
            if (providers != null)
            {
                foreach (var provider in providers)
                {
                    if (provider == null) continue;
                    var results = provider.Evaluate(context);
                    if (results == null) continue;
                    decisions.AddRange(results.Where(result => !string.IsNullOrWhiteSpace(result.SourceId)));
                }
            }

            if (decisions.Count == 0)
            {
                return new ActivityRuleSnapshot(false, -1, "No rule allowed this activity", decisions);
            }

            var authorityDecisions = decisions
                .Where(result => result.Priority >= AuthorityPriority)
                .OrderByDescending(result => result.Priority)
                .ThenByDescending(result => result.Decision is ActivityRuleDecision.OverrideDeny or ActivityRuleDecision.Deny)
                .ThenBy(result => result.SourceId, StringComparer.Ordinal)
                .ToArray();
            if (authorityDecisions.Length > 0)
            {
                // Host authority is the final layer. A denial from that layer
                // cannot be reopened by any local rule, even if a local rule
                // has a higher numeric priority by mistake.
                var authorityDeny = authorityDecisions.FirstOrDefault(result =>
                    result.Decision is ActivityRuleDecision.Deny or ActivityRuleDecision.OverrideDeny);
                if (!string.IsNullOrWhiteSpace(authorityDeny.SourceId))
                {
                    return CreateSnapshot(authorityDeny, decisions);
                }

                return CreateSnapshot(authorityDecisions[0], decisions);
            }

            var overrideDecision = decisions
                .Where(result => result.IsOverride)
                .OrderByDescending(result => result.Priority)
                .ThenByDescending(result => result.Decision == ActivityRuleDecision.OverrideDeny)
                .ThenBy(result => result.SourceId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(overrideDecision.SourceId))
            {
                return CreateSnapshot(overrideDecision, decisions);
            }

            var winningPriority = decisions.Max(result => result.Priority);
            var winningDecisions = decisions.Where(result => result.Priority == winningPriority).ToArray();
            var deny = winningDecisions
                .Where(result => result.Decision == ActivityRuleDecision.Deny)
                .OrderBy(result => result.SourceId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(deny.SourceId))
            {
                return CreateSnapshot(deny, decisions);
            }

            var allow = winningDecisions
                .Where(result => result.Decision == ActivityRuleDecision.Allow)
                .OrderBy(result => result.SourceId, StringComparer.Ordinal)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(allow.SourceId)
                ? new ActivityRuleSnapshot(false, winningPriority, "No rule allowed this activity", decisions)
                : CreateSnapshot(allow, decisions);
        }

        private static ActivityRuleSnapshot CreateSnapshot(ActivityRuleResult result, IReadOnlyList<ActivityRuleResult> decisions)
        {
            return new ActivityRuleSnapshot(result.IsAllowed, result.Priority, result.Reason, decisions);
        }
    }
}
