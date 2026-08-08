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
        [Header("Fight Tuning")]
        [SerializeField, Range(0.01f, 1f)] private float _reelTensionGain = 0.2f;
        [SerializeField, Range(0.01f, 1f)] private float _reelProgressGain = 0.2f;
        [SerializeField, Range(0.01f, 1f)] private float _easeTensionDrop = 0.34f;
        [SerializeField, Range(0f, 1f)] private float _easeProgressLoss = 0.06f;
        [SerializeField, Min(0f)] private float _fishPullPerSecond = 0.035f;
        [SerializeField, Min(0f)] private float _fishPullVariance = 0.08f;

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
                _reelTensionGain,
                _reelProgressGain,
                _easeTensionDrop,
                _easeProgressLoss,
                _fishPullPerSecond,
                _fishPullVariance);
        }

        private void OnValidate()
        {
            _rodFuelCost = Mathf.Max(0f, _rodFuelCost);
            _catchesPerRod = Mathf.Max(1, _catchesPerRod);
            _fuelPerCatch = Mathf.Max(0f, _fuelPerCatch);
            _minimumBiteDelay = Mathf.Max(0.1f, _minimumBiteDelay);
            _maximumBiteDelay = Mathf.Max(_minimumBiteDelay, _maximumBiteDelay);
            _hookWindowSeconds = Mathf.Max(0.1f, _hookWindowSeconds);
            _reelTensionGain = Mathf.Clamp(_reelTensionGain, 0.01f, 1f);
            _reelProgressGain = Mathf.Clamp(_reelProgressGain, 0.01f, 1f);
            _easeTensionDrop = Mathf.Clamp(_easeTensionDrop, 0.01f, 1f);
            _easeProgressLoss = Mathf.Clamp01(_easeProgressLoss);
            _fishPullPerSecond = Mathf.Max(0f, _fishPullPerSecond);
            _fishPullVariance = Mathf.Max(0f, _fishPullVariance);
        }
    }
}
