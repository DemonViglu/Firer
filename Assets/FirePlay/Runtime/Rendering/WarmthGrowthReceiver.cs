using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    [DisallowMultipleComponent]
    public sealed class WarmthGrowthReceiver : EnvironmentWarmthReceiverBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float _activationThreshold = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _minimumScale = 0.04f;
        [SerializeField, Min(0.01f)] private float _growthSpeed = 0.65f;
        [SerializeField, Min(0f)] private float _buriedDepth = 0.12f;
        [SerializeField] private Animator _animator;
        [SerializeField] private bool _useAnimator;

        private Vector3 _authoredScale;
        private Vector3 _authoredPosition;
        private float _growth;
        private float _targetGrowth;
        private int _growthStateHash;
        private float _lastAnimationValue = -1f;

        private void Awake()
        {
            _authoredScale = transform.localScale;
            _authoredPosition = transform.localPosition;
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            _growthStateHash = Animator.StringToHash("WarmthGrowth");
            _useAnimator = _useAnimator
                && _animator != null
                && _animator.runtimeAnimatorController != null
                && _animator.HasState(0, _growthStateHash);
            if (_useAnimator)
            {
                // The heat field owns the animation phase. Letting the Animator
                // advance on its own would make the plant move between samples and
                // then jump back to the sampled phase, which reads as twitching.
                _animator.speed = 0f;
            }
            ApplyGrowth(0f);
        }

        private void Start()
        {
            ApplyGrowth(EaseGrowth(_growth));
        }

        private void Update()
        {
            var nextGrowth = Mathf.MoveTowards(_growth, _targetGrowth, _growthSpeed * Time.deltaTime);
            if (Mathf.Abs(nextGrowth - _growth) < 0.00001f)
            {
                return;
            }

            _growth = nextGrowth;
            ApplyGrowth(EaseGrowth(_growth));
        }

        public override void ApplyEnvironmentWarmth(EnvironmentWarmthField field, float deltaTime)
        {
            _targetGrowth = Mathf.InverseLerp(_activationThreshold, 1f, field.Sample(transform.position));
        }

        private float EaseGrowth(float value)
        {
            return value * value * (3f - 2f * value);
        }

        private void ApplyGrowth(float value)
        {
            if (_useAnimator && _animator != null)
            {
                var animationValue = Mathf.Clamp01(value);
                if (Mathf.Abs(animationValue - _lastAnimationValue) < 0.001f)
                {
                    return;
                }

                _animator.Play(_growthStateHash, 0, animationValue);
                _animator.Update(0f);
                _lastAnimationValue = animationValue;
                return;
            }

            transform.localScale = _authoredScale * Mathf.Lerp(_minimumScale, 1f, value);
            transform.localPosition = _authoredPosition + Vector3.down * (_buriedDepth * (1f - value));
        }

        private void OnValidate()
        {
            _minimumScale = Mathf.Clamp01(_minimumScale);
            _growthSpeed = Mathf.Max(0.01f, _growthSpeed);
            _buriedDepth = Mathf.Max(0f, _buriedDepth);
        }
    }
}
