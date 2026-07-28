using Unity.Cinemachine;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 管理本地玩家的仪式镜头。动态篝火在进入仪式时传入自己的 Look Target，
    /// 本类将玩家和目标临时加入 Target Group，并提高仪式镜头优先级。
    /// </summary>
    public sealed class RitualCameraDirector : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _exploreCamera;
        [SerializeField] private CinemachineCamera _ritualCamera;
        [SerializeField] private CinemachineTargetGroup _ritualTargetGroup;
        [Header("Stargazing")]
        [SerializeField] private CinemachineCamera _stargazingCamera;
        [SerializeField] private CinemachineTargetGroup _stargazingTargetGroup;
        [SerializeField] private Transform _stargazingFollowAnchor;
        [Tooltip("仪式镜头的 Follow 锚点。旋转 180° 后可让 Third Person Follow 位于角色正面。")]
        [SerializeField] private Transform _ritualFollowAnchor;
        [Tooltip("Target Group 中代表玩家的构图锚点。建议放在角色胸口高度，而非 Player 根部。")]
        [SerializeField] private Transform _playerFrameTarget;
        [SerializeField] private int _explorePriority = 10;
        [SerializeField] private int _ritualPriority = 20;
        [SerializeField] private int _stargazingPriority = 20;
        [SerializeField, Min(0f)] private float _playerTargetRadius = 0.7f;
        [SerializeField, Min(0f)] private float _ritualTargetRadius = 0.3f;
        [SerializeField, Min(0f)] private float _ritualTargetWeight = 1.4f;
        [SerializeField, Min(0f)] private float _stargazingCompanionWeight = 1f;
        [SerializeField, Min(0f)] private float _stargazingSkyWeight = 0.35f;

        private Transform _activePlayerTarget;
        private Transform _activeFollowTarget;
        private Transform _activeRitualTarget;
        private RestInteraction _activeInteraction;
        private RestInteraction _stargazingInteraction;
        private Transform _stargazingPlayerTarget;
        private Transform _stargazingCompanionTarget;
        private Transform _stargazingSkyTarget;

        public bool HasValidSetup => _exploreCamera != null && _ritualCamera != null && _ritualTargetGroup != null;
        public bool IsRitualCameraActive => _activeInteraction != null;

        private void Awake()
        {
            ApplyExplorePriority();
            DeactivateRitualCamera();
            DeactivateStargazingCamera();
        }

        private void OnDisable()
        {
            EndRitual(_activeInteraction);
            EndStargazing(_stargazingInteraction);
        }

        /// <summary>
        /// 以独立的 Target Group 构图观星：玩家、朋友占位和天空目标可同时入镜。
        /// </summary>
        public bool TryBeginStargazing(RestInteraction interaction, Transform skyTarget, Transform companionTarget)
        {
            if (_stargazingCamera == null || _stargazingTargetGroup == null || interaction == null || skyTarget == null || _activeInteraction != null)
            {
                return false;
            }

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

        public bool TryBeginRitual(RestInteraction interaction, Transform ritualTarget)
        {
            if (!HasValidSetup || interaction == null || ritualTarget == null)
            {
                return false;
            }

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
        }
    }
}
