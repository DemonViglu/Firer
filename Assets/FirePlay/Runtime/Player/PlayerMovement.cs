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
        [SerializeField, Min(0f)] private float _groundJumpHeight = 1.25f;
        [SerializeField, Min(0f)] private float _groundedStickSpeed = 2f;
        [SerializeField] private Transform _cameraTransform;
        [Tooltip("Only the visual model turns while moving. Keep the Player root stable so it can own collision and the camera pivot.")]
        [SerializeField] private Transform _visualTransform;
        [SerializeField, Min(0f)] private float _turnSpeed = 900f;
        [SerializeField] private MonoBehaviour _sprintPolicyBehaviour;

        [Header("Water Movement")]
        [SerializeField, Range(0.1f, 1f)] private float _waterMoveSpeedMultiplier = 0.62f;
        [SerializeField, Range(0.1f, 1f)] private float _waterEntryMoveSpeedMultiplier = 0.42f;
        [SerializeField, Min(0f)] private float _waterEntrySinkDuration = 0.72f;
        [SerializeField, Min(0f)] private float _waterEntryMinSinkSpeed = 3.6f;
        [SerializeField, Min(0f)] private float _waterEntryMaxSinkSpeed = 6.2f;
        [SerializeField, Min(0f)] private float _waterRiseSpeed = 2.8f;
        [SerializeField, Min(0f)] private float _waterBuoyancyAcceleration = 7.5f;
        [SerializeField, Min(0f)] private float _waterSurfaceOffset = 1.25f;
        [SerializeField, Min(0f)] private float _waterJumpDepth = 0.32f;
        [SerializeField, Min(0f)] private float _waterExitSpeed = 8.2f;

        private enum WaterMotionPhase
        {
            None,
            EntrySubmerge,
            Buoyant,
            Exiting
        }

        private CharacterController _controller;
        private FirePlayPlayerInput _input;
        private IPlayerSprintPolicy _sprintPolicy;
        private float _verticalVelocity;
        private bool _localControl = true;
        private MonoBehaviour _waterSource;
        private float _waterSurfaceY;
        private WaterMotionPhase _waterMotionPhase;
        private float _waterEntryElapsed;
        private float _waterEntryStartVelocity;

        public bool IsSprinting { get; private set; }
        public bool MovementLocked { get; private set; }
        public bool HasLocalControl => _localControl;
        public float BaseMoveSpeed => _moveSpeed;
        public float SprintSpeed => _moveSpeed * _sprintSpeedMultiplier;
        public bool IsInWater => _waterSource != null;
        public bool IsGrounded { get; private set; }
        public bool IsJumping => !IsInWater && !IsGrounded && _verticalVelocity > 0f;
        public float VerticalVelocity => _verticalVelocity;

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

            // The authored facing root is deliberately separate from the animated
            // visual child. Locomotion owns this root; presentation cues may only
            // add local offsets below it.
            if (_visualTransform == null)
            {
                _visualTransform = transform.Find("CharacterFacingRoot")
                    ?? transform.Find("SnowTravelerVisual");
            }
        }

        private void Update()
        {
            if (!_localControl)
            {
                IsSprinting = false;
                return;
            }

            if (_cameraTransform == null)
            {
                return;
            }

            var input = MovementLocked ? Vector2.zero : _input.Move;
            var jumpPressed = !MovementLocked && _input.ConsumeJumpPressed();
            var isInWater = IsInWater;
            IsSprinting = !MovementLocked && !isInWater && TrySprint(input);
            // 移动只读取视角的世界 Yaw。不要直接使用 forward/right：当相机 Pivot
            // 因俯仰、滚转或 Cinemachine 层级调整而倾斜时，移动仍必须严格留在地面平面。
            var yawOnlyRotation = Quaternion.Euler(0f, _cameraTransform.eulerAngles.y, 0f);
            var forward = yawOnlyRotation * Vector3.forward;
            var right = yawOnlyRotation * Vector3.right;
            var speed = _moveSpeed * (IsSprinting ? _sprintSpeedMultiplier : 1f);
            if (isInWater)
            {
                speed *= _waterMotionPhase == WaterMotionPhase.EntrySubmerge
                    ? _waterEntryMoveSpeedMultiplier
                    : _waterMoveSpeedMultiplier;
            }

            var horizontalVelocity = (forward * input.y + right * input.x) * speed;
            RotateTowardsMovement(horizontalVelocity);

            if (isInWater)
            {
                IsGrounded = false;
                UpdateWaterVerticalVelocity(jumpPressed);
            }
            else
            {
                IsGrounded = _controller.isGrounded;
                if (IsGrounded && _verticalVelocity < 0f)
                {
                    _verticalVelocity = -_groundedStickSpeed;
                }

                if (jumpPressed && IsGrounded && _groundJumpHeight > 0f && _gravity > 0f)
                {
                    _verticalVelocity = Mathf.Sqrt(2f * _gravity * _groundJumpHeight);
                    IsGrounded = false;
                }

                _verticalVelocity -= _gravity * Time.deltaTime;
            }

            var velocity = horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
            if (!isInWater)
            {
                IsGrounded = _controller.isGrounded;
            }
        }

        /// <summary>
        /// Scene water volumes explicitly register here. This keeps water detection in
        /// the environment while the existing, visible PlayerMovement owns locomotion.
        /// </summary>
        public void SetWaterContact(MonoBehaviour source, float surfaceY, bool touching)
        {
            if (source == null)
            {
                return;
            }

            if (touching)
            {
                var wasInWater = IsInWater;
                if (_waterSource == null || _waterSource == source || surfaceY >= _waterSurfaceY)
                {
                    _waterSource = source;
                    _waterSurfaceY = surfaceY;
                }

                if (!wasInWater && IsInWater)
                {
                    BeginWaterEntry();
                }

                return;
            }

            if (_waterSource == source)
            {
                _waterSource = null;
                _waterMotionPhase = WaterMotionPhase.None;
            }
        }

        public void SetMovementLocked(bool locked)
        {
            MovementLocked = locked;
            if (locked)
            {
                IsSprinting = false;
            }
        }

        public void SetLocalControl(bool enabled)
        {
            _localControl = enabled;
            if (!enabled)
                IsSprinting = false;
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

        /// <summary>
        /// Faces the visible character without rotating the Player root. The root is
        /// shared by CharacterController, camera pivot and gameplay attachment points.
        /// </summary>
        public bool TryFaceDirection(Vector3 direction, bool instant = false)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            var turnTarget = _visualTransform != null ? _visualTransform : transform;
            turnTarget.rotation = instant
                ? targetRotation
                : Quaternion.RotateTowards(
                turnTarget.rotation,
                targetRotation,
                _turnSpeed * Time.deltaTime);
            return true;
        }

        private void RotateTowardsMovement(Vector3 horizontalVelocity)
        {
            TryFaceDirection(horizontalVelocity);
        }

        private void BeginWaterEntry()
        {
            _waterMotionPhase = WaterMotionPhase.EntrySubmerge;
            _waterEntryElapsed = 0f;
            _waterEntryStartVelocity = Mathf.Clamp(
                _verticalVelocity,
                -_waterEntryMaxSinkSpeed,
                -_waterEntryMinSinkSpeed);
        }

        private void UpdateWaterVerticalVelocity(bool jumpPressed)
        {
            var targetRootY = _waterSurfaceY - _waterSurfaceOffset;

            if (_waterMotionPhase == WaterMotionPhase.EntrySubmerge)
            {
                _waterEntryElapsed += Time.deltaTime;
                var progress = _waterEntrySinkDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(_waterEntryElapsed / _waterEntrySinkDuration);
                var easedProgress = progress * progress * (3f - 2f * progress);
                _verticalVelocity = Mathf.Lerp(_waterEntryStartVelocity, 0f, easedProgress);
                if (progress >= 1f)
                {
                    _waterMotionPhase = WaterMotionPhase.Buoyant;
                }

                return;
            }

            if (_waterMotionPhase == WaterMotionPhase.Exiting)
            {
                _verticalVelocity -= _gravity * Time.deltaTime;
                if (_verticalVelocity <= 0f && transform.position.y <= targetRootY + 0.1f)
                {
                    _waterMotionPhase = WaterMotionPhase.Buoyant;
                }

                return;
            }

            if (jumpPressed && transform.position.y >= targetRootY - _waterJumpDepth)
            {
                _waterMotionPhase = WaterMotionPhase.Exiting;
                _verticalVelocity = _waterExitSpeed;
                return;
            }

            _waterMotionPhase = WaterMotionPhase.Buoyant;
            var heightDelta = targetRootY - transform.position.y;
            var desiredVerticalVelocity = Mathf.Clamp(heightDelta * 3f, -_waterRiseSpeed, _waterRiseSpeed);
            _verticalVelocity = Mathf.MoveTowards(
                _verticalVelocity,
                desiredVerticalVelocity,
                _waterBuoyancyAcceleration * Time.deltaTime);
        }

        private void OnValidate()
        {
            _moveSpeed = Mathf.Max(0f, _moveSpeed);
            _sprintSpeedMultiplier = Mathf.Max(1f, _sprintSpeedMultiplier);
            _gravity = Mathf.Max(0f, _gravity);
            _groundJumpHeight = Mathf.Max(0f, _groundJumpHeight);
            _groundedStickSpeed = Mathf.Max(0f, _groundedStickSpeed);
            _turnSpeed = Mathf.Max(0f, _turnSpeed);
            _waterMoveSpeedMultiplier = Mathf.Clamp(_waterMoveSpeedMultiplier, 0.1f, 1f);
            _waterEntryMoveSpeedMultiplier = Mathf.Clamp(_waterEntryMoveSpeedMultiplier, 0.1f, 1f);
            _waterEntrySinkDuration = Mathf.Max(0f, _waterEntrySinkDuration);
            _waterEntryMinSinkSpeed = Mathf.Max(0f, _waterEntryMinSinkSpeed);
            _waterEntryMaxSinkSpeed = Mathf.Max(_waterEntryMinSinkSpeed, _waterEntryMaxSinkSpeed);
            _waterRiseSpeed = Mathf.Max(0f, _waterRiseSpeed);
            _waterBuoyancyAcceleration = Mathf.Max(0f, _waterBuoyancyAcceleration);
            _waterSurfaceOffset = Mathf.Max(0f, _waterSurfaceOffset);
            _waterJumpDepth = Mathf.Max(0f, _waterJumpDepth);
            _waterExitSpeed = Mathf.Max(0f, _waterExitSpeed);
        }
    }
}
