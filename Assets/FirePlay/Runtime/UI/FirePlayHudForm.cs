using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using SUIFW;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// SUIFW 固定窗体：只把 FirePlay 的既有状态映射为 HUD，不修改玩法或输入。
    /// 当前可直接放在 Canvas 的 Fixed 层；待 SUIFW 的 Canvas / 配置资源补齐后，
    /// 也可作为同名 Fixed Form 交给 UIManager 加载。
    /// </summary>
    public sealed class FirePlayHudForm : BaseUIForms
    {
        [Header("State sources")]
        [SerializeField] private FlameResourceController _flameResource;
        [SerializeField] private PlayerInteraction _playerInteraction;
        [SerializeField] private RestInteraction _restInteraction;
        [SerializeField] private MarshmallowInteraction _marshmallowInteraction;
        [SerializeField] private FishingInteraction _fishingInteraction;

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
        [SerializeField] private GameObject _ritualPromptRoot;
        [SerializeField] private Text _ritualPromptText;
        [SerializeField] private bool _showLegacyRitualPrompt;

        private float _nextReferenceSearchTime;

        private void Awake()
        {
            ResolveUiReferences();
            ResolveReferences();
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextReferenceSearchTime)
            {
                ResolveUiReferences();
                ResolveReferences();
                _nextReferenceSearchTime = Time.unscaledTime + 1f;
            }

            UpdateFuel();
            UpdateInteractionPrompt();
            UpdateRitualPrompt();
        }

        private void ResolveReferences()
        {
            _flameResource ??= FindAnyObjectByType<FlameResourceController>();
            _playerInteraction ??= FindAnyObjectByType<PlayerInteraction>();
            _restInteraction ??= FindAnyObjectByType<RestInteraction>();
            _marshmallowInteraction ??= FindAnyObjectByType<MarshmallowInteraction>();
            _fishingInteraction ??= FindAnyObjectByType<FishingInteraction>();
        }

        private void ResolveUiReferences()
        {
            // Keep Inspector references authoritative, but make the fixed HUD robust
            // against normal hierarchy edits in the SUIFW Canvas prefab.
            _fuelRoot ??= FindChild("FuelRoot")?.gameObject;
            _fuelFill ??= FindChild("FuelFill")?.GetComponent<Image>();
            _flameImage ??= FindChild("FlameImage")?.GetComponent<Image>();
            _fuelText ??= FindChild("FuelText")?.GetComponent<Text>();
            _interactionPromptRoot ??= FindChild("InteractionPromptRoot")?.gameObject;
            _ritualPromptRoot ??= FindChild("RitualPromptRoot")?.gameObject;
            _interactionPromptText ??= FindText(_interactionPromptRoot);
            _ritualPromptText ??= FindText(_ritualPromptRoot);

            // The original SUIFW sample used a Simple Image. A fuel amount only
            // changes visually when the Image is a horizontal filled image.
            if (_fuelFill != null)
            {
                _fuelFill.type = Image.Type.Filled;
                _fuelFill.fillMethod = Image.FillMethod.Horizontal;
                _fuelFill.fillOrigin = 0;
            }
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
            var shouldShow = !string.IsNullOrWhiteSpace(prompt);
            SetActive(_interactionPromptRoot, shouldShow);
            if (shouldShow && _interactionPromptText != null)
            {
                _interactionPromptText.text = prompt;
            }
        }

        private void UpdateRitualPrompt()
        {
            if (!_showLegacyRitualPrompt)
            {
                SetActive(_ritualPromptRoot, false);
                return;
            }

            var activeSpot = _restInteraction != null && _restInteraction.IsResting
                ? _restInteraction.ActiveRestSpot
                : null;
            if (activeSpot == null)
            {
                SetActive(_ritualPromptRoot, false);
                return;
            }

            string status = null;
            var fishingRitual = activeSpot.GetComponent<FishingRitual>();
            var marshmallowRitual = activeSpot.GetComponent<MarshmallowRitual>();
            if (fishingRitual != null && _fishingInteraction != null)
            {
                status = _fishingInteraction.Status;
            }
            else if (marshmallowRitual != null && _marshmallowInteraction != null)
            {
                status = _marshmallowInteraction.Status;
            }

            // Rest is itself a mobile action. Showing it as a fallback prevents a
            // silent panel when a site has no ritual component yet.
            status ??= "Resting — choose a ritual or stand up";

            var shouldShow = !string.IsNullOrWhiteSpace(status);
            SetActive(_ritualPromptRoot, shouldShow);
            if (shouldShow && _ritualPromptText != null)
            {
                _ritualPromptText.text = status;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private Transform FindChild(string childName)
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static Text FindText(GameObject root)
        {
            return root != null ? root.GetComponentInChildren<Text>(true) : null;
        }
    }
}
