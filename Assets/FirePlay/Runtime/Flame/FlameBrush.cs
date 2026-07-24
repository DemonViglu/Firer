using DemonViglu.FirePlay.Data;
using UnityEngine;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// 火苗的空间行为：作为独立世界对象，平滑跟随一个由玩家提供的 Anchor。
    /// 颜色和亮度由 FlameColorState 保存，视觉表现由 FlameVisuals 读取。
    /// </summary>
    public sealed class FlameBrush : MonoBehaviour
    {
        [SerializeField] private FlameBrushConfig _config;
        [SerializeField] private Transform _followTarget;
        [SerializeField] private Vector3 _localOffset = new(0.55f, 0f, 0.35f);
        [SerializeField, Min(0f)] private float _hoverAmplitude = 0.08f;
        [SerializeField, Min(0f)] private float _hoverFrequency = 1.4f;

        private readonly FlameColorState _state = new();
        private Vector3 _followVelocity;

        public FlameColorState State => _state;
        public float InteractionRadius => _config.InteractionRadius;
        public float LightRange => _config.LightRange;
        public float LightIntensity => _config.LightIntensity;

        public void SetFollowAnchor(Transform anchor)
        {
            _followTarget = anchor;
        }

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError("[FlameBrush] 未指定 FlameBrushConfig。", this);
                enabled = false;
                return;
            }

            if (_followTarget == null)
            {
                Debug.LogError("[FlameBrush] 未指定 Follow Target。", this);
                enabled = false;
                return;
            }

            _state.Initialize(_config.InitialColor, _config.InitialIntensity);
        }

        private void Update()
        {
            var worldOffset = _followTarget.TransformDirection(_localOffset);
            var hover = Mathf.Sin(Time.time * _hoverFrequency) * _hoverAmplitude;
            var targetPosition = _followTarget.position + worldOffset + Vector3.up * (_config.FollowHeight + hover);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref _followVelocity,
                _config.FollowSmoothTime);
        }

        /// <summary>
        /// 由色彩源等世界对象调用。视觉组件会在同一帧末尾读取更新后的状态。
        /// </summary>
        public void AbsorbColor(Color color)
        {
            _state.SetColor(color);
        }
    }
}
