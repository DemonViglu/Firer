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
        [SerializeField] private bool _isRuntimeCreated;
        [SerializeField] private string _sourceSmallFireId;

        public string CampfireId => GetComponent<StableSceneId>().Value;
        public int Level => _level;
        public float TotalContribution => _totalContribution;
        public CampfireConfig Config => _config;
        public bool HasValidSetup => _config != null && GetComponent<StableSceneId>().IsValid;
        public bool IsMaxLevel => _config != null && _level >= _config.MaximumLevel;
        public float NextUpgradeCost => _config != null ? _config.GetUpgradeCost(_level) : 0f;
        public string LastUpgradeStatus { get; private set; } = "Ready";
        public bool IsRuntimeCreated => _isRuntimeCreated;
        public static IReadOnlyList<Campfire> ActiveInstances => ActiveCampfires;

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
            LastUpgradeStatus = IsMaxLevel ? "Maximum level" : "Restored";
        }

        public bool TryUpgrade(FlameResourceController resourceController)
        {
            if (!HasValidSetup)
            {
                LastUpgradeStatus = "Missing setup";
                return false;
            }

            if (IsMaxLevel)
            {
                LastUpgradeStatus = "Maximum level";
                return false;
            }

            var cost = NextUpgradeCost;
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
            _level++;
            LastUpgradeStatus = IsMaxLevel ? "Maximum level" : "Upgraded";
            Upgraded?.Invoke(this);
            return true;
        }

        private void OnValidate()
        {
            _level = Mathf.Max(0, _level);
            _totalContribution = Mathf.Max(0f, _totalContribution);
            if (_config != null)
            {
                _level = Mathf.Min(_level, _config.MaximumLevel);
            }
        }
    }
}
