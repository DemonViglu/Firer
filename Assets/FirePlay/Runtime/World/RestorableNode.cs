using DemonViglu.FirePlay.Data;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Rendering;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 可由特定火焰颜色复苏的世界节点，例如古树、桥面或石碑。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class RestorableNode : MonoBehaviour
    {
        private enum RestoreState
        {
            Dormant,
            Restoring,
            Restored
        }

        [SerializeField] private string _nodeId = "forest.node.unnamed";
        [SerializeField] private RestorableNodeConfig _config;
        [SerializeField] private WorldBloom _worldBloom;
        [SerializeField] private ParticleSystem _completionVfx;
        [SerializeField] private AudioSource _completionAudio;

        private RestoreState _state;

        public string NodeId => _nodeId;
        public bool IsRestored => _state == RestoreState.Restored;

        private void Awake()
        {
            if (_worldBloom == null)
            {
                _worldBloom = GetComponent<WorldBloom>();
            }

            if (_config == null || _worldBloom == null)
            {
                Debug.LogError("[RestorableNode] 缺少 Config 或 WorldBloom。", this);
                enabled = false;
            }
        }

        public bool TryRestore(FlameBrush flame)
        {
            if (!enabled || flame == null || _state != RestoreState.Dormant)
            {
                return false;
            }

            if (GetColorMatch(flame.State.CurrentColor, _config.RequiredColor) < _config.MinimumColorMatch)
            {
                return false;
            }

            if (!_worldBloom.BeginRestore(_config, CompleteRestore))
            {
                return false;
            }

            _state = RestoreState.Restoring;
            return true;
        }

        private void CompleteRestore()
        {
            _state = RestoreState.Restored;

            if (_completionVfx != null)
            {
                _completionVfx.Play(true);
            }

            if (_completionAudio != null)
            {
                _completionAudio.Play();
            }
        }

        private static float GetColorMatch(Color source, Color required)
        {
            Color.RGBToHSV(source, out var sourceHue, out var sourceSaturation, out _);
            Color.RGBToHSV(required, out var requiredHue, out var requiredSaturation, out _);

            // 灰阶颜色没有可靠色相，避免它们意外点亮需要彩色火苗的节点。
            if (sourceSaturation < 0.05f || requiredSaturation < 0.05f)
            {
                return 0f;
            }

            var hueDistance = Mathf.Abs(sourceHue - requiredHue);
            hueDistance = Mathf.Min(hueDistance, 1f - hueDistance);
            var hueMatch = 1f - hueDistance / 0.5f;
            var saturationMatch = 1f - Mathf.Abs(sourceSaturation - requiredSaturation);

            return Mathf.Clamp01(hueMatch * 0.85f + saturationMatch * 0.15f);
        }
    }
}
