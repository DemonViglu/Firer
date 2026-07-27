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

        public float MaxFuel => _maxFuel;
        public float InitialFuel => Mathf.Clamp(_initialFuel, 0f, _maxFuel);
        public float ReceiverEnterFuel => Mathf.Clamp(_receiverEnterFuel, 0f, _maxFuel);
        public float GiverEnterFuel => Mathf.Clamp(_giverEnterFuel, ReceiverEnterFuel, _maxFuel);
        public float NightDrainPerSecond => _nightDrainPerSecond;
        public float SprintDrainPerSecond => _sprintDrainPerSecond;
        public float RestorePerSecond => _restorePerSecond;

        private void OnValidate()
        {
            _maxFuel = Mathf.Max(0.01f, _maxFuel);
            _initialFuel = Mathf.Clamp(_initialFuel, 0f, _maxFuel);
            _receiverEnterFuel = Mathf.Clamp(_receiverEnterFuel, 0f, _maxFuel);
            _giverEnterFuel = Mathf.Clamp(_giverEnterFuel, _receiverEnterFuel, _maxFuel);
        }
    }
}
