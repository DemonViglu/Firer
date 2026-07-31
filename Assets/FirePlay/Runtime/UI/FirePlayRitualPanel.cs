using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.Activity;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>Prefab-driven ritual panel state. It never creates or lays out UI.</summary>
    public sealed class FirePlayRitualPanel : MonoBehaviour
    {
        [SerializeField] private LocalPlayerContext _localPlayer;
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Text _statusText;
        [SerializeField] private Text _primaryActionText;
        [SerializeField] private Text _secondaryActionText;
        private RitualInteractionCoordinator _coordinator;
        private FirePlayRitualContentBinding[] _contentBindings;

        private void Awake()
        {
            _panelRoot ??= gameObject;
            _contentBindings = GetComponentsInChildren<FirePlayRitualContentBinding>(true);
        }

        private void Update()
        {
            _localPlayer ??= LocalPlayerContext.Current;
            _coordinator ??= _localPlayer != null ? _localPlayer.RitualCoordinator : null;
            // The new Activity PresentationHost owns its dedicated form. Hide
            // the legacy ritual panel while that Session is active so its
            // RitualPrimary/RitualSecondary buttons cannot steal the click.
            var activityHost = PlayerActivityHost.Local;
            if (activityHost != null && activityHost.HasActiveActivity)
            {
                SetActive(_panelRoot, false);
                return;
            }
            var view = _coordinator != null ? _coordinator.ViewState : default;
            SetActive(_panelRoot, view.IsVisible);
            foreach (var binding in _contentBindings)
                binding.SetVisible(view.IsVisible && binding.RitualId == view.RitualId);
            if (!view.IsVisible) return;

            SetText(_statusText, view.Status);
            SetText(_primaryActionText, view.PrimaryLabel);
            SetText(_secondaryActionText, view.SecondaryLabel);
            SetInteractable(_primaryActionText, view.PrimaryAvailable);
            SetInteractable(_secondaryActionText, view.SecondaryAvailable);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value) target.SetActive(value);
        }
        private static void SetText(Text target, string value)
        {
            if (target != null) target.text = value;
        }
        private static void SetInteractable(Text label, bool value)
        {
            var button = label != null ? label.GetComponentInParent<Button>(true) : null;
            if (button != null) button.interactable = value;
        }
    }
}
