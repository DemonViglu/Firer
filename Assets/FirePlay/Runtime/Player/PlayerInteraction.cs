using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
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
        [SerializeField] private LayerMask _interactionLayers = ~0;

        private readonly Collider[] _overlapResults = new Collider[MaxDetectedColliders];

        public FlameSource NearestFlameSource { get; private set; }
        public SmallFire NearestSmallFire { get; private set; }

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

            if (_flameController == null || _input == null || _flameResourceController == null)
            {
                Debug.LogError("[PlayerInteraction] 缺少 PlayerFlameController、输入或余火控制器。", this);
                enabled = false;
            }
        }

        private void Update()
        {
            var activeFlame = _flameController.ActiveFlame;
            if (activeFlame == null)
            {
                NearestFlameSource = null;
                NearestSmallFire = null;
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

                _overlapResults[index] = null;
            }

            NearestFlameSource = nearestFlameSource;
            NearestSmallFire = nearestSmallFire;

            if (interactPressed && nearestSmallFire != null && nearestSmallFireDistance <= nearestFlameSourceDistance)
            {
                nearestSmallFire.TryReclaim(_flameResourceController);
            }
            else if (interactPressed && nearestFlameSource != null)
            {
                nearestFlameSource.TryRestore(_flameResourceController);
            }
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
