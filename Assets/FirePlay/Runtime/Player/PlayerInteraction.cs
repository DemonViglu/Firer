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
        [SerializeField] private LayerMask _interactionLayers = ~0;

        private readonly Collider[] _overlapResults = new Collider[MaxDetectedColliders];

        private void Awake()
        {
            if (_flameController == null)
            {
                _flameController = GetComponent<PlayerFlameController>();
            }

            if (_flameController == null)
            {
                Debug.LogError("[PlayerInteraction] 未指定 PlayerFlameController。", this);
                enabled = false;
            }
        }

        private void Update()
        {
            var activeFlame = _flameController.ActiveFlame;
            if (activeFlame == null)
            {
                return;
            }

            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                activeFlame.InteractionRadius,
                _overlapResults,
                _interactionLayers,
                QueryTriggerInteraction.Collide);

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

                _overlapResults[index] = null;
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
