using System.Collections.Generic;
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

        private readonly List<SmallFire> _activeFires = new();
        private Vector2 _screenPoint;
        private RaycastHit _candidate;

        public bool IsPlacing { get; private set; }
        public bool IsPlacementValid { get; private set; }
        public string PlacementStatus { get; private set; } = "Idle";

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

            if (_placementCamera == null)
            {
                _placementCamera = Camera.main;
            }

            SetPreviewVisible(false);
        }

        private void Update()
        {
            if (_input != null && _input.PlaceFirePressedThisFrame)
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
            if (_config == null || _smallFirePrefab == null || _resourceController == null)
            {
                PlacementStatus = "Missing setup";
                return false;
            }

            CleanupExpiredFires();
            if (_activeFires.Count >= _config.MaximumActiveCount)
            {
                PlacementStatus = "Fire limit reached";
                return false;
            }

            if (_resourceController.State == null || _resourceController.State.CurrentFuel < _config.FuelCost)
            {
                PlacementStatus = "Not enough fuel";
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
            if (Physics.Raycast(ray, out _candidate, 100f, _config.PlacementLayers,
                    QueryTriggerInteraction.Ignore))
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

            CleanupExpiredFires();
            if (_activeFires.Count >= _config.MaximumActiveCount || !_resourceController.TryConsume(_config.FuelCost))
            {
                PlacementStatus = "Cannot place";
                return false;
            }

            var rotation = Quaternion.FromToRotation(Vector3.up, _candidate.normal);
            var instance = Instantiate(_smallFirePrefab, _candidate.point + _candidate.normal * 0.02f, rotation);
            instance.Initialize(_config.DurationSeconds);
            _activeFires.Add(instance);

            IsPlacing = false;
            IsPlacementValid = false;
            PlacementStatus = "Placed";
            SetPreviewVisible(false);
            return true;
        }

        public void CancelPlacement()
        {
            IsPlacing = false;
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

        private void SetPreviewVisible(bool visible)
        {
            if (_preview != null && _preview.gameObject.activeSelf != visible)
            {
                _preview.gameObject.SetActive(visible);
            }
        }

        private void CleanupExpiredFires()
        {
            _activeFires.RemoveAll(fire => fire == null);
        }
    }
}
