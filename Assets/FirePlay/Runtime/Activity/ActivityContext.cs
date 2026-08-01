using System;
using System.Collections.Generic;

namespace DemonViglu.FirePlay.Activity
{
    public readonly struct ActivityParticipant
    {
        public string PlayerId { get; }
        public bool IsOwner { get; }

        public ActivityParticipant(string playerId, bool isOwner = false)
        {
            PlayerId = playerId ?? string.Empty;
            IsOwner = isOwner;
        }
    }

    /// <summary>活动可读取的地点事实，不暴露地点组件的 Unity 实现。</summary>
    public interface IActivityLocationView
    {
        string AnchorId { get; }
        string RegionId { get; }
        bool HasTag(string tag);
    }

    /// <summary>活动可读取的玩家能力/状态事实，不暴露 Player MonoBehaviour。</summary>
    public interface IActivityPlayerStateView
    {
        string ModeId { get; }
        bool HasState(string stateId);
    }

    /// <summary>活动可读取的目标事实。</summary>
    public interface IActivityTargetView
    {
        string TargetId { get; }
        bool IsAvailable { get; }
        bool HasTag(string tag);
    }

    /// <summary>活动经济规则使用的余火接口。当前实现可直接代理玩家 TryConsume。</summary>
    public interface IActivityFlameResource
    {
        float CurrentFuel { get; }
        bool TryConsume(float amount);
        bool Restore(float amount);
    }

    /// <summary>
    /// 活动对 UI、Camera 和 Player 能力的唯一出口。实现方属于 Player
    /// Presentation/Camera 系统，活动逻辑不持有 Unity 控件、相机、Animator
    /// 或移动组件。
    /// </summary>
    public interface IActivityPresentationRequests
    {
        bool RequestUi(ActivityUiRequest request);
        bool RequestCamera(ActivityCameraRequest request);
        bool RequestPlayer(ActivityPlayerRequest request);
    }

    public interface IActivityRuleSnapshot
    {
        bool IsAllowed { get; }
        int WinningPriority { get; }
        string Reason { get; }
        IReadOnlyList<ActivityRuleResult> Decisions { get; }
    }

    /// <summary>
    /// ActivityLogic 唯一可见的运行上下文。所有引用均为只读能力或稳定身份。
    /// </summary>
    public interface IActivityContext
    {
        string PlayerId { get; }
        string ActivityId { get; }
        string AnchorId { get; }
        IActivityLocationView Location { get; }
        IActivityPlayerStateView PlayerState { get; }
        IActivityTargetView Target { get; }
        IActivityRuleSnapshot Rules { get; }
        IActivityFlameResource Flame { get; }
        IActivityPresentationRequests Presentation { get; }
        IReadOnlyList<ActivityParticipant> Participants { get; }
    }

    /// <summary>由 ActivitySystem 创建的不可变上下文实现。</summary>
    public sealed class ActivityContext : IActivityContext
    {
        private readonly ActivityParticipant[] _participants;

        public string PlayerId { get; }
        public string ActivityId { get; }
        public string AnchorId => Location != null ? Location.AnchorId ?? string.Empty : string.Empty;
        public IActivityLocationView Location { get; }
        public IActivityPlayerStateView PlayerState { get; }
        public IActivityTargetView Target { get; }
        public IActivityRuleSnapshot Rules { get; }
        public IActivityFlameResource Flame { get; }
        public IActivityPresentationRequests Presentation { get; }
        public IReadOnlyList<ActivityParticipant> Participants => _participants;

        public ActivityContext(
            string playerId,
            string activityId,
            IActivityLocationView location = null,
            IActivityPlayerStateView playerState = null,
            IActivityTargetView target = null,
            IActivityFlameResource flame = null,
            IActivityPresentationRequests presentation = null,
            IReadOnlyList<ActivityParticipant> participants = null,
            IActivityRuleSnapshot rules = null)
        {
            PlayerId = playerId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            Location = location;
            PlayerState = playerState;
            Target = target;
            Flame = flame;
            Presentation = presentation;
            Rules = rules;
            _participants = participants == null
                ? Array.Empty<ActivityParticipant>()
                : new List<ActivityParticipant>(participants).ToArray();
        }

        public ActivityContext WithRules(IActivityRuleSnapshot rules)
        {
            return new ActivityContext(
                PlayerId,
                ActivityId,
                Location,
                PlayerState,
                Target,
                Flame,
                Presentation,
                _participants,
                rules);
        }
    }
}
