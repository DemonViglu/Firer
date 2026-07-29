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

        private void Awake()
        {
            if (_resourceController == null)
            {
                _resourceController = GetComponent<FlameResourceController>();
            }

            if (!HasValidSetup)
            {
                LastUpgradeStatus = "Missing setup";
                Debug.LogError("[CampfireUpgradeController] 需要余火控制器，以及 StableSceneId 为空且 Allow Runtime Assignment 已勾选的 CampFire Prefab。不要引用场景内的 Campfire。", this);
            }
        }

        public bool TryTendSmallFire(SmallFire smallFire)
        {
            if (!HasValidSetup || smallFire == null)
            {
                LastUpgradeStatus = "Missing setup";
                return false;
            }

            var instance = Instantiate(_campfirePrefab, smallFire.transform.position, smallFire.transform.rotation);
            var runtimeId = $"campfire.{Guid.NewGuid():N}";
            var sourceId = smallFire.GetComponent<StableSceneId>();
            if (!instance.InitializeRuntime(runtimeId, sourceId != null && sourceId.IsValid ? sourceId.Value : null))
            {
                LastUpgradeStatus = "Create failed";
                Destroy(instance.gameObject);
                return false;
            }

            if (!instance.TryTend(_resourceController))
            {
                LastUpgradeStatus = instance.LastUpgradeStatus;
                Destroy(instance.gameObject);
                return false;
            }

            Destroy(smallFire.gameObject);
            LastUpgradeStatus = "Started a public fire";
            return true;
        }
    }
}
