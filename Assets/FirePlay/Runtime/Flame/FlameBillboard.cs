using UnityEngine;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// 让火苗的 Quad 始终面向主相机。
    /// 火苗仍是 3D 世界中的对象，只是视觉平面朝向观察者。
    /// </summary>
    public sealed class FlameBillboard : MonoBehaviour
    {
        [SerializeField] private Camera _targetCamera;

        private void LateUpdate()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_targetCamera == null)
            {
                return;
            }

            var direction = _targetCamera.transform.position - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }
    }
}
