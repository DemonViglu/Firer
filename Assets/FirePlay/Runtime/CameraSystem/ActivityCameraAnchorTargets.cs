using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.CameraSystem
{
    /// <summary>
    /// Optional per-anchor camera composition. ActivityCameraRig remains the
    /// sole Cinemachine owner, while a location can provide semantic targets
    /// such as a sky marker or a companion placeholder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityCameraAnchorTargets : MonoBehaviour
    {
        [SerializeField] private Transform _lookTarget;
        [SerializeField] private ActivityCameraTargetEntry[] _additionalTargets = Array.Empty<ActivityCameraTargetEntry>();

        public Transform LookTarget => _lookTarget;
        public IReadOnlyList<ActivityCameraTargetEntry> AdditionalTargets =>
            _additionalTargets ?? Array.Empty<ActivityCameraTargetEntry>();

        private void OnValidate()
        {
            if (_additionalTargets == null)
                _additionalTargets = Array.Empty<ActivityCameraTargetEntry>();
        }
    }

    [Serializable]
    public sealed class ActivityCameraTargetEntry
    {
        [SerializeField] private Transform _target;
        [SerializeField, Min(0f)] private float _weight = 1f;
        [SerializeField, Min(0f)] private float _radius = 0.3f;

        public Transform Target => _target;
        public float Weight => _weight;
        public float Radius => _radius;
    }
}
