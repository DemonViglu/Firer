using System;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 可选停留状态：进入后相机自然下沉并停止水平移动，再次触发停留操作即可离开。
    /// 公开方法由未来移动端 UI 调用，R 键只用于原型验证。
    /// </summary>
    public sealed class RestInteraction : MonoBehaviour
    {
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerModeController _modeController;
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private PlayerAnimationController _animation;
        [SerializeField, Min(0f)] private float _cameraDrop = 0.55f;
        [SerializeField, Min(0f)] private float _cameraTransitionSpeed = 5f;

        private Transform _cameraPivot;
        private Vector3 _standingPivotLocalPosition;
        private Campfire _activeCampfire;
        private IEventPublisher _events;
        private LocalPlayerContext _playerContext;
        private bool _allowLocalStateMutation = true;
        private bool _applyLocalPresentation = true;
        private IRestRequestTransport _requestTransport;

        public bool IsResting { get; private set; }
        public bool IsRecovering => _activeCampfire != null;
        public Campfire ActiveCampfire => _activeCampfire;
        public RestSpot NearestRestSpot { get; private set; }
        public RestSpot ActiveRestSpot { get; private set; }

        public event Action<RestSpot> RestStarted;
        public event Action<RestSpot> RestEnded;

        private void Awake()
        {
            _playerContext = GetComponent<LocalPlayerContext>()
                ?? GetComponentInParent<LocalPlayerContext>();
            _movement ??= GetComponent<PlayerMovement>()
                ?? GetComponentInParent<PlayerMovement>();
            _look ??= GetComponent<PlayerLook>()
                ?? GetComponentInParent<PlayerLook>();
            _modeController ??= GetComponent<PlayerModeController>()
                ?? GetComponentInParent<PlayerModeController>();
            _resourceController ??= _playerContext != null
                ? _playerContext.GetComponentInChildren<FlameResourceController>(true)
                : GetComponentInChildren<FlameResourceController>(true);
            _animation ??= GetComponent<PlayerAnimationController>()
                ?? GetComponentInParent<PlayerAnimationController>();
            _cameraPivot = _look != null ? _look.CameraPivot : null;

            if (_movement == null || _cameraPivot == null || _modeController == null)
            {
                Debug.LogError("[RestInteraction] 缺少输入、移动组件或 Camera Pivot。", this);
                enabled = false;
                return;
            }

            _standingPivotLocalPosition = _cameraPivot.localPosition;
        }

        private void OnEnable()
        {
            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _events.Subscribe<PlayerIntentRequested>(OnIntentRequested);
        }

        private void Update()
        {
            RefreshNearestRestSpot();
        }

        private void LateUpdate()
        {
            if (_cameraPivot == null) return;

            var target = IsResting && _applyLocalPresentation
                ? _standingPivotLocalPosition + Vector3.down * _cameraDrop
                : _standingPivotLocalPosition;
            var blend = 1f - Mathf.Exp(-_cameraTransitionSpeed * Time.deltaTime);
            _cameraPivot.localPosition = Vector3.Lerp(_cameraPivot.localPosition, target, blend);
        }

        private void OnIntentRequested(PlayerIntentRequested intent)
        {
            var local = LocalPlayerContext.Current;
            if (local == null || local != _playerContext || intent.PlayerId != local.PlayerId) return;
            if (intent.Kind != PlayerIntentKind.Rest) return;
            if (_allowLocalStateMutation)
            {
                if (IsResting) EndRest();
                else TryBeginRest();
                return;
            }

            _requestTransport?.RequestRestToggle();
        }

        /// <summary>
        /// Network Players may only mutate Rest locally while they own Rest
        /// authority. Pure Clients remain disabled until the Rest request RPC
        /// and confirmed snapshot path are connected.
        /// </summary>
        public void ConfigureNetworkAccess(
            bool allowLocalStateMutation,
            IRestRequestTransport requestTransport)
        {
            _allowLocalStateMutation = allowLocalStateMutation;
            _requestTransport = requestTransport;
        }

        public bool TryBeginRest()
        {
            var spot = RefreshNearestRestSpot();
            // Rest availability belongs to RestSpot; activity discovery must not
            // decide whether every RestSpot can be entered.
            if (IsResting || spot == null)
            {
                return false;
            }

            if (_movement == null || _modeController == null || _cameraPivot == null)
                return false;
            if (!_modeController.TryEnter(PlayerMode.Resting))
            {
                return false;
            }

            _movement.SetMovementLocked(true);
            BeginCampfireComfort(spot);
            IsResting = true;
            ActiveRestSpot = spot;
            _animation?.SetState(PlayerAnimationStateIds.Resting, true);
            ActiveRestSpot.NotifyRestStarted(this);
            RestStarted?.Invoke(ActiveRestSpot);
            return true;
        }

        /// <summary>
        /// Applies a Server-confirmed mirror without replaying RestSpot events,
        /// campfire resource mutations or activity composition on Clients.
        /// </summary>
        public void ApplyConfirmedNetworkState(bool resting, bool applyOwnerPresentation)
        {
            _applyLocalPresentation = applyOwnerPresentation;
            if (resting)
            {
                IsResting = true;
                ActiveRestSpot = RefreshNearestRestSpot();
                _modeController?.ApplyAuthoritativeMode(PlayerMode.Resting);
                _animation?.SetState(PlayerAnimationStateIds.Resting, true);
                if (applyOwnerPresentation)
                    _movement?.SetMovementLocked(true);
                return;
            }

            IsResting = false;
            ActiveRestSpot = null;
            _modeController?.Exit(PlayerMode.Resting);
            _animation?.SetState(PlayerAnimationStateIds.Resting, false);
            if (applyOwnerPresentation)
                _movement?.SetMovementLocked(false);
        }

        public RestSpot RefreshNearestRestSpot()
        {
            var position = _playerContext != null
                ? _playerContext.transform.position
                : transform.position;
            NearestRestSpot = RestSpot.FindNearest(position);
            return NearestRestSpot;
        }

        private void OnValidate()
        {
            _movement ??= GetComponent<PlayerMovement>()
                ?? GetComponentInParent<PlayerMovement>();
            _look ??= GetComponent<PlayerLook>()
                ?? GetComponentInParent<PlayerLook>();
            _modeController ??= GetComponent<PlayerModeController>()
                ?? GetComponentInParent<PlayerModeController>();
        }

        public void EndRest()
        {
            if (!IsResting)
            {
                return;
            }

            var completedSpot = ActiveRestSpot;
            IsResting = false;
            ActiveRestSpot = null;
            _modeController?.Exit(PlayerMode.Resting);
            _animation?.SetState(PlayerAnimationStateIds.Resting, false);
            EndCampfireComfort();
            if (completedSpot != null)
            {
                completedSpot.NotifyRestEnded(this);
                RestEnded?.Invoke(completedSpot);
            }
            // Release the base Rest lock after Activity presentation has had
            // a chance to release its own requests. This keeps nested locks
            // symmetric when a Rest-triggered Activity ends in the callback.
            _movement?.SetMovementLocked(false);
        }

        private void BeginCampfireComfort(RestSpot spot)
        {
            EndCampfireComfort();

            var campfire = spot != null ? spot.GetComponent<Campfire>() : null;
            if (campfire == null || campfire.IsExtinguished || campfire.Config == null)
                return;
            if (_resourceController == null) return;

            _resourceController.EnterCampfireRest(campfire);
            _activeCampfire = campfire;
        }

        private void EndCampfireComfort()
        {
            if (_resourceController != null && _activeCampfire != null)
                _resourceController.ExitCampfireRest();
            _activeCampfire = null;
        }

        private void OnDisable()
        {
            _events?.Unsubscribe<PlayerIntentRequested>(OnIntentRequested);
            EndRest();
            EndCampfireComfort();
            if (_cameraPivot != null)
                _cameraPivot.localPosition = _standingPivotLocalPosition;
        }

    }

    public interface IRestRequestTransport
    {
        bool RequestRestToggle();
    }
}
