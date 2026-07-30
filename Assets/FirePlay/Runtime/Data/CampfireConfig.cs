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

        [Header("Comfort area")]
        [SerializeField, Min(0.1f)] private float _nearbyComfortRadius = 3.5f;

        [Header("Emergency warmth withdrawal")]
        [SerializeField, Min(0.01f)] private float _emergencyWithdrawFuel = 5f;
        [SerializeField, Min(0.01f)] private float _emergencyWithdrawWarmthCost = 25f;

        public int MaximumLevel => _levelContributionThresholds != null
            ? Mathf.Max(0, _levelContributionThresholds.Length - 1)
            : 0;
        public float TendFuelCost => _tendFuelCost;
        public float MaximumWarmth => _maximumWarmth;
        public float InitialWarmth => _maximumWarmth * _initialWarmthNormalized;
        public float WarmthDecayPerSecond => _warmthDecayPerSecond;
        public float WarmthPerTend => _warmthPerTend;
        public float NearbyComfortRadius => _nearbyComfortRadius;
        public float EmergencyWithdrawFuel => _emergencyWithdrawFuel;
        public float EmergencyWithdrawWarmthCost => _emergencyWithdrawWarmthCost;
        public float[] CreateLevelThresholdSnapshot() => _levelContributionThresholds != null
            ? (float[])_levelContributionThresholds.Clone()
            : new[] { 0f };

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
            _nearbyComfortRadius = Mathf.Max(0.1f, _nearbyComfortRadius);
            _emergencyWithdrawFuel = Mathf.Max(0.01f, _emergencyWithdrawFuel);
            _emergencyWithdrawWarmthCost = Mathf.Max(0.01f, _emergencyWithdrawWarmthCost);
        }
    }
}
