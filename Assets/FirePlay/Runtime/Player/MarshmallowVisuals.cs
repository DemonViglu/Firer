using DemonViglu.FirePlay.Activity;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Presentation for the marshmallow activity. The prop reads the typed
    /// ActivityLogic state through PlayerActivityHost; it never consumes fuel,
    /// handles actions or depends on RestInteraction.
    /// </summary>
    public sealed class MarshmallowVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerActivityHost _activityHost;
        [SerializeField] private PlayerAnimationController _animationController;
        [Tooltip("棉花糖道具的实际 Transform。通常绑定 Player/Hand/Marshmallow。")]
        [SerializeField] private Transform _marshmallowProp;
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
        private MarshmallowActivityLogic _logic;
        private bool _wasMaterialized;
        private bool _wasReadyToEat;
        private int _completedTurns;
        private bool _hasResult;
        private bool _loggedMissingHost;

        private void Awake()
        {
            ResolveReferences();
            _marshmallowProp ??= transform;
            if (_renderers == null || _renderers.Length == 0)
                _renderers = _marshmallowProp.GetComponentsInChildren<Renderer>(true);

            _propertyBlock = new MaterialPropertyBlock();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_renderers.Length == 0)
                Debug.LogWarning("[MarshmallowVisuals] 棉花糖 Transform 下没有 Renderer。", this);
#endif
        }

        private void OnEnable()
        {
            ResetPresentation();
        }

        private void Update()
        {
            ResolveReferences();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_activityHost == null && !_loggedMissingHost)
            {
                Debug.LogWarning("[MarshmallowVisuals] 未找到 PlayerActivityHost，无法读取活动状态。", this);
                _loggedMissingHost = true;
            }
#endif
            // A missing host during prefab/bootstrap timing is not the same
            // as an ended activity. Do not hide the prop merely because the
            // reference is still being resolved this frame.
            if (_activityHost == null)
                return;

            var session = _activityHost.ActiveSession;
            if (session == null)
            {
                if (_wasMaterialized)
                {
                    _animationController?.Play(_wasReadyToEat
                        ? PlayerAnimationCueIds.MarshmallowEat
                        : PlayerAnimationCueIds.MarshmallowCancel);
                    PlayOneShot(_wasReadyToEat ? _eatClip : _cancelClip);
                }

                if (_logic != null || _wasMaterialized)
                    ResetPresentation();
                return;
            }

            var nextLogic = session.Definition.ActivityId == MarshmallowActivityLogic.ActivityId
                ? session.Logic as MarshmallowActivityLogic
                : null;

            if (nextLogic == null)
            {
                if (_logic != null || _wasMaterialized)
                    ResetPresentation();
                return;
            }

            _logic = nextLogic;
            if (!_wasMaterialized && _logic.HasMaterialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[MarshmallowVisuals] 显示棉花糖：prop={_marshmallowProp.name}，renderers={_renderers.Length}", this);
#endif
                _animationController?.Play(PlayerAnimationCueIds.MarshmallowMaterialize);
                PlayOneShot(_materializeClip);
            }

            if (_logic.CompletedTurns > _completedTurns)
            {
                _animationController?.Play(PlayerAnimationCueIds.MarshmallowTurn);
                _marshmallowProp.Rotate(0f, 0f, 90f, Space.Self);
            }

            if (!_hasResult && _logic.CompletedResult.HasValue)
            {
                var result = _logic.CompletedResult.Value;
                var resultColor = result.Quality switch
                {
                    MarshmallowRoastQuality.Perfect => _perfectColor,
                    MarshmallowRoastQuality.Toasted => _toastedColor,
                    _ => _scorchedColor
                };
                ApplyColor(resultColor);
                PlayOneShot(result.Quality switch
                {
                    MarshmallowRoastQuality.Perfect => _perfectClip,
                    MarshmallowRoastQuality.Toasted => _toastedClip,
                    _ => _scorchedClip
                });
                _hasResult = true;
            }

            if (!_logic.HasMaterialized)
                ApplyColor(_rawColor);
            else if (!_hasResult)
                ApplyColor(_roastingColor);

            SetVisible(_logic.HasMaterialized);
            _wasMaterialized = _logic.HasMaterialized;
            _wasReadyToEat = _logic.IsReadyToEat;
            _completedTurns = _logic.CompletedTurns;
        }

        private void ResolveReferences()
        {
            _activityHost ??= GetComponentInParent<PlayerActivityHost>();
            _activityHost ??= PlayerActivityHost.Local;
            _activityHost ??= FindAnyObjectByType<PlayerActivityHost>();
            _animationController ??= GetComponentInParent<PlayerAnimationController>();
            _audioSource ??= GetComponent<AudioSource>();
        }

        private void ResetPresentation()
        {
            _logic = null;
            _wasMaterialized = false;
            _wasReadyToEat = false;
            _completedTurns = 0;
            _hasResult = false;
            ApplyColor(_rawColor);
            SetVisible(false);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private void ApplyColor(Color color)
        {
            if (_propertyBlock == null || _renderers == null) return;
            foreach (var targetRenderer in _renderers)
            {
                if (targetRenderer == null) continue;
                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                _propertyBlock.SetColor("_Color", color);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void SetVisible(bool visible)
        {
            if (_marshmallowProp != null && _marshmallowProp.gameObject.activeSelf != visible)
                _marshmallowProp.gameObject.SetActive(visible);

            if (_renderers == null) return;
            foreach (var targetRenderer in _renderers)
            {
                if (targetRenderer != null)
                    targetRenderer.enabled = visible;
            }
        }
    }
}
