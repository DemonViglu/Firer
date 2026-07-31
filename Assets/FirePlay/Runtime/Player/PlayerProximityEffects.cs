using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>消费 Scanner 的接触发现，承接不进入世界命令层的本地被动效果。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerProximityEffects : MonoBehaviour
    {
        private PlayerInteraction _scanner;
        private PlayerFlameController _flameController;

        public void Initialize(PlayerInteraction scanner)
        {
            if (_scanner != null) _scanner.ProximityContactDetected -= OnProximityContactDetected;
            _scanner = scanner;
            _flameController ??= GetComponent<PlayerFlameController>();
            if (isActiveAndEnabled && _scanner != null)
                _scanner.ProximityContactDetected += OnProximityContactDetected;
        }

        private void Awake() => Initialize(GetComponent<PlayerInteraction>());

        private void OnEnable()
        {
            if (_scanner != null)
            {
                _scanner.ProximityContactDetected -= OnProximityContactDetected;
                _scanner.ProximityContactDetected += OnProximityContactDetected;
            }
        }

        private void OnDisable()
        {
            if (_scanner != null) _scanner.ProximityContactDetected -= OnProximityContactDetected;
        }

        private void OnProximityContactDetected(Collider targetCollider)
        {
            var activeFlame = _flameController != null ? _flameController.ActiveFlame : null;
            if (targetCollider == null || activeFlame == null) return;

            targetCollider.GetComponentInParent<ColorSource>()?.TryAbsorb(activeFlame);
            targetCollider.GetComponentInParent<RestorableNode>()?.TryRestore(activeFlame);
        }
    }
}
