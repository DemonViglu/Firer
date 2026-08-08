using System;
using System.Collections.Generic;
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
    public sealed class FlameSource : MonoBehaviour, IWorldCommandVersioned
    {
        private static readonly List<FlameSource> Active = new();

        [SerializeField, Min(0.01f)] private float _restoreAmount = 20f;
        [Header("Respawn")]
        [SerializeField] private bool _respawnEnabled = true;
        [SerializeField, Min(1f)] private float _respawnSeconds = 600f;
        [SerializeField] private Color _warmthColor = new(1f, 0.72f, 0.35f, 1f);
        [SerializeField] private ParticleSystem _restoreVfx;
        [SerializeField] private AudioSource _restoreAudio;
        [SerializeField] private Renderer[] _renderersToHide;

        private Collider _interactionCollider;
        private bool _collected;
        private float _respawnRemainingSeconds;
        private bool _simulateAuthority = true;

        public static IReadOnlyList<FlameSource> ActiveInstances => Active;
        public bool IsAvailable => !_collected;
        public uint CommandVersion { get; private set; }
        public string SourceId => GetComponent<StableSceneId>().Value;
        public event Action<FlameSource> StateChanged;

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

            ApplyAvailabilityVisuals();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this))
                Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public bool TryRestore(FlameResourceController resourceController)
        {
            if (!_simulateAuthority || resourceController == null || !IsAvailable || !resourceController.Restore(_restoreAmount))
            {
                return false;
            }

            _collected = true;
            _respawnRemainingSeconds = _respawnEnabled ? _respawnSeconds : 0f;
            CommandVersion++;
            PlayFeedback();
            ApplyAvailabilityVisuals();
            StateChanged?.Invoke(this);
            return true;
        }

        public void ConfigureSimulation(bool simulateAuthority)
        {
            _simulateAuthority = simulateAuthority;
        }

        /// <summary>Applies a Host snapshot without restoring fuel or replaying local gameplay.</summary>
        public void ApplyNetworkSnapshot(bool isAvailable, uint commandVersion, bool playCollectedFeedback)
        {
            if (_simulateAuthority)
                return;

            var wasAvailable = IsAvailable;
            _collected = !isAvailable;
            _respawnRemainingSeconds = 0f;
            CommandVersion = commandVersion;
            if (playCollectedFeedback && wasAvailable && !isAvailable)
                PlayFeedback();
            ApplyAvailabilityVisuals();
        }

        private void Update()
        {
            if (!_simulateAuthority || !_collected || !_respawnEnabled)
            {
                return;
            }

            _respawnRemainingSeconds -= Time.deltaTime;
            if (_respawnRemainingSeconds <= 0f)
            {
                RestoreSource();
            }
        }

        private void ApplyAvailabilityVisuals()
        {
            if (_interactionCollider != null)
            {
                _interactionCollider.enabled = IsAvailable;
            }

            foreach (var sourceRenderer in _renderersToHide)
            {
                if (sourceRenderer != null)
                {
                    sourceRenderer.enabled = IsAvailable;
                }
            }
        }

        private void RestoreSource()
        {
            _collected = false;
            _respawnRemainingSeconds = 0f;
            CommandVersion++;
            ApplyAvailabilityVisuals();
            StateChanged?.Invoke(this);
        }

        private void OnValidate()
        {
            _restoreAmount = Mathf.Max(0.01f, _restoreAmount);
            _respawnSeconds = Mathf.Max(1f, _respawnSeconds);
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveInstances()
        {
            Active.Clear();
        }
    }
}
