using System.Collections.Generic;
using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// M1 数值验证用恢复区域。只声明玩家当前处于恢复环境，
    /// 不承担自然火源、篝火的视觉或交互设计。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FlameRecoveryVolume : MonoBehaviour
    {
        private readonly Dictionary<FlameResourceController, int> _insideControllers = new();

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var controller = other.GetComponentInParent<FlameResourceController>();
            if (controller == null)
            {
                return;
            }

            _insideControllers.TryGetValue(controller, out var overlapCount);
            _insideControllers[controller] = overlapCount + 1;
            if (overlapCount == 0)
            {
                controller.EnterRecoverySource();
                controller.EnterSafeZone();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var controller = other.GetComponentInParent<FlameResourceController>();
            if (controller == null || !_insideControllers.TryGetValue(controller, out var overlapCount))
            {
                return;
            }

            if (overlapCount > 1)
            {
                _insideControllers[controller] = overlapCount - 1;
                return;
            }

            _insideControllers.Remove(controller);
            controller.ExitRecoverySource();
            controller.ExitSafeZone();
        }

        private void OnDisable()
        {
            foreach (var pair in _insideControllers)
            {
                var controller = pair.Key;
                if (controller != null)
                {
                    controller.ExitRecoverySource();
                    controller.ExitSafeZone();
                }
            }

            _insideControllers.Clear();
        }

        private void OnDrawGizmos()
        {
            var volumeCollider = GetComponent<Collider>();
            if (volumeCollider == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.22f);
            Gizmos.DrawCube(volumeCollider.bounds.center, volumeCollider.bounds.size);
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(volumeCollider.bounds.center, volumeCollider.bounds.size);
        }
    }
}
