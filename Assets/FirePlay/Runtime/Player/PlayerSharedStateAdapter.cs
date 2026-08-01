using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>本地组件状态与可同步玩家语义快照之间的适配器。</summary>
    [DefaultExecutionOrder(75)]
    [DisallowMultipleComponent]
    public sealed class PlayerSharedStateAdapter : MonoBehaviour
    {
        private PlayerModeController _mode;
        private PlayerAnimationController _animation;

        public PlayerSharedState State { get; } = new();
        public PlayerSharedStateSnapshot Snapshot => State.Snapshot;
        public event Action<PlayerSharedStateSnapshot> Changed;

        public void Initialize(LocalPlayerContext context)
        {
            _mode = GetComponent<PlayerModeController>();
            _animation = context != null ? context.Animation : GetComponent<PlayerAnimationController>();
        }

        private void Awake() => Initialize(GetComponent<LocalPlayerContext>());

        private void LateUpdate()
        {
            var mode = _mode != null ? _mode.CurrentMode : PlayerMode.Exploring;
            // Activity sessions have their own semantic/network path. This
            // legacy shared snapshot only mirrors Player mode and Resting
            // animation state until the realtime DTO replaces it.
            var ritualStateId = mode == PlayerMode.Resting ? PlayerAnimationStateIds.Resting : string.Empty;
            var ritualId = string.Empty;
            if (State.Set(mode, ritualStateId, ritualId)) Changed?.Invoke(State.Snapshot);
            _animation?.ApplySharedState(State.Snapshot);
        }

        public bool ApplyRemoteSnapshot(PlayerSharedStateSnapshot snapshot)
        {
            if (!State.Apply(snapshot)) return false;
            _animation?.ApplySharedState(State.Snapshot);
            Changed?.Invoke(State.Snapshot);
            return true;
        }
    }
}
