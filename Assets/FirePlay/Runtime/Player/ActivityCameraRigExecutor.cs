using System;
using DemonViglu.FirePlay.Activity;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using Unity.Cinemachine;
using UnityEngine;

namespace DemonViglu.FirePlay.CameraSystem
{
    /// <summary>
    /// Scene-side executor for new Activity camera requests.
    ///
    /// This component owns Cinemachine references and activity camera rigs;
    /// Player and ActivityLogic exchange only stable profile/anchor IDs.
    /// Legacy Rest/观星/旧钓鱼 camera methods remain in RitualCameraDirector
    /// until those paths are migrated completely.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityCameraRigExecutor : MonoBehaviour, IActivityCameraRequestExecutor
    {
        [SerializeField] private CinemachineCamera _exploreCamera;
        [SerializeField] private Transform _fallbackPlayerFrameTarget;
        [SerializeField] private ActivityCameraProfile[] _profiles = Array.Empty<ActivityCameraProfile>();

        private ActivityCameraProfile _activeProfile;
        private Transform _activePlayerTarget;
        private Transform _activeLookTarget;
        private string _activeProfileId;
        private uint _activeRevision;

        public bool HasValidSetup => _exploreCamera != null;

        private void Awake() => ApplyExplorePriority();

        private void OnDisable() => End(default);

        public bool Execute(ActivityCameraRequest request)
        {
            if (request.Kind == ActivityCameraRequestKind.Exit)
                return End(request);

            if (string.IsNullOrWhiteSpace(request.CameraProfileId))
                return true;

            var profile = FindProfile(request.CameraProfileId);
            if (profile == null || profile.Camera == null || profile.TargetGroup == null)
            {
                Debug.LogWarning(
                    $"[ActivityCameraRigExecutor] Camera profile is not configured: {request.ActivityId}/{request.CameraProfileId}",
                    this);
                return false;
            }

            End(default);

            var playerTargets = LocalPlayerContext.Current?.CameraTargets;
            var playerTarget = playerTargets?.FrameTarget ?? _fallbackPlayerFrameTarget ?? transform;
            var anchor = ActivityAnchorNode.FindById(request.AnchorId);
            var lookTarget = profile.LookTarget != null
                ? profile.LookTarget
                : anchor != null ? anchor.transform : playerTargets?.LookAtTarget ?? playerTarget;
            var followTarget = profile.FollowAnchor != null
                ? profile.FollowAnchor
                : playerTargets?.FollowTarget ?? playerTarget;

            AddMemberIfMissing(profile.TargetGroup, playerTarget, profile.PlayerWeight, profile.PlayerRadius);
            if (lookTarget != playerTarget)
            {
                AddMemberIfMissing(profile.TargetGroup, lookTarget, profile.LookTargetWeight, profile.LookTargetRadius);
            }

            profile.Camera.Follow = followTarget;
            profile.Camera.LookAt = profile.TargetGroup.transform;
            profile.Camera.Priority = profile.Priority;
            ApplyExplorePriority();

            _activeProfile = profile;
            _activePlayerTarget = playerTarget;
            _activeLookTarget = lookTarget;
            _activeProfileId = request.CameraProfileId;
            _activeRevision = request.SessionRevision;
            return true;
        }

        private bool End(ActivityCameraRequest request)
        {
            if (_activeProfile == null)
                return true;

            if (request.Kind == ActivityCameraRequestKind.Exit
                && ((!string.IsNullOrWhiteSpace(request.CameraProfileId)
                        && !string.Equals(request.CameraProfileId, _activeProfileId, StringComparison.Ordinal))
                    || (request.SessionRevision != 0 && request.SessionRevision != _activeRevision)))
            {
                return false;
            }

            RemoveMemberIfPresent(_activeProfile.TargetGroup, _activePlayerTarget);
            if (_activeLookTarget != _activePlayerTarget)
                RemoveMemberIfPresent(_activeProfile.TargetGroup, _activeLookTarget);

            _activeProfile.Camera.Priority = 0;
            _activeProfile = null;
            _activePlayerTarget = null;
            _activeLookTarget = null;
            _activeProfileId = string.Empty;
            _activeRevision = 0;
            ApplyExplorePriority();
            return true;
        }

        private ActivityCameraProfile FindProfile(string profileId)
        {
            if (_profiles == null) return null;
            foreach (var profile in _profiles)
            {
                if (profile != null && string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal))
                    return profile;
            }

            return null;
        }

        private void ApplyExplorePriority()
        {
            if (_exploreCamera != null)
                _exploreCamera.Priority = 10;
        }

        private static void AddMemberIfMissing(CinemachineTargetGroup targetGroup, Transform target, float weight, float radius)
        {
            if (target != null && targetGroup != null && targetGroup.FindMember(target) < 0)
                targetGroup.AddMember(target, weight, radius);
        }

        private static void RemoveMemberIfPresent(CinemachineTargetGroup targetGroup, Transform target)
        {
            if (target != null && targetGroup != null)
                targetGroup.RemoveMember(target);
        }

        private void OnValidate()
        {
            if (_profiles == null) return;
            foreach (var profile in _profiles)
                profile?.Validate();
        }

        [Serializable]
        private sealed class ActivityCameraProfile
        {
            [SerializeField] private string _profileId;
            [SerializeField] private CinemachineCamera _camera;
            [SerializeField] private CinemachineTargetGroup _targetGroup;
            [SerializeField] private Transform _followAnchor;
            [SerializeField] private Transform _lookTarget;
            [SerializeField] private int _priority = 20;
            [SerializeField, Min(0f)] private float _playerWeight = 1f;
            [SerializeField, Min(0f)] private float _playerRadius = 0.7f;
            [SerializeField, Min(0f)] private float _lookTargetWeight = 1f;
            [SerializeField, Min(0f)] private float _lookTargetRadius = 0.3f;

            public string ProfileId => _profileId;
            public CinemachineCamera Camera => _camera;
            public CinemachineTargetGroup TargetGroup => _targetGroup;
            public Transform FollowAnchor => _followAnchor;
            public Transform LookTarget => _lookTarget;
            public int Priority => _priority;
            public float PlayerWeight => _playerWeight;
            public float PlayerRadius => _playerRadius;
            public float LookTargetWeight => _lookTargetWeight;
            public float LookTargetRadius => _lookTargetRadius;

            public void Validate()
            {
                _profileId = _profileId?.Trim();
                _playerWeight = Mathf.Max(0f, _playerWeight);
                _playerRadius = Mathf.Max(0f, _playerRadius);
                _lookTargetWeight = Mathf.Max(0f, _lookTargetWeight);
                _lookTargetRadius = Mathf.Max(0f, _lookTargetRadius);
            }
        }
    }
}
