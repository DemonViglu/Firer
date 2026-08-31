using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.Player;
using SUIFW;
using UnityEngine;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Mobile UI's sole gameplay gateway. Hook UI Button OnClick events to the
    /// public methods below; they are translated into the existing player input
    /// actions rather than duplicating ritual or world interaction logic in UI.
    /// </summary>
    public sealed class FirePlayMobileInputRouter : MonoBehaviour
    {
        [SerializeField] private FirePlayPlayerInput _playerInput;
        [SerializeField] private RestInteraction _rest;
        [SerializeField] private LocalPlayerContext _localPlayer;
        [SerializeField] private PlayerActivityHost _activityHost;
        [SerializeField, Min(0.1f)] private float _activityAnchorSearchDistance = 3f;
        private IEventPublisher _events;

        private void Awake()
        {
            ResolvePlayerInput();
        }

        public void SetMove(Vector2 value)
        {
            ResolvePlayerInput();
            _playerInput?.SetVirtualMove(value);
        }

        public void AddLookDelta(Vector2 delta)
        {
            ResolvePlayerInput();
            _playerInput?.AddVirtualLookDelta(delta);
        }

        public void Interact() => Request(input => input.RequestVirtualInteract());
        public void Jump() => Request(input => input.RequestVirtualJump());
        public void Rest() => Request(input => input.RequestVirtualRest());
        public void RitualPrimary()
        {
            ResolvePlayerInput();
            _playerInput?.RequestVirtualEmote();
        }
        public void RitualSecondary()
        {
            ResolvePlayerInput();

            _playerInput?.RequestVirtualInteract();
        }
        public void PlaceFire() => Request(input => input.RequestVirtualPlaceFire());
        /// <summary>
        /// Shared cancel action for the existing placement cancel button. During
        /// placement it clears the preview; during any resting ritual it gets the
        /// player back up. It is intentionally harmless in exploration.
        /// </summary>
        public void CancelFirePlacement()
        {
            ResolvePlayerInput();
            _playerInput?.RequestVirtualCancelPlacement();
            if (_rest != null && _rest.IsResting)
            {
                _playerInput?.RequestVirtualRest();
            }
        }

        // Explicit world actions. UI still goes through the input facade; it never
        // modifies fuel or campfire state directly.
        public void AddFire() => Request(input => input.RequestVirtualAddFire());
        public void TendFire() => Request(input => input.RequestVirtualTendFire());
        public void GatherEmber() => Request(input => input.RequestVirtualGatherEmber());
        public void StartPublicFire() => Request(input => input.RequestVirtualStartPublicFire());
        public void DrawFire() => Request(input => input.RequestVirtualDrawFire());
        public void ReclaimSmallFire() => Request(input => input.RequestVirtualReclaimSmallFire());
        public void ContributeWorldTree() => Request(input => input.RequestVirtualContributeWorldTree());
        [System.Obsolete("Use DrawFire or ReclaimSmallFire so the button label matches its context.")]
        public void WithdrawOrReclaim() => DrawFire();
        public void CycleTreeLightColor() => Request(input => input.RequestVirtualCycleTreeLightColor());
        public void RequestExpression(string expressionId)
        {
            ResolvePlayerInput();
            if (_localPlayer != null && !string.IsNullOrWhiteSpace(expressionId))
                _events?.Publish(new ExpressionRequested(_localPlayer.PlayerId, expressionId));
        }

        /// <summary>
        /// Generic activity-wheel entry. The nearest Anchor is resolved only
        /// to provide its stable id; the ActivityHost performs the actual
        /// definition lookup, rule evaluation and Session start.
        /// </summary>
        public void SelectActivity(string activityId)
        {
            ResolveActivityHost();
            if (_activityHost == null || string.IsNullOrWhiteSpace(activityId)) return;

            var anchor = ActivityAnchorNode.FindNearest(
                _activityHost.transform.position,
                _activityAnchorSearchDistance);
            if (anchor != null && anchor.Provides(activityId))
            {
                SelectActivityAtAnchor(anchor.AnchorId, activityId);
                return;
            }

            if (_activityHost.Catalog != null
                && _activityHost.Catalog.TryGet(activityId, out var definition)
                && definition.Scope == ActivityScope.Anywhere)
            {
                SelectAnywhereActivity(activityId);
                return;
            }

            Debug.LogWarning(
                $"[FirePlayMobileInputRouter] 当前 Anchor 未提供活动：{activityId}。请使用 SelectActivityAtAnchor 指定正确地点，或将活动定义为 Anywhere。",
                this);
        }

        /// <summary>Explicit activity selection for a wheel attached to a known Anchor.</summary>
        public void SelectActivityAtAnchor(string anchorId, string activityId)
        {
            ResolveActivityHost();
            if (_activityHost == null || string.IsNullOrWhiteSpace(anchorId) || string.IsNullOrWhiteSpace(activityId)) return;

            EnsureEventBus();
            _events?.Publish(new DemonViglu.FirePlay.Activity.ActivitySelectionRequested(
                _activityHost.PlayerId,
                anchorId,
                activityId));
        }

        /// <summary>Explicit entry for Anywhere activities such as emotes or guitar.</summary>
        public void SelectAnywhereActivity(string activityId)
        {
            ResolveActivityHost();
            if (_activityHost == null || string.IsNullOrWhiteSpace(activityId)) return;

            EnsureEventBus();
            _events?.Publish(new DemonViglu.FirePlay.Activity.ActivitySelectionRequested(
                _activityHost.PlayerId,
                string.Empty,
                activityId));
        }

        /// <summary>Opens the generic activity selection surface.</summary>
        public void OpenActivitySelection()
        {
            UIManager.GetInstance().ShowUIForms("ActivitySelectionForms");
        }

        private void Request(System.Action<FirePlayPlayerInput> request)
        {
            ResolvePlayerInput();
            if (_playerInput != null)
            {
                request(_playerInput);
            }
        }

        private void ResolvePlayerInput()
        {
            var currentPlayer = LocalPlayerContext.Current;
            if (_localPlayer != currentPlayer)
            {
                _localPlayer = currentPlayer;
                _playerInput = currentPlayer != null ? currentPlayer.Input : null;
                _rest = currentPlayer != null ? currentPlayer.RestInteraction : null;
            }

            if (_localPlayer == null) return;
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _playerInput ??= _localPlayer.Input;
            _rest ??= _localPlayer.RestInteraction;
        }

        private void ResolveActivityHost()
        {
            _activityHost ??= PlayerActivityHost.Local;
        }

        private void EnsureEventBus()
        {
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
        }
    }
}
