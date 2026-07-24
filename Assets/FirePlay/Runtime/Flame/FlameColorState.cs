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

        public Color CurrentColor => _currentColor;
        public float Intensity => _intensity;

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
    }
}
