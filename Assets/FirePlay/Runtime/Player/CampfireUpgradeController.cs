using System;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 将玩家自由留下的小火种转换为公共篝火。
    /// 创建命令显式分配运行时稳定 ID，后续存档只需记录 Campfire 状态与该 ID。
    /// </summary>
    public sealed class CampfireUpgradeController : MonoBehaviour
    {
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private Campfire _campfirePrefab;
        [Header("Public fire limit")]
        [SerializeField, Min(1)] private int _maximumActiveRuntimeCampfires = 4;

        public bool HasValidSetup
        {
            get
            {
                var stableId = _campfirePrefab != null ? _campfirePrefab.GetComponent<StableSceneId>() : null;
                return _resourceController != null && stableId != null &&
                       stableId.AllowsRuntimeAssignment && !stableId.IsValid;
            }
        }
        public string LastUpgradeStatus { get; private set; } = "Ready";
        public float TendFuelCost => _campfirePrefab != null && _campfirePrefab.Config != null
            ? _campfirePrefab.Config.TendFuelCost
            : 0f;
        public int MaximumActiveRuntimeCampfires => _maximumActiveRuntimeCampfires;
        public int ActiveRuntimeCampfireCount => CountActiveRuntimeCampfires();
        public bool CanStartPublicFire => ActiveRuntimeCampfireCount < _maximumActiveRuntimeCampfires;

        private void Awake()
        {
            if (_resourceController == null)
            {
                _resourceController = GetComponentInParent<FlameResourceController>();
            }

            if (!HasValidSetup)
            {
                LastUpgradeStatus = "Missing setup";
                Debug.LogError("[CampfireUpgradeController] 需要余火控制器，以及 StableSceneId 为空且 Allow Runtime Assignment 已勾选的 CampFire Prefab。不要引用场景内的 Campfire。", this);
            }
        }

        public bool TryTendSmallFire(SmallFire smallFire)
        {
            if (!TryPrepareAuthorityUpgrade(smallFire, out var instance, out var reason))
            {
                LastUpgradeStatus = reason;
                return false;
            }

            if (!TryCommitPreparedUpgrade(instance, smallFire, out reason))
            {
                LastUpgradeStatus = reason;
                Destroy(instance.gameObject);
                return false;
            }

            LastUpgradeStatus = "Started a public fire";
            return true;
        }

        public bool TryPrepareAuthorityUpgrade(
            SmallFire smallFire,
            out Campfire instance,
            out string reason)
        {
            instance = null;
            if (!HasValidSetup || smallFire == null)
            {
                reason = "Missing setup";
                return false;
            }

            if (!CanStartPublicFire)
            {
                reason = $"Public fire limit reached ({ActiveRuntimeCampfireCount}/{_maximumActiveRuntimeCampfires})";
                return false;
            }

            instance = Instantiate(_campfirePrefab, smallFire.transform.position, smallFire.transform.rotation);
            var runtimeId = $"campfire.{Guid.NewGuid():N}";
            var sourceId = smallFire.GetComponent<StableSceneId>();
            if (!instance.InitializeRuntime(runtimeId, sourceId != null && sourceId.IsValid ? sourceId.Value : null))
            {
                reason = "Create failed";
                Destroy(instance.gameObject);
                instance = null;
                return false;
            }

            reason = "Prepared";
            return true;
        }

        public bool TryCommitPreparedUpgrade(
            Campfire instance,
            SmallFire sourceSmallFire,
            out string reason)
        {
            if (instance == null || sourceSmallFire == null || _resourceController == null)
            {
                reason = "Upgrade authority is unavailable";
                return false;
            }

            if (!instance.TryTend(_resourceController))
            {
                reason = instance.LastUpgradeStatus;
                return false;
            }

            sourceSmallFire.ReleaseAuthorityObject();
            LastUpgradeStatus = "Started a public fire";
            reason = LastUpgradeStatus;
            return true;
        }

        private static int CountActiveRuntimeCampfires()
        {
            var count = 0;
            foreach (var campfire in Campfire.ActiveInstances)
            {
                if (campfire != null && campfire.IsRuntimeCreated && !campfire.IsRetired)
                {
                    count++;
                }
            }

            return count;
        }

        private void OnValidate()
        {
            _maximumActiveRuntimeCampfires = Mathf.Max(1, _maximumActiveRuntimeCampfires);
        }
    }
}
