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
    public sealed class Campfire : MonoBehaviour
    {
        private static readonly List<Campfire> ActiveCampfires = new();

        [SerializeField] private CampfireConfig _config;
        [SerializeField, Min(0)] private int _level;
        [SerializeField, Min(0f)] private float _totalContribution;
        [SerializeField, Min(0f)] private float _warmth;
        [SerializeField] private bool _warmthInitialized;
        [SerializeField] private bool _isRuntimeCreated;
        [SerializeField] private string _sourceSmallFireId;

        public string CampfireId => GetComponent<StableSceneId>().Value;
        public int Level => _level;
        public float TotalContribution => _totalContribution;
        public float Warmth => _warmth;
        public float NormalizedWarmth => _config == null ? 0f : Mathf.Clamp01(_warmth / _config.MaximumWarmth);
        public bool IsExtinguished => _warmth <= 0.001f;
        public bool NeedsTending => _config != null && _warmth < _config.MaximumWarmth - 0.001f;
        public CampfireConfig Config => _config;
        public bool HasValidSetup => _config != null && GetComponent<StableSceneId>().IsValid;
        public bool IsMaxLevel => _config != null && _level >= _config.MaximumLevel;
        public float TendFuelCost => _config != null ? _config.TendFuelCost : 0f;
        public string LastUpgradeStatus { get; private set; } = "Ready";
        public bool IsRuntimeCreated => _isRuntimeCreated;
        public string SourceSmallFireId => _sourceSmallFireId;
        public static IReadOnlyList<Campfire> ActiveInstances => ActiveCampfires;
        public static event Action<Campfire> StateChanged;

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
            StateChanged = null;
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
        }

        private void Update()
        {
            if (_config != null && _warmth > 0f)
            {
                _warmth = Mathf.Max(0f, _warmth - _config.WarmthDecayPerSecond * Time.deltaTime);
            }
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
            _sourceSmallFireId = sourceSmallFireId;
            // Runtime campfires must not inherit authoring values from CampFire.prefab.
            // Scene-only center campfires may start at level 3, but a fire born from a small fire always starts at level 0.
            _level = 0;
            _totalContribution = 0f;
            // A fire born from a small fire must be kindled by the triggering contribution.
            // Pre-placed campfires keep their inspector-configured initial warmth instead.
            _warmth = 0f;
            _warmthInitialized = true;
            return true;
        }

        public CampfireRecord CreateRecord()
        {
            return new CampfireRecord
            {
                id = CampfireId,
                position = transform.position,
                rotation = transform.rotation,
                level = _level,
                totalContribution = _totalContribution,
                warmth = _warmth,
                warmthInitialized = _warmthInitialized,
                runtimeCreated = _isRuntimeCreated,
                sourceSmallFireId = _sourceSmallFireId
            };
        }

        public bool RestoreRuntime(CampfireRecord record)
        {
            if (record == null || !InitializeRuntime(record.id, record.sourceSmallFireId))
            {
                return false;
            }

            _level = Mathf.Clamp(record.level, 0, _config.MaximumLevel);
            _totalContribution = Mathf.Max(0f, record.totalContribution);
            RestoreWarmth(record);
            LastUpgradeStatus = IsMaxLevel ? "Maximum level" : "Restored";
            return true;
        }

        public void ApplySavedState(CampfireRecord record)
        {
            if (record == null || _config == null)
            {
                return;
            }

            _level = Mathf.Clamp(record.level, 0, _config.MaximumLevel);
            _totalContribution = Mathf.Max(0f, record.totalContribution);
            RestoreWarmth(record);
            LastUpgradeStatus = IsMaxLevel ? "Maximum level" : "Restored";
        }

        public bool TryTend(FlameResourceController resourceController)
        {
            if (!HasValidSetup)
            {
                LastUpgradeStatus = "Missing setup";
                return false;
            }

            if (!NeedsTending)
            {
                LastUpgradeStatus = "Already warm";
                return false;
            }

            var cost = TendFuelCost;
            if (resourceController == null || resourceController.State == null || resourceController.State.CurrentFuel < cost)
            {
                LastUpgradeStatus = "Not enough fuel";
                return false;
            }

            if (!resourceController.TryConsume(cost))
            {
                LastUpgradeStatus = "Consume failed";
                return false;
            }

            _totalContribution += cost;
            _warmth = Mathf.Min(_config.MaximumWarmth, _warmth + _config.WarmthPerTend);
            _warmthInitialized = true;
            var previousLevel = _level;
            _level = Mathf.Max(_level, _config.GetLevelForContribution(_totalContribution));
            LastUpgradeStatus = _level > previousLevel ? $"Fire grew to level {_level}" : "Tended";
            Upgraded?.Invoke(this);
            StateChanged?.Invoke(this);
            return true;
        }

        public float DrawWarmthForPlayer(float requestedFuel)
        {
            if (_config == null || IsExtinguished || requestedFuel <= 0f)
            {
                return 0f;
            }

            var restoredFuel = Mathf.Min(requestedFuel, _warmth / _config.WarmthCostPerRecoveredFuel);
            _warmth = Mathf.Max(0f, _warmth - restoredFuel * _config.WarmthCostPerRecoveredFuel);
            return restoredFuel;
        }

        private void RestoreWarmth(CampfireRecord record)
        {
            _warmthInitialized = record.warmthInitialized;
            _warmth = _warmthInitialized
                ? Mathf.Clamp(record.warmth, 0f, _config.MaximumWarmth)
                : _config.InitialWarmth;
            _warmthInitialized = true;
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
