using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// 火苗的纯运行时状态，不持有场景对象或视觉引用。
    /// </summary>
    [Serializable]
    public sealed class FlameColorState
    {
        [SerializeField] private Color _currentColor;
        [SerializeField, Min(0f)] private float _intensity;
        [SerializeField, Range(0f, 1f)] private float _resourceLevel = 1f;

        public Color CurrentColor => _currentColor;
        public float Intensity => _intensity;
        public float ResourceLevel => _resourceLevel;

        public void Initialize(Color color, float intensity)
        {
            _currentColor = color;
            _intensity = Mathf.Max(0f, intensity);
        }

        public void SetColor(Color color)
        {
            _currentColor = color;
        }

        public void SetIntensity(float intensity)
        {
            _intensity = Mathf.Max(0f, intensity);
        }

        public void SetResourceLevel(float resourceLevel)
        {
            _resourceLevel = Mathf.Clamp01(resourceLevel);
        }
    }
}
