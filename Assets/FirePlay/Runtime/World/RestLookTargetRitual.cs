using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 停留期间锁定常规视角输入，并平滑看向关卡指定目标。
    /// 观星、烤棉花等仪式只需继承本类，不修改玩家停留状态。
    /// </summary>
    public abstract class RestLookTargetRitual : RestSpotRitual
    {
        [SerializeField] private Transform _lookTarget;
        [SerializeField, Min(0.01f)] private float _turnSpeed = 2.5f;

        private RestInteraction _interaction;
        private RitualCameraDirector _cameraDirector;
        private Transform _cameraTransform;
        private Quaternion _cameraLocalRotation;
        private bool _usingExternalCamera;
        private bool _usingStargazingCamera;
        private bool _usingFishingCamera;

        public Transform LookTarget => _lookTarget;
        protected virtual bool RotatePlayerTowardsTarget => false;
        protected virtual bool UsesExternalCamera => false;

        public override void OnRestStarted(RestInteraction interaction)
        {
            if (!IsSelectedFor(interaction)) return;
            if (_lookTarget == null)
            {
                Debug.LogWarning($"[{GetType().Name}] 未指定 Look Target；本次停留不会锁定视角。", this);
                return;
            }

            _interaction = interaction;
            _interaction.GetComponent<PlayerLook>()?.SetLookLocked(true);

            _cameraDirector = UsesExternalCamera || UsesStargazingCamera || UsesFishingCamera
                ? Object.FindAnyObjectByType<RitualCameraDirector>()
                : null;
            if (UsesExternalCamera)
            {
                _usingExternalCamera = _cameraDirector != null && _cameraDirector.TryBeginRitual(interaction, _lookTarget);
            }
            else if (UsesStargazingCamera)
            {
                _usingStargazingCamera = _cameraDirector != null && _cameraDirector.TryBeginStargazing(interaction, _lookTarget, StargazingCompanionTarget);
            }
            else if (UsesFishingCamera)
            {
                _usingFishingCamera = _cameraDirector != null && _cameraDirector.TryBeginFishing(interaction, _lookTarget);
            }

            if (!_usingExternalCamera && !_usingStargazingCamera && !_usingFishingCamera)
            {
                _cameraTransform = Camera.main != null ? Camera.main.transform : null;
                if (_cameraTransform != null)
                {
                    _cameraLocalRotation = _cameraTransform.localRotation;
                }
            }
        }

        public override void OnRestEnded(RestInteraction interaction)
        {
            if (_interaction != interaction)
            {
                return;
            }

            if (_usingExternalCamera)
            {
                _cameraDirector?.EndRitual(interaction);
            }
            else if (_usingStargazingCamera)
            {
                _cameraDirector?.EndStargazing(interaction);
            }
            else if (_usingFishingCamera)
            {
                _cameraDirector?.EndFishing(interaction);
            }
            else if (_cameraTransform != null)
            {
                _cameraTransform.localRotation = _cameraLocalRotation;
            }

            interaction.GetComponent<PlayerLook>()?.SetLookLocked(false);
            _interaction = null;
            _cameraDirector = null;
            _cameraTransform = null;
            _usingExternalCamera = false;
            _usingStargazingCamera = false;
            _usingFishingCamera = false;
        }

        private void LateUpdate()
        {
            SyncSelectedActivityState();
            if (_interaction == null || _lookTarget == null || _interaction.ActiveRestSpot != GetComponent<RestSpot>())
            {
                return;
            }

            var blend = 1f - Mathf.Exp(-_turnSpeed * Time.deltaTime);
            if (RotatePlayerTowardsTarget)
            {
                var playerDirection = _lookTarget.position - _interaction.transform.position;
                playerDirection.y = 0f;
                if (playerDirection.sqrMagnitude >= 0.001f)
                {
                    _interaction.transform.rotation = Quaternion.Slerp(
                        _interaction.transform.rotation,
                        Quaternion.LookRotation(playerDirection),
                        blend);
                }
            }

            if (!_usingExternalCamera && !_usingStargazingCamera && !_usingFishingCamera && _cameraTransform != null)
            {
                var direction = (_lookTarget.position - _cameraTransform.position).normalized;
                if (direction.sqrMagnitude >= 0.001f)
                {
                    _cameraTransform.rotation = Quaternion.Slerp(
                        _cameraTransform.rotation,
                        Quaternion.LookRotation(direction),
                        blend);
                }
            }
        }

        private void SyncSelectedActivityState()
        {
            var interaction = _interaction != null
                ? _interaction
                : LocalPlayerContext.Current != null ? LocalPlayerContext.Current.RestInteraction : null;
            var shouldBeActive = interaction != null
                && interaction.IsResting
                && interaction.ActiveRestSpot == GetComponent<RestSpot>()
                && IsSelectedFor(interaction);

            if (shouldBeActive && _interaction == null)
            {
                OnRestStarted(interaction);
            }
            else if (!shouldBeActive && _interaction != null)
            {
                OnRestEnded(_interaction);
            }
        }

        protected virtual bool UsesStargazingCamera => false;
        protected virtual bool UsesFishingCamera => false;
        protected virtual Transform StargazingCompanionTarget => null;
    }
}
