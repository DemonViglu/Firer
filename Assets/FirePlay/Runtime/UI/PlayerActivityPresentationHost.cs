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
    public sealed class PlayerActivityPresentationHost : MonoBehaviour, IActivityPresentationRequests, IActivityPlayerRequestExecutor
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

        private void Awake()
        {
            _movement ??= GetComponent<PlayerMovement>();
            _look ??= GetComponent<PlayerLook>();
            _animation ??= GetComponent<PlayerAnimationController>();
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

            return Execute(request);
        }

        public bool Execute(ActivityPlayerRequest request)
        {
            switch (request.Kind)
            {
                case ActivityPlayerRequestKind.MovementLock:
                    if (_movement == null) return false;
                    _movement.SetMovementLocked(request.Active);
                    return true;

                case ActivityPlayerRequestKind.LookTarget:
                    if (_look == null) return false;
                    if (!request.Active)
                    {
                        _look.SetLookLocked(false);
                        return true;
                    }

                    var anchor = ActivityAnchorNode.FindById(request.TargetId);
                    if (anchor == null || !_look.TryFaceTarget(anchor.transform))
                        return false;
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
        }
    }
}
