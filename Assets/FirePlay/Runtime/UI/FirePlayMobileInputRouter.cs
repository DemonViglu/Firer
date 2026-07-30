using DemonViglu.FirePlay.Player;
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
            _localPlayer ??= LocalPlayerContext.Current;
            if (_localPlayer == null) return;
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _playerInput ??= _localPlayer.Input;
            _rest ??= _localPlayer.RestInteraction;
        }
    }
}
