using UnityEngine;

namespace DemonViglu.FirePlay.Data
{
    [CreateAssetMenu(menuName = "DemonViglu/FirePlay/Small Fire Config", fileName = "SmallFireConfig_")]
    public sealed class SmallFireConfig : ScriptableObject
    {
        [SerializeField, Min(0.01f)] private float _fuelCost = 15f;
        [SerializeField, Min(1f)] private float _durationSeconds = 45f;
        [SerializeField, Min(1)] private int _maximumActiveCount = 3;
        [SerializeField, Min(0.5f)] private float _maximumPlacementDistance = 5f;
        [SerializeField, Range(0f, 89f)] private float _maximumSlopeDegrees = 35f;
        [SerializeField] private LayerMask _placementLayers = ~0;

        public float FuelCost => _fuelCost;
        public float DurationSeconds => _durationSeconds;
        public int MaximumActiveCount => _maximumActiveCount;
        public float MaximumPlacementDistance => _maximumPlacementDistance;
        public float MaximumSlopeDegrees => _maximumSlopeDegrees;
        public LayerMask PlacementLayers => _placementLayers;
    }
}
