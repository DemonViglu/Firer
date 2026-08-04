using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Composition boundary for local interaction discovery and raw-input
    /// routing. The scanner only describes targets; the router only publishes
    /// semantic intents for world/activity consumers.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionModule : MonoBehaviour, IPlayerModule
    {
        [SerializeField] private PlayerInteraction _scanner;
        [SerializeField] private InteractionRouter _router;

        private PlayerModuleContext _context;

        public string ModuleId => "interaction";
        public bool IsReady { get; private set; }
        public PlayerInteraction Scanner => _scanner;
        public InteractionRouter Router => _router;

        public void Initialize(PlayerModuleContext context)
        {
            _context = context;
            ResolveReferences();
            IsReady = _context != null;
        }

        public void Shutdown()
        {
            _context = null;
            IsReady = false;
        }

        private void ResolveReferences()
        {
            if (_context == null) return;

            _scanner ??= _context.GetComponent<PlayerInteraction>();
            _router ??= _context.GetComponent<InteractionRouter>();
        }

        private void OnValidate()
        {
            _scanner ??= GetComponent<PlayerInteraction>();
            _router ??= GetComponent<InteractionRouter>();
        }
    }
}
