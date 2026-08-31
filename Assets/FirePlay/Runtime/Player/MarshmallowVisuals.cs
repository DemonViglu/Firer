using DemonViglu.FirePlay.Activity;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Presentation for the marshmallow activity. Host/single-player reads the
    /// typed Logic directly; a network Client reads the same activity-owned
    /// snapshot mirrored by PlayerActivityHost.
    /// </summary>
    public sealed class MarshmallowVisuals : MonoBehaviour, IActivityVfxRequestExecutor
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
        [Header("Optional VFX")]
        [Tooltip("棉花糖拟造、翻面、完成和收到赠礼共用的一次性表现。玩法状态不保存在粒子里。")]
        [SerializeField] private ParticleSystem _feedbackVfx;

        private MaterialPropertyBlock _propertyBlock;
        private Quaternion _authoredLocalRotation;
        private bool _hasActivityState;
        private bool _wasMaterialized;
        private bool _wasReadyToEat;
        private int _completedTurns;
        private bool _hasResult;
        private bool _loggedMissingHost;

        private void Awake()
        {
            ResolveReferences();
            _marshmallowProp ??= transform;
            _authoredLocalRotation = _marshmallowProp.localRotation;
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

            if (!TryReadState(out var state))
            {
                if (_wasMaterialized)
                {
                    _animationController?.Play(_wasReadyToEat
                        ? PlayerAnimationCueIds.MarshmallowEat
                        : PlayerAnimationCueIds.MarshmallowCancel);
                    PlayOneShot(_wasReadyToEat ? _eatClip : _cancelClip);
                }

                if (_hasActivityState || _wasMaterialized)
                    ResetPresentation();
                return;
            }

            var firstState = !_hasActivityState;
            _hasActivityState = true;
            if (!firstState && !_wasMaterialized && state.HasMaterialized)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[MarshmallowVisuals] 显示棉花糖：prop={_marshmallowProp.name}，renderers={_renderers.Length}", this);
#endif
                _animationController?.Play(PlayerAnimationCueIds.MarshmallowMaterialize);
                PlayOneShot(_materializeClip);
                PlayFeedback(_rawColor);
            }

            if (state.CompletedTurns != _completedTurns)
            {
                if (!firstState && state.CompletedTurns > _completedTurns)
                {
                    _animationController?.Play(PlayerAnimationCueIds.MarshmallowTurn);
                    PlayFeedback(_roastingColor);
                }

                // Rotation is persistent state. Apply its absolute pose so a
                // late join can restore it without replaying the turn cue.
                _marshmallowProp.localRotation = _authoredLocalRotation
                                                   * Quaternion.Euler(
                                                       0f,
                                                       0f,
                                                       90f * state.CompletedTurns);
            }

            if (!_hasResult && state.HasResult)
            {
                var resultColor = state.Quality switch
                {
                    MarshmallowRoastQuality.Perfect => _perfectColor,
                    MarshmallowRoastQuality.Toasted => _toastedColor,
                    _ => _scorchedColor
                };
                ApplyColor(resultColor);
                if (!firstState)
                {
                    PlayOneShot(state.Quality switch
                    {
                        MarshmallowRoastQuality.Perfect => _perfectClip,
                        MarshmallowRoastQuality.Toasted => _toastedClip,
                        _ => _scorchedClip
                    });
                    PlayFeedback(resultColor);
                }
                _hasResult = true;
            }

            if (!state.HasMaterialized)
                ApplyColor(_rawColor);
            else if (!_hasResult)
                ApplyColor(_roastingColor);

            SetVisible(state.HasMaterialized);
            _wasMaterialized = state.HasMaterialized;
            _wasReadyToEat = state.IsReadyToEat;
            _completedTurns = state.CompletedTurns;
        }

        public bool Execute(ActivityPlayerRequest request)
        {
            if (request.Kind != ActivityPlayerRequestKind.VfxCue
                || request.ActivityId != MarshmallowActivityLogic.ActivityId
                || request.CueId != MarshmallowActivityLogic.ReceiveVfxCueId)
            {
                return false;
            }

            PlayFeedback(_perfectColor);
            return _feedbackVfx != null;
        }

        private bool TryReadState(out MarshmallowActivityStateSnapshot state)
        {
            state = default;
            var session = _activityHost?.ActiveSession;
            if (session?.Definition?.ActivityId != MarshmallowActivityLogic.ActivityId)
                return false;

            if (session.Logic is MarshmallowActivityLogic logic)
            {
                state = new MarshmallowActivityStateSnapshot(
                    logic.HasMaterialized,
                    logic.IsReadyToEat,
                    logic.CompletedTurns,
                    logic.PerfectTurns,
                    logic.TurnsRequired,
                    logic.CompletedResult.HasValue,
                    logic.CompletedResult?.Quality ?? MarshmallowRoastQuality.Scorched,
                    logic.NeedlePosition,
                    logic.TargetCenter,
                    logic.PerfectZoneWidth);
                return true;
            }

            return _activityHost.TryGetActiveStatePayload(
                       MarshmallowActivityLogic.ActivityId,
                       out var payload)
                   && MarshmallowActivityStateSnapshot.TryParse(payload, out state);
        }

        private void ResolveReferences()
        {
            _activityHost ??= GetComponentInParent<PlayerActivityHost>();
            _activityHost ??= PlayerActivityHost.Local;
            _animationController ??= GetComponentInParent<PlayerAnimationController>();
            _audioSource ??= GetComponent<AudioSource>();
        }

        private void ResetPresentation()
        {
            _hasActivityState = false;
            _wasMaterialized = false;
            _wasReadyToEat = false;
            _completedTurns = 0;
            _hasResult = false;
            if (_marshmallowProp != null)
                _marshmallowProp.localRotation = _authoredLocalRotation;
            ApplyColor(_rawColor);
            SetVisible(false);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (_audioSource != null && clip != null)
                _audioSource.PlayOneShot(clip);
        }

        private void PlayFeedback(Color color)
        {
            if (_feedbackVfx == null)
                return;

            var main = _feedbackVfx.main;
            main.startColor = color;
            _feedbackVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _feedbackVfx.Play(true);
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
