using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    public readonly struct EnvironmentWarmthField
    {
        public const int MaximumSourceCount = 8;

        private readonly Vector4[] _sources;
        private readonly float[] _strengths;

        public EnvironmentWarmthField(Vector4[] sources, float[] strengths, int count)
        {
            _sources = sources;
            _strengths = strengths;
            Count = Mathf.Clamp(count, 0, MaximumSourceCount);
        }

        public int Count { get; }
        public Vector4[] ShaderSources => _sources;
        public float[] ShaderStrengths => _strengths;

        public float Sample(Vector3 worldPosition)
        {
            var warmth = 0f;
            for (var index = 0; index < Count; index++)
            {
                var source = _sources[index];
                var radius = Mathf.Max(0.01f, source.w);
                var distance = Vector3.Distance(worldPosition, new Vector3(source.x, source.y, source.z));
                var falloff = 1f - Mathf.Clamp01(distance / radius);
                warmth = Mathf.Max(warmth, falloff * falloff * (3f - 2f * falloff) * _strengths[index]);
            }

            return Mathf.Clamp01(warmth);
        }

        public void GetSource(int index, out Vector3 position, out float radius, out float strength)
        {
            if (index < 0 || index >= Count)
            {
                position = default;
                radius = 0f;
                strength = 0f;
                return;
            }

            var source = _sources[index];
            position = new Vector3(source.x, source.y, source.z);
            radius = source.w;
            strength = _strengths[index];
        }
    }

    public interface IEnvironmentWarmthReceiver
    {
        void ApplyEnvironmentWarmth(EnvironmentWarmthField field, float deltaTime);
    }
}
