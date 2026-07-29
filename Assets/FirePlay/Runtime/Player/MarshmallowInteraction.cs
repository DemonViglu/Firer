using DemonViglu.FirePlay.World;
using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public sealed class MarshmallowInteraction : MonoBehaviour
    {
        [SerializeField] private RestInteraction _rest;
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private Transform _marshmallowProp;
        [SerializeField] private float _turnDegrees = 90f;
        [Header("Prototype Meter")]
        [SerializeField] private bool _showPrototypeMeter = true;
        [SerializeField, Min(120f)] private float _meterWidth = 420f;
        [SerializeField, Min(12f)] private float _meterHeight = 28f;
        [SerializeField, Min(0f)] private float _meterBottomMargin = 70f;

        private MarshmallowRitual _activeRitual;
        private bool _hasMaterializedMarshmallow;
        private MarshmallowRoastSession _roastSession;
        private GUIStyle _meterLabelStyle;

        public string Status { get; private set; } = "Sit by a campfire to roast";
        public bool HasMaterializedMarshmallow => _hasMaterializedMarshmallow;
        public bool IsRoasting => _roastSession != null && _roastSession.IsRoasting;
        public bool IsReadyToEat => _roastSession != null && _roastSession.IsReadyToEat;
        public int CompletedTurns => _roastSession?.CompletedTurns ?? 0;
        public int PerfectTurns => _roastSession?.PerfectTurns ?? 0;

        private void Awake()
        {
            _rest ??= GetComponent<RestInteraction>();
            _input ??= GetComponent<FirePlayPlayerInput>();
            _resourceController ??= GetComponent<FlameResourceController>();
            if (_rest == null || _input == null || _resourceController == null || _marshmallowProp == null) { enabled = false; return; }
            _marshmallowProp.gameObject.SetActive(false);
        }

        private void Update()
        {
            var ritual = _rest.IsResting && _rest.ActiveRestSpot != null
                ? _rest.ActiveRestSpot.GetComponent<MarshmallowRitual>()
                : null;
            if (ritual != _activeRitual)
            {
                _activeRitual = ritual;
                ClearSession();
                Status = ritual == null ? "Sit by a campfire to roast" : $"Press Q: materialize marshmallow ({ritual.MaterializeFuelCost:0})";
            }

            if (_activeRitual != null && _hasMaterializedMarshmallow && !_activeRitual.IsCampfireBurning)
            {
                ClearSession();
                Status = "The fire went out";
            }

            if (_input.EmotePressedThisFrame && _activeRitual != null)
            {
                HandleEmotePressed();
            }

            if (_input.InteractPressedThisFrame && IsReadyToEat)
            {
                EatMarshmallow();
            }

            _roastSession?.Advance(Time.deltaTime);

            if (_marshmallowProp.gameObject.activeSelf != _hasMaterializedMarshmallow)
            {
                _marshmallowProp.gameObject.SetActive(_hasMaterializedMarshmallow);
            }
        }

        private void HandleEmotePressed()
        {
            if (!_hasMaterializedMarshmallow)
            {
                _hasMaterializedMarshmallow = _activeRitual.TryMaterialize(_resourceController, out var materializeStatus);
                Status = materializeStatus;
                if (_hasMaterializedMarshmallow)
                {
                    BeginRoasting();
                }

                return;
            }

            if (!IsRoasting)
            {
                return;
            }

            _marshmallowProp.Rotate(Vector3.forward, _turnDegrees, Space.Self);
            var isPerfect = _roastSession.TryTurn();

            if (IsReadyToEat)
            {
                Status = _roastSession.IsPerfect
                    ? "Perfectly toasted — Press E to eat"
                    : "Toasted — Press E to eat";
                return;
            }

            Status = isPerfect ? "Golden turn" : "A little uneven";
        }

        private void BeginRoasting()
        {
            _roastSession = new MarshmallowRoastSession(
                _activeRitual.TurnsRequired,
                _activeRitual.NeedleCyclesPerSecond,
                _activeRitual.PerfectZoneWidth,
                _activeRitual.TargetEdgePadding);
            Status = "Press Q in the golden zone";
        }

        private void EatMarshmallow()
        {
            var refund = _activeRitual.EatFuelRefund;
            if (refund > 0f)
            {
                _resourceController.Restore(refund);
            }

            var wasPerfect = _roastSession.IsPerfect;
            ClearSession();
            Status = wasPerfect ? "A warm, perfect bite" : "A warm bite";
        }

        private void ClearSession()
        {
            _hasMaterializedMarshmallow = false;
            _roastSession = null;
        }

        private void OnGUI()
        {
            if (!_showPrototypeMeter || !IsRoasting || _activeRitual == null)
            {
                return;
            }

            EnsureMeterStyle();
            var x = (Screen.width - _meterWidth) * 0.5f;
            var y = Screen.height - _meterBottomMargin - _meterHeight;
            var meterRect = new Rect(x, y, _meterWidth, _meterHeight);
            DrawRect(new Rect(x - 3f, y - 3f, _meterWidth + 6f, _meterHeight + 6f), new Color(1f, 0.72f, 0.28f, 0.96f));
            DrawRect(meterRect, new Color(0.16f, 0.055f, 0.025f, 0.98f));
            DrawRect(new Rect(x + 4f, y + 4f, _meterWidth - 8f, _meterHeight - 8f), new Color(0.32f, 0.12f, 0.045f, 1f));

            var zoneWidth = _meterWidth * _activeRitual.PerfectZoneWidth;
            var zoneRect = new Rect(x + _meterWidth * _roastSession.TargetCenter - zoneWidth * 0.5f, y + 4f, zoneWidth, _meterHeight - 8f);
            DrawRect(zoneRect, new Color(1f, 0.58f, 0.08f, 1f));
            DrawRect(new Rect(zoneRect.x + 3f, zoneRect.y + 3f, zoneRect.width - 6f, zoneRect.height - 6f), new Color(1f, 0.84f, 0.28f, 1f));
            var needleRect = new Rect(x + _meterWidth * _roastSession.NeedlePosition - 3f, y - 6f, 6f, _meterHeight + 12f);
            DrawRect(new Rect(needleRect.x - 2f, needleRect.y, needleRect.width + 4f, needleRect.height), new Color(0.08f, 0.025f, 0.01f, 1f));
            DrawRect(needleRect, new Color(1f, 0.96f, 0.78f, 1f));

            GUI.Label(new Rect(x, y - 25f, _meterWidth, 22f),
                $"Turn {_roastSession.CompletedTurns + 1}/{_roastSession.TurnsRequired} — Press Q in the golden zone",
                _meterLabelStyle);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void EnsureMeterStyle()
        {
            if (_meterLabelStyle != null)
            {
                return;
            }

            _meterLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal = { textColor = new Color(1f, 0.9f, 0.65f) }
            };
        }
    }
}
