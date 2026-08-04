using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public enum PlayerInteractTargetKind
    {
        None,
        WorldTree,
        Campfire,
        FlameSource,
        SmallFire
    }

    /// <summary>
    /// 玩家在世界中的近距离扫描器：只发现、排序和描述目标，并发布接触发现。
    /// 交互范围围绕玩家本体，而不是视觉上有偏移的火苗；玩法写入由独立消费者处理。
    /// </summary>
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private const int MaxDetectedColliders = 8;

        [SerializeField] private PlayerFlameController _flameController;
        [SerializeField] private LayerMask _interactionLayers = ~0;
        [SerializeField] private PlayerModeController _modeController;

        private readonly Collider[] _overlapResults = new Collider[MaxDetectedColliders];
        private FlameModule _flameModule;

        public FlameSource NearestFlameSource { get; private set; }
        public SmallFire NearestSmallFire { get; private set; }
        public Campfire NearestCampfire { get; private set; }
        public WorldTreeContribution NearestWorldTree { get; private set; }
        public RestSpot NearestRestSpot { get; private set; }
        public CampfireUpgradeController CampfireUpgradeController => _flameModule?.CampfireUpgrade;
        public PlayerInteractTargetKind CurrentInteractTargetKind { get; private set; }
        public string CurrentInteractPrompt { get; private set; } = string.Empty;
        public event Action<Collider> ProximityContactDetected;
        public string CurrentTargetId => CurrentInteractTargetKind switch
        {
            PlayerInteractTargetKind.Campfire => NearestCampfire != null ? NearestCampfire.CampfireId : string.Empty,
            PlayerInteractTargetKind.FlameSource => NearestFlameSource != null ? NearestFlameSource.SourceId : string.Empty,
            PlayerInteractTargetKind.SmallFire => ReadStableId(NearestSmallFire),
            PlayerInteractTargetKind.WorldTree => ReadStableId(NearestWorldTree),
            _ => string.Empty
        };

        public void GetIntentTarget(PlayerIntentKind intent, out PlayerInteractTargetKind kind, out string stableId)
        {
            kind = CurrentInteractTargetKind;
            stableId = CurrentTargetId;

            switch (intent)
            {
                case PlayerIntentKind.AddFire:
                    if (NearestCampfire != null) SetTarget(PlayerInteractTargetKind.Campfire, NearestCampfire, out kind, out stableId);
                    else SetTarget(PlayerInteractTargetKind.SmallFire, NearestSmallFire, out kind, out stableId);
                    break;
                case PlayerIntentKind.TendFire:
                case PlayerIntentKind.DrawFire:
                    SetTarget(PlayerInteractTargetKind.Campfire, NearestCampfire, out kind, out stableId);
                    break;
                case PlayerIntentKind.GatherEmber:
                    SetTarget(PlayerInteractTargetKind.FlameSource, NearestFlameSource, out kind, out stableId);
                    break;
                case PlayerIntentKind.StartPublicFire:
                case PlayerIntentKind.ReclaimSmallFire:
                    SetTarget(PlayerInteractTargetKind.SmallFire, NearestSmallFire, out kind, out stableId);
                    break;
                case PlayerIntentKind.ContributeWorldTree:
                    SetTarget(PlayerInteractTargetKind.WorldTree, NearestWorldTree, out kind, out stableId);
                    break;
                case PlayerIntentKind.LegacyWithdrawOrReclaim:
                    if (NearestCampfire != null) SetTarget(PlayerInteractTargetKind.Campfire, NearestCampfire, out kind, out stableId);
                    else SetTarget(PlayerInteractTargetKind.SmallFire, NearestSmallFire, out kind, out stableId);
                    break;
            }
        }

        private void Awake()
        {
            LocalPlayerContext.EnsureFor(this);
            _flameModule ??= GetComponentInChildren<FlameModule>(true);
            if (_flameController == null)
            {
                _flameController = _flameModule?.PlayerFlameController ?? GetComponentInChildren<PlayerFlameController>(true);
            }
            _modeController ??= GetComponentInParent<PlayerModeController>();

            if (_flameController == null)
            {
                Debug.LogError("[PlayerInteraction] 缺少 PlayerFlameController。请挂载 FlameModule，或为扫描器配置火苗控制器。", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (_modeController != null && !_modeController.IsExploring)
            {
                ClearNearbyTargets();
                return;
            }
            var activeFlame = _flameController.ActiveFlame;
            if (activeFlame == null)
            {
                ClearNearbyTargets();
                return;
            }

            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                activeFlame.InteractionRadius,
                _overlapResults,
                _interactionLayers,
                QueryTriggerInteraction.Collide);

            FlameSource nearestFlameSource = null;
            var nearestFlameSourceDistance = float.PositiveInfinity;
            var nearestSmallFire = SmallFire.FindNearest(
                transform.position,
                activeFlame.InteractionRadius,
                out var nearestSmallFireDistance);
            var nearestCampfire = Campfire.FindNearest(
                transform.position,
                activeFlame.InteractionRadius,
                out _);
            var nearestRestSpot = RestSpot.FindNearest(transform.position);
            WorldTreeContribution nearestWorldTree = null;
            var nearestWorldTreeDistance = float.PositiveInfinity;

            for (var index = 0; index < count; index++)
            {
                ProximityContactDetected?.Invoke(_overlapResults[index]);

                var flameSource = _overlapResults[index].GetComponentInParent<FlameSource>();
                if (flameSource != null)
                {
                    var distance = (flameSource.transform.position - transform.position).sqrMagnitude;
                    if (distance < nearestFlameSourceDistance)
                    {
                        nearestFlameSource = flameSource;
                        nearestFlameSourceDistance = distance;
                    }
                }

                var worldTree = _overlapResults[index].GetComponentInParent<WorldTreeContribution>();
                if (worldTree != null)
                {
                    var distance = (worldTree.transform.position - transform.position).sqrMagnitude;
                    if (distance < nearestWorldTreeDistance)
                    {
                        nearestWorldTree = worldTree;
                        nearestWorldTreeDistance = distance;
                    }
                }

                _overlapResults[index] = null;
            }

            NearestFlameSource = nearestFlameSource;
            NearestSmallFire = nearestSmallFire;
            NearestCampfire = nearestCampfire;
            NearestWorldTree = nearestWorldTree;
            NearestRestSpot = nearestRestSpot;
            SelectCurrentInteractTarget(nearestSmallFire, nearestSmallFireDistance, nearestFlameSource, nearestFlameSourceDistance, nearestWorldTree, nearestWorldTreeDistance, nearestRestSpot);

        }

        private void SelectCurrentInteractTarget(
            SmallFire smallFire,
            float smallFireDistance,
            FlameSource flameSource,
            float flameSourceDistance,
            WorldTreeContribution worldTree,
            float worldTreeDistance,
            RestSpot restSpot)
        {
            CurrentInteractTargetKind = PlayerInteractTargetKind.None;
            CurrentInteractPrompt = string.Empty;

            // Explicit priorities prevent overlapping colliders from silently changing E behaviour.
            if (worldTree != null)
            {
                CurrentInteractTargetKind = PlayerInteractTargetKind.WorldTree;
                CurrentInteractPrompt = worldTree.HasLocalContribution
                    ? "你已经把一束暖光留在树上了"
                    : $"向大树献上一点暖意（{worldTree.ContributionCost:0.0} 余火）";
                return;
            }

            if (NearestCampfire != null)
            {
                CurrentInteractTargetKind = PlayerInteractTargetKind.Campfire;
                CurrentInteractPrompt = NearestCampfire.NeedsTending
                    ? $"给篝火添一点暖意（{NearestCampfire.TendFuelCost:0.0} 余火）／也可取火 +{NearestCampfire.EmergencyWithdrawFuel:0}"
                    : $"篝火正暖着，也可取火 +{NearestCampfire.EmergencyWithdrawFuel:0}";
                CurrentInteractPrompt += GetRestHint(restSpot, true);
                return;
            }

            if (smallFire != null)
            {
                CurrentInteractTargetKind = PlayerInteractTargetKind.SmallFire;
                var upgrade = _flameModule?.CampfireUpgrade;
                var tendCost = upgrade != null ? upgrade.TendFuelCost : 0f;
                if (tendCost <= 0f)
                {
                    CurrentInteractPrompt = "这团小火还没有准备好";
                    return;
                }

                CurrentInteractPrompt = upgrade != null && !upgrade.CanStartPublicFire
                    ? $"公共篝火已经够多了（{upgrade.ActiveRuntimeCampfireCount}/{upgrade.MaximumActiveRuntimeCampfires}），可以先收回这团小火"
                    : $"让小火慢慢长成篝火（{tendCost:0.0} 余火）／也可收回";
                return;
            }

            if (flameSource != null)
            {
                CurrentInteractTargetKind = PlayerInteractTargetKind.FlameSource;
                CurrentInteractPrompt = flameSource.IsAvailable
                    ? "拾起这一点暖暖的余烬"
                    : "这里的余烬正在慢慢聚拢";
                return;
            }

            if (restSpot != null)
            {
                CurrentInteractPrompt = GetRestHint(restSpot, false);
            }
        }

        private static string ReadStableId(Component target)
        {
            var stableId = target != null ? target.GetComponent<DemonViglu.FirePlay.Core.StableSceneId>() : null;
            return stableId != null && stableId.IsValid ? stableId.Value : string.Empty;
        }

        private static void SetTarget(PlayerInteractTargetKind targetKind, Component target, out PlayerInteractTargetKind kind, out string stableId)
        {
            kind = target != null ? targetKind : PlayerInteractTargetKind.None;
            stableId = ReadStableId(target);
        }

        private static string GetRestHint(RestSpot restSpot, bool shortForm)
        {
            if (restSpot == null) return string.Empty;
            return restSpot.GetRestHint(shortForm);
        }

        private void ClearNearbyTargets()
        {
            NearestFlameSource = null;
            NearestSmallFire = null;
            NearestCampfire = null;
            NearestWorldTree = null;
            NearestRestSpot = null;
            CurrentInteractTargetKind = PlayerInteractTargetKind.None;
            CurrentInteractPrompt = string.Empty;
        }

        private void OnDrawGizmosSelected()
        {
            if (_flameController == null || _flameController.ActiveFlame == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.72f, 0.24f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, _flameController.ActiveFlame.InteractionRadius);
        }
    }
}
