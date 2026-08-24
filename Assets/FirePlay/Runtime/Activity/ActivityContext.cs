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

    public readonly struct MarshmallowGift
    {
        public string SourcePlayerId { get; }
        public string EventId { get; }
        public MarshmallowRoastQuality Quality { get; }
        public float FuelValue { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(SourcePlayerId)
            && !string.IsNullOrWhiteSpace(EventId)
            && FuelValue >= 0f;

        public MarshmallowGift(
            string sourcePlayerId,
            string eventId,
            MarshmallowRoastQuality quality,
            float fuelValue)
        {
            SourcePlayerId = sourcePlayerId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            Quality = quality;
            FuelValue = fuelValue;
        }
    }

    public interface IActivityTargetGiftReceiver
    {
        bool TryReceiveMarshmallow(MarshmallowGift gift, out string reason);
    }

    public sealed class ActivityTargetAvailabilityChanged : IGameEvent
    {
        public string TargetId { get; }
        public bool IsAvailable { get; }

        public ActivityTargetAvailabilityChanged(string targetId, bool isAvailable)
        {
            TargetId = targetId ?? string.Empty;
            IsAvailable = isAvailable;
        }
    }

    public interface IActivityTargetDirectory
    {
        bool RegisterPlayer(string playerId, IActivityTargetGiftReceiver giftReceiver = null);
        bool Remove(string targetId);
        bool TryResolve(string targetId, out IActivityTargetView target);
        bool TryDeliverMarshmallow(string targetId, MarshmallowGift gift, out string reason);
        IReadOnlyList<string> GetAvailablePlayerIds(string excludePlayerId = null);
    }

    /// <summary>
    /// Stable-ID-only target projection. Network/player adapters own target
    /// lifetime; Activity Logic receives only immutable availability facts.
    /// </summary>
    public sealed class ActivityTargetDirectory : IActivityTargetDirectory
    {
        private sealed class PlayerTargetView : IActivityTargetView
        {
            public string TargetId { get; }
            public bool IsAvailable => true;
            public IActivityTargetGiftReceiver GiftReceiver { get; private set; }

            public PlayerTargetView(string targetId, IActivityTargetGiftReceiver giftReceiver)
            {
                TargetId = targetId;
                GiftReceiver = giftReceiver;
            }

            public void SetGiftReceiver(IActivityTargetGiftReceiver giftReceiver) =>
                GiftReceiver = giftReceiver;

            public bool HasTag(string tag) =>
                string.Equals(tag, "player", StringComparison.Ordinal);
        }

        private readonly Dictionary<string, IActivityTargetView> _targets =
            new(StringComparer.Ordinal);
        private readonly IEventPublisher _events;

        public ActivityTargetDirectory(IEventPublisher events)
        {
            _events = events;
        }

        public bool RegisterPlayer(
            string playerId,
            IActivityTargetGiftReceiver giftReceiver = null)
        {
            playerId = playerId?.Trim() ?? string.Empty;
            if (playerId.Length == 0) return false;
            if (_targets.TryGetValue(playerId, out var existing))
            {
                if (existing is PlayerTargetView existingPlayer)
                    existingPlayer.SetGiftReceiver(giftReceiver);
                return true;
            }

            _targets[playerId] = new PlayerTargetView(playerId, giftReceiver);
            _events?.Publish(new ActivityTargetAvailabilityChanged(playerId, true));
            return true;
        }

        public bool Remove(string targetId)
        {
            targetId = targetId?.Trim() ?? string.Empty;
            if (targetId.Length == 0 || !_targets.Remove(targetId))
                return false;

            _events?.Publish(new ActivityTargetAvailabilityChanged(targetId, false));
            return true;
        }

        public bool TryResolve(string targetId, out IActivityTargetView target)
        {
            targetId = targetId?.Trim() ?? string.Empty;
            if (targetId.Length > 0
                && _targets.TryGetValue(targetId, out target)
                && target.IsAvailable)
            {
                return true;
            }

            target = null;
            return false;
        }

        public bool TryDeliverMarshmallow(
            string targetId,
            MarshmallowGift gift,
            out string reason)
        {
            if (!gift.IsValid
                || !TryResolve(targetId, out var target)
                || target is not PlayerTargetView player
                || player.GiftReceiver == null)
            {
                reason = "Target Player cannot receive a marshmallow";
                return false;
            }

            return player.GiftReceiver.TryReceiveMarshmallow(gift, out reason);
        }

        public IReadOnlyList<string> GetAvailablePlayerIds(string excludePlayerId = null)
        {
            var result = new List<string>();
            foreach (var target in _targets.Values)
            {
                if (target.IsAvailable
                    && target.HasTag("player")
                    && target.TargetId != excludePlayerId)
                {
                    result.Add(target.TargetId);
                }
            }
            result.Sort(StringComparer.Ordinal);
            return result;
        }
    }

    /// <summary>活动经济规则使用的余火接口。当前实现可直接代理玩家 TryConsume。</summary>
    public interface IActivityFlameResource
    {
        float CurrentFuel { get; }
        float MaximumFuel { get; }
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

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// A transport-neutral notification that the membership of one shared
    /// activity group changed. PlayerIds are stable identities, not Player
    /// components or NetworkObjects.
    /// </summary>
    public sealed class ActivityParticipantsChanged : IGameEvent
    {
        private readonly string[] _playerIds;

        public string AnchorId { get; }
        public string ActivityId { get; }
        public ActivityParticipationMode ParticipationMode { get; }
        public uint Revision { get; }
        public IReadOnlyList<string> PlayerIds => _playerIds;

        public ActivityParticipantsChanged(
            string anchorId,
            string activityId,
            ActivityParticipationMode participationMode,
            uint revision,
            IReadOnlyList<string> playerIds)
        {
            AnchorId = anchorId ?? string.Empty;
            ActivityId = activityId ?? string.Empty;
            ParticipationMode = participationMode;
            Revision = revision;
            _playerIds = playerIds == null
                ? Array.Empty<string>()
                : new List<string>(playerIds).ToArray();
        }
    }

    public interface IActivityParticipationDirectory
    {
        IReadOnlyList<ActivityParticipant> Resolve(
            string ownerPlayerId,
            string anchorId,
            string activityId,
            ActivityParticipationMode participationMode,
            string targetId = null);

        bool Register(ActivitySessionStarted fact);
        bool Remove(ActivitySessionEnded fact);
        bool RemovePlayer(string playerId);
    }

    /// <summary>
    /// Process-local projection of authority-confirmed activity lifecycle
    /// facts. The Server records its authoritative Sessions; Clients record
    /// the same Sessions after network fact validation. It never owns Logic.
    /// </summary>
    public sealed class ActivityParticipationDirectory : IActivityParticipationDirectory
    {
        private readonly struct Entry
        {
            public string PlayerId { get; }
            public string AnchorId { get; }
            public string ActivityId { get; }
            public ActivityParticipationMode ParticipationMode { get; }
            public uint SessionRevision { get; }

            public Entry(ActivitySessionStarted fact)
            {
                PlayerId = fact.PlayerId;
                AnchorId = fact.AnchorId;
                ActivityId = fact.ActivityId;
                ParticipationMode = fact.ParticipationMode;
                SessionRevision = fact.SessionRevision;
            }
        }

        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
        private readonly IEventPublisher _events;
        private uint _revision;

        public ActivityParticipationDirectory(IEventPublisher events)
        {
            _events = events;
        }

        public IReadOnlyList<ActivityParticipant> Resolve(
            string ownerPlayerId,
            string anchorId,
            string activityId,
            ActivityParticipationMode participationMode,
            string targetId = null)
        {
            if (string.IsNullOrWhiteSpace(ownerPlayerId))
                return Array.Empty<ActivityParticipant>();

            if (participationMode == ActivityParticipationMode.TargetedInteraction)
            {
                if (string.IsNullOrWhiteSpace(targetId) || targetId == ownerPlayerId)
                    return new[] { new ActivityParticipant(ownerPlayerId, isOwner: true) };
                return new[]
                {
                    new ActivityParticipant(ownerPlayerId, isOwner: true),
                    new ActivityParticipant(targetId)
                };
            }

            // Independent sessions are never implicitly merged merely
            // because players happen to share an Anchor.
            if (participationMode != ActivityParticipationMode.SharedGroup)
                return new[] { new ActivityParticipant(ownerPlayerId, isOwner: true) };

            var playerIds = ResolvePlayerIds(anchorId, activityId, participationMode);
            if (!playerIds.Exists(playerId => playerId == ownerPlayerId))
                playerIds.Add(ownerPlayerId);
            playerIds.Sort(StringComparer.Ordinal);

            var participants = new ActivityParticipant[playerIds.Count];
            for (var index = 0; index < playerIds.Count; index++)
            {
                participants[index] = new ActivityParticipant(
                    playerIds[index],
                    playerIds[index] == ownerPlayerId);
            }
            return participants;
        }

        public bool Register(ActivitySessionStarted fact)
        {
            if (fact == null
                || string.IsNullOrWhiteSpace(fact.PlayerId)
                || string.IsNullOrWhiteSpace(fact.ActivityId)
                || fact.SessionRevision == 0)
            {
                return false;
            }

            if (_entries.TryGetValue(fact.PlayerId, out var previous))
            {
                if (previous.SessionRevision > fact.SessionRevision)
                    return false;
                if (previous.SessionRevision == fact.SessionRevision
                    && previous.AnchorId == fact.AnchorId
                    && previous.ActivityId == fact.ActivityId
                    && previous.ParticipationMode == fact.ParticipationMode)
                {
                    return true;
                }

                _entries.Remove(fact.PlayerId);
                Publish(previous);
            }

            var entry = new Entry(fact);
            _entries[fact.PlayerId] = entry;
            Publish(entry);
            return true;
        }

        public bool Remove(ActivitySessionEnded fact)
        {
            if (fact == null
                || !_entries.TryGetValue(fact.PlayerId, out var entry)
                || entry.SessionRevision != fact.SessionRevision
                || entry.ActivityId != fact.ActivityId
                || entry.AnchorId != fact.AnchorId)
            {
                return false;
            }

            _entries.Remove(fact.PlayerId);
            Publish(entry);
            return true;
        }

        public bool RemovePlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId)
                || !_entries.Remove(playerId, out var entry))
            {
                return false;
            }

            Publish(entry);
            return true;
        }

        private List<string> ResolvePlayerIds(
            string anchorId,
            string activityId,
            ActivityParticipationMode participationMode)
        {
            var result = new List<string>();
            foreach (var entry in _entries.Values)
            {
                if (entry.AnchorId == (anchorId ?? string.Empty)
                    && entry.ActivityId == (activityId ?? string.Empty)
                    && entry.ParticipationMode == participationMode)
                {
                    result.Add(entry.PlayerId);
                }
            }
            return result;
        }

        private void Publish(Entry entry)
        {
            if (entry.ParticipationMode != ActivityParticipationMode.SharedGroup)
                return;

            var playerIds = ResolvePlayerIds(
                entry.AnchorId,
                entry.ActivityId,
                entry.ParticipationMode);
            playerIds.Sort(StringComparer.Ordinal);
            _revision = _revision == uint.MaxValue ? 1u : _revision + 1u;
            _events?.Publish(new ActivityParticipantsChanged(
                entry.AnchorId,
                entry.ActivityId,
                entry.ParticipationMode,
                _revision,
                playerIds));
        }
    }
}
