using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
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
        [SerializeField] private PlayerRitualAnimationController _ritualAnimation;
        [SerializeField] private RestInteraction _rest;
        [SerializeField] private MarshmallowInteraction _marshmallow;
        [SerializeField] private FishingInteraction _fishing;

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
            var isFishingSpot = _rest != null
                && _rest.IsResting
                && _rest.ActiveRestSpot != null
                && _rest.ActiveRestSpot.GetComponent<FishingRitual>() != null;
            if (isFishingSpot && _fishing != null)
            {
                _fishing.TryPrimaryAction();
                return;
            }

            _playerInput?.RequestVirtualEmote();
        }
        public void RitualSecondary()
        {
            ResolvePlayerInput();

            // During an unfinished marshmallow roast, the secondary button is an
            // explicit leave action. Once it is ready, that same slot collects it.
            // Fishing keeps its existing secondary interaction (wait / reel in).
            var isMarshmallowSpot = _rest != null
                && _rest.IsResting
                && _rest.ActiveRestSpot != null
                && _rest.ActiveRestSpot.GetComponent<MarshmallowRitual>() != null;
            if (isMarshmallowSpot && (_marshmallow == null || !_marshmallow.IsReadyToEat))
            {
                _playerInput?.RequestVirtualRest();
                return;
            }

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
        public void ExpressionWave() => PlayExpression(RitualAnimationCue.ExpressionWave);
        public void ExpressionThanks() => PlayExpression(RitualAnimationCue.ExpressionThanks);
        public void ExpressionWarmth() => PlayExpression(RitualAnimationCue.ExpressionWarmth);
        public void ExpressionSit() => PlayExpression(RitualAnimationCue.ExpressionSit);

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
            _playerInput ??= FindAnyObjectByType<FirePlayPlayerInput>();
            _ritualAnimation ??= FindAnyObjectByType<PlayerRitualAnimationController>();
            _rest ??= FindAnyObjectByType<RestInteraction>();
            _marshmallow ??= FindAnyObjectByType<MarshmallowInteraction>();
            _fishing ??= FindAnyObjectByType<FishingInteraction>();
        }

        private void PlayExpression(RitualAnimationCue cue)
        {
            ResolvePlayerInput();
            _ritualAnimation?.Play(cue);
        }
    }
}
