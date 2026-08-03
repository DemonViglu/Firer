using DemonViglu.FirePlay.Data;
using DemonViglu.FirePlay.World;
using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// Player 上的余火入口。负责固定夜间消耗、恢复区域计数，
    /// 并向后续冲刺、自然火源和篝火提供显式消耗/恢复接口。
    /// </summary>
    public sealed class FlameResourceController : MonoBehaviour, IActivityFlameResource, IPlayerSprintPolicy
    {
        [SerializeField] private FlameResourceConfig _config;
        [SerializeField] private bool _nightDrainActive = true;
        [SerializeField, Min(0.1f)] private float _debugStep = 10f;

        private int _activeRecoverySources;
        private int _activeSafeZones;
        private Campfire _campfireRestSource;

        public FlameResourceState State { get; private set; }
        public FlameResourceConfig Config => _config;
        public float CurrentFuel => State?.CurrentFuel ?? 0f;
        public bool NightDrainActive => _nightDrainActive;
        public bool IsRecovering => _activeRecoverySources > 0;
        public bool IsInSafeZone => _activeSafeZones > 0;
        public float CurrentCampfireDrainMultiplier { get; private set; } = 1f;

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
                CurrentCampfireDrainMultiplier = 1f;
                State.Restore(_config.RestorePerSecond * Time.deltaTime);
            }
            else
            {
                CurrentCampfireDrainMultiplier = GetCampfireDrainMultiplier();
                if (_nightDrainActive)
                {
                    State.ConsumeUpTo(_config.NightDrainPerSecond * CurrentCampfireDrainMultiplier * Time.deltaTime);
                }
            }
        }

        private void OnDisable()
        {
            _activeRecoverySources = 0;
            _activeSafeZones = 0;
            _campfireRestSource = null;
            CurrentCampfireDrainMultiplier = 1f;
            State?.SetReceiverOverride(false);
        }

        public bool TryConsume(float amount) => State != null && State.TryConsume(amount);
        public bool ConsumeUpTo(float amount) => State != null && State.ConsumeUpTo(amount);
        public bool TryConsumeSprint(float deltaTime)
        {
            var sprintCost = Config != null ? Config.SprintDrainPerSecond * Mathf.Max(0f, deltaTime) : 0f;
            return sprintCost <= 0f || ConsumeUpTo(sprintCost);
        }
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

        public void EnterCampfireRest(Campfire campfire)
        {
            _campfireRestSource = campfire;
        }

        public void ExitCampfireRest()
        {
            _campfireRestSource = null;
        }

        private float GetCampfireDrainMultiplier()
        {
            if (!TryGetNearbyCampfire(out var nearbyCampfire))
            {
                return 1f;
            }

            return nearbyCampfire == _campfireRestSource
                ? _config.RestingCampfireDrainMultiplier
                : _config.NearbyCampfireDrainMultiplier;
        }

        private bool TryGetNearbyCampfire(out Campfire nearbyCampfire)
        {
            nearbyCampfire = null;
            var maximumSearchRadius = 0f;
            foreach (var campfire in Campfire.ActiveInstances)
            {
                if (campfire != null && campfire.Config != null)
                {
                    maximumSearchRadius = Mathf.Max(maximumSearchRadius, campfire.Config.NearbyComfortRadius);
                }
            }

            if (maximumSearchRadius <= 0f)
            {
                return false;
            }

            var nearestCampfire = Campfire.FindNearest(transform.position, maximumSearchRadius, out var squaredDistance);
            if (nearestCampfire == null || nearestCampfire.IsExtinguished || nearestCampfire.Config == null ||
                squaredDistance > nearestCampfire.Config.NearbyComfortRadius * nearestCampfire.Config.NearbyComfortRadius)
            {
                return false;
            }

            nearbyCampfire = nearestCampfire;
            return true;
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
