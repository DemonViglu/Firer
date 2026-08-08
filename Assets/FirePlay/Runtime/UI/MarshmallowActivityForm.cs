using DemonViglu.FirePlay.Activity;
using SUIFW;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Dedicated marshmallow UI form. It owns no roasting rules and no Flame
    /// reference; every button submits a semantic action to PlayerActivityHost.
    /// </summary>
    public sealed class MarshmallowActivityForm : BaseUIForms
    {
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _materializeButton;
        [SerializeField] private Button _turnButton;
        [SerializeField] private Button _eatButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private GameObject _timingPanel;
        [SerializeField] private RectTransform _timingTrack;
        [SerializeField] private RectTransform _targetZone;
        [SerializeField] private RectTransform _needle;

        private IActivityActionRequester _requester;

        private void Awake()
        {
            ResolveControls();
        }

        private void ResolveControls()
        {
            _statusText ??= FindText("Status");
            _materializeButton ??= FindButton("MaterializeButton");
            _turnButton ??= FindButton("TurnButton");
            _eatButton ??= FindButton("EatButton");
            _closeButton ??= FindButton("CloseButton");
            _timingPanel ??= FindRect("TimingPanel")?.gameObject;
            _timingTrack ??= FindRect("TimingTrack");
            _targetZone ??= FindRect("PerfectZone");
            _needle ??= FindRect("Needle");
        }

        public override void Display()
        {
            base.Display();
            ResolveControls();
            ResolveRequester();
            BindButtons();
            Refresh();
        }

        public override void Hiding()
        {
            UnbindButtons();
            base.Hiding();
        }

        private void ResolveRequester()
        {
            _requester = PlayerActivityHost.Local;
        }

        private void Update()
        {
            if (gameObject.activeInHierarchy)
                Refresh();
        }

        private void Refresh()
        {
            ResolveRequester();
            var host = _requester as PlayerActivityHost;
            var logic = host?.ActiveSession?.Logic as MarshmallowActivityLogic;
            if (logic != null)
            {
                ApplyState(new MarshmallowActivityStateSnapshot(
                    logic.HasMaterialized,
                    logic.IsReadyToEat,
                    logic.CompletedTurns,
                    logic.PerfectTurns,
                    logic.TurnsRequired,
                    logic.CompletedResult.HasValue,
                    logic.CompletedResult?.Quality ?? MarshmallowRoastQuality.Scorched,
                    logic.NeedlePosition,
                    logic.TargetCenter,
                    logic.PerfectZoneWidth));
                return;
            }

            if (host != null
                && host.TryGetActiveStatePayload(
                    MarshmallowActivityLogic.ActivityId,
                    out var payload)
                && MarshmallowActivityStateSnapshot.TryParse(payload, out var snapshot))
            {
                ApplyState(snapshot);
                return;
            }

            SetStatus("等待主机同步棉花糖状态");
            SetInteractable(_materializeButton, false);
            SetInteractable(_turnButton, false);
            SetInteractable(_eatButton, false);
            SetTimingVisible(false);
        }

        private void ApplyState(MarshmallowActivityStateSnapshot state)
        {
            if (!state.HasMaterialized)
            {
                SetStatus("消耗余火拟造一份棉花糖");
            }
            else if (state.IsReadyToEat)
            {
                SetStatus($"烤制完成：{QualityName(state.Quality)} · 完美翻面 {state.PerfectTurns}/{state.TurnsRequired}");
            }
            else
            {
                SetStatus($"火候 {state.CompletedTurns}/{state.TurnsRequired} · 完美 {state.PerfectTurns}\n指针进入亮区时翻面");
            }

            SetInteractable(_materializeButton, !state.HasMaterialized);
            SetInteractable(_turnButton, state.HasMaterialized && !state.IsReadyToEat);
            SetInteractable(_eatButton, state.IsReadyToEat);
            SetTimingVisible(state.HasMaterialized && !state.IsReadyToEat);
            UpdateTimingGauge(state);
        }

        private void UpdateTimingGauge(MarshmallowActivityStateSnapshot state)
        {
            if (_timingTrack == null || _targetZone == null || _needle == null)
                return;

            var width = Mathf.Abs(_timingTrack.rect.width);
            if (width < 1f)
                width = Mathf.Abs(_timingTrack.sizeDelta.x);
            if (width < 1f)
                width = 320f;

            var zoneWidth = Mathf.Max(8f, width * state.PerfectZonePercent / 100f);
            _targetZone.sizeDelta = new Vector2(zoneWidth, _targetZone.sizeDelta.y);
            _targetZone.anchoredPosition = new Vector2(
                (state.TargetCenterPercent / 100f - 0.5f) * width,
                _targetZone.anchoredPosition.y);
            _needle.anchoredPosition = new Vector2(
                (state.NeedlePercent / 100f - 0.5f) * width,
                _needle.anchoredPosition.y);
        }

        private void BindButtons()
        {
            UnbindButtons();
            _materializeButton?.onClick.AddListener(OnMaterializeClicked);
            _turnButton?.onClick.AddListener(OnTurnClicked);
            _eatButton?.onClick.AddListener(OnEatClicked);
            _closeButton?.onClick.AddListener(OnCloseClicked);
        }

        private void UnbindButtons()
        {
            _materializeButton?.onClick.RemoveListener(OnMaterializeClicked);
            _turnButton?.onClick.RemoveListener(OnTurnClicked);
            _eatButton?.onClick.RemoveListener(OnEatClicked);
            _closeButton?.onClick.RemoveListener(OnCloseClicked);
        }

        private void OnMaterializeClicked() => Submit("marshmallow.materialize");
        private void OnTurnClicked() => Submit("marshmallow.turn");
        private void OnEatClicked() => Submit("marshmallow.eat");
        private void OnCloseClicked() => Submit("activity.exit");

        private void Submit(string actionId)
        {
            ResolveRequester();
            if (_requester == null)
            {
                SetStatus("活动宿主未就绪");
                return;
            }

            var result = _requester.RequestAction(actionId);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[MarshmallowActivityForm] 提交动作：{actionId}，consumed={result.Consumed}，reason={result.Reason}", this);
#endif
            SetStatus(result.Consumed ? result.Reason : $"未执行：{result.Reason}");
        }

        private void SetStatus(string value)
        {
            if (_statusText != null)
                _statusText.text = value ?? string.Empty;
        }

        private void SetTimingVisible(bool visible)
        {
            if (_timingPanel != null && _timingPanel.activeSelf != visible)
                _timingPanel.SetActive(visible);
        }

        private static void SetInteractable(Button button, bool value)
        {
            if (button != null)
                button.interactable = value;
        }

        private static string QualityName(MarshmallowRoastQuality quality) => quality switch
        {
            MarshmallowRoastQuality.Perfect => "金黄完美",
            MarshmallowRoastQuality.Toasted => "焦香",
            _ => "烤焦了"
        };

        private Text FindText(string childName)
        {
            foreach (var text in GetComponentsInChildren<Text>(true))
            {
                if (text.gameObject.name == childName)
                    return text;
            }

            return null;
        }

        private Button FindButton(string childName)
        {
            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                if (button.gameObject.name == childName)
                    return button;
            }

            return null;
        }

        private RectTransform FindRect(string childName)
        {
            foreach (var rect in GetComponentsInChildren<RectTransform>(true))
            {
                if (rect.gameObject.name == childName)
                    return rect;
            }

            return null;
        }

        private void OnDisable()
        {
            UnbindButtons();
        }
    }
}
