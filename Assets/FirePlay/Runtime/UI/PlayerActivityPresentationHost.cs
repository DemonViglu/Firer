using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using SUIFW;
using UnityEngine;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Player-side execution boundary for Activity UI/Camera/Player requests.
    /// ActivityLogic never touches UIManager, buttons or camera components;
    /// this host is the only adapter that is allowed to do so.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerActivityPresentationHost : MonoBehaviour, IActivityPresentationRequests,
        IActivityPlayerRequestExecutor, IActivityObserverPlayerRequestExecutor
    {
        [SerializeField] private MonoBehaviour _cameraExecutorBehaviour;
        [SerializeField] private MonoBehaviour _vfxExecutorBehaviour;
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private PlayerAnimationController _animation;

        private IActivityCameraRequestExecutor _cameraExecutor;
        private IActivityVfxRequestExecutor _vfxExecutor;
        private string _shownActivityId;
        private string _shownUiKey;
        private uint _shownRevision;
        private string _playerRequestActivityId;
        private uint _playerRequestRevision;
        private bool _hasPlayerRequestSession;
        private bool _movementLockRequested;
        private bool _lookLockRequested;
        private bool _previousMovementLocked;
        private bool _previousLookLocked;

        public bool ConfigureSceneExecutors(
            MonoBehaviour cameraExecutorBehaviour,
            MonoBehaviour vfxExecutorBehaviour = null)
        {
            if (cameraExecutorBehaviour is not IActivityCameraRequestExecutor)
                return false;
            if (vfxExecutorBehaviour != null
                && vfxExecutorBehaviour is not IActivityVfxRequestExecutor)
            {
                return false;
            }

            _cameraExecutorBehaviour = cameraExecutorBehaviour;
            _vfxExecutorBehaviour = vfxExecutorBehaviour;
            _cameraExecutor = (IActivityCameraRequestExecutor)cameraExecutorBehaviour;
            _vfxExecutor = vfxExecutorBehaviour as IActivityVfxRequestExecutor;
            return true;
        }

        private void Awake()
        {
            _movement ??= GetComponentInParent<PlayerMovement>();
            _look ??= GetComponentInParent<PlayerLook>();
            _animation ??= GetComponentInParent<PlayerAnimationController>();
            _vfxExecutor = _vfxExecutorBehaviour as IActivityVfxRequestExecutor;
        }

        public bool RequestUi(ActivityUiRequest request)
        {
            if (request.Kind == ActivityUiRequestKind.Open)
                return OpenUi(request);
            return CloseUi(request);
        }

        public bool RequestCamera(ActivityCameraRequest request)
        {
            _cameraExecutor ??= _cameraExecutorBehaviour as IActivityCameraRequestExecutor;
            if (_cameraExecutor != null)
                return _cameraExecutor.Execute(request);

            if (string.IsNullOrWhiteSpace(request.CameraProfileId))
                return true;

            Debug.LogWarning(
                $"[PlayerActivityPresentationHost] 未配置 Camera Executor：{request.ActivityId}/{request.CameraProfileId}",
                this);
            return false;
        }

        public bool RequestPlayer(ActivityPlayerRequest request)
        {
            if (request.Kind == ActivityPlayerRequestKind.VfxCue)
            {
                _vfxExecutor ??= _vfxExecutorBehaviour as IActivityVfxRequestExecutor;
                if (_vfxExecutor != null)
                    return _vfxExecutor.Execute(request);

                Debug.LogWarning(
                    $"[PlayerActivityPresentationHost] 未配置 VFX Executor：{request.ActivityId}/{request.CueId}",
                    this);
                return false;
            }

            // One-shot cues never own the activity presentation session. A
            // social cue may arrive while Fishing/Guitar/etc. owns movement
            // and look locks; replacing that bookkeeping here would leave the
            // original activity unable to restore Player control on exit.
            if (request.Kind != ActivityPlayerRequestKind.AnimationCue)
                BeginPlayerRequestSession(request);
            return Execute(request);
        }

        public bool Execute(ActivityPlayerRequest request)
        {
            switch (request.Kind)
            {
                case ActivityPlayerRequestKind.MovementLock:
                    if (_movement == null) return false;
                    if (request.Active)
                    {
                        if (!_movementLockRequested)
                        {
                            _previousMovementLocked = _movement.MovementLocked;
                            _movementLockRequested = true;
                        }
                        _movement.SetMovementLocked(true);
                    }
                    else if (_movementLockRequested)
                    {
                        _movement.SetMovementLocked(_previousMovementLocked);
                        _movementLockRequested = false;
                    }
                    return true;

                case ActivityPlayerRequestKind.LookTarget:
                    if (_look == null) return false;
                    if (!request.Active)
                    {
                        if (_lookLockRequested)
                        {
                            _look.SetLookLocked(_previousLookLocked);
                            _lookLockRequested = false;
                        }
                        return true;
                    }

                    var anchor = ActivityAnchorNode.FindById(request.TargetId);
                    if (anchor == null || !_look.TryFaceTarget(anchor.transform))
                        return false;
                    if (!_lookLockRequested)
                    {
                        _previousLookLocked = _look.LookLocked;
                        _lookLockRequested = true;
                    }
                    _look.SetLookLocked(true);
                    return true;

                case ActivityPlayerRequestKind.AnimationState:
                    if (_animation == null) return false;
                    _animation.SetState(request.CueId, request.Active);
                    return true;

                case ActivityPlayerRequestKind.AnimationCue:
                    if (_animation == null) return false;
                    _animation.Play(request.CueId);
                    return true;

                default:
                    return false;
            }
        }

        public bool ExecuteObserver(ActivityPlayerRequest request)
        {
            switch (request.Kind)
            {
                case ActivityPlayerRequestKind.AnimationState:
                case ActivityPlayerRequestKind.AnimationCue:
                    return Execute(request);

                case ActivityPlayerRequestKind.VfxCue:
                    _vfxExecutor ??= _vfxExecutorBehaviour as IActivityVfxRequestExecutor;
                    return _vfxExecutor != null && _vfxExecutor.Execute(request);

                // Observer presentation never owns this device's movement,
                // look, UI or camera state.
                default:
                    return false;
            }
        }

        private bool OpenUi(ActivityUiRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UiPrefabKey))
                return true;

            var uiManager = UIManager.GetInstance();
            if (uiManager == null)
                return false;

            if (!string.IsNullOrWhiteSpace(_shownUiKey) && _shownUiKey != request.UiPrefabKey)
                uiManager.CloseOrReturnUIForms(_shownUiKey);

            uiManager.ShowUIForms(request.UiPrefabKey);
            _shownActivityId = request.ActivityId;
            _shownUiKey = request.UiPrefabKey;
            _shownRevision = request.SessionRevision;
            return true;
        }

        private void BeginPlayerRequestSession(ActivityPlayerRequest request)
        {
            if (_hasPlayerRequestSession
                && _playerRequestActivityId == request.ActivityId
                && _playerRequestRevision == request.SessionRevision)
            {
                return;
            }

            if (_hasPlayerRequestSession)
                ReleasePlayerRequests();

            _playerRequestActivityId = request.ActivityId;
            _playerRequestRevision = request.SessionRevision;
            _hasPlayerRequestSession = true;
            _movementLockRequested = false;
            _lookLockRequested = false;
        }

        private bool CloseUi(ActivityUiRequest request)
        {
            if (string.IsNullOrWhiteSpace(_shownUiKey))
                return true;
            if (!string.IsNullOrWhiteSpace(_shownActivityId)
                && (request.ActivityId != _shownActivityId || request.SessionRevision != _shownRevision))
            {
                return false;
            }

            var uiManager = UIManager.TryGetInstance();
            uiManager?.CloseOrReturnUIForms(_shownUiKey);
            _shownActivityId = string.Empty;
            _shownUiKey = string.Empty;
            _shownRevision = 0;
            return true;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrWhiteSpace(_shownUiKey))
            {
                CloseUi(new ActivityUiRequest(
                    ActivityUiRequestKind.Close,
                    string.Empty,
                    _shownActivityId,
                    _shownUiKey,
                    _shownRevision));
            }

            ReleasePlayerRequests();
        }

        private void ReleasePlayerRequests()
        {
            if (_movementLockRequested && _movement != null)
                _movement.SetMovementLocked(_previousMovementLocked);
            if (_lookLockRequested && _look != null)
                _look.SetLookLocked(_previousLookLocked);

            _movementLockRequested = false;
            _lookLockRequested = false;
            _hasPlayerRequestSession = false;
            _playerRequestActivityId = string.Empty;
            _playerRequestRevision = 0;
        }
    }
}
