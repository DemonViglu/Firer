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
        private RitualInteractionCoordinator _rituals;
        private PlayerAnimationController _animation;

        public PlayerSharedState State { get; } = new();
        public PlayerSharedStateSnapshot Snapshot => State.Snapshot;
        public event Action<PlayerSharedStateSnapshot> Changed;

        public void Initialize(LocalPlayerContext context)
        {
            _mode = GetComponent<PlayerModeController>();
            _rituals = context != null ? context.RitualCoordinator : GetComponent<RitualInteractionCoordinator>();
            _animation = context != null ? context.Animation : GetComponent<PlayerAnimationController>();
        }

        private void Awake() => Initialize(GetComponent<LocalPlayerContext>());

        private void LateUpdate()
        {
            var mode = _mode != null ? _mode.CurrentMode : PlayerMode.Exploring;
            var active = _rituals != null ? _rituals.ActiveRitual : null;
            var ritualStateId = active != null
                ? active.SharedStateId
                : mode == PlayerMode.Resting ? PlayerAnimationStateIds.Resting : string.Empty;
            var ritualId = active != null ? active.ViewState.RitualId : string.Empty;
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
