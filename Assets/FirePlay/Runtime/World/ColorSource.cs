using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 玩家可采集颜色的世界对象，例如花、水洼或落叶。
    /// 需要一个非 Trigger Collider 定义自身可互动范围。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ColorSource : MonoBehaviour
    {
        [SerializeField] private Color _providedColor = new(0.96f, 0.65f, 0.78f, 1f);
        [SerializeField, Min(0f)] private float _cooldownSeconds = 1.5f;
        [SerializeField] private ParticleSystem _absorbVfx;
        [SerializeField] private AudioSource _absorbAudio;

        private float _nextAvailableTime;

        private void Reset()
        {
            var collider = GetComponent<Collider>();
            collider.isTrigger = false;
        }

        /// <summary>
        /// 由玩家交互组件调用。颜色仍存入玩家携带的火苗状态。
        /// </summary>
        public bool TryAbsorb(FlameBrush flame)
        {
            if (Time.time < _nextAvailableTime)
            {
                return false;
            }

            if (flame == null)
            {
                return false;
            }

            _nextAvailableTime = Time.time + _cooldownSeconds;
            flame.AbsorbColor(_providedColor);
            PlayFeedback();
            return true;
        }

        private void PlayFeedback()
        {
            if (_absorbVfx != null)
            {
                var main = _absorbVfx.main;
                main.startColor = _providedColor;
                _absorbVfx.Play(true);
            }

            if (_absorbAudio != null)
            {
                _absorbAudio.Play();
            }
        }

        private void OnDrawGizmosSelected()
        {
            var collider = GetComponent<Collider>();
            if (collider == null)
            {
                return;
            }

            Gizmos.color = new Color(_providedColor.r, _providedColor.g, _providedColor.b, 0.3f);
            Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
        }
    }
}
