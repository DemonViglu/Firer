using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public interface IPlayerSprintPolicy
    {
        bool TryConsumeSprint(float deltaTime);
    }

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
        [SerializeField] private MonoBehaviour _sprintPolicyBehaviour;

        private CharacterController _controller;
        private FirePlayPlayerInput _input;
        private IPlayerSprintPolicy _sprintPolicy;
        private float _verticalVelocity;

        public bool IsSprinting { get; private set; }
        public bool MovementLocked { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<FirePlayPlayerInput>();
            _sprintPolicy = _sprintPolicyBehaviour as IPlayerSprintPolicy;

            // 移动方向优先跟随 PlayerLook 的 CameraPivot，而不是实际渲染相机。
            // 这样 Main Camera 交给 Cinemachine 或从 Player 层级移出后，探索移动仍与玩家视角一致。
            if (_cameraTransform == null)
            {
                _cameraTransform = GetComponent<PlayerLook>()?.CameraPivot;
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

            var input = MovementLocked ? Vector2.zero : _input.Move;
            IsSprinting = !MovementLocked && TrySprint(input);
            // 移动只读取视角的世界 Yaw。不要直接使用 forward/right：当相机 Pivot
            // 因俯仰、滚转或 Cinemachine 层级调整而倾斜时，移动仍必须严格留在地面平面。
            var yawOnlyRotation = Quaternion.Euler(0f, _cameraTransform.eulerAngles.y, 0f);
            var forward = yawOnlyRotation * Vector3.forward;
            var right = yawOnlyRotation * Vector3.right;
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

        public void SetMovementLocked(bool locked)
        {
            MovementLocked = locked;
            if (locked)
            {
                IsSprinting = false;
            }
        }

        public void BindSprintPolicy(MonoBehaviour behaviour)
        {
            _sprintPolicyBehaviour = behaviour;
            _sprintPolicy = behaviour as IPlayerSprintPolicy;
        }

        private bool TrySprint(Vector2 moveInput)
        {
            if (!_input.SprintHeld || moveInput.sqrMagnitude < 0.01f)
            {
                return false;
            }

            // A base Player has no resource policy and can sprint freely. The
            // FlameModule supplies a policy when sprint should consume fuel.
            return _sprintPolicy == null || _sprintPolicy.TryConsumeSprint(Time.deltaTime);
        }

        private void OnValidate()
        {
            _moveSpeed = Mathf.Max(0f, _moveSpeed);
            _sprintSpeedMultiplier = Mathf.Max(1f, _sprintSpeedMultiplier);
            _gravity = Mathf.Max(0f, _gravity);
        }
    }
}
