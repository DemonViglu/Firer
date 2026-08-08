using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Generic radial activity wheel. It only displays reusable definitions
    /// and publishes semantic selection requests; it contains no concrete
    /// activity branches.
    /// </summary>
    public sealed class ActivitySelectionForms : BaseUIForms
    {
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Transform _buttonRoot;
        [SerializeField] private Button _buttonTemplate;
        [SerializeField] private Button _closeButton;
        [SerializeField, Min(0.1f)] private float _anchorSearchDistance = 3f;
        [SerializeField, Min(0.05f)] private float _refreshInterval = 0.15f;
        [SerializeField, Min(40f)] private float _wheelRadius = 130f;
        [SerializeField] private Color _anchorButtonColor = new(0.44f, 0.25f, 0.09f, 1f);
        [SerializeField] private Color _anywhereButtonColor = new(0.12f, 0.3f, 0.38f, 1f);

        private readonly List<GameObject> _buttonPool = new();
        private PlayerActivityHost _host;
        private ActivityAnchorNode _anchor;
        private ActivityAnchorDiscovery _anchorDiscovery;
        private IEventPublisher _events;
        private float _nextRefreshAt;
        private string _pendingActivityId = string.Empty;
        private bool _eventsBound;

        private void Awake()
        {
            _titleText ??= FindText("Title");
            _statusText ??= FindText("Status");
            _buttonRoot ??= FindTransform("Buttons");
            _buttonTemplate ??= FindButton("ActivityButtonTemplate");
            _closeButton ??= FindButton("CloseButton");
            _anchorDiscovery = new ActivityAnchorDiscovery(_anchorSearchDistance);
        }

        public override void Display()
        {
            base.Display();
            transform.SetAsLastSibling();
            ResolveRuntime();
            BindEvents();
            RefreshNearbyAnchor(true);
            RebuildEntries();
            _nextRefreshAt = Time.unscaledTime + _refreshInterval;
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            if (Time.unscaledTime < _nextRefreshAt) return;
            _nextRefreshAt = Time.unscaledTime + _refreshInterval;

            var hostChanged = ResolveRuntime();
            var anchorChanged = RefreshNearbyAnchor(hostChanged);
            if (hostChanged || anchorChanged)
                RebuildEntries();
        }

        public override void Hiding()
        {
            HideButtons();
            _closeButton?.onClick.RemoveListener(CloseSelection);
            UnbindEvents();
            _pendingActivityId = string.Empty;
            base.Hiding();
        }

        private bool ResolveRuntime()
        {
            var localHost = PlayerActivityHost.Local;
            var changed = !ReferenceEquals(_host, localHost);
            if (changed)
            {
                _host = localHost;
                _anchor = null;
                _anchorDiscovery = new ActivityAnchorDiscovery(_anchorSearchDistance);
            }

            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            if (gameObject.activeInHierarchy) BindEvents();
            return changed;
        }

        private bool RefreshNearbyAnchor(bool force)
        {
            if (_host == null)
            {
                var anchorWasAvailable = _anchor != null;
                _anchor = null;
                return force || anchorWasAvailable;
            }
            _anchorDiscovery ??= new ActivityAnchorDiscovery(_anchorSearchDistance);
            var changed = _anchorDiscovery.Refresh(_host.transform.position);
            if (!changed && !force) return false;

            _anchor = _anchorDiscovery.Current;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ActivitySelectionForms] 附近 Anchor 刷新：{(_anchor != null ? _anchor.AnchorId : "<none>")}", this);
#endif
            return true;
        }

        private void RebuildEntries()
        {
            _closeButton?.onClick.RemoveListener(CloseSelection);
            _closeButton?.onClick.AddListener(CloseSelection);

            if (_titleText != null)
                _titleText.text = _anchor != null ? _anchor.DisplayName : "随时可做";

            if (_host == null)
            {
                HideButtons();
                SetStatus("正在寻找本地玩家……");
                return;
            }

            var entries = CollectEntries();
            if (entries.Count == 0)
            {
                HideButtons();
                SetStatus("当前没有可用活动");
                return;
            }

            SetStatus(_anchor != null ? "选择一个活动" : "选择一个随时可用的活动");
            if (_buttonRoot == null || _buttonTemplate == null)
            {
                SetStatus("活动选择 UI 未配置按钮模板");
                return;
            }

            EnsureButtonPool(entries.Count);
            for (var index = 0; index < _buttonPool.Count; index++)
            {
                if (index >= entries.Count)
                {
                    _buttonPool[index].SetActive(false);
                    continue;
                }

                var entry = entries[index];
                var buttonObject = _buttonPool[index];
                buttonObject.name = $"ActivityButton_{entry.ActivityId}";
                buttonObject.SetActive(true);

                if (buttonObject.transform is RectTransform buttonRect)
                {
                    buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
                    buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
                    buttonRect.pivot = new Vector2(0.5f, 0.5f);
                    buttonRect.anchoredPosition = GetWheelPosition(index, entries.Count);
                }

                var button = buttonObject.GetComponent<Button>();
                var label = buttonObject.GetComponentInChildren<Text>(true);
                if (label != null) label.text = entry.DisplayName;
                if (button == null) continue;

                if (button.targetGraphic is Graphic graphic)
                    graphic.color = entry.IsAnywhere ? _anywhereButtonColor : _anchorButtonColor;

                var captured = entry;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => RequestSelection(captured));
                button.interactable = string.IsNullOrEmpty(_pendingActivityId);
            }
        }

        private void EnsureButtonPool(int count)
        {
            if (_buttonRoot == null || _buttonTemplate == null) return;
            while (_buttonPool.Count < count)
            {
                var buttonObject = Instantiate(_buttonTemplate.gameObject, _buttonRoot);
                buttonObject.SetActive(false);
                _buttonPool.Add(buttonObject);
            }
            _buttonTemplate.gameObject.SetActive(false);
        }

        private Vector2 GetWheelPosition(int index, int count)
        {
            if (count <= 1) return new Vector2(0f, -85f);

            var step = 360f / count;
            var angle = 90f - step * 0.5f - index * step;
            var radians = angle * Mathf.Deg2Rad;
            var radius = _wheelRadius + Mathf.Max(0, count - 5) * 8f;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * radius;
        }

        private List<SelectionEntry> CollectEntries()
        {
            var entries = new List<SelectionEntry>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            if (_anchor != null)
            {
                foreach (var asset in _anchor.Activities)
                {
                    if (asset == null || string.IsNullOrWhiteSpace(asset.ActivityId) || !ids.Add(asset.ActivityId))
                        continue;
                    entries.Add(new SelectionEntry(
                        asset.ActivityId,
                        string.IsNullOrWhiteSpace(asset.DisplayName) ? asset.ActivityId : asset.DisplayName,
                        _anchor.AnchorId,
                        isAnywhere: false));
                }
            }

            if (_host?.Catalog != null)
            {
                foreach (var definition in _host.Catalog.Definitions)
                {
                    if (definition == null || definition.Scope != ActivityScope.Anywhere || !ids.Add(definition.ActivityId))
                        continue;
                    entries.Add(new SelectionEntry(
                        definition.ActivityId,
                        string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.ActivityId : definition.DisplayName,
                        string.Empty,
                        isAnywhere: true));
                }
            }

            return entries;
        }

        private void RequestSelection(SelectionEntry entry)
        {
            if (_host == null || _events == null) return;

            _pendingActivityId = entry.ActivityId;
            SetButtonsInteractable(false);
            SetStatus($"正在开始「{entry.DisplayName}」……");

            _events.Publish(new ActivitySelectionRequested(
                _host.PlayerId,
                entry.AnchorId,
                entry.ActivityId));
        }

        private void OnSelectionResolved(ActivitySelectionResolved result)
        {
            if (result == null
                || _host == null
                || result.PlayerId != _host.PlayerId
                || result.ActivityId != _pendingActivityId)
            {
                return;
            }

            _pendingActivityId = string.Empty;
            if (result.Accepted)
            {
                CloseSelection();
                return;
            }

            SetButtonsInteractable(true);
            SetStatus($"暂时不能开始：{FormatReason(result.Reason)}");
        }

        private void CloseSelection()
        {
            UIManager.TryGetInstance()?.CloseOrReturnUIForms("ActivitySelectionForms");
        }

        private void HideButtons()
        {
            foreach (var button in _buttonPool)
                if (button != null) button.SetActive(false);
            if (_buttonTemplate != null) _buttonTemplate.gameObject.SetActive(false);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            foreach (var buttonObject in _buttonPool)
            {
                if (buttonObject != null
                    && buttonObject.activeSelf
                    && buttonObject.TryGetComponent<Button>(out var button))
                {
                    button.interactable = interactable;
                }
            }
        }

        private void BindEvents()
        {
            if (_eventsBound || _events == null) return;
            _events.Subscribe<ActivitySelectionResolved>(OnSelectionResolved);
            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound || _events == null) return;
            _events.Unsubscribe<ActivitySelectionResolved>(OnSelectionResolved);
            _eventsBound = false;
        }

        private static string FormatReason(string reason)
        {
            return reason switch
            {
                "No activity anchor is in range" => "已经离开活动地点",
                "Activity anchor is out of range" => "距离活动地点太远",
                "Anchor does not provide this activity" => "这个地点没有提供该活动",
                "PlayerActivityHost is not ready" => "活动系统尚未就绪",
                "This activity needs an anchor" => "该活动需要一个地点",
                "This activity needs an available target" => "该活动需要一个可用伙伴",
                "Fishing needs a flame resource" => "钓鱼需要可用的余火",
                "Stargazing requires a resting player" => "请先在这里坐下",
                _ => string.IsNullOrWhiteSpace(reason) ? "条件尚未满足" : reason
            };
        }

        private void SetStatus(string value)
        {
            if (_statusText != null) _statusText.text = value ?? string.Empty;
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        private Text FindText(string childName)
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
                if (text.gameObject.name == childName) return text;
            return null;
        }

        private Button FindButton(string childName)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
                if (button.gameObject.name == childName) return button;
            return null;
        }

        private Transform FindTransform(string childName)
        {
            foreach (var transform in GetComponentsInChildren<Transform>(true))
                if (transform.gameObject.name == childName) return transform;
            return null;
        }

        private readonly struct SelectionEntry
        {
            public string ActivityId { get; }
            public string DisplayName { get; }
            public string AnchorId { get; }
            public bool IsAnywhere { get; }

            public SelectionEntry(string activityId, string displayName, string anchorId, bool isAnywhere)
            {
                ActivityId = activityId;
                DisplayName = displayName;
                AnchorId = anchorId;
                IsAnywhere = isAnywhere;
            }
        }
    }
}
