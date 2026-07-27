using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>停留时将视角平滑引向关卡指定的观星目标。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class StargazingRitual : RestSpotRitual
    {
        [SerializeField] private Transform _lookTarget;
        [SerializeField, Min(0.01f)] private float _turnSpeed = 2.5f;
        private RestInteraction _interaction;
        private Transform _cameraTransform;
        private Quaternion _cameraLocalRotation;

        public override void OnRestStarted(RestInteraction interaction)
        {
            _interaction = interaction;
            _interaction.GetComponent<PlayerLook>()?.SetLookLocked(true);
            _cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (_cameraTransform != null)
            {
                _cameraLocalRotation = _cameraTransform.localRotation;
            }
        }

        public override void OnRestEnded(RestInteraction interaction)
        {
            if (_cameraTransform != null)
            {
                _cameraTransform.localRotation = _cameraLocalRotation;
            }
            interaction.GetComponent<PlayerLook>()?.SetLookLocked(false);
            _interaction = null;
            _cameraTransform = null;
        }

        private void LateUpdate()
        {
            if (_interaction == null || _lookTarget == null || _interaction.ActiveRestSpot != GetComponent<RestSpot>()) return;
            var cameraTransform = _cameraTransform;
            if (cameraTransform == null) return;
            var direction = (_lookTarget.position - cameraTransform.position).normalized;
            if (direction.sqrMagnitude < 0.001f) return;
            cameraTransform.rotation = Quaternion.Slerp(
                cameraTransform.rotation,
                Quaternion.LookRotation(direction),
                1f - Mathf.Exp(-_turnSpeed * Time.deltaTime));
        }
    }
}
