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
        [SerializeField, Min(0.01f)] private float _scalePerLevel = 0.2f;
        [SerializeField, Min(0f)] private float _lightIntensityPerLevel = 0.5f;

        private Campfire _campfire;
        private Vector3 _baseScale;
        private float _baseLightIntensity;

        private void Awake()
        {
            _campfire = GetComponent<Campfire>();
            _visualRoot ??= transform;
            _baseScale = _visualRoot.localScale;
            if (_fireLight != null)
            {
                _baseLightIntensity = _fireLight.intensity;
            }
        }

        private void Update()
        {
            var level = _campfire.Level;
            _visualRoot.localScale = _baseScale * (1f + level * _scalePerLevel);
            if (_fireLight != null)
            {
                _fireLight.intensity = _baseLightIntensity + level * _lightIntensityPerLevel;
            }

            if (_fireVfx != null)
            {
                var emission = _fireVfx.emission;
                emission.rateOverTimeMultiplier = Mathf.Max(1f, 4f + level * 5f);
            }
        }
    }
}
