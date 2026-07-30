using System.Collections.Generic;
using DemonViglu.FirePlay.Data;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Core;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 临时小火种生命周期。表现组件均为可选，状态不依赖特定素材。
    /// </summary>
    [RequireComponent(typeof(StableSceneId))]
    public sealed class SmallFire : MonoBehaviour
    {
        private static readonly List<SmallFire> ActiveFires = new();

        [SerializeField] private Light _fireLight;
        [SerializeField] private ParticleSystem _fireVfx;
        [SerializeField, Min(0f)] private float _visualFlameIntensity = 1.35f;

        private float _remainingSeconds;
        private float _initialLightIntensity;
        [SerializeField] private SmallFireConfig _config;
        private bool _initialized;
        private Renderer[] _visualRenderers;
        private MaterialPropertyBlock _visualProperties;

        public static int ActiveCount
        {
            get
            {
                CleanupDestroyedFires();
                return ActiveFires.Count;
            }
        }

        public bool IsInitialized => _initialized;
        public float RemainingSeconds => Mathf.Max(0f, _remainingSeconds);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveFires()
        {
            ActiveFires.Clear();
        }

        public static SmallFire FindNearest(Vector3 position, float maximumDistance, out float squaredDistance)
        {
            SmallFire nearest = null;
            squaredDistance = maximumDistance * maximumDistance;

            CleanupDestroyedFires();
            for (var index = ActiveFires.Count - 1; index >= 0; index--)
            {
                var fire = ActiveFires[index];
                var distance = (fire.transform.position - position).sqrMagnitude;
                if (distance <= squaredDistance)
                {
                    nearest = fire;
                    squaredDistance = distance;
                }
            }

            return nearest;
        }

        private void OnEnable()
        {
            if (!ActiveFires.Contains(this))
            {
                ActiveFires.Add(this);
            }
        }

        private void Awake()
        {
            // SmallFire prefabs are authored by artists and often leave these
            // optional references blank. Resolve child effects once so a placed
            // fire remains readable without depending on Inspector wiring.
            _fireLight ??= GetComponentInChildren<Light>(true);
            _fireVfx ??= GetComponentInChildren<ParticleSystem>(true);
            _visualRenderers = GetComponentsInChildren<Renderer>(true);
            _visualProperties = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            ActiveFires.Remove(this);
        }

        private void Start()
        {
            if (!_initialized && _config != null)
            {
                Initialize(_config);
            }
            else if (!_initialized)
            {
                Debug.LogWarning("[SmallFire] 未指定 SmallFireConfig，火种不会自动熄灭。", this);
            }
        }

        private static void CleanupDestroyedFires()
        {
            ActiveFires.RemoveAll(fire => fire == null);
        }

        public void Initialize(SmallFireConfig config)
        {
            if (config == null)
            {
                Debug.LogError("[SmallFire] Initialize 需要 SmallFireConfig。", this);
                return;
            }

            _config = config;
            _remainingSeconds = Mathf.Max(0.01f, _config.DurationSeconds);
            _initialized = true;
            if (_fireLight != null)
            {
                _initialLightIntensity = _fireLight.intensity;
            }

            _fireVfx?.Play(true);
            ApplyVisualReadability();
        }

        public bool TryReclaim(FlameResourceController resourceController)
        {
            if (resourceController == null || _config == null)
            {
                return false;
            }

            resourceController.Restore(_config.ReclaimFuel);
            Destroy(gameObject);
            return true;
        }

        public void AlignToSurface(Vector3 surfacePoint, Vector3 surfaceNormal)
        {
            transform.SetPositionAndRotation(surfacePoint, Quaternion.FromToRotation(Vector3.up, surfaceNormal));

            var colliders = GetComponentsInChildren<Collider>();
            if (colliders.Length == 0)
            {
                return;
            }

            var lowestProjection = float.PositiveInfinity;
            foreach (var targetCollider in colliders)
            {
                var bounds = targetCollider.bounds;
                var extentAlongNormal =
                    Mathf.Abs(surfaceNormal.x) * bounds.extents.x +
                    Mathf.Abs(surfaceNormal.y) * bounds.extents.y +
                    Mathf.Abs(surfaceNormal.z) * bounds.extents.z;
                lowestProjection = Mathf.Min(
                    lowestProjection,
                    Vector3.Dot(bounds.center, surfaceNormal) - extentAlongNormal);
            }

            var targetProjection = Vector3.Dot(surfacePoint, surfaceNormal) + 0.02f;
            transform.position += surfaceNormal * (targetProjection - lowestProjection);
        }

        private void Update()
        {
            if (_remainingSeconds <= 0f)
            {
                return;
            }

            _remainingSeconds -= Time.deltaTime;
            if (_fireLight != null)
            {
                _fireLight.intensity = _initialLightIntensity * Mathf.Clamp01(_remainingSeconds / 3f);
            }

            if (_remainingSeconds <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void ApplyVisualReadability()
        {
            if (_visualRenderers == null)
            {
                return;
            }

            foreach (var renderer in _visualRenderers)
            {
                renderer.GetPropertyBlock(_visualProperties);
                _visualProperties.SetFloat("_FlameIntensity", _visualFlameIntensity);
                renderer.SetPropertyBlock(_visualProperties);
            }
        }
    }
}
