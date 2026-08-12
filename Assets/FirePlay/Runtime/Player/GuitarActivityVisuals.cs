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
        [SerializeField] private PlayerAnimationController _animationController;
        [SerializeField] private Transform _guitarProp;
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private AudioSource _audioSource;
        [Tooltip("按 Do4-Si6 顺序对应 21 键的正式音效；可留空，缺失项使用程序钢琴音。")]
        [SerializeField] private AudioClip[] _keyClips;

        [Header("Procedural Piano Fallback")]
        [Tooltip("没有配置对应 AudioClip 时，使用程序化柔和钢琴音；正式音频资源会自动优先。")]
        [SerializeField] private bool _useProceduralFallback = true;
        [SerializeField, Range(0.01f, 1f)] private float _proceduralVolume = 0.34f;
        [SerializeField, Min(0.2f)] private float _proceduralDuration = 1.35f;
        [Tooltip("第一个音位 Do4 的频率。其余 20 键按三组 C 大调自然音阶计算。")]
        [SerializeField, Min(55f)] private float _rootFrequency = 261.6256f;
        [SerializeField, Range(0.2f, 1f)] private float _pianoBrightness = 0.62f;

        private bool _hasActivityState;
        private bool _loggedMissingHost;
        private PlayerAnimationController _subscribedAnimationController;
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
            ResolveReferences();
            SubscribeAnimationCues();
            ResetPresentation();
        }

        private void Update()
        {
            ResolveReferences();
            SubscribeAnimationCues();
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

            _hasActivityState = true;
            SetVisible(true);
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
            _animationController ??= GetComponentInParent<PlayerAnimationController>();
            _audioSource ??= GetComponent<AudioSource>();
        }

        private void SubscribeAnimationCues()
        {
            if (_subscribedAnimationController == _animationController)
                return;

            UnsubscribeAnimationCues();
            if (_animationController == null)
                return;

            _animationController.CuePlayed += OnPlayerCuePlayed;
            _subscribedAnimationController = _animationController;
        }

        private void UnsubscribeAnimationCues()
        {
            if (_subscribedAnimationController != null)
                _subscribedAnimationController.CuePlayed -= OnPlayerCuePlayed;
            _subscribedAnimationController = null;
        }

        private void OnPlayerCuePlayed(string cueId)
        {
            if (_activityHost?.ActiveActivityId != GuitarActivityLogic.ActivityId
                || !GuitarActivityLogic.TryGetKeyIndex(cueId, out var keyIndex))
            {
                return;
            }

            PlayKeySound(keyIndex);
        }

        private void ResetPresentation()
        {
            _hasActivityState = false;
            SetVisible(false);
        }

        private void OnDisable()
        {
            UnsubscribeAnimationCues();
            ResetPresentation();
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
            var frequency = GuitarActivityLogic.GetNaturalNoteFrequency(
                keyIndex,
                Mathf.Max(55f, _rootFrequency));
            var samples = new float[sampleCount];
            var random = new System.Random(0x51A7 + keyIndex * 7919);
            var brightness = Mathf.Clamp(_pianoBrightness, 0.2f, 1f);
            var twoPi = Mathf.PI * 2f;

            for (var i = 0; i < sampleCount; i++)
            {
                var time = i / (float)sampleRate;
                var attack = 1f - Mathf.Exp(-time * 360f);
                var remaining = duration - time;
                var release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining / 0.14f));
                var tone = 0f;

                // Piano strings are slightly stiff: higher partials are not
                // perfectly harmonic and decay faster than the fundamental.
                for (var harmonic = 1; harmonic <= 7; harmonic++)
                {
                    var harmonicWeight = Mathf.Pow(brightness, harmonic - 1)
                                         / Mathf.Pow(harmonic, 0.72f);
                    var inharmonicity = Mathf.Sqrt(1f + 0.00012f * harmonic * harmonic);
                    var partialFrequency = frequency * harmonic * inharmonicity;
                    var partialDecay = Mathf.Exp(-time * (1.45f + harmonic * 0.72f));
                    tone += Mathf.Sin(twoPi * partialFrequency * time + harmonic * 0.19f)
                            * harmonicWeight
                            * partialDecay;
                }

                // A quiet three-string spread makes the placeholder warmer
                // and less synthetic without changing the note frequency.
                var stringDecay = Mathf.Exp(-time * 1.75f);
                var stringSpread = (
                    Mathf.Sin(twoPi * frequency * 0.9986f * time)
                    + Mathf.Sin(twoPi * frequency * 1.0014f * time))
                    * 0.07f
                    * stringDecay;
                var hammerNoise = (float)(random.NextDouble() * 2d - 1d)
                                  * Mathf.Exp(-time * 75f)
                                  * 0.055f;
                samples[i] = Mathf.Clamp(
                    (tone * 0.38f + stringSpread + hammerNoise) * attack * release,
                    -1f,
                    1f);
            }

            cached = AudioClip.Create(
                $"Piano_{GuitarActivityLogic.GetNoteLabel(keyIndex)}",
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
            UnsubscribeAnimationCues();
            foreach (var clip in _proceduralClips)
            {
                if (clip != null)
                    Destroy(clip);
            }
        }
    }
}
