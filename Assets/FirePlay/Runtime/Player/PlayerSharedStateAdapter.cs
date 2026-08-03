using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>本地组件状态与可同步玩家语义快照之间的适配器。</summary>
    [DefaultExecutionOrder(75)]
    [DisallowMultipleComponent]
    public sealed class PlayerSharedStateAdapter : MonoBehaviour
    {
        private readonly PlayerSharedState _fallbackState = new();
        private PlayerSharedStateService _service;
        private LocalPlayerContext _context;
        private PlayerAnimationController _animation;

        public PlayerSharedState State => _service != null ? _service.State : _fallbackState;
        public PlayerSharedStateSnapshot Snapshot => State.Snapshot;
        public event Action<PlayerSharedStateSnapshot> Changed;

        public void Initialize(LocalPlayerContext context)
        {
            Initialize(context, context?.CoreHost?.SharedStateService);
        }

        public void Initialize(LocalPlayerContext context, PlayerSharedStateService service)
        {
            if (_service != null)
                _service.Changed -= OnServiceChanged;

            _context = context;
            _animation = context != null ? context.Animation : GetComponent<PlayerAnimationController>();
            _service = service;
            if (isActiveAndEnabled && _service != null)
                _service.Changed += OnServiceChanged;
        }

        private void Awake() => Initialize(GetComponent<LocalPlayerContext>());

        private void OnEnable()
        {
            if (_service != null)
            {
                _service.Changed -= OnServiceChanged;
                _service.Changed += OnServiceChanged;
            }
        }

        private void OnDisable()
        {
            if (_service != null)
                _service.Changed -= OnServiceChanged;
        }

        private void OnServiceChanged(PlayerSharedStateSnapshot snapshot)
        {
            Changed?.Invoke(snapshot);
        }

        public bool ApplyRemoteSnapshot(PlayerSharedStateSnapshot snapshot)
        {
            if (_service != null)
                return _service.ApplyRemoteSnapshot(snapshot);

            if (!_fallbackState.Apply(snapshot)) return false;
            _animation?.ApplySharedState(_fallbackState.Snapshot);
            Changed?.Invoke(_fallbackState.Snapshot);
            return true;
        }

        private void OnDestroy()
        {
            if (_service != null)
                _service.Changed -= OnServiceChanged;
        }
    }
}
