using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public enum PlayerInteractTargetKind
    {
        None,
        WorldTree,
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
        public CampfireUpgradeController CampfireUpgradeController => _campfireUpgradeController;
        public PlayerInteractTargetKind CurrentInteractTargetKind { get; private set; }
        public string CurrentInteractPrompt { get; private set; } = "No interaction target";

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
            SelectCurrentInteractTarget(nearestSmallFire, nearestSmallFireDistance, nearestFlameSource, nearestFlameSourceDistance, nearestWorldTree, nearestWorldTreeDistance);

            if (_input.UpgradeCampfirePressedThisFrame && nearestCampfire != null)
            {
                nearestCampfire.TryUpgrade(_flameResourceController);
            }
            else if (_input.UpgradeCampfirePressedThisFrame && nearestSmallFire != null)
            {
                _campfireUpgradeController?.TryUpgradeSmallFire(nearestSmallFire);
            }

            if (interactPressed)
            {
                InteractWithCurrentTarget(activeFlame);
            }
        }

        private void SelectCurrentInteractTarget(
            SmallFire smallFire,
            float smallFireDistance,
            FlameSource flameSource,
            float flameSourceDistance,
            WorldTreeContribution worldTree,
            float worldTreeDistance)
        {
            CurrentInteractTargetKind = PlayerInteractTargetKind.None;
            CurrentInteractPrompt = "No interaction target";

            // Explicit priorities prevent overlapping colliders from silently changing E behaviour.
            if (worldTree != null)
            {
                CurrentInteractTargetKind = PlayerInteractTargetKind.WorldTree;
                CurrentInteractPrompt = worldTree.HasLocalContribution
                    ? "World Tree: already contributed"
                    : $"Press E: contribute ({worldTree.ContributionCost:0.0})";
                return;
            }

            if (flameSource != null)
            {
                CurrentInteractTargetKind = PlayerInteractTargetKind.FlameSource;
                CurrentInteractPrompt = flameSource.IsAvailable
                    ? "Press E: restore flame"
                    : $"Flame source cooling ({flameSource.RemainingCooldownSeconds:0.0}s)";
                return;
            }

            if (smallFire != null)
            {
                CurrentInteractTargetKind = PlayerInteractTargetKind.SmallFire;
                CurrentInteractPrompt = "Press E: reclaim small fire";
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
                case PlayerInteractTargetKind.SmallFire:
                    NearestSmallFire?.TryReclaim(_flameResourceController);
                    break;
            }
        }

        private void ClearNearbyTargets()
        {
            NearestFlameSource = null;
            NearestSmallFire = null;
            NearestCampfire = null;
            NearestWorldTree = null;
            CurrentInteractTargetKind = PlayerInteractTargetKind.None;
            CurrentInteractPrompt = "No interaction target";
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
