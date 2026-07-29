using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 只读取烤棉花状态与结果，使用 MaterialPropertyBlock 更新道具颜色。
    /// 不修改余火、火候、结果或停留状态。
    /// </summary>
    public sealed class MarshmallowVisuals : MonoBehaviour
    {
        [SerializeField] private MarshmallowInteraction _interaction;
        [SerializeField] private Renderer[] _renderers;
        [Header("Colors")]
        [SerializeField] private Color _rawColor = new(1f, 0.93f, 0.78f, 1f);
        [SerializeField] private Color _roastingColor = new(1f, 0.72f, 0.3f, 1f);
        [SerializeField] private Color _perfectColor = new(1f, 0.48f, 0.08f, 1f);
        [SerializeField] private Color _toastedColor = new(0.62f, 0.22f, 0.035f, 1f);
        [SerializeField] private Color _scorchedColor = new(0.12f, 0.035f, 0.012f, 1f);
        [Header("Optional Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _materializeClip;
        [SerializeField] private AudioClip _perfectClip;
        [SerializeField] private AudioClip _toastedClip;
        [SerializeField] private AudioClip _scorchedClip;
        [SerializeField] private AudioClip _eatClip;
        [SerializeField] private AudioClip _cancelClip;

        private MaterialPropertyBlock _propertyBlock;
        private bool _hasResult;
        private Color _resultColor;

        private void Awake()
        {
            _interaction ??= GetComponentInParent<MarshmallowInteraction>();
            _audioSource ??= GetComponent<AudioSource>();
            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            Subscribe();
            _hasResult = false;
            ApplyColor(_rawColor);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (_interaction == null)
            {
                return;
            }

            if (!_interaction.HasMaterializedMarshmallow)
            {
                _hasResult = false;
                ApplyColor(_rawColor);
                return;
            }

            if (!_hasResult)
            {
                ApplyColor(_roastingColor);
            }
        }

        private void Subscribe()
        {
            if (_interaction == null)
            {
                _interaction = GetComponentInParent<MarshmallowInteraction>();
            }

            if (_interaction == null)
            {
                return;
            }

            _interaction.Materialized += HandleMaterialized;
            _interaction.ResultReady += HandleResultReady;
            _interaction.ResultCollected += HandleResultCollected;
            _interaction.Cancelled += HandleCancelled;
        }

        private void Unsubscribe()
        {
            if (_interaction == null)
            {
                return;
            }

            _interaction.Materialized -= HandleMaterialized;
            _interaction.ResultReady -= HandleResultReady;
            _interaction.ResultCollected -= HandleResultCollected;
            _interaction.Cancelled -= HandleCancelled;
        }

        private void HandleMaterialized()
        {
            _hasResult = false;
            ApplyColor(_roastingColor);
            PlayOneShot(_materializeClip);
        }

        private void HandleResultReady(MarshmallowResult result)
        {
            _hasResult = true;
            _resultColor = result.Quality switch
            {
                MarshmallowRoastQuality.Perfect => _perfectColor,
                MarshmallowRoastQuality.Toasted => _toastedColor,
                _ => _scorchedColor
            };
            ApplyColor(_resultColor);
            PlayOneShot(result.Quality switch
            {
                MarshmallowRoastQuality.Perfect => _perfectClip,
                MarshmallowRoastQuality.Toasted => _toastedClip,
                _ => _scorchedClip
            });
        }

        private void HandleResultCollected(MarshmallowResult result)
        {
            _hasResult = false;
            PlayOneShot(_eatClip);
        }

        private void HandleCancelled()
        {
            _hasResult = false;
            ApplyColor(_rawColor);
            PlayOneShot(_cancelClip);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        private void ApplyColor(Color color)
        {
            if (_propertyBlock == null || _renderers == null)
            {
                return;
            }

            foreach (var targetRenderer in _renderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                _propertyBlock.SetColor("_Color", color);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
