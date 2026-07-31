using DemonViglu.FirePlay.World;
using System;
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
        [SerializeField, Min(0f)] private float _cameraDrop = 0.55f;
        [SerializeField, Min(0f)] private float _cameraTransitionSpeed = 5f;

        private Transform _cameraPivot;
        private Vector3 _standingPivotLocalPosition;
        private PlayerRestPoseController _restPose;
        private PlayerCampfireComfortController _campfireComfort;
        private IEventPublisher _events;

        public bool IsResting { get; private set; }
        public RestSpot NearestRestSpot { get; private set; }
        public RestSpot ActiveRestSpot { get; private set; }

        public event Action<RestSpot> RestStarted;
        public event Action<RestSpot> RestEnded;

        private void Awake()
        {
            _movement ??= GetComponent<PlayerMovement>();
            _look ??= GetComponent<PlayerLook>();
            _modeController ??= GetComponent<PlayerModeController>();
            _cameraPivot = _look != null ? _look.CameraPivot : null;

            if (_movement == null || _cameraPivot == null || _modeController == null)
            {
                Debug.LogError("[RestInteraction] 缺少输入、移动组件或 Camera Pivot。", this);
                enabled = false;
                return;
            }

            InitializeActivitySupport();
        }

        public void InitializeActivitySupport()
        {
            _movement ??= GetComponent<PlayerMovement>();
            _look ??= GetComponent<PlayerLook>();
            _modeController ??= GetComponent<PlayerModeController>();
            _restPose ??= GetComponent<PlayerRestPoseController>();
            _campfireComfort ??= GetComponent<PlayerCampfireComfortController>();
            _cameraPivot = _look != null ? _look.CameraPivot : null;
            if (_movement == null || _cameraPivot == null || _modeController == null || _restPose == null) return;

            _standingPivotLocalPosition = _cameraPivot.localPosition;
            _restPose.Initialize(_movement, _look, _modeController, _cameraDrop, _cameraTransitionSpeed, _standingPivotLocalPosition);
            _campfireComfort?.Initialize();
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
            var anchor = spot != null ? spot.ActivityAnchor : null;
            if (IsResting || spot == null || anchor == null || !anchor.TryGetOffer("rest", out _))
            {
                return false;
            }

            _restPose ??= GetComponent<PlayerRestPoseController>();
            if (_restPose == null || !_restPose.TryEnter())
            {
                return false;
            }

            _campfireComfort ??= GetComponent<PlayerCampfireComfortController>();
            _campfireComfort?.TryBegin(spot);
            IsResting = true;
            ActiveRestSpot = spot;
            ApplyRestLookLock(true);
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
            _restPose?.Exit();
            _campfireComfort?.End();
            ApplyRestLookLock(false, completedSpot);
            if (completedSpot != null)
            {
                completedSpot.NotifyRestEnded(this);
                RestEnded?.Invoke(completedSpot);
            }
        }

        private void ApplyRestLookLock(bool active, RestSpot spot = null)
        {
            var anchor = (spot ?? ActiveRestSpot)?.ActivityAnchor;
            var shouldLock = active
                && anchor != null
                && anchor.TryGetSingleLegacyActivityOffer(out var offer)
                && offer.locksLookInput;
            _look?.SetLookLocked(shouldLock);
        }

        private void OnDisable()
        {
            _events?.Unsubscribe<PlayerIntentRequested>(OnIntentRequested);
            EndRest();
        }

    }
}
