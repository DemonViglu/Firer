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
        [Header("Vertical Catch Tuning")]
        [SerializeField, Range(0.1f, 0.75f)] private float _catchZoneSize = 0.28f;
        [SerializeField, Min(0.1f)] private float _liftAcceleration = 2.25f;
        [SerializeField, Min(0.1f)] private float _gravityAcceleration = 1.65f;
        [SerializeField, Min(0f)] private float _catchBarDrag = 2.8f;
        [SerializeField, Min(0.1f)] private float _maximumCatchBarSpeed = 0.9f;
        [SerializeField, Min(0.05f)] private float _fishMoveSpeed = 0.52f;
        [SerializeField, Min(0.1f)] private float _minimumFishTargetDuration = 0.45f;
        [SerializeField, Min(0.1f)] private float _maximumFishTargetDuration = 1.15f;
        [SerializeField, Min(0.01f)] private float _catchGainPerSecond = 0.34f;
        [SerializeField, Min(0.01f)] private float _catchLossPerSecond = 0.2f;

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
                _hookWindowSeconds,
                _catchZoneSize,
                _liftAcceleration,
                _gravityAcceleration,
                _catchBarDrag,
                _maximumCatchBarSpeed,
                _fishMoveSpeed,
                _minimumFishTargetDuration,
                _maximumFishTargetDuration,
                _catchGainPerSecond,
                _catchLossPerSecond);
        }

        private void OnValidate()
        {
            _rodFuelCost = Mathf.Max(0f, _rodFuelCost);
            _catchesPerRod = Mathf.Max(1, _catchesPerRod);
            _fuelPerCatch = Mathf.Max(0f, _fuelPerCatch);
            _minimumBiteDelay = Mathf.Max(0.1f, _minimumBiteDelay);
            _maximumBiteDelay = Mathf.Max(_minimumBiteDelay, _maximumBiteDelay);
            _hookWindowSeconds = Mathf.Max(0.1f, _hookWindowSeconds);
            _catchZoneSize = Mathf.Clamp(_catchZoneSize, 0.1f, 0.75f);
            _liftAcceleration = Mathf.Max(0.1f, _liftAcceleration);
            _gravityAcceleration = Mathf.Max(0.1f, _gravityAcceleration);
            _catchBarDrag = Mathf.Max(0f, _catchBarDrag);
            _maximumCatchBarSpeed = Mathf.Max(0.1f, _maximumCatchBarSpeed);
            _fishMoveSpeed = Mathf.Max(0.05f, _fishMoveSpeed);
            _minimumFishTargetDuration = Mathf.Max(0.1f, _minimumFishTargetDuration);
            _maximumFishTargetDuration = Mathf.Max(_minimumFishTargetDuration, _maximumFishTargetDuration);
            _catchGainPerSecond = Mathf.Max(0.01f, _catchGainPerSecond);
            _catchLossPerSecond = Mathf.Max(0.01f, _catchLossPerSecond);
        }
    }
}
