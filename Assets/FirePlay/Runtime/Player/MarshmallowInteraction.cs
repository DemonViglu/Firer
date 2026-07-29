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

        private MarshmallowRitual _activeRitual;
        private bool _hasMaterializedMarshmallow;

        public string Status { get; private set; } = "Sit by a campfire to roast";
        public bool HasMaterializedMarshmallow => _hasMaterializedMarshmallow;

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
                _hasMaterializedMarshmallow = false;
                Status = ritual == null ? "Sit by a campfire to roast" : $"Press Q: materialize marshmallow ({ritual.MaterializeFuelCost:0})";
            }

            if (_input.EmotePressedThisFrame && _activeRitual != null)
            {
                if (!_hasMaterializedMarshmallow)
                {
                    _hasMaterializedMarshmallow = _activeRitual.TryMaterialize(_resourceController, out var materializeStatus);
                    Status = materializeStatus;
                }
                else
                {
                    _marshmallowProp.Rotate(Vector3.forward, _turnDegrees, Space.Self);
                    Status = "Roasting";
                }
            }

            if (_marshmallowProp.gameObject.activeSelf != _hasMaterializedMarshmallow)
            {
                _marshmallowProp.gameObject.SetActive(_hasMaterializedMarshmallow);
            }
        }
    }
}
