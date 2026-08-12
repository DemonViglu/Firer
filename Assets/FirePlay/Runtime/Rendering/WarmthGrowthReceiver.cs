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

        private Vector3 _authoredScale;
        private Vector3 _authoredPosition;
        private float _growth;

        private void Awake()
        {
            _authoredScale = transform.localScale;
            _authoredPosition = transform.localPosition;
            ApplyGrowth(0f);
        }

        private void Start()
        {
            ApplyGrowth(_growth);
        }

        public override void ApplyEnvironmentWarmth(EnvironmentWarmthField field, float deltaTime)
        {
            var target = Mathf.InverseLerp(_activationThreshold, 1f, field.Sample(transform.position));
            _growth = Mathf.MoveTowards(_growth, target, _growthSpeed * Mathf.Max(0f, deltaTime));
            ApplyGrowth(_growth * _growth * (3f - 2f * _growth));
        }

        private void ApplyGrowth(float value)
        {
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
