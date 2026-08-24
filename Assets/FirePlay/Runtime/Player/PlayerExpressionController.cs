using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    [Serializable]
    public sealed class PlayerExpressionDefinition
    {
        public string expressionId;
        public string displayName;
        public Sprite icon;
        public string animationCueId;
        public bool allowWhileExploring = true;
        public bool allowWhileResting;
    }

    public sealed class ExpressionRequested : IGameEvent
    {
        public string PlayerId { get; }
        public string ExpressionId { get; }

        public ExpressionRequested(string playerId, string expressionId)
        {
            PlayerId = playerId;
            ExpressionId = expressionId;
        }
    }

    /// <summary>
    /// Emitted only after the local expression has passed the Player-side
    /// availability check and has been played locally. The network adapter
    /// may turn this semantic cue into a Host-authoritative observer cue;
    /// it never carries an Animator or Unity object reference.
    /// </summary>
    public sealed class PlayerExpressionPlayed : IGameEvent
    {
        public string PlayerId { get; }
        public string ExpressionId { get; }
        public string CueId { get; }
        public uint Sequence { get; }

        public PlayerExpressionPlayed(
            string playerId,
            string expressionId,
            string cueId,
            uint sequence)
        {
            PlayerId = playerId ?? string.Empty;
            ExpressionId = expressionId ?? string.Empty;
            CueId = cueId ?? string.Empty;
            Sequence = sequence;
        }
    }

    /// <summary>按 expressionId 解析数据定义并提交统一动画 Cue；轮盘和网络层不接触 Animator。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerExpressionController : MonoBehaviour
    {
        [SerializeField] private string _defaultExpressionId = "wave";
        [SerializeField] private PlayerExpressionDefinition[] _definitions =
        {
            new() { expressionId = "wave", displayName = "挥手", animationCueId = "expression.wave" },
            new() { expressionId = "thanks", displayName = "感谢", animationCueId = "expression.thanks" },
            new() { expressionId = "warmth", displayName = "取暖", animationCueId = "expression.warmth" },
            new() { expressionId = "sit", displayName = "坐下", animationCueId = "expression.sit" }
        };

        private LocalPlayerContext _context;
        private PlayerModeController _mode;
        private PlayerAnimationController _animation;
        private IEventPublisher _events;
        private bool _eventsAttached;

        public PlayerExpressionDefinition[] Definitions => _definitions;
        public string LastExpressionId { get; private set; } = string.Empty;
        public uint ExpressionSequence { get; private set; }

        public void Initialize(LocalPlayerContext context)
        {
            DetachEvents();
            _context = context ?? GetComponentInParent<LocalPlayerContext>();
            _mode ??= GetComponent<PlayerModeController>()
                ?? GetComponentInParent<PlayerModeController>()
                ?? _context?.GetComponentInChildren<PlayerModeController>(true);
            _animation ??= GetComponent<PlayerAnimationController>()
                ?? GetComponentInParent<PlayerAnimationController>()
                ?? _context?.GetComponentInChildren<PlayerAnimationController>(true);

            // ExpressionRequested and PlayerIntentRequested are local input
            // events. A remote Player may still use TryPlay when a future
            // network presentation applies an authoritative cue, but it must
            // never subscribe to the local EventBus itself.
            if (_context == null || !_context.IsLocalPlayer)
                return;

            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            AttachEvents();
        }

        private void AttachEvents()
        {
            if (_eventsAttached || _events == null) return;
            _events.Subscribe<ExpressionRequested>(OnExpressionRequested);
            _events.Subscribe<PlayerIntentRequested>(OnPlayerIntentRequested);
            _eventsAttached = true;
        }

        private void DetachEvents()
        {
            if (!_eventsAttached || _events == null) return;
            _events.Unsubscribe<ExpressionRequested>(OnExpressionRequested);
            _events.Unsubscribe<PlayerIntentRequested>(OnPlayerIntentRequested);
            _eventsAttached = false;
        }

        private void Awake() => Initialize(GetComponent<LocalPlayerContext>() ?? GetComponentInParent<LocalPlayerContext>());
        private void OnEnable() => Initialize(GetComponent<LocalPlayerContext>() ?? GetComponentInParent<LocalPlayerContext>());

        private void OnDisable()
        {
            DetachEvents();
        }

        private void OnExpressionRequested(ExpressionRequested request)
        {
            if (_context == null || request == null || request.PlayerId != _context.PlayerId) return;
            TryPlay(request.ExpressionId);
        }

        private void OnPlayerIntentRequested(PlayerIntentRequested intent)
        {
            if (_context == null || intent == null || intent.PlayerId != _context.PlayerId || intent.Kind != PlayerIntentKind.Emote) return;
            TryPlay(_defaultExpressionId);
        }

        public bool TryPlay(string expressionId)
        {
            var definition = Find(expressionId);
            if (definition == null || !IsAllowed(definition)) return false;
            _animation?.Play(definition.animationCueId);
            LastExpressionId = definition.expressionId;
            ExpressionSequence++;
            _events?.Publish(new PlayerExpressionPlayed(
                _context?.PlayerId,
                definition.expressionId,
                definition.animationCueId,
                ExpressionSequence));
            return true;
        }

        private PlayerExpressionDefinition Find(string expressionId)
        {
            if (string.IsNullOrWhiteSpace(expressionId) || _definitions == null) return null;
            foreach (var definition in _definitions)
                if (definition != null && definition.expressionId == expressionId) return definition;
            return null;
        }

        private bool IsAllowed(PlayerExpressionDefinition definition)
        {
            var mode = _mode != null ? _mode.CurrentMode : PlayerMode.Exploring;
            return mode == PlayerMode.Exploring ? definition.allowWhileExploring
                : mode == PlayerMode.Resting && definition.allowWhileResting;
        }
    }
}
