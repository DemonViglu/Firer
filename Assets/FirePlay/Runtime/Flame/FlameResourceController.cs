using DemonViglu.FirePlay.Data;
using UnityEngine;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// Player 上的余火入口。负责固定夜间消耗、恢复区域计数，
    /// 并向后续冲刺、自然火源和篝火提供显式消耗/恢复接口。
    /// </summary>
    public sealed class FlameResourceController : MonoBehaviour
    {
        [SerializeField] private FlameResourceConfig _config;
        [SerializeField] private bool _nightDrainActive = true;
        [SerializeField, Min(0.1f)] private float _debugStep = 10f;

        private int _activeRecoverySources;
        private int _activeSafeZones;

        public FlameResourceState State { get; private set; }
        public FlameResourceConfig Config => _config;
        public bool NightDrainActive => _nightDrainActive;
        public bool IsRecovering => _activeRecoverySources > 0;
        public bool IsInSafeZone => _activeSafeZones > 0;

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[FlameResourceController] 未指定 FlameResourceConfig。", this);
                enabled = false;
                return;
            }

            State = new FlameResourceState(
                _config.InitialFuel,
                _config.MaxFuel,
                _config.ReceiverEnterFuel,
                _config.GiverEnterFuel);
        }

        private void Update()
        {
            if (State == null)
            {
                return;
            }

            if (IsRecovering)
            {
                State.Restore(_config.RestorePerSecond * Time.deltaTime);
            }
            else if (_nightDrainActive)
            {
                State.TryConsume(_config.NightDrainPerSecond * Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            _activeRecoverySources = 0;
            _activeSafeZones = 0;
            State?.SetReceiverOverride(false);
        }

        public bool TryConsume(float amount) => State != null && State.TryConsume(amount);
        public bool Restore(float amount) => State != null && State.Restore(amount);
        public void SetNightDrainActive(bool active) => _nightDrainActive = active;
        public void SetReceiverOverride(bool active) => State?.SetReceiverOverride(active && IsInSafeZone);

        public void EnterRecoverySource()
        {
            _activeRecoverySources++;
        }

        public void ExitRecoverySource()
        {
            _activeRecoverySources = Mathf.Max(0, _activeRecoverySources - 1);
        }

        public void EnterSafeZone()
        {
            _activeSafeZones++;
        }

        public void ExitSafeZone()
        {
            _activeSafeZones = Mathf.Max(0, _activeSafeZones - 1);
            if (!IsInSafeZone)
            {
                State?.SetReceiverOverride(false);
            }
        }

        [ContextMenu("Debug/Consume Fuel Step")]
        private void DebugConsume()
        {
            TryConsume(_debugStep);
        }

        [ContextMenu("Debug/Restore Fuel Step")]
        private void DebugRestore()
        {
            Restore(_debugStep);
        }

        [ContextMenu("Debug/Toggle Night Drain")]
        private void DebugToggleNightDrain()
        {
            _nightDrainActive = !_nightDrainActive;
        }
    }
}
