using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// Keeps the exploration camera between its pivot and authored orbit point
    /// when terrain or scene geometry blocks the view. It changes presentation
    /// only; PlayerLook remains the sole owner of yaw and pitch.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PlayerCameraObstruction : MonoBehaviour
    {
        [SerializeField] private Transform _pivot;
        [SerializeField] private Transform _ignoredRoot;
        [SerializeField] private Vector3 _desiredLocalPosition = new(0f, 1f, -6f);
        [SerializeField] private LayerMask _collisionLayers = ~0;
        [SerializeField, Min(0.01f)] private float _collisionRadius = 0.24f;
        [SerializeField, Min(0f)] private float _surfacePadding = 0.08f;
        [SerializeField, Min(0.01f)] private float _returnSpeed = 10f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private LocalPlayerContext _ownerContext;
        private float _currentDistance;

        private void Awake()
        {
            ResolveReferences();
            _currentDistance = GetDesiredDistance();
        }

        private void LateUpdate()
        {
            _ownerContext ??= GetComponentInParent<LocalPlayerContext>();
            if ((_ownerContext != null && !_ownerContext.IsLocalPlayer) || _pivot == null)
                return;

            // Preserve the authored vertical/side offset as the orbit focus,
            // then cast only along the camera arm. This catches the ground
            // when looking upward as well as walls and ceilings behind Player.
            var focusLocal = new Vector3(
                _desiredLocalPosition.x,
                _desiredLocalPosition.y,
                0f);
            var focusWorld = _pivot.TransformPoint(focusLocal);
            var desiredWorld = _pivot.TransformPoint(_desiredLocalPosition);
            var cameraArm = desiredWorld - focusWorld;
            var desiredDistance = cameraArm.magnitude;
            if (desiredDistance <= 0.0001f)
                return;

            var direction = cameraArm / desiredDistance;
            var targetDistance = FindUnobstructedDistance(
                focusWorld,
                direction,
                desiredDistance);
            // Pull in immediately so one fast look frame cannot cross the
            // surface. Only the unobstructed return is smoothed.
            _currentDistance = targetDistance < _currentDistance
                ? targetDistance
                : Mathf.MoveTowards(
                    _currentDistance,
                    targetDistance,
                    _returnSpeed * Time.unscaledDeltaTime);
            transform.position = focusWorld + direction * _currentDistance;
        }

        private float FindUnobstructedDistance(
            Vector3 origin,
            Vector3 direction,
            float desiredDistance)
        {
            var count = Physics.SphereCastNonAlloc(
                origin,
                _collisionRadius,
                direction,
                _hits,
                desiredDistance + _surfacePadding,
                _collisionLayers,
                QueryTriggerInteraction.Ignore);
            var nearest = desiredDistance;
            for (var index = 0; index < count; index++)
            {
                var collider = _hits[index].collider;
                if (collider == null || IsIgnored(collider.transform))
                    continue;

                nearest = Mathf.Min(
                    nearest,
                    Mathf.Max(0f, _hits[index].distance - _surfacePadding));
            }

            return nearest;
        }

        private bool IsIgnored(Transform candidate)
        {
            return _ignoredRoot != null
                && (candidate == _ignoredRoot || candidate.IsChildOf(_ignoredRoot));
        }

        private float GetDesiredDistance()
        {
            var cameraArm = new Vector3(0f, 0f, _desiredLocalPosition.z);
            return Mathf.Abs(cameraArm.z);
        }

        private void ResolveReferences()
        {
            _ownerContext ??= GetComponentInParent<LocalPlayerContext>();
            _pivot ??= transform.parent;
            _ignoredRoot ??= _ownerContext?.transform;
            if (_desiredLocalPosition.sqrMagnitude <= 0.0001f)
                _desiredLocalPosition = transform.localPosition;
        }

        private void Reset()
        {
            _pivot = transform.parent;
            _ignoredRoot = GetComponentInParent<LocalPlayerContext>()?.transform;
            _desiredLocalPosition = transform.localPosition;
        }

        private void OnValidate()
        {
            _collisionRadius = Mathf.Max(0.01f, _collisionRadius);
            _surfacePadding = Mathf.Max(0f, _surfacePadding);
            _returnSpeed = Mathf.Max(0.01f, _returnSpeed);
        }
    }
}
