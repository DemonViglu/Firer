using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// First playable mobile composition. It builds inside SUIFW's Fixed layer so
    /// scene authors can later replace its visuals without coupling buttons to world
    /// objects. It deliberately reads existing state and delegates every command to
    /// FirePlayMobileInputRouter.
    /// </summary>
    public sealed class FirePlayMobileOverlay : MonoBehaviour
    {
        private const string RuntimeRootName = "FirePlayMobileOverlay";

        private FirePlayMobileInputRouter _router;
        private FlameResourceController _flame;
        private PlayerInteraction _interaction;
        private RestInteraction _rest;
        private MarshmallowInteraction _marshmallow;
        private FishingInteraction _fishing;
        private Image _fuelFill;
        private Text _fuelText;
        private GameObject _contextButton;
        private Text _contextText;
        private GameObject _restButton;
        private Text _restText;
        private GameObject _ritualPanel;
        private Text _ritualStatus;
        private GameObject _expressionWheel;
        private Font _font;

        public static void Ensure(Transform fixedLayer)
        {
            if (fixedLayer == null || fixedLayer.Find(RuntimeRootName) != null)
            {
                return;
            }

            var root = new GameObject(RuntimeRootName, typeof(RectTransform));
            root.transform.SetParent(fixedLayer, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            root.AddComponent<FirePlayMobileOverlay>();
        }

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _router = gameObject.AddComponent<FirePlayMobileInputRouter>();
            BuildLayout();
            HideLegacyHudNodes();
        }

        private void Update()
        {
            ResolveReferences();
            UpdateFuel();
            UpdateContextActions();
            UpdateRestAndRitual();
        }

        private void ResolveReferences()
        {
            _flame ??= FindAnyObjectByType<FlameResourceController>();
            _interaction ??= FindAnyObjectByType<PlayerInteraction>();
            _rest ??= FindAnyObjectByType<RestInteraction>();
            _marshmallow ??= FindAnyObjectByType<MarshmallowInteraction>();
            _fishing ??= FindAnyObjectByType<FishingInteraction>();
        }

        private void UpdateFuel()
        {
            var state = _flame != null ? _flame.State : null;
            if (state == null)
            {
                return;
            }

            var normalized = state.NormalizedFuel;
            _fuelFill.fillAmount = normalized;
            _fuelFill.color = Color.Lerp(new Color(0.5f, 0.08f, 0.03f), new Color(1f, 0.72f, 0.2f), normalized);
            _fuelText.text = $"FLAME  {state.CurrentFuel:0} / {state.MaxFuel:0}";
        }

        private void UpdateContextActions()
        {
            var prompt = _interaction != null ? _interaction.CurrentInteractPrompt : null;
            var visible = _rest == null || !_rest.IsResting;
            visible &= !string.IsNullOrWhiteSpace(prompt) && prompt != "No interaction target";
            _contextButton.SetActive(visible);
            if (visible)
            {
                _contextText.text = prompt;
            }
        }

        private void UpdateRestAndRitual()
        {
            var resting = _rest != null && _rest.IsResting;
            var restAvailable = resting || (_rest != null && _rest.NearestRestSpot != null);
            _restButton.SetActive(restAvailable);
            _restText.text = resting ? "STAND" : "REST";
            _ritualPanel.SetActive(resting);
            if (!resting)
            {
                return;
            }

            var spot = _rest.ActiveRestSpot;
            if (spot != null && spot.GetComponent<FishingRitual>() != null && _fishing != null)
            {
                _ritualStatus.text = _fishing.Status;
            }
            else if (spot != null && spot.GetComponent<MarshmallowRitual>() != null && _marshmallow != null)
            {
                _ritualStatus.text = _marshmallow.Status;
            }
            else
            {
                _ritualStatus.text = "Resting in warmth";
            }
        }

        private void BuildLayout()
        {
            var fuelPanel = CreatePanel("Fuel", new Vector2(18, -18), new Vector2(260, 56), new Vector2(0, 1));
            _fuelText = CreateText(fuelPanel.transform, "Label", "FLAME", 18, TextAnchor.UpperLeft, Color.white);
            SetRect(_fuelText.rectTransform, new Vector2(12, -8), new Vector2(236, 24), new Vector2(0, 1));
            var background = CreateImage(fuelPanel.transform, "BarBackground", new Color(0.08f, 0.035f, 0.02f, 0.9f));
            SetRect(background.rectTransform, new Vector2(12, -38), new Vector2(236, 10), new Vector2(0, 1));
            _fuelFill = CreateImage(background.transform, "Fill", new Color(1f, 0.7f, 0.2f));
            _fuelFill.type = Image.Type.Filled;
            _fuelFill.fillMethod = Image.FillMethod.Horizontal;
            _fuelFill.raycastTarget = false;
            Stretch(_fuelFill.rectTransform, Vector2.zero, Vector2.zero);

            var joystick = CreateImage(transform, "MoveJoystick", new Color(0.08f, 0.06f, 0.12f, 0.72f));
            SetRect(joystick.rectTransform, new Vector2(36, 38), new Vector2(156, 156), Vector2.zero);
            var handle = CreateImage(joystick.transform, "Handle", new Color(1f, 0.77f, 0.38f, 0.92f));
            SetRect(handle.rectTransform, Vector2.zero, new Vector2(66, 66), new Vector2(0.5f, 0.5f));
            var joystickControl = joystick.gameObject.AddComponent<FirePlayMobileJoystick>();
            joystickControl.Configure(_router, joystick.rectTransform, handle.rectTransform);

            _contextButton = CreateButton("ContextAction", "INTERACT", _router.Interact, new Vector2(0, 38), new Vector2(340, 58), new Vector2(0.5f, 0), new Color(0.38f, 0.16f, 0.055f, 0.94f), out _contextText);
            _restButton = CreateButton("Rest", "REST", _router.Rest, new Vector2(-34, 44), new Vector2(108, 58), new Vector2(1, 0), new Color(0.16f, 0.12f, 0.22f, 0.95f), out _restText);
            var wheelToggle = CreateButton("ExpressionToggle", "✦", ToggleExpressionWheel, new Vector2(-34, 116), new Vector2(62, 62), new Vector2(1, 0), new Color(0.26f, 0.13f, 0.24f, 0.96f), out _);
            _expressionWheel = CreateExpressionWheel(wheelToggle.transform.parent);
            _expressionWheel.SetActive(false);

            _ritualPanel = CreatePanel("RitualPanel", new Vector2(0, 122), new Vector2(440, 148), new Vector2(0.5f, 0));
            _ritualStatus = CreateText(_ritualPanel.transform, "Status", "", 18, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.7f));
            SetRect(_ritualStatus.rectTransform, new Vector2(16, -12), new Vector2(408, 48), new Vector2(0, 1));
            CreateButton(_ritualPanel.transform, "Primary", "DO", _router.RitualPrimary, new Vector2(16, 14), new Vector2(190, 48), Vector2.zero, new Color(0.6f, 0.28f, 0.06f, 0.98f), out _);
            CreateButton(_ritualPanel.transform, "Secondary", "TAKE", _router.RitualSecondary, new Vector2(-16, 14), new Vector2(190, 48), new Vector2(1, 0), new Color(0.23f, 0.17f, 0.12f, 0.98f), out _);
        }

        private GameObject CreateExpressionWheel(Transform parent)
        {
            var root = new GameObject("ExpressionWheel", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            SetRect(rect, new Vector2(-142, 184), new Vector2(220, 120), new Vector2(1, 0));
            CreateButton(root.transform, "Wave", "WAVE", _router.ExpressionWave, new Vector2(0, 66), new Vector2(104, 46), new Vector2(0, 0), new Color(0.18f, 0.19f, 0.3f, 0.96f), out _);
            CreateButton(root.transform, "Thanks", "THANKS", _router.ExpressionThanks, new Vector2(116, 66), new Vector2(104, 46), new Vector2(0, 0), new Color(0.18f, 0.19f, 0.3f, 0.96f), out _);
            CreateButton(root.transform, "Warmth", "WARMTH", _router.ExpressionWarmth, new Vector2(0, 10), new Vector2(104, 46), new Vector2(0, 0), new Color(0.32f, 0.16f, 0.08f, 0.96f), out _);
            CreateButton(root.transform, "Sit", "SIT", _router.ExpressionSit, new Vector2(116, 10), new Vector2(104, 46), new Vector2(0, 0), new Color(0.18f, 0.19f, 0.3f, 0.96f), out _);
            return root;
        }

        private void ToggleExpressionWheel()
        {
            _expressionWheel.SetActive(!_expressionWheel.activeSelf);
        }

        private void HideLegacyHudNodes()
        {
            var hud = FindAnyObjectByType<FirePlayHudForm>();
            if (hud == null)
            {
                return;
            }

            foreach (var name in new[] { "FuelRoot", "InteractionPromptRoot", "RitualPromptRoot" })
            {
                var node = FindDeepChild(hud.transform, name);
                if (node != null)
                {
                    node.gameObject.SetActive(false);
                }
            }

            // The overlay now owns the same three display responsibilities. Disable
            // the legacy presenter after hiding its authored placeholders so its
            // Update loop cannot reactivate them on the next frame.
            hud.enabled = false;
        }

        private GameObject CreatePanel(string name, Vector2 position, Vector2 size, Vector2 anchor)
        {
            var panel = CreateImage(transform, name, new Color(0.055f, 0.035f, 0.075f, 0.9f));
            SetRect(panel.rectTransform, position, size, anchor);
            return panel.gameObject;
        }

        private GameObject CreateButton(string name, string label, UnityAction action, Vector2 position, Vector2 size, Vector2 anchor, Color color, out Text text)
        {
            return CreateButton(transform, name, label, action, position, size, anchor, color, out text);
        }

        private GameObject CreateButton(Transform parent, string name, string label, UnityAction action, Vector2 position, Vector2 size, Vector2 anchor, Color color, out Text text)
        {
            var image = CreateImage(parent, name, color);
            SetRect(image.rectTransform, position, size, anchor);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            text = CreateText(image.transform, "Label", label, 16, TextAnchor.MiddleCenter, Color.white);
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            return image.gameObject;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            target.transform.SetParent(parent, false);
            var image = target.GetComponent<Image>();
            image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            image.color = color;
            return image;
        }

        private Text CreateText(Transform parent, string name, string value, int size, TextAnchor alignment, Color color)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            target.transform.SetParent(parent, false);
            var text = target.GetComponent<Text>();
            text.font = _font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static void SetRect(RectTransform target, Vector2 position, Vector2 size, Vector2 anchor)
        {
            target.anchorMin = anchor;
            target.anchorMax = anchor;
            target.pivot = anchor;
            target.anchoredPosition = position;
            target.sizeDelta = size;
        }

        private static void Stretch(RectTransform target, Vector2 offsetMin, Vector2 offsetMax)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = offsetMin;
            target.offsetMax = offsetMax;
        }

        private static Transform FindDeepChild(Transform root, string targetName)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == targetName)
                {
                    return child;
                }
            }
            return null;
        }

    }
}
