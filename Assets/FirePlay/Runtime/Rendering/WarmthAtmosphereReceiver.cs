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

        public override void ApplyEnvironmentWarmth(EnvironmentWarmthField field, float deltaTime)
        {
            if (_sourceRoots == null)
            {
                return;
            }

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

                if (index < _lights.Length && _lights[index] != null)
                {
                    _lights[index].intensity = _maxLightIntensity * warmth;
                    _lights[index].range = _maxLightRange * Mathf.Sqrt(warmth);
                }

                if (index < _thawMists.Length && _thawMists[index] != null)
                {
                    var mist = _thawMists[index];
                    var emission = mist.emission;
                    emission.rateOverTime = _maxEmissionRate * warmth;
                    if (active && warmth > 0.02f && !mist.isPlaying)
                    {
                        mist.Play(true);
                    }
                    else if ((!active || warmth <= 0.02f) && mist.isPlaying)
                    {
                        mist.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }

                if (index < _audioSources.Length && _audioSources[index] != null)
                {
                    var audioSource = _audioSources[index];
                    audioSource.volume = _maxAudioVolume * warmth;
                    if (active && warmth > 0.02f && !audioSource.isPlaying)
                    {
                        audioSource.Play();
                    }
                    else if (!active && audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }
                }
            }
        }
    }
}
