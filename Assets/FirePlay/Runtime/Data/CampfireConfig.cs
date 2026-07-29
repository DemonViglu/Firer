using UnityEngine;

namespace DemonViglu.FirePlay.Data
{
    [CreateAssetMenu(menuName = "DemonViglu/FirePlay/Campfire Config", fileName = "CampfireConfig_")]
    public sealed class CampfireConfig : ScriptableObject
    {
        [Header("Growth")]
        [SerializeField] private float[] _levelContributionThresholds = { 0f, 20f, 55f, 110f };
        [SerializeField, Min(0.01f)] private float _tendFuelCost = 5f;

        [Header("Fire strength")]
        [SerializeField, Min(1f)] private float _maximumWarmth = 100f;
        [SerializeField, Range(0f, 1f)] private float _initialWarmthNormalized = 1f;
        [SerializeField, Min(0f)] private float _warmthDecayPerSecond = 0.1f;
        [SerializeField, Min(0.01f)] private float _warmthPerTend = 16f;

        [Header("Player recovery")]
        [SerializeField, Min(0.1f)] private float _nearbyRecoveryRadius = 3.5f;
        [SerializeField, Min(0f)] private float _nearbyRecoveryPerSecond = 1.2f;
        [SerializeField, Min(0f)] private float _restingRecoveryPerSecond = 5f;
        [SerializeField, Min(0.01f)] private float _warmthCostPerRecoveredFuel = 4f;

        public int MaximumLevel => _levelContributionThresholds != null
            ? Mathf.Max(0, _levelContributionThresholds.Length - 1)
            : 0;
        public float TendFuelCost => _tendFuelCost;
        public float MaximumWarmth => _maximumWarmth;
        public float InitialWarmth => _maximumWarmth * _initialWarmthNormalized;
        public float WarmthDecayPerSecond => _warmthDecayPerSecond;
        public float WarmthPerTend => _warmthPerTend;
        public float NearbyRecoveryRadius => _nearbyRecoveryRadius;
        public float NearbyRecoveryPerSecond => _nearbyRecoveryPerSecond;
        public float RestingRecoveryPerSecond => _restingRecoveryPerSecond;
        public float WarmthCostPerRecoveredFuel => _warmthCostPerRecoveredFuel;

        public float GetUpgradeCost(int currentLevel)
        {
            if (_levelContributionThresholds == null || currentLevel < 0 || currentLevel >= _levelContributionThresholds.Length)
            {
                return 0f;
            }

            return Mathf.Max(0f, _levelContributionThresholds[currentLevel]);
        }

        public int GetLevelForContribution(float contribution)
        {
            if (_levelContributionThresholds == null || _levelContributionThresholds.Length == 0)
            {
                return 0;
            }

            var level = 0;
            for (var index = 1; index < _levelContributionThresholds.Length; index++)
            {
                if (contribution < _levelContributionThresholds[index])
                {
                    break;
                }
                level = index;
            }
            return level;
        }

        private void OnValidate()
        {
            if (_levelContributionThresholds == null || _levelContributionThresholds.Length == 0)
            {
                _levelContributionThresholds = new[] { 0f };
                return;
            }

            _levelContributionThresholds[0] = 0f;
            for (var index = 1; index < _levelContributionThresholds.Length; index++)
            {
                _levelContributionThresholds[index] = Mathf.Max(_levelContributionThresholds[index - 1] + 0.01f, _levelContributionThresholds[index]);
            }
            _tendFuelCost = Mathf.Max(0.01f, _tendFuelCost);
            _maximumWarmth = Mathf.Max(1f, _maximumWarmth);
            _warmthDecayPerSecond = Mathf.Max(0f, _warmthDecayPerSecond);
            _warmthPerTend = Mathf.Max(0.01f, _warmthPerTend);
            _nearbyRecoveryRadius = Mathf.Max(0.1f, _nearbyRecoveryRadius);
            _nearbyRecoveryPerSecond = Mathf.Max(0f, _nearbyRecoveryPerSecond);
            _restingRecoveryPerSecond = Mathf.Max(0f, _restingRecoveryPerSecond);
            _warmthCostPerRecoveredFuel = Mathf.Max(0.01f, _warmthCostPerRecoveredFuel);
        }
    }
}
