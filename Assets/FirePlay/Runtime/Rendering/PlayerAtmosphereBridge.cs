using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    /// <summary>
    /// 只读取本地玩家的余火与停留状态，平滑驱动音景。
    /// 全局夜色由固定 Volume 负责，近场明暗由玩家火苗灯光负责，避免整屏调色随状态变化。
    /// </summary>
    public sealed class PlayerAtmosphereBridge : MonoBehaviour
    {
        [Header("State Sources")]
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private RestInteraction _restInteraction;

        [Header("Audio Layers")]
        [Tooltip("常驻环境底噪，例如夜风、虫鸣或远处水声。")]
        [SerializeField] private AudioSource _ambientBed;
        [Tooltip("随余火增强的温暖层，例如火焰噼啪或柔和音乐层。")]
        [SerializeField] private AudioSource _warmthLayer;
        [Tooltip("仅在停留时淡入的近景层，例如篝火近场、湖边细节或仪式音乐。")]
        [SerializeField] private AudioSource _restLayer;

        [Header("Blend")]
        [SerializeField, Min(0.01f)] private float _blendSpeed = 2f;
        [SerializeField, Range(0f, 1f)] private float _ambientMinimumVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _warmthMaximumVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _restMaximumVolume = 1f;

        public float CurrentWarmth { get; private set; }
        public bool IsConfigured => _resourceController != null;

        private void Awake()
        {
            _resourceController ??= GetComponent<FlameResourceController>();
            _restInteraction ??= GetComponent<RestInteraction>();

            if (_resourceController == null)
            {
                Debug.LogError("[PlayerAtmosphereBridge] 缺少 FlameResourceController。", this);
                enabled = false;
                return;
            }

            CurrentWarmth = ReadTargetWarmth();
            ApplyImmediate(CurrentWarmth, _restInteraction != null && _restInteraction.IsResting);
        }

        private void Update()
        {
            var targetWarmth = ReadTargetWarmth();
            var blend = 1f - Mathf.Exp(-_blendSpeed * Time.deltaTime);
            CurrentWarmth = Mathf.Lerp(CurrentWarmth, targetWarmth, blend);
            Apply(CurrentWarmth, _restInteraction != null && _restInteraction.IsResting, blend);
        }

        private float ReadTargetWarmth()
        {
            var state = _resourceController.State;
            if (state == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(state.NormalizedFuel);
        }

        private void Apply(float warmth, bool isResting, float blend)
        {
            SetVolume(_ambientBed, Mathf.Lerp(1f, _ambientMinimumVolume, warmth), blend);
            SetVolume(_warmthLayer, warmth * _warmthMaximumVolume, blend);
            SetVolume(_restLayer, isResting ? _restMaximumVolume : 0f, blend);
        }

        private void ApplyImmediate(float warmth, bool isResting)
        {
            SetVolumeImmediate(_ambientBed, Mathf.Lerp(1f, _ambientMinimumVolume, warmth));
            SetVolumeImmediate(_warmthLayer, warmth * _warmthMaximumVolume);
            SetVolumeImmediate(_restLayer, isResting ? _restMaximumVolume : 0f);
        }

        private static void SetVolume(AudioSource source, float target, float blend)
        {
            if (source != null)
            {
                source.volume = Mathf.Lerp(source.volume, target, blend);
            }
        }

        private static void SetVolumeImmediate(AudioSource source, float target)
        {
            if (source != null)
            {
                source.volume = target;
            }
        }
    }
}
