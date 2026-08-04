using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.UI;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Player-side composition boundary for Activity services.
    /// ActivityHost and PresentationHost remain the authoritative Unity
    /// adapters, while activity-specific visuals live under this module.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityModule : MonoBehaviour, IPlayerModule
    {
        [SerializeField] private PlayerActivityHost _activityHost;
        [SerializeField] private PlayerActivityPresentationHost _presentationHost;

        private PlayerModuleContext _context;

        public string ModuleId => "activity";
        public bool IsReady { get; private set; }
        public PlayerActivityHost ActivityHost => _activityHost;
        public PlayerActivityPresentationHost PresentationHost => _presentationHost;

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

            _activityHost ??= _context.GetComponent<PlayerActivityHost>();
            _presentationHost ??= _context.GetComponent<PlayerActivityPresentationHost>();
        }

        private void OnValidate()
        {
            _activityHost ??= GetComponentInParent<PlayerActivityHost>();
            _presentationHost ??= GetComponentInParent<PlayerActivityPresentationHost>();
        }
    }
}
