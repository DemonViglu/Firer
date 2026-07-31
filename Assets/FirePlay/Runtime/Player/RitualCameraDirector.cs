using System;
using DemonViglu.FirePlay.Activity;
using Unity.Cinemachine;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 管理本地玩家的仪式镜头。动态篝火在进入仪式时传入自己的 Look Target，
    /// 本类将玩家和目标临时加入 Target Group，并提高仪式镜头优先级。
    /// </summary>
    public sealed class RitualCameraDirector : MonoBehaviour, IActivityCameraRequestExecutor
    {
        [SerializeField] private CinemachineCamera _exploreCamera;
        [SerializeField] private CinemachineCamera _ritualCamera;
        [SerializeField] private CinemachineTargetGroup _ritualTargetGroup;
        [Header("Stargazing")]
        [SerializeField] private CinemachineCamera _stargazingCamera;
        [SerializeField] private CinemachineTargetGroup _stargazingTargetGroup;
        [SerializeField] private Transform _stargazingFollowAnchor;
        [Header("Fishing")]
        [SerializeField] private CinemachineCamera _fishingCamera;
        [SerializeField] private CinemachineTargetGroup _fishingTargetGroup;
        [SerializeField] private Transform _fishingFollowAnchor;
        [Tooltip("仪式镜头的 Follow 锚点。旋转 180° 后可让 Third Person Follow 位于角色正面。")]
        [SerializeField] private Transform _ritualFollowAnchor;
        [Tooltip("Target Group 中代表玩家的构图锚点。建议放在角色胸口高度，而非 Player 根部。")]
        [SerializeField] private Transform _playerFrameTarget;
        [SerializeField] private int _explorePriority = 10;
        [SerializeField] private int _ritualPriority = 20;
        [SerializeField] private int _stargazingPriority = 20;
        [SerializeField] private int _fishingPriority = 20;
        [SerializeField, Min(0f)] private float _playerTargetRadius = 0.7f;
        [SerializeField, Min(0f)] private float _ritualTargetRadius = 0.3f;
        [SerializeField, Min(0f)] private float _ritualTargetWeight = 1.4f;
        [SerializeField, Min(0f)] private float _stargazingCompanionWeight = 1f;
        [SerializeField, Min(0f)] private float _stargazingSkyWeight = 0.35f;
        [SerializeField, Min(0f)] private float _fishingPlayerWeight = 0.75f;
        [SerializeField, Min(0f)] private float _fishingWaterWeight = 1.25f;
        [Header("Activity camera profiles")]
        [SerializeField] private ActivityCameraProfile[] _activityCameraProfiles = Array.Empty<ActivityCameraProfile>();

        private Transform _activePlayerTarget;
        private Transform _activeFollowTarget;
        private Transform _activeRitualTarget;
        private RestInteraction _activeInteraction;
        private RestInteraction _stargazingInteraction;
        private Transform _stargazingPlayerTarget;
        private Transform _stargazingCompanionTarget;
        private Transform _stargazingSkyTarget;
        private RestInteraction _fishingInteraction;
        private Transform _fishingPlayerTarget;
        private Transform _fishingWaterTarget;
        private ActivityCameraProfile _activeActivityProfile;
        private Transform _activeActivityPlayerTarget;
        private Transform _activeActivityLookTarget;
        private string _activeActivityProfileId;
        private uint _activeActivityRevision;
        private PlayerLook _playerLook;
        private bool _activityLookLockCaptured;
        private bool _previousActivityLookLocked;

        public bool HasValidSetup => _exploreCamera != null && _ritualCamera != null && _ritualTargetGroup != null;
        public bool IsRitualCameraActive => _activeInteraction != null;

        private void Awake()
        {
            _playerLook ??= GetComponent<PlayerLook>();
            ApplyExplorePriority();
            DeactivateRitualCamera();
            DeactivateStargazingCamera();
            DeactivateFishingCamera();
        }

        private void OnDisable()
        {
            EndActivityCamera(default);
            EndRitual(_activeInteraction);
            EndStargazing(_stargazingInteraction);
            EndFishing(_fishingInteraction);
        }

        /// <summary>
        /// Executes a camera request emitted by the new Activity system. The
        /// request contains only a stable profile id; Cinemachine references
        /// remain owned by this player-side presentation component.
        /// </summary>
        public bool Execute(ActivityCameraRequest request)
        {
            if (request.Kind == ActivityCameraRequestKind.Exit)
            {
                return EndActivityCamera(request);
            }

            if (string.IsNullOrWhiteSpace(request.CameraProfileId))
            {
                return true;
            }

            var profile = FindActivityProfile(request.CameraProfileId);
            if (profile == null || profile.Camera == null || profile.TargetGroup == null)
            {
                Debug.LogWarning(
                    $"[RitualCameraDirector] Activity camera profile is not configured: {request.CameraProfileId}",
                    this);
                return false;
            }

            EndActivityCamera(default);
            EndRitual(_activeInteraction);
            EndStargazing(_stargazingInteraction);
            EndFishing(_fishingInteraction);

            var anchor = ActivityAnchorNode.FindById(request.AnchorId);
            var playerTarget = _playerFrameTarget != null ? _playerFrameTarget : transform;
            var lookTarget = profile.LookTarget != null
                ? profile.LookTarget
                : anchor != null ? anchor.transform : playerTarget;
            var followTarget = profile.FollowAnchor != null ? profile.FollowAnchor : playerTarget;

            AddMemberIfMissing(profile.TargetGroup, playerTarget, profile.PlayerWeight, profile.PlayerRadius);
            if (lookTarget != playerTarget)
            {
                AddMemberIfMissing(profile.TargetGroup, lookTarget, profile.LookTargetWeight, profile.LookTargetRadius);
            }

            profile.Camera.Follow = followTarget;
            profile.Camera.LookAt = profile.TargetGroup.transform;
            profile.Camera.Priority = profile.Priority;
            ApplyExplorePriority();

            _activeActivityProfile = profile;
            _activeActivityPlayerTarget = playerTarget;
            _activeActivityLookTarget = lookTarget;
            _activeActivityProfileId = request.CameraProfileId;
            _activeActivityRevision = request.SessionRevision;
            if (_playerLook != null)
            {
                _previousActivityLookLocked = _playerLook.LookLocked;
                _activityLookLockCaptured = true;
            }
            _playerLook?.SetLookLocked(true);
            return true;
        }

        private bool EndActivityCamera(ActivityCameraRequest request)
        {
            if (_activeActivityProfile == null)
            {
                return true;
            }

            if (request.Kind == ActivityCameraRequestKind.Exit
                && ((!string.IsNullOrWhiteSpace(request.CameraProfileId)
                        && !string.Equals(request.CameraProfileId, _activeActivityProfileId, StringComparison.Ordinal))
                    || (request.SessionRevision != 0
                        && request.SessionRevision != _activeActivityRevision)))
            {
                return false;
            }

            RemoveMemberIfPresent(_activeActivityProfile.TargetGroup, _activeActivityPlayerTarget);
            if (_activeActivityLookTarget != _activeActivityPlayerTarget)
            {
                RemoveMemberIfPresent(_activeActivityProfile.TargetGroup, _activeActivityLookTarget);
            }

            _activeActivityProfile.Camera.Priority = 0;
            _activeActivityProfile = null;
            _activeActivityPlayerTarget = null;
            _activeActivityLookTarget = null;
            _activeActivityProfileId = string.Empty;
            _activeActivityRevision = 0;
            if (_playerLook != null)
            {
                _playerLook.SetLookLocked(_activityLookLockCaptured && _previousActivityLookLocked);
                _activityLookLockCaptured = false;
                _previousActivityLookLocked = false;
            }
            ApplyExplorePriority();
            return true;
        }

        private ActivityCameraProfile FindActivityProfile(string profileId)
        {
            if (_activityCameraProfiles == null) return null;
            foreach (var profile in _activityCameraProfiles)
            {
                if (profile != null && string.Equals(profile.ProfileId, profileId, StringComparison.Ordinal))
                {
                    return profile;
                }
            }

            return null;
        }

        /// <summary>
        /// 以独立的 Target Group 构图观星：玩家、朋友占位和天空目标可同时入镜。
        /// </summary>
        public bool TryBeginStargazing(RestInteraction interaction, Transform skyTarget, Transform companionTarget)
        {
            if (_stargazingCamera == null || _stargazingTargetGroup == null || interaction == null || skyTarget == null)
            {
                return false;
            }

            EndRitual(_activeInteraction);
            EndFishing(_fishingInteraction);
            EndStargazing(_stargazingInteraction);
            _stargazingInteraction = interaction;
            _stargazingPlayerTarget = _playerFrameTarget != null ? _playerFrameTarget : interaction.transform;
            _stargazingCompanionTarget = companionTarget;
            _stargazingSkyTarget = skyTarget;

            AddMemberIfMissing(_stargazingTargetGroup, _stargazingPlayerTarget, 1f, _playerTargetRadius);
            AddMemberIfMissing(_stargazingTargetGroup, _stargazingCompanionTarget, _stargazingCompanionWeight, _ritualTargetRadius);
            AddMemberIfMissing(_stargazingTargetGroup, _stargazingSkyTarget, _stargazingSkyWeight, _ritualTargetRadius);

            _stargazingCamera.Follow = _stargazingFollowAnchor != null ? _stargazingFollowAnchor : interaction.transform;
            _stargazingCamera.LookAt = _stargazingTargetGroup.transform;
            _stargazingCamera.Priority = _stargazingPriority;
            ApplyExplorePriority();
            return true;
        }

        public void EndStargazing(RestInteraction interaction)
        {
            if (_stargazingInteraction == null || (interaction != null && interaction != _stargazingInteraction))
            {
                return;
            }

            if (_stargazingTargetGroup != null)
            {
                RemoveMemberIfPresent(_stargazingTargetGroup, _stargazingPlayerTarget);
                RemoveMemberIfPresent(_stargazingTargetGroup, _stargazingCompanionTarget);
                RemoveMemberIfPresent(_stargazingTargetGroup, _stargazingSkyTarget);
            }

            _stargazingInteraction = null;
            _stargazingPlayerTarget = null;
            _stargazingCompanionTarget = null;
            _stargazingSkyTarget = null;
            DeactivateStargazingCamera();
            ApplyExplorePriority();
        }

        /// <summary>
        /// 钓鱼使用独立镜头和 Target Group，因此可拥有比篝火仪式更宽广的湖景构图。
        /// </summary>
        public bool TryBeginFishing(RestInteraction interaction, Transform waterTarget)
        {
            if (_fishingCamera == null || _fishingTargetGroup == null || interaction == null || waterTarget == null)
            {
                return false;
            }

            EndRitual(_activeInteraction);
            EndStargazing(_stargazingInteraction);
            EndFishing(_fishingInteraction);
            _fishingInteraction = interaction;
            _fishingPlayerTarget = _playerFrameTarget != null ? _playerFrameTarget : interaction.transform;
            _fishingWaterTarget = waterTarget;

            AddMemberIfMissing(_fishingTargetGroup, _fishingPlayerTarget, _fishingPlayerWeight, _playerTargetRadius);
            AddMemberIfMissing(_fishingTargetGroup, _fishingWaterTarget, _fishingWaterWeight, _ritualTargetRadius);

            _fishingCamera.Follow = _fishingFollowAnchor != null ? _fishingFollowAnchor : interaction.transform;
            _fishingCamera.LookAt = _fishingTargetGroup.transform;
            _fishingCamera.Priority = _fishingPriority;
            ApplyExplorePriority();
            return true;
        }

        public void EndFishing(RestInteraction interaction)
        {
            if (_fishingInteraction == null || (interaction != null && interaction != _fishingInteraction))
            {
                return;
            }

            if (_fishingTargetGroup != null)
            {
                RemoveMemberIfPresent(_fishingTargetGroup, _fishingPlayerTarget);
                RemoveMemberIfPresent(_fishingTargetGroup, _fishingWaterTarget);
            }

            _fishingInteraction = null;
            _fishingPlayerTarget = null;
            _fishingWaterTarget = null;
            DeactivateFishingCamera();
            ApplyExplorePriority();
        }

        public bool TryBeginRitual(RestInteraction interaction, Transform ritualTarget)
        {
            if (!HasValidSetup || interaction == null || ritualTarget == null)
            {
                return false;
            }

            EndStargazing(_stargazingInteraction);
            EndFishing(_fishingInteraction);
            EndRitual(_activeInteraction);

            _activeInteraction = interaction;
            _activePlayerTarget = _playerFrameTarget != null ? _playerFrameTarget : interaction.transform;
            _activeFollowTarget = _ritualFollowAnchor != null ? _ritualFollowAnchor : interaction.transform;
            _activeRitualTarget = ritualTarget;

            AddMemberIfMissing(_ritualTargetGroup, _activePlayerTarget, 1f, _playerTargetRadius);
            AddMemberIfMissing(_ritualTargetGroup, _activeRitualTarget, _ritualTargetWeight, _ritualTargetRadius);

            _ritualCamera.Follow = _activeFollowTarget;
            _ritualCamera.LookAt = _ritualTargetGroup.transform;
            _ritualCamera.Priority = _ritualPriority;
            ApplyExplorePriority();
            return true;
        }

        public void EndRitual(RestInteraction interaction)
        {
            if (_activeInteraction == null || (interaction != null && interaction != _activeInteraction))
            {
                return;
            }

            if (_ritualTargetGroup != null)
            {
                RemoveMemberIfPresent(_ritualTargetGroup, _activePlayerTarget);
                RemoveMemberIfPresent(_ritualTargetGroup, _activeRitualTarget);
            }

            _activePlayerTarget = null;
            _activeFollowTarget = null;
            _activeRitualTarget = null;
            _activeInteraction = null;
            DeactivateRitualCamera();
            ApplyExplorePriority();
        }

        private void ApplyExplorePriority()
        {
            if (_exploreCamera != null)
            {
                _exploreCamera.Priority = _explorePriority;
            }
        }

        private void DeactivateRitualCamera()
        {
            if (_ritualCamera != null)
            {
                _ritualCamera.Priority = 0;
            }
        }

        private void DeactivateStargazingCamera()
        {
            if (_stargazingCamera != null)
            {
                _stargazingCamera.Priority = 0;
            }
        }

        private void DeactivateFishingCamera()
        {
            if (_fishingCamera != null)
            {
                _fishingCamera.Priority = 0;
            }
        }

        private static void AddMemberIfMissing(CinemachineTargetGroup targetGroup, Transform target, float weight, float radius)
        {
            if (target != null && targetGroup != null && targetGroup.FindMember(target) < 0)
            {
                targetGroup.AddMember(target, weight, radius);
            }
        }

        private static void RemoveMemberIfPresent(CinemachineTargetGroup targetGroup, Transform target)
        {
            if (target != null && targetGroup != null)
            {
                targetGroup.RemoveMember(target);
            }
        }

        private void OnValidate()
        {
            _playerTargetRadius = Mathf.Max(0f, _playerTargetRadius);
            _ritualTargetRadius = Mathf.Max(0f, _ritualTargetRadius);
            _ritualTargetWeight = Mathf.Max(0f, _ritualTargetWeight);
            _stargazingCompanionWeight = Mathf.Max(0f, _stargazingCompanionWeight);
            _stargazingSkyWeight = Mathf.Max(0f, _stargazingSkyWeight);
            _fishingPlayerWeight = Mathf.Max(0f, _fishingPlayerWeight);
            _fishingWaterWeight = Mathf.Max(0f, _fishingWaterWeight);
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
        }
    }
}
