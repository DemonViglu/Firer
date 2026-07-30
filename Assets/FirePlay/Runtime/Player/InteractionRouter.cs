using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public interface IInteractionRouter
    {
        void Route(RawPlayerInput input);
    }

    /// <summary>本地离散输入的唯一语义裁决点；只发布意图，不执行玩法。</summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class InteractionRouter : MonoBehaviour, IInteractionRouter
    {
        private LocalPlayerContext _context;
        private PlayerModeController _mode;
        private IEventPublisher _events;
        private readonly Queue<RawPlayerInput> _pendingInputs = new();

        public void Initialize(LocalPlayerContext context)
        {
            _context = context;
            _mode = GetComponent<PlayerModeController>();
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            var registered = GameInstanceSubsystem.TryGet<IInteractionRouter>();
            if (registered == null)
            {
                GameInstanceSubsystem.Register<IInteractionRouter>(this);
            }
            else if (!ReferenceEquals(registered, this))
            {
                Debug.LogError("[InteractionRouter] 已存在另一个本地交互路由。", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _context ??= GetComponent<LocalPlayerContext>();
            Initialize(_context);
            if (_context != null && _context.Input != null)
            {
                _context.Input.RawInputPerformed += Route;
            }
        }

        private void OnDisable()
        {
            if (_context != null && _context.Input != null)
            {
                _context.Input.RawInputPerformed -= Route;
            }
            if (ReferenceEquals(GameInstanceSubsystem.TryGet<IInteractionRouter>(), this))
            {
                GameInstanceSubsystem.Unregister<IInteractionRouter>();
            }
            _pendingInputs.Clear();
        }

        public void Route(RawPlayerInput input)
        {
            _pendingInputs.Enqueue(input);
        }

        private void Update()
        {
            while (_pendingInputs.Count > 0)
            {
                PublishIntent(_pendingInputs.Dequeue());
            }
        }

        private void PublishIntent(RawPlayerInput input)
        {
            if (_context == null || _events == null) return;

            var mode = _mode != null ? _mode.CurrentMode : PlayerMode.Exploring;
            var kind = ResolveIntent(input, mode);
            var targetKind = PlayerInteractTargetKind.None;
            var targetId = string.Empty;
            _context.Interaction?.GetIntentTarget(kind, out targetKind, out targetId);
            _events.Publish(new PlayerIntentRequested(_context.PlayerId, kind, mode, targetKind, targetId));
        }

        private static PlayerIntentKind ResolveIntent(RawPlayerInput input, PlayerMode mode)
        {
            if (input == RawPlayerInput.Emote)
                return mode == PlayerMode.Resting ? PlayerIntentKind.RitualPrimary : PlayerIntentKind.Emote;
            if (input == RawPlayerInput.Interact)
                return mode == PlayerMode.Resting ? PlayerIntentKind.RitualSecondary : PlayerIntentKind.Interact;

            return input switch
            {
                RawPlayerInput.Rest => PlayerIntentKind.Rest,
                RawPlayerInput.PlaceFire => PlayerIntentKind.PlaceFire,
                RawPlayerInput.CancelPlacement => PlayerIntentKind.CancelPlacement,
                RawPlayerInput.AddFire => PlayerIntentKind.AddFire,
                RawPlayerInput.TendFire => PlayerIntentKind.TendFire,
                RawPlayerInput.GatherEmber => PlayerIntentKind.GatherEmber,
                RawPlayerInput.StartPublicFire => PlayerIntentKind.StartPublicFire,
                RawPlayerInput.DrawFire => PlayerIntentKind.DrawFire,
                RawPlayerInput.ReclaimSmallFire => PlayerIntentKind.ReclaimSmallFire,
                RawPlayerInput.ContributeWorldTree => PlayerIntentKind.ContributeWorldTree,
                RawPlayerInput.CycleTreeLightColor => PlayerIntentKind.CycleTreeLightColor,
                RawPlayerInput.Pause => PlayerIntentKind.Pause,
                // Legacy UpgradeCampfire keeps its old path until CommandExecutor removes it.
                RawPlayerInput.UpgradeCampfire => PlayerIntentKind.LegacyWithdrawOrReclaim,
                _ => PlayerIntentKind.Interact
            };
        }
    }
}
