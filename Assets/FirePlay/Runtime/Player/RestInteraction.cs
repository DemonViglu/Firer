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
        [SerializeField, Min(0f)] private float _cameraDrop = 0.55f;
        [SerializeField, Min(0f)] private float _cameraTransitionSpeed = 5f;

        private Transform _cameraPivot;
        private Vector3 _standingPivotLocalPosition;
        private Campfire _activeCampfire;
        private IEventPublisher _events;

        public bool IsResting { get; private set; }
        public bool IsRecovering => _activeCampfire != null;
        public Campfire ActiveCampfire => _activeCampfire;
        public RestSpot NearestRestSpot { get; private set; }
        public RestSpot ActiveRestSpot { get; private set; }

        public event Action<RestSpot> RestStarted;
        public event Action<RestSpot> RestEnded;

        private void Awake()
        {
            _movement ??= GetComponent<PlayerMovement>();
            _look ??= GetComponent<PlayerLook>();
            _modeController ??= GetComponent<PlayerModeController>();
            _resourceController ??= GetComponentInChildren<FlameResourceController>(true);
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
            NearestRestSpot = RestSpot.FindNearest(transform.position);
        }

        private void LateUpdate()
        {
            if (_cameraPivot == null) return;

            var target = IsResting
                ? _standingPivotLocalPosition + Vector3.down * _cameraDrop
                : _standingPivotLocalPosition;
            var blend = 1f - Mathf.Exp(-_cameraTransitionSpeed * Time.deltaTime);
            _cameraPivot.localPosition = Vector3.Lerp(_cameraPivot.localPosition, target, blend);
        }

        private void OnIntentRequested(PlayerIntentRequested intent)
        {
            var local = LocalPlayerContext.Current;
            if (local == null || local.gameObject != gameObject || intent.PlayerId != local.PlayerId) return;
            if (intent.Kind != PlayerIntentKind.Rest) return;
            if (IsResting) EndRest();
            else TryBeginRest();
        }

        public bool TryBeginRest()
        {
            var spot = NearestRestSpot;
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
            ActiveRestSpot.NotifyRestStarted(this);
            RestStarted?.Invoke(ActiveRestSpot);
            return true;
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
            _movement?.SetMovementLocked(false);
            EndCampfireComfort();
            if (completedSpot != null)
            {
                completedSpot.NotifyRestEnded(this);
                RestEnded?.Invoke(completedSpot);
            }
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
}
