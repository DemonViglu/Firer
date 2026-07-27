using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public sealed class MarshmallowInteraction : MonoBehaviour
    {
        [SerializeField] private RestInteraction _rest;
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private Transform _marshmallowProp;
        [SerializeField] private float _turnDegrees = 90f;

        private void Awake()
        {
            _rest ??= GetComponent<RestInteraction>();
            _input ??= GetComponent<FirePlayPlayerInput>();
            if (_rest == null || _input == null || _marshmallowProp == null) { enabled = false; return; }
            _marshmallowProp.gameObject.SetActive(false);
        }

        private void Update()
        {
            var active = _rest.IsResting && _rest.ActiveRestSpot != null && _rest.ActiveRestSpot.GetComponent<MarshmallowRitual>() != null;
            if (_marshmallowProp.gameObject.activeSelf != active) _marshmallowProp.gameObject.SetActive(active);
            if (active && _input.EmotePressedThisFrame) _marshmallowProp.Rotate(Vector3.forward, _turnDegrees, Space.Self);
        }
    }
}
