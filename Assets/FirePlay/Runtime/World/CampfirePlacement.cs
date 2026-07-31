using System;
using DemonViglu.FirePlay.Data;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 小火种放置状态机。公开方法可由移动端 UI/触摸调用，键盘仅作原型验证。
    /// </summary>
    public sealed class CampfirePlacement : MonoBehaviour
    {
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private Camera _placementCamera;
        [SerializeField] private SmallFireConfig _config;
        [SerializeField] private SmallFire _smallFirePrefab;
        [SerializeField] private Transform _preview;
        [SerializeField] private PlayerModeController _modeController;
        [Header("Runtime preview")]
        [SerializeField] private bool _createPreviewWhenMissing = true;
        [SerializeField] private Color _validPreviewColor = new(1f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color _invalidPreviewColor = new(0.9f, 0.18f, 0.08f, 1f);

        private Vector2 _screenPoint;
        private RaycastHit _candidate;
        private bool _ownsRuntimePreview;
        private Renderer[] _previewRenderers;
        private MaterialPropertyBlock _previewProperties;
        private IEventPublisher _events;

        public bool IsPlacing { get; private set; }
        public bool IsPlacementValid { get; private set; }
        public string PlacementStatus { get; private set; } = "Idle";
        public int ActiveFireCount => SmallFire.ActiveCount;
        public int MaximumActiveFireCount => _config != null ? _config.MaximumActiveCount : 0;
        public bool HasRequiredSetup => _resourceController != null && _config != null && _smallFirePrefab != null;

        private void Awake()
        {
            if (_resourceController == null)
            {
                _resourceController = GetComponent<FlameResourceController>();
            }

            _modeController ??= GetComponent<PlayerModeController>();

            if (_placementCamera == null)
            {
                _placementCamera = Camera.main;
            }

            if (!HasRequiredSetup)
            {
                PlacementStatus = "Missing setup";
                Debug.LogError("[CampfirePlacement] 缺少余火控制器、小火种配置或小火种 Prefab。", this);
            }

            CreateRuntimePreviewIfNeeded();
            SetPreviewVisible(false);
        }

        private void OnEnable()
        {
            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _events.Subscribe<PlayerIntentRequested>(OnIntentRequested);
        }

        private void OnDisable()
        {
            _events?.Unsubscribe<PlayerIntentRequested>(OnIntentRequested);
            if (IsPlacing) CancelPlacement();
        }

        private void Update()
        {
            if (!IsPlacing && _config != null && PlacementStatus != "Missing setup")
            {
                PlacementStatus = SmallFire.ActiveCount >= _config.MaximumActiveCount
                    ? "Fire limit reached"
                    : "Ready";
            }

            if (IsPlacing && _placementCamera != null)
            {
                UpdatePlacement(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }
        }

        private void OnIntentRequested(PlayerIntentRequested intent)
        {
            var local = LocalPlayerContext.Current;
            if (local == null || local.gameObject != gameObject || intent.PlayerId != local.PlayerId) return;
            if (intent.Kind == PlayerIntentKind.PlaceFire && (_modeController == null || _modeController.IsExploring || IsPlacing))
            {
                if (IsPlacing)
                {
                    if (!ConfirmPlacement()) CancelPlacement();
                }
                else BeginPlacement();
            }
            else if (IsPlacing && intent.Kind is PlayerIntentKind.Pause or PlayerIntentKind.CancelPlacement)
            {
                CancelPlacement();
            }
        }

        public bool BeginPlacement()
        {
            if (!HasRequiredSetup)
            {
                PlacementStatus = "Missing setup";
                return false;
            }

            if (SmallFire.ActiveCount >= _config.MaximumActiveCount)
            {
                PlacementStatus = "Fire limit reached";
                return false;
            }

            if (_resourceController.State == null || _resourceController.State.CurrentFuel < _config.FuelCost)
            {
                PlacementStatus = "Not enough fuel";
                return false;
            }

            if (_modeController != null && !_modeController.TryEnter(PlayerMode.Placing))
            {
                PlacementStatus = "Unavailable in current mode";
                return false;
            }
            IsPlacing = true;
            PlacementStatus = "Find ground";
            return true;
        }

        public void UpdatePlacement(Vector2 screenPoint)
        {
            if (!IsPlacing || _placementCamera == null)
            {
                return;
            }

            _screenPoint = screenPoint;
            var ray = _placementCamera.ScreenPointToRay(_screenPoint);
            if (TryFindPlacementHit(ray, out _candidate))
            {
                IsPlacementValid = CampfireSiteValidator.TryValidate(
                    _candidate,
                    transform.position,
                    _config,
                    out var reason);
                PlacementStatus = reason;
                UpdatePreview(_candidate);
                return;
            }

            IsPlacementValid = false;
            PlacementStatus = "No ground";
            SetPreviewVisible(false);
        }

        public bool ConfirmPlacement()
        {
            if (!IsPlacing || !IsPlacementValid || _resourceController == null || _config == null)
            {
                PlacementStatus = "Cannot place";
                return false;
            }

            if (SmallFire.ActiveCount >= _config.MaximumActiveCount ||
                _resourceController.State == null ||
                _resourceController.State.CurrentFuel < _config.FuelCost)
            {
                PlacementStatus = "Cannot place";
                return false;
            }

            var instance = Instantiate(_smallFirePrefab, _candidate.point, Quaternion.identity);
            // A placed fire is a live world object. Keep this invariant here as
            // well as in the prefab so an accidentally inactive authoring prefab
            // cannot silently consume fuel without registering in ActiveCount.
            instance.gameObject.SetActive(true);
            var stableId = instance.GetComponent<StableSceneId>() ?? instance.gameObject.AddComponent<StableSceneId>();
            if (!stableId.TryAssignRuntimeSpawnValue($"smallfire.{Guid.NewGuid():N}"))
            {
                Destroy(instance.gameObject);
                PlacementStatus = "Stable ID assignment failed";
                return false;
            }
            // Stable ID 已就绪后才写入资源，避免生成失败却扣除余火。
            if (!_resourceController.TryConsume(_config.FuelCost))
            {
                Destroy(instance.gameObject);
                PlacementStatus = "Not enough fuel";
                return false;
            }
            instance.AlignToSurface(_candidate.point, _candidate.normal);
            instance.Initialize(_config);

            IsPlacing = false;
            _modeController?.Exit(PlayerMode.Placing);
            IsPlacementValid = false;
            PlacementStatus = "Placed";
            SetPreviewVisible(false);
            return true;
        }

        public void CancelPlacement()
        {
            IsPlacing = false;
            _modeController?.Exit(PlayerMode.Placing);
            IsPlacementValid = false;
            PlacementStatus = "Cancelled";
            SetPreviewVisible(false);
        }

        private void UpdatePreview(RaycastHit hit)
        {
            if (_preview == null)
            {
                return;
            }

            _preview.SetPositionAndRotation(hit.point + hit.normal * 0.02f, Quaternion.FromToRotation(Vector3.up, hit.normal));
            UpdatePreviewColor(IsPlacementValid);
            SetPreviewVisible(true);
        }

        private bool TryFindPlacementHit(Ray ray, out RaycastHit placementHit)
        {
            placementHit = default;
            var hits = Physics.RaycastAll(ray, 100f, _config.PlacementLayers, QueryTriggerInteraction.Ignore);
            var closestDistance = float.PositiveInfinity;

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<CampfirePlacementSurface>() == null || hit.distance >= closestDistance)
                {
                    continue;
                }

                placementHit = hit;
                closestDistance = hit.distance;
            }

            return closestDistance < float.PositiveInfinity;
        }

        private void SetPreviewVisible(bool visible)
        {
            if (_preview != null && _preview.gameObject.activeSelf != visible)
            {
                _preview.gameObject.SetActive(visible);
            }
        }

        private void CreateRuntimePreviewIfNeeded()
        {
            if (_preview != null || !_createPreviewWhenMissing || _smallFirePrefab == null)
            {
                return;
            }

            var previewFire = Instantiate(_smallFirePrefab);
            previewFire.name = "SmallFirePreview (Runtime)";
            previewFire.enabled = false;
            foreach (var collider in previewFire.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            foreach (var light in previewFire.GetComponentsInChildren<Light>(true))
            {
                light.enabled = false;
            }

            _preview = previewFire.transform;
            _previewRenderers = previewFire.GetComponentsInChildren<Renderer>(true);
            _previewProperties = new MaterialPropertyBlock();
            _ownsRuntimePreview = true;
        }

        private void UpdatePreviewColor(bool isValid)
        {
            if (_previewRenderers == null)
            {
                return;
            }

            var color = isValid ? _validPreviewColor : _invalidPreviewColor;
            foreach (var renderer in _previewRenderers)
            {
                renderer.GetPropertyBlock(_previewProperties);
                _previewProperties.SetColor("_FlameColor", color);
                _previewProperties.SetColor("_CoreColor", Color.Lerp(Color.white, color, 0.35f));
                _previewProperties.SetFloat("_FlameIntensity", isValid ? 1.8f : 0.55f);
                renderer.SetPropertyBlock(_previewProperties);
            }
        }

        private void OnDestroy()
        {
            if (_ownsRuntimePreview && _preview != null)
            {
                Destroy(_preview.gameObject);
            }
        }

    }
}
