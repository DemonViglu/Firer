using System;
using UnityEngine;
using DemonViglu.FirePlay.World;

namespace DemonViglu.FirePlay.Player
{
    public readonly struct RitualViewState
    {
        public string RitualId { get; }
        public string Status { get; }
        public string PrimaryLabel { get; }
        public string SecondaryLabel { get; }
        public bool PrimaryAvailable { get; }
        public bool SecondaryAvailable { get; }
        public bool IsVisible => !string.IsNullOrWhiteSpace(RitualId);

        public RitualViewState(string ritualId, string status, string primaryLabel, string secondaryLabel, bool primaryAvailable = true, bool secondaryAvailable = true)
        {
            RitualId = ritualId;
            Status = status;
            PrimaryLabel = primaryLabel;
            SecondaryLabel = secondaryLabel;
            PrimaryAvailable = primaryAvailable;
            SecondaryAvailable = secondaryAvailable;
        }
    }

    public interface IRitualInteraction
    {
        bool IsActive { get; }
        RitualViewState ViewState { get; }
        bool TryPrimaryAction();
        bool TrySecondaryAction();
    }

    /// <summary>活跃 Ritual 的唯一选择与操作入口；新增仪式无需修改 Router 或 UI。</summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class RitualInteractionCoordinator : MonoBehaviour
    {
        private IRitualInteraction[] _rituals = Array.Empty<IRitualInteraction>();
        private IEventPublisher _events;
        private LocalPlayerContext _context;

        public IRitualInteraction ActiveRitual { get; private set; }
        public RitualViewState ViewState => ActiveRitual != null ? ActiveRitual.ViewState : default;

        public void Initialize(LocalPlayerContext context)
        {
            _context = context;
            var behaviours = GetComponents<MonoBehaviour>();
            var rituals = new System.Collections.Generic.List<IRitualInteraction>();
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IRitualInteraction ritual) rituals.Add(ritual);
            }
            _rituals = rituals.ToArray();
        }

        private void OnEnable()
        {
            Initialize(GetComponent<LocalPlayerContext>());
            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _events.Subscribe<PlayerIntentRequested>(OnIntentRequested);
        }

        private void OnDisable()
        {
            _events?.Unsubscribe<PlayerIntentRequested>(OnIntentRequested);
            ActiveRitual = null;
        }

        private void Update()
        {
            ActiveRitual = null;
            foreach (var ritual in _rituals)
            {
                if (ritual.IsActive)
                {
                    ActiveRitual = ritual;
                    break;
                }
            }
        }

        private void OnIntentRequested(PlayerIntentRequested intent)
        {
            if (_context == null || intent.PlayerId != _context.PlayerId || ActiveRitual == null) return;
            if (intent.Kind == PlayerIntentKind.RitualPrimary) ActiveRitual.TryPrimaryAction();
            else if (intent.Kind == PlayerIntentKind.RitualSecondary) ActiveRitual.TrySecondaryAction();
        }
    }
}
