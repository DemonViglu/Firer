using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 读取附近火源的温暖状态，并把结果提供给场景表现。
    /// 不保存、不消耗余火，也不修改火源状态。
    /// </summary>
    public sealed class WarmthNode : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField, Min(0.1f)] private float _radius = 6f;
        [SerializeField, Range(0f, 1f)] private float _smallFireWarmth = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _campfireBaseWarmth = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _campfireWarmthPerLevel = 0.15f;
        [SerializeField, Min(0.01f)] private float _transitionSpeed = 2f;

        [Header("Optional Placeholder Visual")]
        [SerializeField] private Light _warmthLight;
        [SerializeField, Min(0f)] private float _lightIntensityAtFullWarmth = 2f;
        [SerializeField, Min(0f)] private float _lightRangeAtFullWarmth = 6f;

        private float _warmth;

        public float Warmth => _warmth;
        public float Radius => _radius;

        private void Awake()
        {
            ApplyVisuals();
        }

        private void Update()
        {
            var targetWarmth = CalculateTargetWarmth();
            _warmth = Mathf.MoveTowards(_warmth, targetWarmth, _transitionSpeed * Time.deltaTime);
            ApplyVisuals();
        }

        private float CalculateTargetWarmth()
        {
            var targetWarmth = 0f;

            var smallFire = SmallFire.FindNearest(transform.position, _radius, out var smallFireDistance);
            if (smallFire != null)
            {
                targetWarmth = Mathf.Max(targetWarmth, _smallFireWarmth * CalculateFalloff(smallFireDistance));
            }

            var campfire = Campfire.FindNearest(transform.position, _radius, out var campfireDistance);
            if (campfire != null && campfire.Level > 0)
            {
                var campfireWarmth = _campfireBaseWarmth + (campfire.Level - 1) * _campfireWarmthPerLevel;
                targetWarmth = Mathf.Max(targetWarmth, Mathf.Clamp01(campfireWarmth) * CalculateFalloff(campfireDistance));
            }

            return Mathf.Clamp01(targetWarmth);
        }

        private float CalculateFalloff(float squaredDistance)
        {
            var distance = Mathf.Sqrt(squaredDistance);
            return 1f - Mathf.Clamp01(distance / _radius);
        }

        private void ApplyVisuals()
        {
            if (_warmthLight == null)
            {
                return;
            }

            _warmthLight.intensity = _lightIntensityAtFullWarmth * _warmth;
            _warmthLight.range = Mathf.Lerp(0f, _lightRangeAtFullWarmth, _warmth);
        }

        private void OnValidate()
        {
            _radius = Mathf.Max(0.1f, _radius);
            _transitionSpeed = Mathf.Max(0.01f, _transitionSpeed);
            _lightIntensityAtFullWarmth = Mathf.Max(0f, _lightIntensityAtFullWarmth);
            _lightRangeAtFullWarmth = Mathf.Max(0f, _lightRangeAtFullWarmth);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.Lerp(new Color(0.25f, 0.4f, 1f, 0.35f), new Color(1f, 0.55f, 0.1f, 0.55f), _warmth);
            Gizmos.DrawWireSphere(transform.position, _radius);
        }
    }
}
