using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>承接坐姿活动共用的模式、移动锁定和镜头过渡。</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRestPoseController : MonoBehaviour
    {
        private PlayerMovement _movement;
        private PlayerLook _look;
        private PlayerModeController _modeController;
        private Transform _cameraPivot;
        private Vector3 _standingPivotLocalPosition;
        private float _cameraDrop;
        private float _cameraTransitionSpeed;

        public bool IsActive { get; private set; }

        public void Initialize(
            PlayerMovement movement,
            PlayerLook look,
            PlayerModeController modeController,
            float cameraDrop,
            float cameraTransitionSpeed,
            Vector3 standingPivotLocalPosition)
        {
            _movement = movement;
            _look = look;
            _modeController = modeController;
            _cameraPivot = _look != null ? _look.CameraPivot : null;
            _cameraDrop = Mathf.Max(0f, cameraDrop);
            _cameraTransitionSpeed = Mathf.Max(0f, cameraTransitionSpeed);
            _standingPivotLocalPosition = standingPivotLocalPosition;
        }

        public bool TryEnter()
        {
            if (IsActive || _movement == null || _cameraPivot == null || _modeController == null)
                return false;
            if (!_modeController.TryEnter(PlayerMode.Resting)) return false;

            IsActive = true;
            _movement.SetMovementLocked(true);
            return true;
        }

        public void Exit()
        {
            if (!IsActive) return;

            IsActive = false;
            _modeController?.Exit(PlayerMode.Resting);
            _movement?.SetMovementLocked(false);
        }

        private void LateUpdate()
        {
            if (_cameraPivot == null) return;

            var target = IsActive
                ? _standingPivotLocalPosition + Vector3.down * _cameraDrop
                : _standingPivotLocalPosition;
            var blend = 1f - Mathf.Exp(-_cameraTransitionSpeed * Time.deltaTime);
            _cameraPivot.localPosition = Vector3.Lerp(_cameraPivot.localPosition, target, blend);
        }

        private void OnDisable()
        {
            Exit();
            if (_cameraPivot != null)
                _cameraPivot.localPosition = _standingPivotLocalPosition;
        }
    }
}
