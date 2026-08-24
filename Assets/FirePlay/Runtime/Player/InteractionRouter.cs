using System.Collections.Generic;
using DemonViglu.FirePlay.Core;
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
        private IWorldObjectRegistry _registry;
        private readonly Queue<RawPlayerInput> _pendingInputs = new();

        public void Initialize(LocalPlayerContext context)
        {
            _context = context;
            if (_context != null && !_context.IsLocalPlayer) return;

            _mode = GetComponentInParent<PlayerModeController>()
                ?? _context?.GetComponentInChildren<PlayerModeController>(true);
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _registry ??= GameInstanceSubsystem.GetOrCreate<IWorldObjectRegistry>(() => new StableIdWorldObjectRegistry());
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

            BindInput();
        }

        private void OnEnable()
        {
            _context ??= GetComponent<LocalPlayerContext>() ?? GetComponentInParent<LocalPlayerContext>();
            Initialize(_context);
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

        private void BindInput()
        {
            if (_context?.Input == null) return;
            _context.Input.RawInputPerformed -= Route;
            _context.Input.RawInputPerformed += Route;
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
            var expectedVersion = _registry != null && _registry.TryGetCommandVersion(targetId, out var version)
                ? version
                : -1L;
            _events.Publish(new PlayerIntentRequested(_context.PlayerId, kind, mode, targetKind, targetId, expectedVersion));
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
