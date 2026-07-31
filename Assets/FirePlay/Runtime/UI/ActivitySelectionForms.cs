using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Generic activity selection surface. It only displays reusable
    /// definitions and publishes a semantic selection request; it contains
    /// no marshmallow/fishing/guitar branches and can later be skinned as a
    /// radial wheel without changing the Activity domain.
    /// </summary>
    public sealed class ActivitySelectionForms : BaseUIForms
    {
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _statusText;
        [SerializeField] private Transform _buttonRoot;
        [SerializeField] private Button _buttonTemplate;
        [SerializeField] private Button _closeButton;
        [SerializeField, Min(0.1f)] private float _anchorSearchDistance = 3f;

        private readonly List<GameObject> _createdButtons = new();
        private PlayerActivityHost _host;
        private ActivityAnchorNode _anchor;
        private ActivityAnchorDiscovery _anchorDiscovery;
        private IEventPublisher _events;

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
            RefreshNearbyAnchor(true);
            RebuildEntries();
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy) return;

            ResolveRuntime();
            if (RefreshNearbyAnchor(false))
                RebuildEntries();
        }

        public override void Hiding()
        {
            ClearButtons();
            _closeButton?.onClick.RemoveListener(CloseSelection);
            base.Hiding();
        }

        private void ResolveRuntime()
        {
            _host ??= PlayerActivityHost.Local;
            _host ??= FindAnyObjectByType<PlayerActivityHost>();
            if (_host == null) return;

            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
        }

        private bool RefreshNearbyAnchor(bool force)
        {
            if (_host == null) return false;
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
            ClearButtons();
            _closeButton?.onClick.RemoveListener(CloseSelection);
            _closeButton?.onClick.AddListener(CloseSelection);

            if (_titleText != null)
                _titleText.text = _anchor != null ? $"活动 · {_anchor.AnchorId}" : "活动";

            var entries = CollectEntries();
            if (entries.Count == 0)
            {
                SetStatus("当前没有可用活动");
                return;
            }

            SetStatus(_anchor != null ? "选择一个活动" : "选择一个随时可用的活动");
            if (_buttonRoot == null || _buttonTemplate == null)
            {
                SetStatus("活动选择 UI 未配置按钮模板");
                return;
            }

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var buttonObject = Instantiate(_buttonTemplate.gameObject, _buttonRoot);
                buttonObject.name = $"ActivityButton_{entry.ActivityId}";
                buttonObject.SetActive(true);
                _createdButtons.Add(buttonObject);

                if (buttonObject.transform is RectTransform buttonRect)
                {
                    buttonRect.anchorMin = new Vector2(0.5f, 1f);
                    buttonRect.anchorMax = new Vector2(0.5f, 1f);
                    buttonRect.pivot = new Vector2(0.5f, 1f);
                    buttonRect.anchoredPosition = new Vector2(0f, -index * 42f);
                }

                var button = buttonObject.GetComponent<Button>();
                var label = buttonObject.GetComponentInChildren<Text>(true);
                if (label != null) label.text = entry.DisplayName;
                if (button == null) continue;

                var captured = entry;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => RequestSelection(captured));
            }
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
                    entries.Add(new SelectionEntry(asset.ActivityId, string.IsNullOrWhiteSpace(asset.DisplayName) ? asset.ActivityId : asset.DisplayName, _anchor.AnchorId));
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
                        string.Empty));
                }
            }

            return entries;
        }

        private void RequestSelection(SelectionEntry entry)
        {
            if (_host == null || _events == null) return;

            _events.Publish(new ActivitySelectionRequested(
                _host.PlayerId,
                entry.AnchorId,
                entry.ActivityId));

            if (_host.HasActiveActivity && _host.ActiveActivityId == entry.ActivityId)
                CloseSelection();
            else
                SetStatus($"无法开始：{entry.DisplayName}");
        }

        private void CloseSelection()
        {
            UIManager.TryGetInstance()?.CloseOrReturnUIForms("ActivitySelectionForms");
        }

        private void ClearButtons()
        {
            foreach (var button in _createdButtons)
            {
                if (button != null) Destroy(button);
            }
            _createdButtons.Clear();
            if (_buttonTemplate != null) _buttonTemplate.gameObject.SetActive(false);
        }

        private void SetStatus(string value)
        {
            if (_statusText != null) _statusText.text = value ?? string.Empty;
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

            public SelectionEntry(string activityId, string displayName, string anchorId)
            {
                ActivityId = activityId;
                DisplayName = displayName;
                AnchorId = anchorId;
            }
        }
    }
}
