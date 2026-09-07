using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.Activity;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Persistent HUD view: maps existing FirePlay state to presentation only.
    /// </summary>
    public sealed class FirePlayHudForm : FirePlayUiView
    {
        [Header("State sources")]
        [SerializeField] private FlameResourceController _flameResource;
        [SerializeField] private PlayerInteraction _playerInteraction;
        [SerializeField] private RestInteraction _restInteraction;
        [SerializeField] private LocalPlayerContext _localPlayer;
        [SerializeField] private PlayerActivityHost _activityHost;

        [Header("Fuel")]
        [SerializeField] private GameObject _fuelRoot;
        [SerializeField] private Image _fuelFill;
        [SerializeField] private Image _flameImage;
        [SerializeField] private Text _fuelText;
        [SerializeField] private Color _lowFuelColor = new(0.38f, 0.08f, 0.025f, 1f);
        [SerializeField] private Color _fullFuelColor = new(1f, 0.68f, 0.2f, 1f);

        [Header("Prompts")]
        [SerializeField] private GameObject _interactionPromptRoot;
        [SerializeField] private Text _interactionPromptText;

        [Header("Exploration controls")]
        [SerializeField] private GameObject _explorationControlsRoot;
        [SerializeField] private GameObject _jumpButtonRoot;
        [SerializeField] private GameObject _placeFireButtonRoot;
        [SerializeField] private GameObject _restButtonRoot;
        [SerializeField] private GameObject _expressionButtonRoot;

        private float _nextReferenceSearchTime;

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextReferenceSearchTime)
            {
                ResolveReferences();
                _nextReferenceSearchTime = Time.unscaledTime + 1f;
            }

            UpdateFuel();
            UpdateInteractionPrompt();
            UpdateExplorationControls();
        }

        private void ResolveReferences()
        {
            _localPlayer = LocalPlayerContext.Current;
            _flameResource = _localPlayer != null ? _localPlayer.FlameResource : null;
            _playerInteraction = _localPlayer != null ? _localPlayer.Interaction : null;
            _restInteraction = _localPlayer != null ? _localPlayer.RestInteraction : null;
            _activityHost = PlayerActivityHost.Local;
        }

        private void UpdateFuel()
        {
            var state = _flameResource != null ? _flameResource.State : null;
            SetActive(_fuelRoot, state != null);
            if (state == null)
            {
                return;
            }

            var normalizedFuel = state.NormalizedFuel;
            var color = Color.Lerp(_lowFuelColor, _fullFuelColor, normalizedFuel);
            if (_fuelFill != null)
            {
                _fuelFill.fillAmount = normalizedFuel;
                _fuelFill.color = color;
            }

            if (_flameImage != null)
            {
                _flameImage.color = color;
            }

            if (_fuelText != null)
            {
                _fuelText.text = $"{state.CurrentFuel:0} / {state.MaxFuel:0}";
            }
        }

        private void UpdateInteractionPrompt()
        {
            var prompt = _playerInteraction != null ? _playerInteraction.CurrentInteractPrompt : null;
            var shouldShow = !HasActiveActivity() && !string.IsNullOrWhiteSpace(prompt);
            SetActive(_interactionPromptRoot, shouldShow);
            if (shouldShow && _interactionPromptText != null)
            {
                _interactionPromptText.text = prompt;
            }
        }

        private void UpdateExplorationControls()
        {
            // HUD controls are isolated by the FirePlay UI layer and focus policy.
            // Hide the jump receiver while an activity owns movement so it
            // cannot steal clicks from the activity's own UI.
            var movement = _localPlayer != null ? _localPlayer.Movement : null;
            var activityActive = HasActiveActivity();
            var activityOwnsMovement = activityActive && movement != null && movement.MovementLocked;
            SetActive(_explorationControlsRoot, movement != null && !activityOwnsMovement);
            SetActive(_jumpButtonRoot, movement != null && !activityOwnsMovement);

            // Optional feature buttons follow explicit Player composition. A
            // partially assembled scene must not display controls whose intent
            // router or consumer has not been attached yet.
            var hasIntentRouter = _localPlayer != null
                && _localPlayer.InteractionRouter != null;
            SetActive(
                _placeFireButtonRoot,
                !activityOwnsMovement
                && hasIntentRouter
                && _localPlayer.FlameModule?.CampfirePlacement != null);
            SetActive(
                _restButtonRoot,
                !activityOwnsMovement
                && hasIntentRouter
                && _localPlayer.RestInteraction != null);
            SetActive(
                _expressionButtonRoot,
                !activityOwnsMovement
                && _localPlayer?.Expressions != null);
        }

        private bool HasActiveActivity() => _activityHost != null && _activityHost.HasActiveActivity;

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

    }
}
