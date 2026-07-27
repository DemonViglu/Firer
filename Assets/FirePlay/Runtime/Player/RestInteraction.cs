using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 可选停留状态：进入后相机自然下沉并停止水平移动，再次触发停留操作即可离开。
    /// 公开方法由未来移动端 UI 调用，R 键只用于原型验证。
    /// </summary>
    public sealed class RestInteraction : MonoBehaviour
    {
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerLook _look;
        [SerializeField, Min(0f)] private float _cameraDrop = 0.55f;
        [SerializeField, Min(0f)] private float _cameraTransitionSpeed = 5f;

        private Transform _cameraPivot;
        private Vector3 _standingPivotLocalPosition;

        public bool IsResting { get; private set; }
        public RestSpot NearestRestSpot { get; private set; }

        private void Awake()
        {
            _input ??= GetComponent<FirePlayPlayerInput>();
            _movement ??= GetComponent<PlayerMovement>();
            _look ??= GetComponent<PlayerLook>();
            _cameraPivot = _look != null ? _look.CameraPivot : null;

            if (_input == null || _movement == null || _cameraPivot == null)
            {
                Debug.LogError("[RestInteraction] 缺少输入、移动组件或 Camera Pivot。", this);
                enabled = false;
                return;
            }

            _standingPivotLocalPosition = _cameraPivot.localPosition;
        }

        private void Update()
        {
            NearestRestSpot = RestSpot.FindNearest(transform.position);

            if (_input.RestPressedThisFrame)
            {
                if (IsResting)
                {
                    EndRest();
                }
                else
                {
                    TryBeginRest();
                }
            }

        }

        private void LateUpdate()
        {
            if (_cameraPivot == null)
            {
                return;
            }

            var target = IsResting
                ? _standingPivotLocalPosition + Vector3.down * _cameraDrop
                : _standingPivotLocalPosition;
            var blend = 1f - Mathf.Exp(-_cameraTransitionSpeed * Time.deltaTime);
            _cameraPivot.localPosition = Vector3.Lerp(_cameraPivot.localPosition, target, blend);
        }

        public bool TryBeginRest()
        {
            if (IsResting || NearestRestSpot == null)
            {
                return false;
            }

            IsResting = true;
            _movement.SetMovementLocked(true);
            return true;
        }

        public void EndRest()
        {
            if (!IsResting)
            {
                return;
            }

            IsResting = false;
            _movement.SetMovementLocked(false);
        }

        private void OnDisable()
        {
            EndRest();
            if (_cameraPivot != null)
            {
                _cameraPivot.localPosition = _standingPivotLocalPosition;
            }
        }
    }
}
