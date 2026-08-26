using System;
using DemonViglu.FirePlay.Data;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Flame;
using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    public readonly struct SmallFirePlacementRequestResult
    {
        public bool Accepted { get; }
        public string Reason { get; }

        public SmallFirePlacementRequestResult(bool accepted, string reason)
        {
            Accepted = accepted;
            Reason = reason ?? string.Empty;
        }

        public static SmallFirePlacementRequestResult Accept(string reason) => new(true, reason);
        public static SmallFirePlacementRequestResult Reject(string reason) => new(false, reason);
    }

    /// <summary>
    /// Optional authority boundary for a locally controlled network Player.
    /// Preview and local candidate validation remain in CampfirePlacement.
    /// </summary>
    public interface ISmallFirePlacementRequestTransport
    {
        SmallFirePlacementRequestResult RequestSmallFirePlacement(Vector3 requestedPoint);
    }

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
        private ISmallFirePlacementRequestTransport _requestTransport;

        public bool IsPlacing { get; private set; }
        public bool IsPlacementValid { get; private set; }
        public string PlacementStatus { get; private set; } = "Idle";
        public int ActiveFireCount => SmallFire.ActiveCount;
        public int MaximumActiveFireCount => _config != null ? _config.MaximumActiveCount : 0;
        public bool HasRequiredSetup =>
            _resourceController != null
            && _config != null
            && _smallFirePrefab != null
            && _smallFirePrefab.gameObject.activeSelf;

        private void Awake()
        {
            if (_resourceController == null)
            {
                _resourceController = GetComponentInParent<FlameResourceController>();
            }

            _modeController ??= GetComponentInParent<PlayerModeController>();

            if (_placementCamera == null)
            {
                _placementCamera = Camera.main;
            }

            if (!HasRequiredSetup)
            {
                PlacementStatus = "Missing setup";
                Debug.LogError(
                    "[CampfirePlacement] 缺少余火控制器、小火种配置或有效 Prefab；SmallFire Prefab 根对象必须保持 Active，供单机与 NGO 使用。",
                    this);
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
            if (local == null || transform.root != local.transform.root || intent.PlayerId != local.PlayerId) return;
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
            var hasCameraHit = TryFindPlacementHit(ray, out var cameraHit);
            var cameraHitIsUsable = hasCameraHit
                && CampfireSiteValidator.TryValidate(
                    cameraHit,
                    transform.root.position,
                    _config,
                    out _);
            var fallbackHit = default(RaycastHit);
            var hasFallbackHit = !cameraHitIsUsable
                                 && TryFindDefaultPlacementHit(out fallbackHit);
            if (cameraHitIsUsable || hasFallbackHit)
            {
                _candidate = cameraHitIsUsable ? cameraHit : fallbackHit;
                IsPlacementValid = CampfireSiteValidator.TryValidate(
                    _candidate,
                    transform.root.position,
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

            if (_requestTransport != null)
            {
                var requestResult = _requestTransport.RequestSmallFirePlacement(_candidate.point);
                PlacementStatus = requestResult.Reason;
                if (!requestResult.Accepted)
                    return false;

                FinishPlacement(requestResult.Reason);
                return true;
            }

            if (!TryPreparePlacement(_candidate, transform.root.position, out var instance, out var reason))
            {
                PlacementStatus = reason;
                return false;
            }

            var stableId = instance.GetComponent<StableSceneId>();
            if (stableId == null || !stableId.TryAssignRuntimeSpawnValue($"smallfire.{Guid.NewGuid():N}"))
            {
                Destroy(instance.gameObject);
                PlacementStatus = "Stable ID assignment failed";
                return false;
            }

            if (!TryCommitPreparedPlacement(instance, out reason))
            {
                Destroy(instance.gameObject);
                PlacementStatus = reason;
                return false;
            }

            FinishPlacement("Placed");
            return true;
        }

        public void ConfigureRequestTransport(ISmallFirePlacementRequestTransport requestTransport)
        {
            _requestTransport = requestTransport;
        }

        /// <summary>
        /// The Host resolves the ground again from the authoritative Player pose.
        /// The uploaded point is only a request, never the final world transform.
        /// </summary>
        public bool TryPrepareAuthorityPlacement(
            Vector3 authorityPlayerPosition,
            Vector3 requestedPoint,
            out SmallFire instance,
            out string reason)
        {
            instance = null;
            if (!IsFinite(authorityPlayerPosition) || !IsFinite(requestedPoint))
            {
                reason = "Invalid placement coordinates";
                return false;
            }

            if (!TryFindAuthorityPlacementHit(requestedPoint, out var hit))
            {
                reason = "No valid ground near requested point";
                return false;
            }

            return TryPreparePlacement(hit, authorityPlayerPosition, out instance, out reason);
        }

        /// <summary>
        /// Completes the resource side of a prepared placement. Network callers
        /// publish identity and spawn immediately before this commit, then revoke
        /// the object if fuel cannot be committed.
        /// </summary>
        public bool TryCommitPreparedPlacement(SmallFire instance, out string reason)
        {
            if (instance == null || _resourceController == null || _config == null)
            {
                reason = "Placement authority is unavailable";
                return false;
            }

            if (!_resourceController.TryConsume(_config.FuelCost))
            {
                reason = "Not enough fuel";
                return false;
            }

            reason = "Placed";
            return true;
        }

        public void ApplyAuthorityPlacementResult(bool accepted, string reason)
        {
            PlacementStatus = string.IsNullOrWhiteSpace(reason)
                ? accepted ? "Placed" : "Placement rejected"
                : reason;
        }

        public void CancelPlacement()
        {
            IsPlacing = false;
            _modeController?.Exit(PlayerMode.Placing);
            IsPlacementValid = false;
            PlacementStatus = "Cancelled";
            SetPreviewVisible(false);
        }

        private bool TryPreparePlacement(
            RaycastHit hit,
            Vector3 authorityPlayerPosition,
            out SmallFire instance,
            out string reason)
        {
            instance = null;
            if (!HasRequiredSetup)
            {
                reason = "Missing setup";
                return false;
            }
            if (SmallFire.ActiveCount >= _config.MaximumActiveCount)
            {
                reason = "Fire limit reached";
                return false;
            }
            if (_resourceController.State == null || _resourceController.State.CurrentFuel < _config.FuelCost)
            {
                reason = "Not enough fuel";
                return false;
            }
            if (!CampfireSiteValidator.TryValidate(hit, authorityPlayerPosition, _config, out reason))
                return false;

            instance = Instantiate(_smallFirePrefab, hit.point, Quaternion.identity);
            // A placed fire is a live world object. An inactive authoring prefab
            // must never consume fuel without entering the active-fire registry.
            instance.gameObject.SetActive(true);
            instance.AlignToSurface(hit.point, hit.normal);
            instance.Initialize(_config);
            return true;
        }

        private bool TryFindAuthorityPlacementHit(Vector3 requestedPoint, out RaycastHit placementHit)
        {
            placementHit = default;
            if (_config == null)
                return false;

            const float verticalProbeOffset = 1.5f;
            const float verticalProbeDistance = 3f;
            const float maximumPointError = 0.75f;
            var origin = requestedPoint + Vector3.up * verticalProbeOffset;
            var hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                verticalProbeDistance,
                _config.PlacementLayers,
                QueryTriggerInteraction.Ignore);
            var closestSquaredDistance = maximumPointError * maximumPointError;

            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<CampfirePlacementSurface>() == null)
                    continue;

                var squaredDistance = (hit.point - requestedPoint).sqrMagnitude;
                if (squaredDistance > closestSquaredDistance)
                    continue;

                placementHit = hit;
                closestSquaredDistance = squaredDistance;
            }

            return placementHit.collider != null;
        }

        private void FinishPlacement(string status)
        {
            IsPlacing = false;
            _modeController?.Exit(PlayerMode.Placing);
            IsPlacementValid = false;
            PlacementStatus = string.IsNullOrWhiteSpace(status) ? "Placed" : status;
            SetPreviewVisible(false);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
            && !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private void UpdatePreview(RaycastHit hit)
        {
            if (_preview == null)
            {
                return;
            }

            _preview.SetPositionAndRotation(hit.point + hit.normal * 0.16f, Quaternion.FromToRotation(Vector3.up, hit.normal));
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

        private bool TryFindDefaultPlacementHit(out RaycastHit placementHit)
        {
            placementHit = default;
            if (_config == null)
                return false;

            var root = transform.root;
            var forward = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;

            var target = root.position
                         + forward * Mathf.Min(2.5f, _config.MaximumPlacementDistance * 0.6f);
            return TryFindPlacementHit(
                new Ray(target + Vector3.up * 3f, Vector3.down),
                out placementHit);
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
            _preview.localScale = Vector3.one * 1.75f;
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
                renderer.enabled = true;
                renderer.GetPropertyBlock(_previewProperties);
                _previewProperties.SetColor("_FlameColor", color);
                _previewProperties.SetColor("_CoreColor", Color.Lerp(Color.white, color, 0.35f));
                _previewProperties.SetColor("_BaseColor", color);
                _previewProperties.SetColor("_Color", color);
                _previewProperties.SetColor("_EmissionColor", color * (isValid ? 1.8f : 0.5f));
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
