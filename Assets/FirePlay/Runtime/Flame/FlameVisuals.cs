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
        [SerializeField] private Transform _visualTransform;

        [Header("余火表现范围")]
        [SerializeField, Range(0.1f, 1f)] private float _receiverVisualScale = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float _receiverLightRangeMultiplier = 0.4f;
        [SerializeField, Range(0.01f, 1f)] private float _receiverLightIntensityMultiplier = 0.2f;

        private FlameBrush _brush;
        private MaterialPropertyBlock _propertyBlock;
        private Vector3 _fullVisualScale = Vector3.one;

        private void Awake()
        {
            _brush = GetComponent<FlameBrush>();
            _propertyBlock = new MaterialPropertyBlock();

            if (_flameRenderer == null)
            {
                _flameRenderer = GetComponentInChildren<Renderer>();
            }

            if (_visualTransform == null && _flameRenderer != null)
            {
                _visualTransform = _flameRenderer.transform;
            }

            if (_visualTransform != null)
            {
                _fullVisualScale = _visualTransform.localScale;
            }
        }

        private void LateUpdate()
        {
            var state = _brush.State;
            var resourceLevel = state.ResourceLevel;

            if (_flameRenderer != null)
            {
                _flameRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(FirePlayShaderPropertyIds.FlameColor, state.CurrentColor);
                _propertyBlock.SetFloat(FirePlayShaderPropertyIds.FlameIntensity, state.Intensity);
                _flameRenderer.SetPropertyBlock(_propertyBlock);
            }

            if (_visualTransform != null)
            {
                var scale = Mathf.Lerp(_receiverVisualScale, 1f, resourceLevel);
                _visualTransform.localScale = _fullVisualScale * scale;
            }

            if (_flameLight != null)
            {
                _flameLight.color = state.CurrentColor;
                _flameLight.range = _brush.LightRange * Mathf.Lerp(
                    _receiverLightRangeMultiplier,
                    1f,
                    resourceLevel);
                _flameLight.intensity = _brush.LightIntensity * Mathf.Lerp(
                    _receiverLightIntensityMultiplier,
                    1f,
                    resourceLevel);
            }
        }

        private void OnValidate()
        {
            _receiverVisualScale = Mathf.Clamp(_receiverVisualScale, 0.1f, 1f);
            _receiverLightRangeMultiplier = Mathf.Clamp(_receiverLightRangeMultiplier, 0.1f, 1f);
            _receiverLightIntensityMultiplier = Mathf.Clamp(_receiverLightIntensityMultiplier, 0.01f, 1f);
        }
    }
}
