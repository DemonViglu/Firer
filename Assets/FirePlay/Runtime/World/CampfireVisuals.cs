using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 只读取 Campfire 等级驱动占位视觉；不写入篝火状态或存档。
    /// </summary>
    [RequireComponent(typeof(Campfire))]
    public sealed class CampfireVisuals : MonoBehaviour
    {
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Light _fireLight;
        [SerializeField] private ParticleSystem _fireVfx;
        [Tooltip("Optional roots for stages 0..MaximumLevel. The active stage root follows Campfire.Level.")]
        [SerializeField] private GameObject[] _stageVisualRoots;
        [SerializeField, Min(0.01f)] private float _scalePerLevel = 0.2f;
        [SerializeField, Min(0f)] private float _lightIntensityPerLevel = 0.5f;

        private Campfire _campfire;
        private Vector3 _baseScale;
        private float _baseLightIntensity;
        private float _baseEmissionRate;

        private void Awake()
        {
            _campfire = GetComponent<Campfire>();
            if (_visualRoot == null)
            {
                _visualRoot = transform;
            }

            if (_visualRoot == null)
            {
                Debug.LogError("[CampfireVisuals] 缺少 Visual Root，组件已禁用。", this);
                enabled = false;
                return;
            }

            _baseScale = _visualRoot.localScale;
            if (_fireLight != null)
            {
                _baseLightIntensity = _fireLight.intensity;
            }
            if (_fireVfx != null)
            {
                _baseEmissionRate = _fireVfx.emission.rateOverTimeMultiplier;
            }
        }

        private void Update()
        {
            var level = _campfire.Level;
            var warmth = _campfire.NormalizedWarmth;
            UpdateStageVisual(level);
            _visualRoot.localScale = _baseScale * (1f + level * _scalePerLevel);
            if (_fireLight != null)
            {
                _fireLight.intensity = (_baseLightIntensity + level * _lightIntensityPerLevel) * warmth;
            }

            if (_fireVfx != null)
            {
                var emission = _fireVfx.emission;
                emission.rateOverTimeMultiplier = (_baseEmissionRate + level * 5f) * warmth;
            }
        }

        private void UpdateStageVisual(int level)
        {
            if (_stageVisualRoots == null || _stageVisualRoots.Length == 0)
            {
                return;
            }

            var activeIndex = Mathf.Clamp(level, 0, _stageVisualRoots.Length - 1);
            for (var index = 0; index < _stageVisualRoots.Length; index++)
            {
                var stageRoot = _stageVisualRoots[index];
                if (stageRoot != null && stageRoot.activeSelf != (index == activeIndex))
                {
                    stageRoot.SetActive(index == activeIndex);
                }
            }
        }
    }
}
