using DemonViglu.FirePlay.Data;
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
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private Camera _placementCamera;
        [SerializeField] private SmallFireConfig _config;
        [SerializeField] private SmallFire _smallFirePrefab;
        [SerializeField] private Transform _preview;
        [SerializeField] private PlayerModeController _modeController;

        private Vector2 _screenPoint;
        private RaycastHit _candidate;

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

            if (_input == null)
            {
                _input = GetComponent<FirePlayPlayerInput>();
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

            SetPreviewVisible(false);
        }

        private void Update()
        {
            if (!IsPlacing && _config != null && PlacementStatus != "Missing setup")
            {
                PlacementStatus = SmallFire.ActiveCount >= _config.MaximumActiveCount
                    ? "Fire limit reached"
                    : "Ready";
            }

            if (_input != null && _input.PlaceFirePressedThisFrame && (_modeController == null || _modeController.IsExploring || IsPlacing))
            {
                if (IsPlacing)
                {
                    ConfirmPlacement();
                }
                else
                {
                    BeginPlacement();
                }
            }

            if (IsPlacing && _placementCamera != null)
            {
                UpdatePlacement(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
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
                return false;
            }

            if (SmallFire.ActiveCount >= _config.MaximumActiveCount ||
                _resourceController.State == null ||
                _resourceController.State.CurrentFuel < _config.FuelCost)
            {
                PlacementStatus = "Cannot place";
                return false;
            }

            // 已确认余额足够；TryConsume 仍负责最终写入状态。
            _resourceController.TryConsume(_config.FuelCost);

            var instance = Instantiate(_smallFirePrefab, _candidate.point, Quaternion.identity);
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

    }
}
