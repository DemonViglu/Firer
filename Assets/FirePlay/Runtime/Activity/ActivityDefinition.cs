using System;

namespace DemonViglu.FirePlay.Activity
{
    public readonly struct ActivityStartCheck
    {
        public bool Allowed { get; }
        public string Reason { get; }

        public ActivityStartCheck(bool allowed, string reason)
        {
            Allowed = allowed;
            Reason = reason ?? string.Empty;
        }

        public static ActivityStartCheck Allow() => new(true, string.Empty);
        public static ActivityStartCheck Deny(string reason) => new(false, reason);
    }

    public readonly struct ActivityActionRequest
    {
        public string PlayerId { get; }
        public string ActivityId { get; }
        public string ActionId { get; }
        public string Payload { get; }
        public ActivityTargetKind TargetKind { get; }
        public string TargetId { get; }
        public string EventId { get; }
        public uint SessionRevision { get; }

        public ActivityActionRequest(
            string playerId,
            string activityId,
            string actionId,
            string payload,
            uint sessionRevision,
            ActivityTargetKind targetKind = ActivityTargetKind.None,
            string targetId = null,
            string eventId = null)
        {
            PlayerId = playerId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            ActionId = actionId ?? string.Empty;
            Payload = payload ?? string.Empty;
            TargetKind = targetKind;
            TargetId = targetId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            SessionRevision = sessionRevision;
        }
    }

    /// <summary>UI/Input 侧提交活动动作的窄接口。</summary>
    public interface IActivityActionRequester
    {
        bool HasActiveActivity { get; }
        string ActiveActivityId { get; }
        ActivityActionResult RequestAction(
            string actionId,
            string payload = null,
            ActivityTargetReference target = default);
    }

    /// <summary>
    /// Optional activity-owned state snapshot. The network layer treats the
    /// payload as opaque data; only the activity Logic and its own UI know the
    /// schema. Revision changes only when presentation-relevant state changes.
    /// </summary>
    public interface IActivityNetworkStateProvider
    {
        uint NetworkStateRevision { get; }
        string CaptureNetworkState();
    }

    public readonly struct ActivityActionResult
    {
        public bool Consumed { get; }
        public bool EndsSession { get; }
        public ActivityEndReason EndReason { get; }
        public string Reason { get; }

        public ActivityActionResult(
            bool consumed,
            string reason,
            bool endsSession = false,
            ActivityEndReason endReason = ActivityEndReason.Requested)
        {
            Consumed = consumed;
            EndsSession = endsSession;
            EndReason = endReason;
            Reason = reason ?? string.Empty;
        }

        public static ActivityActionResult Consume(string reason = "") => new(true, reason);
        public static ActivityActionResult End(
            ActivityEndReason reason = ActivityEndReason.Completed,
            string message = "") => new(true, message, true, reason);
        public static ActivityActionResult Reject(string reason) => new(false, reason);
    }

    /// <summary>
    /// 活动开始的两阶段提交句柄。Prepare 阶段不能产生副作用；Commit 才能
    /// 扣除余火、预留目标或创建活动道具。Commit 失败时旧 Session 仍可保留。
    /// </summary>
    public interface IActivityStartOperation
    {
        bool Commit(out string reason);
        void Rollback();
    }

    public sealed class ActivityStartOperation : IActivityStartOperation
    {
        private readonly Func<bool> _commit;
        private readonly Action _rollback;
        private bool _completed;

        public ActivityStartOperation(Func<bool> commit, Action rollback = null)
        {
            _commit = commit ?? throw new ArgumentNullException(nameof(commit));
            _rollback = rollback;
        }

        public bool Commit(out string reason)
        {
            if (_completed)
            {
                reason = "Activity start operation is already completed";
                return false;
            }

            if (!_commit())
            {
                reason = "Activity start operation was rejected";
                return false;
            }

            _completed = true;
            reason = string.Empty;
            return true;
        }

        public void Rollback()
        {
            if (_completed) return;
            _rollback?.Invoke();
        }
    }

    /// <summary>活动玩法的纯逻辑入口。UI、Camera 和 Unity 对象不属于此接口。</summary>
    public interface IActivityLogic
    {
        ActivityStartCheck CheckStart(IActivityContext context);
        IActivityStartOperation PrepareStart(IActivityContext context);
        ActivityActionResult HandleAction(IActivityContext context, ActivityActionRequest request);
        void End(IActivityContext context, ActivityEndReason reason);
    }

    /// <summary>
    /// Optional per-frame activity hook. Activities that do not need timing
    /// (for example emotes or a menu-only activity) do not implement it.
    /// </summary>
    public interface IActivityTickable
    {
        void Tick(IActivityContext context, float deltaTime);
    }

    public interface IActivityLogicFactory
    {
        IActivityLogic Create(ActivityDefinition definition);
    }

    /// <summary>运行时活动定义，不引用 UnityEngine。</summary>
    public sealed class ActivityDefinition
    {
        public string ActivityId { get; }
        public string DisplayName { get; }
        public ActivityScope Scope { get; }
        public ActivityParticipationMode ParticipationMode { get; }
        public string UiPrefabKey { get; }
        public string InputActionMap { get; }
        public string CameraProfileId { get; }
        public string LogicKey { get; }
        public IActivityRuleProvider DefaultRules { get; }

        public ActivityDefinition(
            string activityId,
            string displayName,
            ActivityScope scope,
            ActivityParticipationMode participationMode,
            string uiPrefabKey,
            string inputActionMap,
            string cameraProfileId,
            string logicKey,
            IActivityRuleProvider defaultRules = null)
        {
            ActivityId = activityId?.Trim() ?? string.Empty;
            DisplayName = displayName?.Trim() ?? string.Empty;
            Scope = scope;
            ParticipationMode = participationMode;
            UiPrefabKey = uiPrefabKey?.Trim() ?? string.Empty;
            InputActionMap = inputActionMap?.Trim() ?? string.Empty;
            CameraProfileId = cameraProfileId?.Trim() ?? string.Empty;
            LogicKey = logicKey?.Trim() ?? string.Empty;
            DefaultRules = defaultRules;
        }

        public bool IsValid => !string.IsNullOrWhiteSpace(ActivityId);
    }
}
