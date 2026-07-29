using UnityEngine;

namespace DemonViglu.FirePlay.Data
{
    /// <summary>
    /// 余火容量、模式阈值与后续消耗/恢复速率的设计参数。
    /// </summary>
    [CreateAssetMenu(menuName = "DemonViglu/FirePlay/Flame Resource Config", fileName = "FlameResourceConfig_")]
    public sealed class FlameResourceConfig : ScriptableObject
    {
        [Header("容量")]
        [SerializeField, Min(0.01f)] private float _maxFuel = 100f;
        [SerializeField, Min(0f)] private float _initialFuel = 80f;

        [Header("模式回差")]
        [Tooltip("给予者降至此值时进入接收者模式。")]
        [SerializeField, Min(0f)] private float _receiverEnterFuel = 25f;
        [Tooltip("接收者恢复至此值时返回给予者模式，必须高于接收者阈值。")]
        [SerializeField, Min(0f)] private float _giverEnterFuel = 40f;

        [Header("后续切片速率")]
        [SerializeField, Min(0f)] private float _nightDrainPerSecond = 0.5f;
        [SerializeField, Min(0f)] private float _sprintDrainPerSecond = 2f;
        [SerializeField, Min(0f)] private float _restorePerSecond = 8f;

        [Header("公共篝火舒适区")]
        [Tooltip("在燃烧的公共篝火附近时，夜间余火消耗乘以此值。")]
        [SerializeField, Range(0f, 1f)] private float _nearbyCampfireDrainMultiplier = 0.375f;
        [Tooltip("坐在燃烧的公共篝火旁时，夜间余火消耗乘以此值。")]
        [SerializeField, Range(0f, 1f)] private float _restingCampfireDrainMultiplier = 0.125f;

        public float MaxFuel => _maxFuel;
        public float InitialFuel => Mathf.Clamp(_initialFuel, 0f, _maxFuel);
        public float ReceiverEnterFuel => Mathf.Clamp(_receiverEnterFuel, 0f, _maxFuel);
        public float GiverEnterFuel => Mathf.Clamp(_giverEnterFuel, ReceiverEnterFuel, _maxFuel);
        public float NightDrainPerSecond => _nightDrainPerSecond;
        public float SprintDrainPerSecond => _sprintDrainPerSecond;
        public float RestorePerSecond => _restorePerSecond;
        public float NearbyCampfireDrainMultiplier => _nearbyCampfireDrainMultiplier;
        public float RestingCampfireDrainMultiplier => _restingCampfireDrainMultiplier;

        private void OnValidate()
        {
            _maxFuel = Mathf.Max(0.01f, _maxFuel);
            _initialFuel = Mathf.Clamp(_initialFuel, 0f, _maxFuel);
            _receiverEnterFuel = Mathf.Clamp(_receiverEnterFuel, 0f, _maxFuel);
            _giverEnterFuel = Mathf.Clamp(_giverEnterFuel, _receiverEnterFuel, _maxFuel);
            _nearbyCampfireDrainMultiplier = Mathf.Clamp01(_nearbyCampfireDrainMultiplier);
            _restingCampfireDrainMultiplier = Mathf.Clamp01(_restingCampfireDrainMultiplier);
        }
    }
}
