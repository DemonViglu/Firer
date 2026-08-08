using DemonViglu.FirePlay.Activity;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Optional presentation for a guitar prop. It owns only the prop and
    /// audio feedback; activity rules and Player animation requests remain in
    /// their respective layers. Attach it to the future guitar prop or Player
    /// root when the model is available.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuitarActivityVisuals : MonoBehaviour
    {
        [SerializeField] private PlayerActivityHost _activityHost;
        [SerializeField] private Transform _guitarProp;
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("按 1-21 键对应的音效；可留空，缺失项不会报错。")]
        [SerializeField] private AudioClip[] _keyClips;

        [Header("Procedural Fallback")]
        [Tooltip("没有配置对应 AudioClip 时，使用轻量程序化拨弦音；正式音频资源会自动优先。")]
        [SerializeField] private bool _useProceduralFallback = true;
        [SerializeField, Range(0.01f, 1f)] private float _proceduralVolume = 0.22f;
        [SerializeField, Min(0.1f)] private float _proceduralDuration = 0.65f;
        [SerializeField, Min(55f)] private float _lowestFrequency = 196f;

        private bool _hasActivityState;
        private int _playedKeyCount;
        private bool _loggedMissingHost;
        private readonly AudioClip[] _proceduralClips = new AudioClip[GuitarActivityLogic.KeyCount];

        private void Awake()
        {
            ResolveReferences();
            if (_guitarProp != null && (_renderers == null || _renderers.Length == 0))
                _renderers = _guitarProp.GetComponentsInChildren<Renderer>(true);
            SetVisible(false);
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
                Debug.LogWarning("[GuitarActivityVisuals] 未找到 PlayerActivityHost，无法读取吉他活动状态。", this);
                _loggedMissingHost = true;
            }
#endif
            if (_activityHost == null)
                return;

            if (!TryReadState(out var state))
            {
                if (_hasActivityState)
                    ResetPresentation();
                return;
            }

            var firstState = !_hasActivityState;
            _hasActivityState = true;
            SetVisible(true);

            if (!firstState && state.PlayedKeyCount > _playedKeyCount)
                PlayKeySound(state.LastKeyIndex);

            _playedKeyCount = state.PlayedKeyCount;
        }

        private bool TryReadState(out GuitarActivityStateSnapshot state)
        {
            state = default;
            var session = _activityHost?.ActiveSession;
            if (session?.Definition?.ActivityId == GuitarActivityLogic.ActivityId
                && session.Logic is GuitarActivityLogic logic)
            {
                state = new GuitarActivityStateSnapshot(
                    logic.LastKeyIndex,
                    logic.PlayedKeyCount);
                return true;
            }

            return _activityHost?.ActiveActivityId == GuitarActivityLogic.ActivityId
                   && _activityHost.TryGetActiveStatePayload(
                       GuitarActivityLogic.ActivityId,
                       out var payload)
                   && GuitarActivityStateSnapshot.TryParse(payload, out state);
        }

        private void ResolveReferences()
        {
            _activityHost ??= GetComponentInParent<PlayerActivityHost>();
            _activityHost ??= PlayerActivityHost.Local;
            _audioSource ??= GetComponent<AudioSource>();
        }

        private void ResetPresentation()
        {
            _hasActivityState = false;
            _playedKeyCount = 0;
            SetVisible(false);
        }

        private void PlayKeySound(int keyIndex)
        {
            if (_audioSource == null || keyIndex < 1 || keyIndex > GuitarActivityLogic.KeyCount)
                return;

            var clip = _keyClips != null && keyIndex <= _keyClips.Length
                ? _keyClips[keyIndex - 1]
                : null;
            var volumeScale = 1f;
            if (clip == null && _useProceduralFallback)
            {
                clip = GetOrCreateProceduralClip(keyIndex);
                volumeScale = _proceduralVolume;
            }

            if (clip != null)
                _audioSource.PlayOneShot(clip, volumeScale);
        }

        private AudioClip GetOrCreateProceduralClip(int keyIndex)
        {
            var cached = _proceduralClips[keyIndex - 1];
            if (cached != null)
                return cached;

            const int sampleRate = 44100;
            var duration = Mathf.Max(0.1f, _proceduralDuration);
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var frequency = Mathf.Max(55f, _lowestFrequency)
                            * Mathf.Pow(2f, (keyIndex - 1) / 12f);
            var delayLength = Mathf.Max(2, Mathf.RoundToInt(sampleRate / frequency));
            var delay = new float[delayLength];
            var samples = new float[sampleCount];
            var random = new System.Random(0x47A1 + keyIndex * 7919);

            for (var i = 0; i < delay.Length; i++)
                delay[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            for (var i = 0; i < sampleCount; i++)
            {
                var cursor = i % delayLength;
                var next = (cursor + 1) % delayLength;
                var envelope = 1f - i / (float)sampleCount;
                var value = delay[cursor];
                samples[i] = value * envelope;
                delay[cursor] = (value + delay[next]) * 0.4965f;
            }

            cached = AudioClip.Create(
                $"GuitarTone_{keyIndex:00}",
                sampleCount,
                1,
                sampleRate,
                false);
            cached.SetData(samples, 0);
            _proceduralClips[keyIndex - 1] = cached;
            return cached;
        }

        private void SetVisible(bool visible)
        {
            // Never disable the object that owns this presenter; a prop-root
            // setup must keep the presenter alive so it can observe the next
            // session and turn its renderers back on.
            if (_guitarProp != null
                && _guitarProp.gameObject != gameObject
                && _guitarProp.gameObject.activeSelf != visible)
                _guitarProp.gameObject.SetActive(visible);

            if (_renderers == null) return;
            foreach (var targetRenderer in _renderers)
            {
                if (targetRenderer != null)
                    targetRenderer.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            foreach (var clip in _proceduralClips)
            {
                if (clip != null)
                    Destroy(clip);
            }
        }
    }
}
