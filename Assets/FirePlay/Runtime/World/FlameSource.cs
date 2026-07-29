using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 可交互的路线燃料。玩家在交互范围内按 E 收集一次余火；
    /// 本局不会刷新，重进 Play Mode 后才会重新出现。
    /// 视觉、音频均为可选挂点，玩法状态不依赖它们。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(StableSceneId))]
    public sealed class FlameSource : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float _restoreAmount = 20f;
        [SerializeField] private Color _warmthColor = new(1f, 0.72f, 0.35f, 1f);
        [SerializeField] private ParticleSystem _restoreVfx;
        [SerializeField] private AudioSource _restoreAudio;
        [SerializeField] private Renderer[] _renderersToHide;

        private Collider _interactionCollider;
        private bool _collected;

        public bool IsAvailable => !_collected;
        public string SourceId => GetComponent<StableSceneId>().Value;

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = false;
        }

        private void Awake()
        {
            _interactionCollider = GetComponent<Collider>();
            if (_renderersToHide == null || _renderersToHide.Length == 0)
            {
                var allRenderers = GetComponentsInChildren<Renderer>(true);
                var collectibleRenderers = new System.Collections.Generic.List<Renderer>(allRenderers.Length);
                foreach (var sourceRenderer in allRenderers)
                {
                    if (sourceRenderer is not ParticleSystemRenderer)
                    {
                        collectibleRenderers.Add(sourceRenderer);
                    }
                }
                _renderersToHide = collectibleRenderers.ToArray();
            }
        }

        public bool TryRestore(FlameResourceController resourceController)
        {
            if (resourceController == null || !IsAvailable || !resourceController.Restore(_restoreAmount))
            {
                return false;
            }

            _collected = true;
            PlayFeedback();
            HideCollectedSource();
            return true;
        }

        private void HideCollectedSource()
        {
            if (_interactionCollider != null)
            {
                _interactionCollider.enabled = false;
            }

            foreach (var sourceRenderer in _renderersToHide)
            {
                if (sourceRenderer != null)
                {
                    sourceRenderer.enabled = false;
                }
            }
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
