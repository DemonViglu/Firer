using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    [DisallowMultipleComponent]
    public sealed class WarmthAtmosphereReceiver : EnvironmentWarmthReceiverBehaviour
    {
        [SerializeField] private Transform[] _sourceRoots;
        [SerializeField] private Light[] _lights;
        [SerializeField] private ParticleSystem[] _thawMists;
        [SerializeField] private AudioSource[] _audioSources;
        [SerializeField, Min(0f)] private float _maxLightIntensity = 2f;
        [SerializeField, Min(0f)] private float _maxLightRange = 14f;
        [SerializeField, Min(0f)] private float _maxEmissionRate = 8f;
        [SerializeField, Range(0f, 1f)] private float _maxAudioVolume = 0.28f;
        [Tooltip("冷热表现追随权威热场的速度。只平滑表现，不修改热量状态。")]
        [SerializeField, Min(0.01f)] private float _responseSpeed = 4f;
        [Tooltip("热场辅助灯只负责暖色补光；关闭其阴影以避免移动端附加灯阴影图集压力。")]
        [SerializeField] private bool _disableRealtimeShadows = true;

        private float[] _currentWarmth;

        public override void ApplyEnvironmentWarmth(EnvironmentWarmthField field, float deltaTime)
        {
            if (_sourceRoots == null)
            {
                return;
            }

            if (_currentWarmth == null || _currentWarmth.Length != _sourceRoots.Length)
                _currentWarmth = new float[_sourceRoots.Length];

            var blend = 1f - Mathf.Exp(
                -Mathf.Max(0.01f, _responseSpeed) * Mathf.Max(0f, deltaTime));

            for (var index = 0; index < _sourceRoots.Length; index++)
            {
                var sourceRoot = _sourceRoots[index];
                if (sourceRoot == null)
                {
                    continue;
                }

                var active = index < field.Count;
                var warmth = 0f;
                if (active)
                {
                    field.GetSource(index, out var sourcePosition, out _, out var sourceStrength);
                    sourceRoot.position = sourcePosition + Vector3.up * 0.12f;
                    warmth = sourceStrength;
                }

                _currentWarmth[index] = Mathf.Lerp(_currentWarmth[index], warmth, blend);
                if (Mathf.Abs(_currentWarmth[index] - warmth) <= 0.001f)
                    _currentWarmth[index] = warmth;
                var presentedWarmth = _currentWarmth[index];

                if (_lights != null && index < _lights.Length && _lights[index] != null)
                {
                    if (_disableRealtimeShadows)
                        _lights[index].shadows = LightShadows.None;
                    _lights[index].intensity = _maxLightIntensity * presentedWarmth;
                    _lights[index].range = _maxLightRange * Mathf.Sqrt(presentedWarmth);
                }

                if (_thawMists != null && index < _thawMists.Length && _thawMists[index] != null)
                {
                    var mist = _thawMists[index];
                    var emission = mist.emission;
                    emission.rateOverTime = _maxEmissionRate * presentedWarmth;
                    if (active && warmth > 0.02f && !mist.isPlaying)
                    {
                        mist.Play(true);
                    }
                    else if ((!active || warmth <= 0.02f)
                             && presentedWarmth <= 0.005f
                             && mist.isPlaying)
                    {
                        mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }

                if (_audioSources != null && index < _audioSources.Length && _audioSources[index] != null)
                {
                    var audioSource = _audioSources[index];
                    audioSource.volume = _maxAudioVolume * presentedWarmth;
                    if (active && warmth > 0.02f && !audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                    else if ((!active || warmth <= 0.02f)
                             && presentedWarmth <= 0.005f
                             && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                }
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_currentWarmth != null)
                System.Array.Clear(_currentWarmth, 0, _currentWarmth.Length);

            if (_lights != null)
            {
                foreach (var targetLight in _lights)
                {
                    if (targetLight == null) continue;
                    targetLight.intensity = 0f;
                    targetLight.range = 0f;
                }
            }

            if (_thawMists != null)
            {
                foreach (var mist in _thawMists)
                {
                    if (mist == null) continue;
                    var emission = mist.emission;
                    emission.rateOverTime = 0f;
                    mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (_audioSources == null) return;
            foreach (var audioSource in _audioSources)
            {
                if (audioSource == null) continue;
                audioSource.volume = 0f;
                audioSource.Stop();
            }
        }

        private void OnValidate()
        {
            _maxLightIntensity = Mathf.Max(0f, _maxLightIntensity);
            _maxLightRange = Mathf.Max(0f, _maxLightRange);
            _maxEmissionRate = Mathf.Max(0f, _maxEmissionRate);
            _maxAudioVolume = Mathf.Clamp01(_maxAudioVolume);
            _responseSpeed = Mathf.Max(0.01f, _responseSpeed);
        }
    }
}
