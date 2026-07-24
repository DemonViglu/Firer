using UnityEngine;

namespace DemonViglu.FirePlay.Data
{
    /// <summary>
    /// 火苗的可调设计与视觉参数。
    /// 一个配置资产可被多个火苗预制体复用。
    /// </summary>
    [CreateAssetMenu(menuName = "DemonViglu/FirePlay/Flame Brush Config", fileName = "FlameBrushConfig_")]
    public sealed class FlameBrushConfig : ScriptableObject
    {
        [Header("状态")]
        [SerializeField] private Color _initialColor = new(1f, 0.82f, 0.48f, 1f);
        [SerializeField, Min(0f)] private float _initialIntensity = 1f;

        [Header("跟随")]
        [SerializeField, Min(0f)] private float _followHeight = 1.35f;
        [SerializeField, Min(0.01f)] private float _followSmoothTime = 0.18f;

        [Header("互动")]
        [SerializeField, Min(0.1f)] private float _interactionRadius = 1.25f;
        [SerializeField] private LayerMask _interactionLayers = ~0;

        [Header("光照")]
        [SerializeField, Min(0f)] private float _lightRange = 3f;
        [SerializeField, Min(0f)] private float _lightIntensity = 1.4f;

        public Color InitialColor => _initialColor;
        public float InitialIntensity => _initialIntensity;
        public float FollowHeight => _followHeight;
        public float FollowSmoothTime => _followSmoothTime;
        public float InteractionRadius => _interactionRadius;
        public LayerMask InteractionLayers => _interactionLayers;
        public float LightRange => _lightRange;
        public float LightIntensity => _lightIntensity;
    }
}
