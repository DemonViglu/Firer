using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.Network
{
    /// <summary>
    /// Explicit scene composition boundary between the authored standalone
    /// Player and an NGO-spawned local Player. It never creates components or
    /// network objects; it only transfers the already-authored scene services.
    /// </summary>
    [DefaultExecutionOrder(-750)]
    [DisallowMultipleComponent]
    public sealed class StandalonePlayerNetworkHandoff : MonoBehaviour
    {
        [SerializeField] private LocalPlayerContext _standalonePlayer;
        [SerializeField] private PlayerSceneServiceBindings _sceneBindings;

        private IEventPublisher _events;
        private bool _standaloneWasActive;
        private bool _networkHoldsPlayerSlot;
        private bool _restorePending;

        public bool HasValidSetup => _standalonePlayer != null && _sceneBindings != null;
        public bool NetworkHoldsPlayerSlot => _networkHoldsPlayerSlot;

        private void Awake()
        {
            if (!HasValidSetup)
            {
                Debug.LogError(
                    "[StandalonePlayerNetworkHandoff] 需要显式绑定 SnowValley 的 Player_Core 和 PlayerSceneServiceBindings。",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!enabled) return;
            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _events.Subscribe<FirePlayNetworkStateChanged>(OnNetworkStateChanged);
        }

        private void OnDisable()
        {
            _events?.Unsubscribe<FirePlayNetworkStateChanged>(OnNetworkStateChanged);
            _restorePending = false;
        }

        private void LateUpdate()
        {
            if (!_restorePending) return;

            // NGO may publish ClientDisconnected immediately before the local
            // NetworkObject finishes despawning. Wait until its local context
            // has released ownership, then restore the authored Player.
            var current = LocalPlayerContext.Current;
            if (current != null && current != _standalonePlayer)
                return;

            _restorePending = false;
            RestoreStandalonePlayer();
        }

        private void OnNetworkStateChanged(FirePlayNetworkStateChanged change)
        {
            if (change == null) return;

            switch (change.State)
            {
                case FirePlayNetworkState.Starting:
                    AcquireForNetwork();
                    break;
                case FirePlayNetworkState.StartFailed:
                case FirePlayNetworkState.Stopped:
                    RestoreStandalonePlayer();
                    break;
                case FirePlayNetworkState.ClientDisconnected
                    when change.Mode == FirePlayNetworkMode.Client:
                    _restorePending = true;
                    break;
            }
        }

        private void AcquireForNetwork()
        {
            if (_networkHoldsPlayerSlot || !HasValidSetup)
                return;

            _standaloneWasActive = _standalonePlayer.gameObject.activeSelf;
            _sceneBindings.ReleaseLocalPlayerCamera(_standalonePlayer.CameraTargets);
            if (_standalonePlayer.gameObject.activeSelf)
                _standalonePlayer.gameObject.SetActive(false);

            // The service stores the identity of the current local Player.
            // Clear it before the NGO owner registers network.player.<id>.
            GameInstanceSubsystem.Unregister<IPlayerIdentityService>();
            _networkHoldsPlayerSlot = true;
            _restorePending = false;
        }

        private void RestoreStandalonePlayer()
        {
            if (!_networkHoldsPlayerSlot || !HasValidSetup)
                return;

            GameInstanceSubsystem.Unregister<IPlayerIdentityService>();
            if (_standaloneWasActive && !_standalonePlayer.gameObject.activeSelf)
                _standalonePlayer.gameObject.SetActive(true);

            if (_standaloneWasActive
                && !_sceneBindings.TryBindLocalPlayerCamera(_standalonePlayer.CameraTargets))
            {
                Debug.LogError(
                    "[StandalonePlayerNetworkHandoff] 网络停止后无法把场景相机重新交还给 Player_Core。",
                    this);
            }

            _networkHoldsPlayerSlot = false;
            _restorePending = false;
        }
    }
}
