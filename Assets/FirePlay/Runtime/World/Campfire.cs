using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Data;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Save;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 可持久化公共篝火的单机状态入口。视觉和存档在后续切片读取本组件状态，
    /// 不参与余火扣除或等级计算。
    /// </summary>
    [RequireComponent(typeof(StableSceneId))]
    public sealed class Campfire : MonoBehaviour, IWorldCommandVersioned
    {
        private static readonly List<Campfire> ActiveCampfires = new();
        private static readonly Dictionary<string, int> RetiredSourceStages = new();

        [SerializeField] private CampfireConfig _config;
        [SerializeField, Min(0)] private int _level;
        [SerializeField, Min(0f)] private float _totalContribution;
        [SerializeField, Min(0f)] private float _warmth;
        [SerializeField] private bool _warmthInitialized;
        [SerializeField] private bool _isRuntimeCreated;
        [SerializeField] private bool _isRetired;
        [SerializeField] private string _sourceSmallFireId;
        private uint _commandVersion;
        private CampfireAuthorityState _authorityState;

        public string CampfireId => GetComponent<StableSceneId>().Value;
        public int Level => _authorityState?.Level ?? _level;
        public float TotalContribution => _authorityState?.TotalContribution ?? _totalContribution;
        public float Warmth => _authorityState?.Warmth ?? _warmth;
        public float NormalizedWarmth => _config == null ? 0f : Mathf.Clamp01(Warmth / _config.MaximumWarmth);
        public bool IsExtinguished => _authorityState?.IsExtinguished ?? _warmth <= 0.001f;
        public bool NeedsTending => _authorityState?.NeedsTending ?? (_config != null && _warmth < _config.MaximumWarmth - 0.001f);
        public CampfireConfig Config => _config;
        public bool HasValidSetup => _config != null && GetComponent<StableSceneId>().IsValid;
        public bool IsMaxLevel => _authorityState?.IsMaxLevel ?? (_config != null && _level >= _config.MaximumLevel);
        public float TendFuelCost => _config != null ? _config.TendFuelCost : 0f;
        public float EmergencyWithdrawFuel => _config != null ? _config.EmergencyWithdrawFuel : 0f;
        public float EmergencyWithdrawWarmthCost => _config != null ? _config.EmergencyWithdrawWarmthCost : 0f;
        public float EstimatedBurnSeconds => _authorityState?.EstimatedBurnSeconds ?? 0f;
        public string LastUpgradeStatus { get; private set; } = "Ready";
        public bool IsRuntimeCreated => _isRuntimeCreated;
        public bool IsRetired => _isRetired;
        public string SourceSmallFireId => _sourceSmallFireId;
        public uint CommandVersion => _commandVersion;
        public static IReadOnlyList<Campfire> ActiveInstances => ActiveCampfires;
        public static event Action<Campfire> StateChanged;
        public static event Action<Campfire> Retired;

        public static void ClearRuntimeInstances()
        {
            for (var index = ActiveCampfires.Count - 1; index >= 0; index--)
            {
                var campfire = ActiveCampfires[index];
                if (campfire == null || !campfire.IsRuntimeCreated)
                {
                    continue;
                }

                campfire.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(campfire.gameObject);
            }
        }

        public event Action<Campfire> Upgraded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveCampfires()
        {
            ActiveCampfires.Clear();
            RetiredSourceStages.Clear();
            StateChanged = null;
            Retired = null;
        }

        public static Campfire FindNearest(Vector3 position, float maximumDistance, out float squaredDistance)
        {
            Campfire nearest = null;
            squaredDistance = maximumDistance * maximumDistance;

            for (var index = ActiveCampfires.Count - 1; index >= 0; index--)
            {
                var campfire = ActiveCampfires[index];
                if (campfire == null)
                {
                    ActiveCampfires.RemoveAt(index);
                    continue;
                }

                var distance = (campfire.transform.position - position).sqrMagnitude;
                if (distance <= squaredDistance)
                {
                    nearest = campfire;
                    squaredDistance = distance;
                }
            }

            return nearest;
        }

        public static void RegisterRetiredRecord(CampfireRecord record)
        {
            if (record == null || !record.retired || string.IsNullOrWhiteSpace(record.sourceSmallFireId))
            {
                return;
            }

            if (RetiredSourceStages.TryGetValue(record.sourceSmallFireId, out var existingStage))
            {
                RetiredSourceStages[record.sourceSmallFireId] = Mathf.Max(existingStage, record.level);
                return;
            }

            RetiredSourceStages.Add(record.sourceSmallFireId, record.level);
        }

        public static bool TryGetRetiredSourceStage(string sourceSmallFireId, out int stage)
        {
            if (string.IsNullOrWhiteSpace(sourceSmallFireId))
            {
                stage = 0;
                return false;
            }

            return RetiredSourceStages.TryGetValue(sourceSmallFireId, out stage);
        }

        private void OnEnable()
        {
            if (!ActiveCampfires.Contains(this))
            {
                ActiveCampfires.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveCampfires.Remove(this);
        }

        private void Awake()
        {
            if (_config == null)
            {
                LastUpgradeStatus = "Missing setup";
                Debug.LogError("[Campfire] 需要 CampfireConfig。", this);
            }
            else if (!_warmthInitialized)
            {
                _warmth = _config.InitialWarmth;
                _warmthInitialized = true;
            }
            if (_config != null) CreateAuthorityState();
        }

        private void Update()
        {
            _authorityState?.Tick(Time.deltaTime);
            SyncSerializedState();

            RetireIfExpired();
        }

        public bool InitializeRuntime(string stableId, string sourceSmallFireId = null)
        {
            var stableSceneId = GetComponent<StableSceneId>();
            if (_config == null || stableSceneId == null || !stableSceneId.TryAssignRuntimeValue(stableId))
            {
                LastUpgradeStatus = "Missing setup";
                return false;
            }

            LastUpgradeStatus = "Ready";
            _isRuntimeCreated = true;
            _isRetired = false;
            _sourceSmallFireId = sourceSmallFireId;
            // Runtime campfires must not inherit authoring values from CampFire.prefab.
            // Scene-only center campfires may start at level 3, but a fire born from a small fire always starts at level 0.
            _level = 0;
            _totalContribution = 0f;
            // A fire born from a small fire must be kindled by the triggering contribution.
            // Pre-placed campfires keep their inspector-configured initial warmth instead.
            _warmth = 0f;
            _warmthInitialized = true;
            CreateAuthorityState();
            _commandVersion = 0;
            return true;
        }

        public CampfireRecord CreateRecord()
        {
            return new CampfireRecord
            {
                id = CampfireId,
                position = transform.position,
                rotation = transform.rotation,
                level = Level,
                totalContribution = TotalContribution,
                warmth = Warmth,
                warmthInitialized = _warmthInitialized,
                runtimeCreated = _isRuntimeCreated,
                retired = _isRetired,
                sourceSmallFireId = _sourceSmallFireId
            };
        }

        public bool RestoreRuntime(CampfireRecord record)
        {
            if (record == null || !InitializeRuntime(record.id, record.sourceSmallFireId))
            {
                return false;
            }

            RestoreWarmth(record);
            RestoreAuthorityState(record);
            LastUpgradeStatus = IsMaxLevel ? "Maximum level" : "Restored";
            _commandVersion++;
            return true;
        }

        public void ApplySavedState(CampfireRecord record)
        {
            if (record == null || _config == null)
            {
                return;
            }

            RestoreWarmth(record);
            RestoreAuthorityState(record);
            LastUpgradeStatus = IsMaxLevel ? "Maximum level" : "Restored";
            _commandVersion++;
        }

        public bool TryTend(FlameResourceController resourceController)
        {
            if (!HasValidSetup)
            {
                LastUpgradeStatus = "Missing setup";
                return false;
            }

            if (_authorityState == null || !NeedsTending)
            {
                LastUpgradeStatus = "Already warm";
                return false;
            }

            var cost = TendFuelCost;
            if (resourceController == null || resourceController.State == null || !_authorityState.CanTend(resourceController.State.CurrentFuel))
            {
                LastUpgradeStatus = "Not enough fuel";
                return false;
            }

            if (!resourceController.TryConsume(cost))
            {
                LastUpgradeStatus = "Consume failed";
                return false;
            }

            var levelIncreased = _authorityState.ApplyTend();
            _warmthInitialized = true;
            SyncSerializedState();
            LastUpgradeStatus = levelIncreased ? $"Fire grew to level {Level}" : "Tended";
            _commandVersion++;
            Upgraded?.Invoke(this);
            StateChanged?.Invoke(this);
            return true;
        }

        public bool TryWithdrawEmergencyFuel(FlameResourceController resourceController)
        {
            if (!HasValidSetup || _authorityState == null || IsExtinguished)
            {
                LastUpgradeStatus = "Fire is out";
                return false;
            }

            var amount = EmergencyWithdrawFuel;
            var warmthCost = EmergencyWithdrawWarmthCost;
            if (resourceController == null || resourceController.State == null ||
                resourceController.State.CurrentFuel > resourceController.State.MaxFuel - amount)
            {
                LastUpgradeStatus = "Fuel already sufficient";
                return false;
            }

            if (Warmth < warmthCost || !_authorityState.CanWithdraw(resourceController.State.CurrentFuel, resourceController.State.MaxFuel))
            {
                LastUpgradeStatus = "Not enough warmth";
                return false;
            }

            if (!resourceController.Restore(amount))
            {
                LastUpgradeStatus = "Restore failed";
                return false;
            }

            if (!_authorityState.ApplyWithdraw()) return false;
            SyncSerializedState();
            LastUpgradeStatus = $"Drew {amount:0} fuel from the fire";
            _commandVersion++;
            StateChanged?.Invoke(this);
            RetireIfExpired();
            return true;
        }

        private void RetireIfExpired()
        {
            if (_isRetired || !_isRuntimeCreated || !IsExtinguished || IsMaxLevel)
            {
                return;
            }

            _isRetired = true;
            RegisterRetiredRecord(CreateRecord());
            ActiveCampfires.Remove(this);
            LastUpgradeStatus = "Fire faded away";
            Retired?.Invoke(this);
            Destroy(gameObject);
        }

        private void RestoreWarmth(CampfireRecord record)
        {
            _warmthInitialized = record.warmthInitialized;
            _warmth = _warmthInitialized
                ? Mathf.Clamp(record.warmth, 0f, _config.MaximumWarmth)
                : _config.InitialWarmth;
            _warmthInitialized = true;
        }

        private void CreateAuthorityState()
        {
            _authorityState = new CampfireAuthorityState(
                _config.CreateLevelThresholdSnapshot(),
                _config.MaximumWarmth,
                _config.WarmthDecayPerSecond,
                _config.WarmthPerTend,
                _config.TendFuelCost,
                _config.EmergencyWithdrawFuel,
                _config.EmergencyWithdrawWarmthCost,
                new CampfireAuthoritySnapshot(_level, _totalContribution, _warmth));
            SyncSerializedState();
        }

        private void RestoreAuthorityState(CampfireRecord record)
        {
            _authorityState ??= new CampfireAuthorityState(
                _config.CreateLevelThresholdSnapshot(), _config.MaximumWarmth, _config.WarmthDecayPerSecond,
                _config.WarmthPerTend, _config.TendFuelCost, _config.EmergencyWithdrawFuel,
                _config.EmergencyWithdrawWarmthCost, default);
            _authorityState.Restore(new CampfireAuthoritySnapshot(record.level, record.totalContribution, _warmth));
            SyncSerializedState();
        }

        private void SyncSerializedState()
        {
            if (_authorityState == null) return;
            _level = _authorityState.Level;
            _totalContribution = _authorityState.TotalContribution;
            _warmth = _authorityState.Warmth;
        }

        private void OnValidate()
        {
            _level = Mathf.Max(0, _level);
            _totalContribution = Mathf.Max(0f, _totalContribution);
            if (_config != null)
            {
                _level = Mathf.Min(_level, _config.MaximumLevel);
                _warmth = Mathf.Clamp(_warmth, 0f, _config.MaximumWarmth);
            }
        }
    }
}
