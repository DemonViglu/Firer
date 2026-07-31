using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Scene configuration for Fishing only. The activity owns its logic and
    /// timing parameters; no fishing fields leak into another activity factory.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingActivityLogicFactory : MonoBehaviour, IActivityLogicFactory
    {
        [SerializeField, Min(0f)] private float _rodFuelCost = 30f;
        [SerializeField, Min(1)] private int _catchesPerRod = 4;
        [SerializeField, Min(0f)] private float _fuelPerCatch = 8f;
        [SerializeField, Min(0.1f)] private float _minimumBiteDelay = 1.5f;
        [SerializeField, Min(0.1f)] private float _maximumBiteDelay = 4f;
        [SerializeField, Min(0.1f)] private float _hookWindowSeconds = 2f;

        public IActivityLogic Create(ActivityDefinition definition)
        {
            if (definition == null || definition.LogicKey != FishingActivityLogic.ActivityId)
                return null;

            return new FishingActivityLogic(
                _rodFuelCost,
                _catchesPerRod,
                _fuelPerCatch,
                _minimumBiteDelay,
                _maximumBiteDelay,
                _hookWindowSeconds);
        }

        private void OnValidate()
        {
            _rodFuelCost = Mathf.Max(0f, _rodFuelCost);
            _catchesPerRod = Mathf.Max(1, _catchesPerRod);
            _fuelPerCatch = Mathf.Max(0f, _fuelPerCatch);
            _minimumBiteDelay = Mathf.Max(0.1f, _minimumBiteDelay);
            _maximumBiteDelay = Mathf.Max(_minimumBiteDelay, _maximumBiteDelay);
            _hookWindowSeconds = Mathf.Max(0.1f, _hookWindowSeconds);
        }
    }
}
