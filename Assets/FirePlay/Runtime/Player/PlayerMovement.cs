using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 基于 CharacterController 的基础第三人称移动。
    /// 移动方向由相机在水平面上的朝向决定。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(FirePlayPlayerInput))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _moveSpeed = 4.5f;
        [SerializeField, Min(0f)] private float _gravity = 20f;
        [SerializeField] private Transform _cameraTransform;

        private CharacterController _controller;
        private FirePlayPlayerInput _input;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<FirePlayPlayerInput>();

            if (_cameraTransform == null && Camera.main != null)
            {
                _cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (_cameraTransform == null)
            {
                return;
            }

            var input = _input.Move;
            var forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
            var horizontalVelocity = (forward * input.y + right * input.x) * _moveSpeed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity -= _gravity * Time.deltaTime;
            var velocity = horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }
    }
}
