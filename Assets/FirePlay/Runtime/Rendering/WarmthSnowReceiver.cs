using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    [DisallowMultipleComponent]
    public sealed class WarmthSnowReceiver : EnvironmentWarmthReceiverBehaviour
    {
        [SerializeField] private Renderer[] _targetRenderers;
        [SerializeField, Range(0f, 1f)] private float _warmthScale = 0.62f;

        private static readonly int SourceCountId = Shader.PropertyToID("_FirePlayWarmthSourceCount");
        private static readonly int SourcesId = Shader.PropertyToID("_FirePlayWarmthSources");
        private static readonly int StrengthsId = Shader.PropertyToID("_FirePlayWarmthStrengths");
        private MaterialPropertyBlock _properties;
        private float[] _scaledStrengths;

        private void Awake()
        {
            _properties = new MaterialPropertyBlock();
        }

        public override void ApplyEnvironmentWarmth(EnvironmentWarmthField field, float deltaTime)
        {
            _properties ??= new MaterialPropertyBlock();
            if (_targetRenderers == null)
            {
                return;
            }

            _scaledStrengths ??= new float[EnvironmentWarmthField.MaximumSourceCount];
            for (var index = 0; index < _scaledStrengths.Length; index++)
            {
                _scaledStrengths[index] = field.ShaderStrengths[index] * _warmthScale;
            }

            foreach (var targetRenderer in _targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(_properties);
                _properties.SetInt(SourceCountId, field.Count);
                _properties.SetVectorArray(SourcesId, field.ShaderSources);
                _properties.SetFloatArray(StrengthsId, _scaledStrengths);
                targetRenderer.SetPropertyBlock(_properties);
            }
        }
    }
}
