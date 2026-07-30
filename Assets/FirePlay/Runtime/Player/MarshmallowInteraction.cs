using DemonViglu.FirePlay.World;
using DemonViglu.FirePlay.Flame;
using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public sealed class MarshmallowInteraction : MonoBehaviour, IRitualInteraction
    {
        [SerializeField] private RestInteraction _rest;
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private Transform _marshmallowProp;
        [SerializeField] private float _turnDegrees = 90f;
        [SerializeField] private PlayerRitualAnimationController _ritualAnimationController;
        [Header("Prototype Meter")]
        [SerializeField] private bool _showPrototypeMeter = true;
        [SerializeField, Min(120f)] private float _meterWidth = 420f;
        [SerializeField, Min(12f)] private float _meterHeight = 28f;
        [SerializeField, Min(0f)] private float _meterBottomMargin = 70f;

        private MarshmallowRitual _activeRitual;
        private bool _hasMaterializedMarshmallow;
        private MarshmallowRoastSession _roastSession;
        private MarshmallowResult _completedResult;
        private GUIStyle _meterLabelStyle;

        public string Status { get; private set; } = "坐在篝火边，烤一颗棉花糖吧";
        public bool HasMaterializedMarshmallow => _hasMaterializedMarshmallow;
        public bool IsRoasting => _roastSession != null && _roastSession.IsRoasting;
        public bool IsReadyToEat => _roastSession != null && _roastSession.IsReadyToEat;
        public int CompletedTurns => _roastSession?.CompletedTurns ?? 0;
        public int PerfectTurns => _roastSession?.PerfectTurns ?? 0;
        public bool IsActive => _activeRitual != null;
        public RitualViewState ViewState => new(
            "marshmallow",
            Status,
            IsReadyToEat ? "烤好了" : IsRoasting ? "轻轻翻面" : "取出棉花糖",
            IsReadyToEat ? "吃一口" : "先去走走");
        public event Action Materialized;
        public event Action<bool> Turned;
        public event Action<bool> Eaten;
        public event Action<MarshmallowResult> ResultReady;
        public event Action<MarshmallowResult> ResultCollected;
        public event Action Cancelled;

        private void Awake()
        {
            _rest ??= GetComponent<RestInteraction>();
            _resourceController ??= GetComponent<FlameResourceController>();
            _ritualAnimationController ??= GetComponent<PlayerRitualAnimationController>();
            if (_rest == null || _resourceController == null || _marshmallowProp == null) { enabled = false; return; }
            _marshmallowProp.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            EndSession(cancelled: true);
        }

        private void Update()
        {
            var ritual = _rest.IsResting && _rest.ActiveRestSpot != null
                ? _rest.ActiveRestSpot.GetComponent<MarshmallowRitual>()
                : null;
            if (ritual != _activeRitual)
            {
                _activeRitual = ritual;
                EndSession(cancelled: true);
                Status = ritual == null ? "坐在篝火边，烤一颗棉花糖吧" : $"消耗 {ritual.MaterializeFuelCost:0} 点余火，取出一颗棉花糖";
            }

            if (_activeRitual != null && _hasMaterializedMarshmallow && !_activeRitual.IsCampfireBurning)
            {
                EndSession(cancelled: true);
                Status = "火焰熄灭了，先添一把火吧";
            }

            _roastSession?.Advance(Time.deltaTime);
            SyncRitualAnimationState();

            if (_marshmallowProp.gameObject.activeSelf != _hasMaterializedMarshmallow)
            {
                _marshmallowProp.gameObject.SetActive(_hasMaterializedMarshmallow);
            }
        }

        public bool TryPrimaryAction()
        {
            if (_activeRitual == null) return false;
            HandleEmotePressed();
            return true;
        }

        public bool TrySecondaryAction()
        {
            if (_activeRitual == null) return false;
            if (IsReadyToEat) EatMarshmallow();
            else _rest.EndRest();
            return true;
        }

        private void HandleEmotePressed()
        {
            if (!_hasMaterializedMarshmallow)
            {
                _hasMaterializedMarshmallow = _activeRitual.TryMaterialize(_resourceController, out var materializeStatus);
                Status = materializeStatus;
                if (_hasMaterializedMarshmallow)
                {
                    BeginRoasting();
                    _ritualAnimationController?.Play(RitualAnimationCue.Materialize);
                    Materialized?.Invoke();
                }

                return;
            }

            if (!IsRoasting)
            {
                return;
            }

            _marshmallowProp.Rotate(Vector3.forward, _turnDegrees, Space.Self);
            var isPerfect = _roastSession.TryTurn();
            _ritualAnimationController?.Play(RitualAnimationCue.Turn);
            Turned?.Invoke(isPerfect);

            if (IsReadyToEat)
            {
                _completedResult = _activeRitual.CreateResult(_roastSession);
                ResultReady?.Invoke(_completedResult);
                Status = $"{GetQualityLabel(_completedResult.Quality)}，吃掉它可回收 {_completedResult.FuelRefund:0} 点余火";
                return;
            }

            Status = isPerfect ? "刚刚好，香甜的颜色出现了" : "还差一点点火候";
        }

        private void BeginRoasting()
        {
            _roastSession = new MarshmallowRoastSession(
                _activeRitual.TurnsRequired,
                _activeRitual.NeedleCyclesPerSecond,
                _activeRitual.PerfectZoneWidth,
                _activeRitual.TargetEdgePadding);
            Status = "等指针进入金色区域，再轻轻翻面";
        }

        private void EatMarshmallow()
        {
            if (_completedResult.FuelRefund > 0f)
            {
                _resourceController.Restore(_completedResult.FuelRefund);
            }

            var result = _completedResult;
            var wasPerfect = result.Quality == MarshmallowRoastQuality.Perfect;
            _ritualAnimationController?.Play(RitualAnimationCue.Eat);
            Eaten?.Invoke(wasPerfect);
            ResultCollected?.Invoke(result);
            EndSession(cancelled: false);
            Status = wasPerfect ? "暖暖地吃掉了，刚刚好" : "暖暖地吃掉了";
        }

        private void EndSession(bool cancelled)
        {
            if (cancelled && _hasMaterializedMarshmallow)
            {
                _ritualAnimationController?.Play(RitualAnimationCue.Cancel);
                Cancelled?.Invoke();
            }

            _hasMaterializedMarshmallow = false;
            _roastSession = null;
            _completedResult = default;
            _ritualAnimationController?.SetState(RitualAnimationState.MarshmallowRoasting, false);
        }

        private void SyncRitualAnimationState()
        {
            _ritualAnimationController?.SetState(RitualAnimationState.Resting, _rest != null && _rest.IsResting);
            _ritualAnimationController?.SetState(RitualAnimationState.MarshmallowRoasting, IsRoasting);
        }

        private void OnGUI()
        {
            if (!_showPrototypeMeter || !IsRoasting || _activeRitual == null)
            {
                return;
            }

            EnsureMeterStyle();
            var x = (Screen.width - _meterWidth) * 0.5f;
            var y = Screen.height - _meterBottomMargin - _meterHeight;
            var meterRect = new Rect(x, y, _meterWidth, _meterHeight);
            DrawRect(new Rect(x - 3f, y - 3f, _meterWidth + 6f, _meterHeight + 6f), new Color(1f, 0.72f, 0.28f, 0.96f));
            DrawRect(meterRect, new Color(0.16f, 0.055f, 0.025f, 0.98f));
            DrawRect(new Rect(x + 4f, y + 4f, _meterWidth - 8f, _meterHeight - 8f), new Color(0.32f, 0.12f, 0.045f, 1f));

            var zoneWidth = _meterWidth * _activeRitual.PerfectZoneWidth;
            var zoneRect = new Rect(x + _meterWidth * _roastSession.TargetCenter - zoneWidth * 0.5f, y + 4f, zoneWidth, _meterHeight - 8f);
            DrawRect(zoneRect, new Color(1f, 0.58f, 0.08f, 1f));
            DrawRect(new Rect(zoneRect.x + 3f, zoneRect.y + 3f, zoneRect.width - 6f, zoneRect.height - 6f), new Color(1f, 0.84f, 0.28f, 1f));
            var needleRect = new Rect(x + _meterWidth * _roastSession.NeedlePosition - 3f, y - 6f, 6f, _meterHeight + 12f);
            DrawRect(new Rect(needleRect.x - 2f, needleRect.y, needleRect.width + 4f, needleRect.height), new Color(0.08f, 0.025f, 0.01f, 1f));
            DrawRect(needleRect, new Color(1f, 0.96f, 0.78f, 1f));

            GUI.Label(new Rect(x, y - 25f, _meterWidth, 22f),
                $"第 {_roastSession.CompletedTurns + 1}/{_roastSession.TurnsRequired} 次翻面：等指针进入金色区域",
                _meterLabelStyle);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;
        }

        private void EnsureMeterStyle()
        {
            if (_meterLabelStyle != null)
            {
                return;
            }

            _meterLabelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                normal = { textColor = new Color(1f, 0.9f, 0.65f) }
            };
        }

        private static string GetQualityLabel(MarshmallowRoastQuality quality)
        {
            return quality switch
            {
                MarshmallowRoastQuality.Perfect => "烤得刚刚好",
                MarshmallowRoastQuality.Toasted => "烤得暖呼呼的",
                _ => "有一点焦香"
            };
        }
    }
}
