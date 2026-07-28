using DemonViglu.FirePlay.World;
using System;
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
        [SerializeField] private PlayerModeController _modeController;
        [SerializeField, Min(0f)] private float _cameraDrop = 0.55f;
        [SerializeField, Min(0f)] private float _cameraTransitionSpeed = 5f;

        private Transform _cameraPivot;
        private Vector3 _standingPivotLocalPosition;

        public bool IsResting { get; private set; }
        public RestSpot NearestRestSpot { get; private set; }
        public RestSpot ActiveRestSpot { get; private set; }

        public event Action<RestSpot> RestStarted;
        public event Action<RestSpot> RestEnded;

        private void Awake()
        {
            _input ??= GetComponent<FirePlayPlayerInput>();
            _movement ??= GetComponent<PlayerMovement>();
            _look ??= GetComponent<PlayerLook>();
            _modeController ??= GetComponent<PlayerModeController>();
            _cameraPivot = _look != null ? _look.CameraPivot : null;

            if (_input == null || _movement == null || _cameraPivot == null || _modeController == null)
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
            if (IsResting || NearestRestSpot == null || !_modeController.TryEnter(PlayerMode.Resting))
            {
                return false;
            }

            IsResting = true;
            ActiveRestSpot = NearestRestSpot;
            _movement.SetMovementLocked(true);
            ActiveRestSpot.NotifyRestStarted(this);
            RestStarted?.Invoke(ActiveRestSpot);
            return true;
        }

        public void EndRest()
        {
            if (!IsResting)
            {
                return;
            }

            var completedSpot = ActiveRestSpot;
            IsResting = false;
            _modeController?.Exit(PlayerMode.Resting);
            ActiveRestSpot = null;
            _movement.SetMovementLocked(false);
            if (completedSpot != null)
            {
                completedSpot.NotifyRestEnded(this);
                RestEnded?.Invoke(completedSpot);
            }
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
