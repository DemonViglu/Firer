using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Scene/bootstrap factory for the marshmallow definition. Keep this on a
    /// global activity bootstrap object, not on the Player; the Player only
    /// references the IActivityLogicFactory interface through PlayerActivityHost.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarshmallowActivityLogicFactory : MonoBehaviour, IActivityLogicFactory
    {
        [SerializeField, Min(0f)] private float _materializeFuelCost = 3f;
        [SerializeField, Min(1)] private int _turnsRequired = 2;
        [SerializeField, Min(0.05f)] private float _needleCyclesPerSecond = 0.35f;
        [SerializeField, Range(0.05f, 0.8f)] private float _perfectZoneWidth = 0.24f;
        [SerializeField, Range(0f, 0.5f)] private float _targetEdgePadding = 0.15f;
        [SerializeField, Min(0f)] private float _perfectEatFuelRefund = 2f;
        [SerializeField, Min(0f)] private float _toastedEatFuelRefund = 1f;
        [SerializeField, Min(0f)] private float _scorchedEatFuelRefund;

        public IActivityLogic Create(ActivityDefinition definition)
        {
            if (definition == null)
                return null;

            if (definition.LogicKey != MarshmallowActivityLogic.ActivityId)
                return null;

            return new MarshmallowActivityLogic(
                _materializeFuelCost,
                _turnsRequired,
                _needleCyclesPerSecond,
                _perfectZoneWidth,
                _targetEdgePadding,
                _perfectEatFuelRefund,
                _toastedEatFuelRefund,
                _scorchedEatFuelRefund);
        }

        private void OnValidate()
        {
            _materializeFuelCost = Mathf.Max(0f, _materializeFuelCost);
            _turnsRequired = Mathf.Max(1, _turnsRequired);
            _needleCyclesPerSecond = Mathf.Max(0.05f, _needleCyclesPerSecond);
            _perfectZoneWidth = Mathf.Clamp(_perfectZoneWidth, 0.05f, 0.8f);
            _targetEdgePadding = Mathf.Clamp(_targetEdgePadding, 0f, 0.5f - _perfectZoneWidth * 0.5f);
            _perfectEatFuelRefund = Mathf.Max(0f, _perfectEatFuelRefund);
            _toastedEatFuelRefund = Mathf.Max(0f, _toastedEatFuelRefund);
            _scorchedEatFuelRefund = Mathf.Max(0f, _scorchedEatFuelRefund);
        }
    }
}
