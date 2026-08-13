using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Converts the existing Player movement facts into Animator parameters.
    /// It owns presentation parameters only; it never changes movement state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLocomotionAnimationBridge : MonoBehaviour
    {
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private Animator _animator;
        [SerializeField, Min(0.01f)] private float _parameterBlendSpeed = 12f;

        private static readonly int MoveSpeedId = Animator.StringToHash("MoveSpeed");
        private static readonly int IsSprintingId = Animator.StringToHash("IsSprinting");
        private static readonly int IsGroundedId = Animator.StringToHash("IsGrounded");
        private static readonly int IsInWaterId = Animator.StringToHash("IsInWater");
        private static readonly int VerticalVelocityId = Animator.StringToHash("VerticalVelocity");

        private bool _hasMoveSpeed;
        private bool _hasIsSprinting;
        private bool _hasIsGrounded;
        private bool _hasIsInWater;
        private bool _hasVerticalVelocity;

        private void Awake()
        {
            if (_movement == null)
            {
                _movement = GetComponentInParent<PlayerMovement>();
            }

            if (_input == null)
            {
                _input = GetComponentInParent<FirePlayPlayerInput>();
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            CacheParameters();
        }

        private void Update()
        {
            if (_animator == null || _movement == null)
            {
                return;
            }

            var inputMagnitude = _input == null ? 0f : Mathf.Clamp01(_input.Move.magnitude);
            var blend = 1f - Mathf.Exp(-_parameterBlendSpeed * Time.deltaTime);
            if (_hasMoveSpeed)
            {
                _animator.SetFloat(MoveSpeedId, inputMagnitude, 0.08f, Time.deltaTime);
            }

            if (_hasIsSprinting)
            {
                _animator.SetBool(IsSprintingId, _movement.IsSprinting);
            }

            if (_hasIsGrounded)
            {
                _animator.SetBool(IsGroundedId, _movement.IsGrounded);
            }

            if (_hasIsInWater)
            {
                _animator.SetBool(IsInWaterId, _movement.IsInWater);
            }

            if (_hasVerticalVelocity)
            {
                _animator.SetFloat(VerticalVelocityId, Mathf.Lerp(
                    _animator.GetFloat(VerticalVelocityId),
                    _movement.VerticalVelocity,
                    blend));
            }
        }

        private void CacheParameters()
        {
            _hasMoveSpeed = HasParameter(MoveSpeedId, AnimatorControllerParameterType.Float);
            _hasIsSprinting = HasParameter(IsSprintingId, AnimatorControllerParameterType.Bool);
            _hasIsGrounded = HasParameter(IsGroundedId, AnimatorControllerParameterType.Bool);
            _hasIsInWater = HasParameter(IsInWaterId, AnimatorControllerParameterType.Bool);
            _hasVerticalVelocity = HasParameter(VerticalVelocityId, AnimatorControllerParameterType.Float);
        }

        private bool HasParameter(int parameterId, AnimatorControllerParameterType type)
        {
            if (_animator == null)
            {
                return false;
            }

            foreach (var parameter in _animator.parameters)
            {
                if (parameter.nameHash == parameterId && parameter.type == type)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            _parameterBlendSpeed = Mathf.Max(0.01f, _parameterBlendSpeed);
        }
    }
}
