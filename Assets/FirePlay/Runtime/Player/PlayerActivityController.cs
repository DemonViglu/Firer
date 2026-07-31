using System;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 玩家级活动会话入口。当前通过 RestInteraction 做兼容同步，后续逐步接管坐姿与活动选择。
    /// </summary>
    [DefaultExecutionOrder(55)]
    [DisallowMultipleComponent]
    public sealed class PlayerActivityController : MonoBehaviour
    {
        private LocalPlayerContext _context;
        private IEventPublisher _events;
        private PlayerRestPoseController _restPose;
        private PlayerCampfireComfortController _campfireComfort;
        private PlayerLook _look;
        private bool _eventsAttached;

        public ActivitySession Session { get; } = new();
        public ActivityAnchor NearestAnchor { get; private set; }
        public ActivityAnchor ActiveAnchor { get; private set; }
        public event Action<ActivitySessionSnapshot> SessionChanged;

        public void Initialize(LocalPlayerContext context)
        {
            DetachEvents();
            _context = context;
            _restPose ??= GetComponent<PlayerRestPoseController>();
            _campfireComfort ??= GetComponent<PlayerCampfireComfortController>();
            _look ??= GetComponent<PlayerLook>();
            AttachEvents();
        }

        private void Awake() => Initialize(GetComponent<LocalPlayerContext>());

        private void OnEnable()
        {
            AttachEvents();
        }

        private void Update()
        {
            var spot = RestSpot.FindNearest(transform.position);
            NearestAnchor = spot != null ? spot.ActivityAnchor : null;
        }

        /// <summary>开始一个已发现的活动；当前不替换旧坐姿流程，只建立 Session 事实。</summary>
        public bool TryBeginActivity(ActivityAnchor anchor, string activityId)
        {
            if (_context == null || anchor == null || !anchor.TryGetOffer(activityId, out var offer)) return false;
            if (!Session.TryBegin(_context.PlayerId, anchor.AnchorId, offer.activityId, offer.presentationId)) return false;

            ActiveAnchor = anchor;
            SessionChanged?.Invoke(Session.Snapshot);
            return true;
        }

        public bool TryBeginNearestActivity(string activityId) => TryBeginActivity(NearestAnchor, activityId);

        public void InitializeRestSupport(
            PlayerMovement movement,
            PlayerLook look,
            PlayerModeController modeController,
            float cameraDrop,
            float cameraTransitionSpeed,
            Vector3 standingPivotLocalPosition)
        {
            _restPose ??= GetComponent<PlayerRestPoseController>() ?? gameObject.AddComponent<PlayerRestPoseController>();
            _restPose.Initialize(movement, look, modeController, cameraDrop, cameraTransitionSpeed, standingPivotLocalPosition);
            _look = look;
            _campfireComfort ??= GetComponent<PlayerCampfireComfortController>() ?? gameObject.AddComponent<PlayerCampfireComfortController>();
            _campfireComfort.Initialize();
        }

        public bool TryBeginLegacyRest(RestSpot spot)
        {
            var anchor = spot != null ? spot.ActivityAnchor : null;
            if (_context == null || anchor == null || !anchor.TryGetOffer("rest", out var offer)) return false;
            if (!Session.TryBegin(_context.PlayerId, anchor.AnchorId, offer.activityId, offer.presentationId)) return false;

            _restPose ??= GetComponent<PlayerRestPoseController>();
            if (_restPose == null || !_restPose.TryEnter())
            {
                Session.End();
                return false;
            }

            _campfireComfort ??= GetComponent<PlayerCampfireComfortController>();
            _campfireComfort?.TryBegin(spot);
            ActiveAnchor = anchor;
            ApplyActivityLookLock(Session.Snapshot);
            SessionChanged?.Invoke(Session.Snapshot);
            return true;
        }

        public void EndLegacyRest(RestSpot spot)
        {
            _restPose?.Exit();
            _campfireComfort?.End();
            if (Session.IsActive && ActiveAnchor != null && (spot == null || spot.ActivityAnchor == ActiveAnchor))
                EndActivity();
        }

        public bool TrySelectActivity(ActivityAnchor anchor, string activityId)
        {
            if (_context == null || anchor == null || !anchor.TryGetOffer(activityId, out var offer)) return false;
            if (!Session.IsActive && (_restPose == null || !_restPose.IsActive)) return false;
            if (Session.IsActive && ActiveAnchor != anchor) return false;
            if (!Session.TrySwitch(_context.PlayerId, anchor.AnchorId, offer.activityId, offer.presentationId)) return false;

            ActiveAnchor = anchor;
            ApplyActivityLookLock(Session.Snapshot);
            SessionChanged?.Invoke(Session.Snapshot);
            return true;
        }

        public bool TrySelectNearestActivity(string activityId) => TrySelectActivity(NearestAnchor, activityId);

        public bool EndActivity()
        {
            if (!Session.End()) return false;

            ActiveAnchor = null;
            ApplyActivityLookLock(Session.Snapshot);
            SessionChanged?.Invoke(Session.Snapshot);
            return true;
        }

        private void ApplyActivityLookLock(ActivitySessionSnapshot snapshot)
        {
            var shouldLock = false;
            if (snapshot.IsActive && ActiveAnchor != null)
            {
                if (snapshot.ActivityId == "rest")
                {
                    shouldLock = ActiveAnchor.TryGetSingleLegacyActivityOffer(out var legacyOffer) && legacyOffer.locksLookInput;
                }
                else
                {
                    shouldLock = ActiveAnchor.TryGetOffer(snapshot.ActivityId, out var offer) && offer.locksLookInput;
                }
            }

            _look?.SetLookLocked(shouldLock);
        }

        private void OnActivitySelectionRequested(ActivitySelectionRequested request)
        {
            if (_context == null || request == null || request.PlayerId != _context.PlayerId) return;

            var anchor = ResolveAnchor(request.AnchorId);
            if (anchor != null) TrySelectActivity(anchor, request.ActivityId);
        }

        private void OnActivityActionRequested(ActivityActionRequested request)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[PlayerActivityController] 收到动作：{request?.ActivityId}/{request?.ActionId}，player={request?.PlayerId}，sessionActive={Session.IsActive}", this);
#endif
            if (_context == null || request == null || request.PlayerId != _context.PlayerId || !Session.IsActive)
                return;

            var snapshot = Session.Snapshot;
            if (request.AnchorId != snapshot.AnchorId || request.ActivityId != snapshot.ActivityId)
                return;

            var matchedHandler = false;
            foreach (var behaviour in GetComponents<MonoBehaviour>())
            {
                if (behaviour is not IActivityActionHandler handler || !behaviour.isActiveAndEnabled)
                    continue;
                if (handler.ActivityId != snapshot.ActivityId)
                    continue;

                matchedHandler = true;
                if (handler.TryHandle(request))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"[PlayerActivityController] Handler 已消费：{snapshot.ActivityId}/{request.ActionId}", this);
#endif
                    return;
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(matchedHandler
                ? $"[PlayerActivityController] Handler 存在但未消费：{snapshot.ActivityId}/{request.ActionId}"
                : $"[PlayerActivityController] 没有启用的 Handler：{snapshot.ActivityId}/{request.ActionId}", this);
#endif
        }

        private ActivityAnchor ResolveAnchor(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId)) return ActiveAnchor ?? NearestAnchor;
            if (ActiveAnchor != null && ActiveAnchor.AnchorId == anchorId) return ActiveAnchor;
            if (NearestAnchor != null && NearestAnchor.AnchorId == anchorId) return NearestAnchor;
            return null;
        }

        private void OnDisable()
        {
            DetachEvents();
            _restPose?.Exit();
            _campfireComfort?.End();
            Session.End();
            ActiveAnchor = null;
            _look?.SetLookLocked(false);
        }

        private void AttachEvents()
        {
            if (_eventsAttached) return;

            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _events.Subscribe<ActivitySelectionRequested>(OnActivitySelectionRequested);
            _events.Subscribe<ActivityActionRequested>(OnActivityActionRequested);
            _eventsAttached = true;
        }

        private void DetachEvents()
        {
            if (!_eventsAttached || _events == null) return;

            _events.Unsubscribe<ActivitySelectionRequested>(OnActivitySelectionRequested);
            _events.Unsubscribe<ActivityActionRequested>(OnActivityActionRequested);
            _eventsAttached = false;
        }
    }
}
