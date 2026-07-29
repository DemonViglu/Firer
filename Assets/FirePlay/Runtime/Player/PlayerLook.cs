using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 鼠标控制角色水平转向及相机枢轴的垂直转向。
    /// 相机应作为 Camera Pivot 的子物体，且 Pivot 位于玩家的肩部附近。
    /// </summary>
    [RequireComponent(typeof(FirePlayPlayerInput))]
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform _cameraPivot;
        [SerializeField, Min(0.01f)] private float _sensitivity = 0.12f;
        [SerializeField] private Vector2 _pitchLimits = new(-55f, 70f);

        private FirePlayPlayerInput _input;
        private float _pitch;
        private int _framesToIgnoreLookInput;
        private bool _cursorCaptured;

        public Transform CameraPivot => _cameraPivot;
        public bool LookLocked { get; private set; }

        private void Awake()
        {
            _input = GetComponent<FirePlayPlayerInput>();

            if (_cameraPivot == null)
            {
                Debug.LogError("[PlayerLook] 未指定 Camera Pivot。", this);
                enabled = false;
                return;
            }

            _pitch = NormalizeAngle(_cameraPivot.localEulerAngles.x);
        }

        private void OnEnable()
        {
            SetCursorCaptured(true);

            // 进入 Play Mode 或重新启用组件时，Unity 编辑器可能会把光标锁定本身
            // 报告为一次很大的鼠标 Delta。丢弃首帧，避免视角直接跳到俯仰角上限。
            _framesToIgnoreLookInput = 1;
        }

        private void OnDisable()
        {
            SetCursorCaptured(false);
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (UnityEngine.InputSystem.Keyboard.current?.f1Key.wasPressedThisFrame == true)
            {
                SetCursorCaptured(!_cursorCaptured);
            }
#endif

            // In the Editor, clicking the Game view can cause Unity to reapply its
            // cursor-focus behavior. While explicitly released for UI testing, keep
            // the native cursor unlocked every frame and never consume look delta.
            if (!_cursorCaptured)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (_framesToIgnoreLookInput > 0)
            {
                _framesToIgnoreLookInput--;
                return;
            }

            if (LookLocked)
            {
                return;
            }

            var look = _input.Look * _sensitivity;
            transform.Rotate(Vector3.up, look.x, Space.World);

            _pitch = Mathf.Clamp(_pitch - look.y, _pitchLimits.x, _pitchLimits.y);
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void SetLookLocked(bool locked) => LookLocked = locked;

        public void SetCursorCaptured(bool captured)
        {
            _cursorCaptured = captured;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }

        private static float NormalizeAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }
    }
}
