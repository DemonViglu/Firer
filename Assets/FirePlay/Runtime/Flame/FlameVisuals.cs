using DemonViglu.FirePlay.Core;
using UnityEngine;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// 将 FlameColorState 转换为火苗 Renderer 与 Point Light 的视觉参数。
    /// 使用 MaterialPropertyBlock，避免为每个火苗创建独立材质。
    /// </summary>
    [RequireComponent(typeof(FlameBrush))]
    public sealed class FlameVisuals : MonoBehaviour
    {
        [SerializeField] private Renderer _flameRenderer;
        [SerializeField] private Light _flameLight;

        private FlameBrush _brush;
        private MaterialPropertyBlock _propertyBlock;

        private void Awake()
        {
            _brush = GetComponent<FlameBrush>();
            _propertyBlock = new MaterialPropertyBlock();

            if (_flameRenderer == null)
            {
                _flameRenderer = GetComponentInChildren<Renderer>();
            }
        }

        private void LateUpdate()
        {
            var state = _brush.State;

            if (_flameRenderer != null)
            {
                _flameRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(FirePlayShaderPropertyIds.FlameColor, state.CurrentColor);
                _propertyBlock.SetFloat(FirePlayShaderPropertyIds.FlameIntensity, state.Intensity);
                _flameRenderer.SetPropertyBlock(_propertyBlock);
            }

            if (_flameLight != null)
            {
                _flameLight.color = state.CurrentColor;
                _flameLight.range = _brush.LightRange;
                _flameLight.intensity = _brush.LightIntensity * state.Intensity;
            }
        }
    }
}
