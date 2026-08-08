using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Framework-neutral camera targets exposed by a Player.
    ///
    /// The Player owns only these semantic targets. Cinemachine, a custom
    /// camera controller, or an activity-specific camera rig owns the actual
    /// camera and decides how to compose them with activity targets.
    /// </summary>
    public interface IPlayerCameraTargetProvider
    {
        Transform FollowTarget { get; }
        Transform ActivityFollowTarget { get; }
        Transform FrameTarget { get; }
        Transform LookAtTarget { get; }
        Transform InputPivot { get; }
    }

    public enum PlayerCameraTargetRole
    {
        Follow,
        ActivityFollow,
        Frame,
        LookAt,
        InputPivot
    }

    [DisallowMultipleComponent]
    public sealed class PlayerCameraTargetSet : MonoBehaviour, IPlayerCameraTargetProvider
    {
        [Header("Player-owned semantic targets")]
        [SerializeField] private Transform _followTarget;
        [SerializeField] private Transform _activityFollowTarget;
        [SerializeField] private Transform _frameTarget;
        [SerializeField] private Transform _lookAtTarget;
        [SerializeField] private Transform _inputPivot;

        public Transform FollowTarget => _followTarget != null ? _followTarget : transform;
        public Transform ActivityFollowTarget => _activityFollowTarget != null ? _activityFollowTarget : FrameTarget;
        public Transform FrameTarget => _frameTarget != null ? _frameTarget : transform;
        public Transform LookAtTarget => _lookAtTarget != null ? _lookAtTarget : FrameTarget;
        public Transform InputPivot => _inputPivot != null ? _inputPivot : transform;

        public bool HasExplicitTargets => _followTarget != null
            && _activityFollowTarget != null
            && _frameTarget != null
            && _lookAtTarget != null
            && _inputPivot != null;

        public Transform Resolve(PlayerCameraTargetRole role)
        {
            return role switch
            {
                PlayerCameraTargetRole.Follow => FollowTarget,
                PlayerCameraTargetRole.ActivityFollow => ActivityFollowTarget,
                PlayerCameraTargetRole.Frame => FrameTarget,
                PlayerCameraTargetRole.LookAt => LookAtTarget,
                PlayerCameraTargetRole.InputPivot => InputPivot,
                _ => transform
            };
        }

        private void Awake() => ResolveDefaults();

        private void OnValidate()
        {
            ResolveDefaults();
        }

        private void ResolveDefaults()
        {
            _inputPivot ??= GetComponent<PlayerLook>()?.CameraPivot;
            _inputPivot ??= FindChild("CameraPivot");

            _followTarget ??= FindChild("CameraPivot/PlayerCameraFollowTarget");
            _followTarget ??= FindChild("CameraPivot/ExploreCameraAnchor");
            _activityFollowTarget ??= FindChild("ActivityCameraFollowTarget");
            _activityFollowTarget ??= FindChild("CinemachineAnchor/RitualCameraFollowAnchor");
            _frameTarget ??= FindChild("PlayerCameraFrameTarget");
            _frameTarget ??= FindChild("RitualCameraFrameTarget");
            _lookAtTarget ??= FindChild("CameraPivot/PlayerCameraLookAtTarget");
            _lookAtTarget ??= FindChild("CameraPivot/ExploreCameraLookAt");
        }

        private Transform FindChild(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? null : transform.Find(path);
        }
    }
}
