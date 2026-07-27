using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 临时小火种生命周期。表现组件均为可选，状态不依赖特定素材。
    /// </summary>
    public sealed class SmallFire : MonoBehaviour
    {
        [SerializeField] private Light _fireLight;
        [SerializeField] private ParticleSystem _fireVfx;

        private float _remainingSeconds;
        private float _initialLightIntensity;

        public void Initialize(float durationSeconds)
        {
            _remainingSeconds = Mathf.Max(0.01f, durationSeconds);
            if (_fireLight != null)
            {
                _initialLightIntensity = _fireLight.intensity;
            }

            if(_fireVfx != null)
            {
                _fireVfx?.Play(true);
            }
        }

        private void Update()
        {
            if (_remainingSeconds <= 0f)
            {
                return;
            }

            _remainingSeconds -= Time.deltaTime;
            if (_fireLight != null)
            {
                _fireLight.intensity = _initialLightIntensity * Mathf.Clamp01(_remainingSeconds / 3f);
            }

            if (_remainingSeconds <= 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
