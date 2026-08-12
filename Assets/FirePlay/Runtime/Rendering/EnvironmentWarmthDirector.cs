using System.Collections.Generic;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    /// <summary>
    /// Scene-level, read-only sampler for environmental art. It reads authoritative
    /// fire state and distributes a compact spatial field to presentation receivers.
    /// It never consumes fuel or writes back to Campfire/SmallFire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentWarmthDirector : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] private float _sampleInterval = 0.15f;
        [SerializeField, Min(0.1f)] private float _smallFireRadius = 7.5f;
        [SerializeField, Min(0.1f)] private float _campfireBaseRadius = 11f;
        [SerializeField, Min(0f)] private float _campfireRadiusPerLevel = 2.2f;

        private static EnvironmentWarmthDirector _active;
        private static readonly List<IEnvironmentWarmthReceiver> PendingReceivers = new();
        private readonly List<IEnvironmentWarmthReceiver> _receivers = new();
        private readonly Vector4[] _sources = new Vector4[EnvironmentWarmthField.MaximumSourceCount];
        private readonly float[] _strengths = new float[EnvironmentWarmthField.MaximumSourceCount];
        private float _elapsed;

        public static void Register(IEnvironmentWarmthReceiver receiver)
        {
            if (receiver == null)
            {
                return;
            }

            if (_active != null && !_active._receivers.Contains(receiver))
            {
                _active._receivers.Add(receiver);
            }
            else if (_active == null && !PendingReceivers.Contains(receiver))
            {
                PendingReceivers.Add(receiver);
            }
        }

        public static void Unregister(IEnvironmentWarmthReceiver receiver)
        {
            if (receiver != null && _active != null)
            {
                _active._receivers.Remove(receiver);
            }

            PendingReceivers.Remove(receiver);
        }

        private void OnEnable()
        {
            if (_active != null && _active != this)
            {
                Debug.LogWarning("[EnvironmentWarmthDirector] Only one scene director should be active.", this);
            }

            _active = this;
            DiscoverReceivers();
            foreach (var receiver in PendingReceivers)
            {
                if (!_receivers.Contains(receiver))
                {
                    _receivers.Add(receiver);
                }
            }
            PendingReceivers.Clear();
            SampleAndDistribute(_sampleInterval);
        }

        private void OnDisable()
        {
            if (_active == this)
            {
                var emptyField = new EnvironmentWarmthField(_sources, _strengths, 0);
                Distribute(emptyField, _sampleInterval);
                _active = null;
            }
        }

        private void Start()
        {
            if (_active == this)
            {
                DiscoverReceivers();
                SampleAndDistribute(_sampleInterval);
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed < _sampleInterval)
            {
                return;
            }

            var deltaTime = _elapsed;
            _elapsed = 0f;
            SampleAndDistribute(deltaTime);
        }

        private void SampleAndDistribute(float deltaTime)
        {
            var count = BuildField();
            Distribute(new EnvironmentWarmthField(_sources, _strengths, count), deltaTime);
        }

        private int BuildField()
        {
            var count = 0;
            foreach (var campfire in Campfire.ActiveInstances)
            {
                if (campfire == null || campfire.IsRetired || campfire.IsExtinguished)
                {
                    continue;
                }

                var normalizedWarmth = campfire.NormalizedWarmth;
                var strength = Mathf.Clamp01(normalizedWarmth * (0.72f + campfire.Level * 0.09f));
                var radius = _campfireBaseRadius + campfire.Level * _campfireRadiusPerLevel;
                AddSource(ref count, campfire.transform.position, radius, strength);
            }

            foreach (var smallFire in SmallFire.ActiveInstances)
            {
                if (smallFire == null || !smallFire.IsInitialized || smallFire.RemainingSeconds <= 0f)
                {
                    continue;
                }

                var strength = Mathf.Clamp01(smallFire.RemainingSeconds / 3f) * 0.72f;
                AddSource(ref count, smallFire.transform.position, _smallFireRadius, strength);
            }

            for (var index = count; index < EnvironmentWarmthField.MaximumSourceCount; index++)
            {
                _sources[index] = Vector4.zero;
                _strengths[index] = 0f;
            }

            return count;
        }

        private void AddSource(ref int count, Vector3 position, float radius, float strength)
        {
            if (count >= EnvironmentWarmthField.MaximumSourceCount || strength <= 0.001f)
            {
                return;
            }

            _sources[count] = new Vector4(position.x, position.y, position.z, radius);
            _strengths[count] = strength;
            count++;
        }

        private void Distribute(EnvironmentWarmthField field, float deltaTime)
        {
            for (var index = _receivers.Count - 1; index >= 0; index--)
            {
                var receiver = _receivers[index];
                if (receiver is Object unityObject && unityObject == null)
                {
                    _receivers.RemoveAt(index);
                    continue;
                }

                receiver.ApplyEnvironmentWarmth(field, deltaTime);
            }
        }

        private void DiscoverReceivers()
        {
            _receivers.Clear();
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            foreach (var behaviour in behaviours)
            {
                if (behaviour is IEnvironmentWarmthReceiver receiver && !_receivers.Contains(receiver))
                {
                    _receivers.Add(receiver);
                }
            }
        }

        private void OnValidate()
        {
            _sampleInterval = Mathf.Max(0.05f, _sampleInterval);
            _smallFireRadius = Mathf.Max(0.1f, _smallFireRadius);
            _campfireBaseRadius = Mathf.Max(0.1f, _campfireBaseRadius);
            _campfireRadiusPerLevel = Mathf.Max(0f, _campfireRadiusPerLevel);
        }
    }
}
