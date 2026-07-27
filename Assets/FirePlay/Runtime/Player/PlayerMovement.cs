using DemonViglu.FirePlay.Flame;
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
        [SerializeField, Min(1f)] private float _sprintSpeedMultiplier = 1.55f;
        [SerializeField, Min(0f)] private float _gravity = 20f;
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private FlameResourceController _flameResourceController;

        private CharacterController _controller;
        private FirePlayPlayerInput _input;
        private float _verticalVelocity;

        public bool IsSprinting { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<FirePlayPlayerInput>();

            if (_flameResourceController == null)
            {
                _flameResourceController = GetComponent<FlameResourceController>();
            }

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
            IsSprinting = TrySprint(input);
            var forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
            var speed = _moveSpeed * (IsSprinting ? _sprintSpeedMultiplier : 1f);
            var horizontalVelocity = (forward * input.y + right * input.x) * speed;

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }

            _verticalVelocity -= _gravity * Time.deltaTime;
            var velocity = horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private bool TrySprint(Vector2 moveInput)
        {
            if (!_input.SprintHeld || moveInput.sqrMagnitude < 0.01f || _flameResourceController == null)
            {
                return false;
            }

            var config = _flameResourceController.Config;
            if (config == null)
            {
                return false;
            }

            var sprintCost = config.SprintDrainPerSecond * Time.deltaTime;
            return sprintCost <= 0f || _flameResourceController.TryConsume(sprintCost);
        }

        private void OnValidate()
        {
            _moveSpeed = Mathf.Max(0f, _moveSpeed);
            _sprintSpeedMultiplier = Mathf.Max(1f, _sprintSpeedMultiplier);
            _gravity = Mathf.Max(0f, _gravity);
        }
    }
}
