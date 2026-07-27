using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 可交互的自然火源。玩家在交互范围内按 E 接火，恢复一次余火。
    /// 视觉、音频均为可选挂点，玩法状态不依赖它们。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(StableSceneId))]
    public sealed class FlameSource : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _restoreAmount = 20f;
        [SerializeField, Min(0f)] private float _cooldownSeconds = 4f;
        [SerializeField] private Color _warmthColor = new(1f, 0.72f, 0.35f, 1f);
        [SerializeField] private ParticleSystem _restoreVfx;
        [SerializeField] private AudioSource _restoreAudio;

        private float _nextAvailableTime;

        public bool IsAvailable => Time.time >= _nextAvailableTime;
        public float RemainingCooldownSeconds => Mathf.Max(0f, _nextAvailableTime - Time.time);
        public string SourceId => GetComponent<StableSceneId>().Value;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = false;
        }

        public bool TryRestore(FlameResourceController resourceController)
        {
            if (resourceController == null || !IsAvailable || !resourceController.Restore(_restoreAmount))
            {
                return false;
            }

            _nextAvailableTime = Time.time + _cooldownSeconds;
            PlayFeedback();
            return true;
        }

        private void PlayFeedback()
        {
            if (_restoreVfx != null)
            {
                var main = _restoreVfx.main;
                main.startColor = _warmthColor;
                _restoreVfx.Play(true);
            }

            if (_restoreAudio != null)
            {
                _restoreAudio.Play();
            }
        }
    }
}
