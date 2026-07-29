using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
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
    /// 玩家在世界中的近距离交互入口。
    /// 交互范围围绕玩家本体，而不是视觉上有偏移的火苗。
    /// </summary>
    public sealed class PlayerInteraction : MonoBehaviour
    {
        private const int MaxDetectedColliders = 8;

        [SerializeField] private PlayerFlameController _flameController;
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private FlameResourceController _flameResourceController;
        [SerializeField] private CampfireUpgradeController _campfireUpgradeController;
        [SerializeField] private LayerMask _interactionLayers = ~0;
        [SerializeField] private PlayerModeController _modeController;

        private readonly Collider[] _overlapResults = new Collider[MaxDetectedColliders];

        public FlameSource NearestFlameSource { get; private set; }
        public SmallFire NearestSmallFire { get; private set; }
        public Campfire NearestCampfire { get; private set; }
        public WorldTreeContribution NearestWorldTree { get; private set; }
        public RestSpot NearestRestSpot { get; private set; }
        public CampfireUpgradeController CampfireUpgradeController => _campfireUpgradeController;
        public PlayerInteractTargetKind CurrentInteractTargetKind { get; private set; }
        public string CurrentInteractPrompt { get; private set; } = string.Empty;

        private void Awake()
        {
            if (_flameController == null)
            {
                _flameController = GetComponent<PlayerFlameController>();
            }

            if (_input == null)
            {
                _input = GetComponent<FirePlayPlayerInput>();
            }

            if (_flameResourceController == null)
            {
                _flameResourceController = GetComponent<FlameResourceController>();
            }

            if (_campfireUpgradeController == null)
            {
                _campfireUpgradeController = GetComponent<CampfireUpgradeController>();
            }
            _modeController ??= GetComponent<PlayerModeController>();

            if (_flameController == null || _input == null || _flameResourceController == null)
            {
                Debug.LogError("[PlayerInteraction] 缺少 PlayerFlameController、输入或余火控制器。", this);
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
            var interactPressed = _input.InteractPressedThisFrame;

            for (var index = 0; index < count; index++)
            {
                var source = _overlapResults[index].GetComponentInParent<ColorSource>();
                if (source != null)
                {
                    source.TryAbsorb(activeFlame);
                }

                var restorableNode = _overlapResults[index].GetComponentInParent<RestorableNode>();
                if (restorableNode != null)
                {
                    restorableNode.TryRestore(activeFlame);
                }

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

            if (interactPressed)
            {
                InteractWithCurrentTarget(activeFlame);
            }

            // Touch controls use explicit verbs. Unlike the legacy E/G prototype,
            // a "Tend" button can never withdraw, and a "Draw" button can never tend.
            if (_input.AddFirePressedThisFrame)
            {
                if (NearestCampfire != null)
                {
                    NearestCampfire.TryTend(_flameResourceController);
                }
                else
                {
                    _campfireUpgradeController?.TryTendSmallFire(NearestSmallFire);
                }
            }
            if (_input.TendFirePressedThisFrame)
            {
                NearestCampfire?.TryTend(_flameResourceController);
            }
            if (_input.GatherEmberPressedThisFrame)
            {
                NearestFlameSource?.TryRestore(_flameResourceController);
            }
            if (_input.StartPublicFirePressedThisFrame)
            {
                _campfireUpgradeController?.TryTendSmallFire(NearestSmallFire);
            }
            if (_input.DrawFirePressedThisFrame)
            {
                NearestCampfire?.TryWithdrawEmergencyFuel(_flameResourceController);
            }
            if (_input.ReclaimSmallFirePressedThisFrame)
            {
                NearestSmallFire?.TryReclaim(_flameResourceController);
            }
            if (_input.ContributeWorldTreePressedThisFrame)
            {
                NearestWorldTree?.TryContribute(_flameResourceController, activeFlame);
            }

            if (_input.UpgradeCampfirePressedThisFrame && CurrentInteractTargetKind == PlayerInteractTargetKind.Campfire)
            {
                NearestCampfire?.TryWithdrawEmergencyFuel(_flameResourceController);
            }
            else if (_input.UpgradeCampfirePressedThisFrame && nearestSmallFire != null)
            {
                nearestSmallFire.TryReclaim(_flameResourceController);
            }
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
                var tendCost = _campfireUpgradeController != null ? _campfireUpgradeController.TendFuelCost : 0f;
                if (tendCost <= 0f)
                {
                    CurrentInteractPrompt = "这团小火还没有准备好";
                    return;
                }

                CurrentInteractPrompt = _campfireUpgradeController != null && !_campfireUpgradeController.CanStartPublicFire
                    ? $"公共篝火已经够多了（{_campfireUpgradeController.ActiveRuntimeCampfireCount}/{_campfireUpgradeController.MaximumActiveRuntimeCampfires}），可以先收回这团小火"
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

        private void InteractWithCurrentTarget(FlameBrush activeFlame)
        {
            switch (CurrentInteractTargetKind)
            {
                case PlayerInteractTargetKind.WorldTree:
                    NearestWorldTree?.TryContribute(_flameResourceController, activeFlame);
                    break;
                case PlayerInteractTargetKind.FlameSource:
                    NearestFlameSource?.TryRestore(_flameResourceController);
                    break;
                case PlayerInteractTargetKind.Campfire:
                    NearestCampfire?.TryTend(_flameResourceController);
                    break;
                case PlayerInteractTargetKind.SmallFire:
                    _campfireUpgradeController?.TryTendSmallFire(NearestSmallFire);
                    break;
            }
        }

        private static string GetRestHint(RestSpot restSpot, bool shortForm)
        {
            if (restSpot == null) return string.Empty;
            if (restSpot.GetComponent<MarshmallowRitual>() != null)
                return shortForm ? " · 这里可以坐下烤棉花糖" : "这里可以坐下，烤一颗棉花糖";
            if (restSpot.GetComponent<FishingRitual>() != null)
                return shortForm ? " · 这里可以坐下钓鱼" : "这里适合坐下来，静静钓一会儿鱼";
            if (restSpot.GetComponent<StargazingRitual>() != null)
                return shortForm ? " · 这里可以坐下看星星" : "这里可以坐下，抬头看看星星";
            return shortForm ? " · 这里可以坐下歇一会儿" : "这里可以坐下，安静歇一会儿";
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
