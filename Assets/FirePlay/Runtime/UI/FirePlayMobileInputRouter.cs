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
        [SerializeField] private PlayerRitualAnimationController _ritualAnimation;

        private void Awake()
        {
            ResolvePlayerInput();
        }

        public void SetMove(Vector2 value)
        {
            ResolvePlayerInput();
            _playerInput?.SetVirtualMove(value);
        }

        public void Interact() => Request(input => input.RequestVirtualInteract());
        public void Rest() => Request(input => input.RequestVirtualRest());
        public void RitualPrimary() => Request(input => input.RequestVirtualEmote());
        public void RitualSecondary() => Request(input => input.RequestVirtualInteract());
        public void PlaceFire() => Request(input => input.RequestVirtualPlaceFire());
        public void WithdrawOrReclaim() => Request(input => input.RequestVirtualUpgradeCampfire());
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
        }

        private void PlayExpression(RitualAnimationCue cue)
        {
            ResolvePlayerInput();
            _ritualAnimation?.Play(cue);
        }
    }
}
