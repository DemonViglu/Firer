using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Player;
using DemonViglu.FirePlay.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DemonViglu.FirePlay.Debugging
{
    /// <summary>
    /// 可选的双活动切换探针。手动挂到本地 Player 后，用两个键发布活动选择语义；不修改正式输入 Router。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivitySelectionProbe : MonoBehaviour
    {
        [SerializeField] private LocalPlayerContext _localPlayer;
        [SerializeField] private ActivityAnchor _anchor;
        [SerializeField] private string _firstActivityId = "marshmallow";
        [SerializeField] private string _secondActivityId = "fishing";
        [SerializeField] private Key _firstKey = Key.Digit1;
        [SerializeField] private Key _secondKey = Key.Digit2;
        [SerializeField] private bool _enabled = true;

        private IEventPublisher _events;

        private void Awake()
        {
            _localPlayer ??= GetComponent<LocalPlayerContext>();
        }

        private void OnEnable()
        {
            _events = GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
        }

        private void Update()
        {
            _localPlayer ??= LocalPlayerContext.Current ?? GetComponent<LocalPlayerContext>();
            if (!_enabled || Keyboard.current == null) return;
            if (Keyboard.current[_firstKey].wasPressedThisFrame) RequestFirstActivity();
            if (Keyboard.current[_secondKey].wasPressedThisFrame) RequestSecondActivity();
        }

        public void RequestFirstActivity() => RequestActivity(_firstActivityId);
        public void RequestSecondActivity() => RequestActivity(_secondActivityId);

        private void RequestActivity(string activityId)
        {
            if (_localPlayer == null || string.IsNullOrWhiteSpace(activityId)) return;
            var anchor = _anchor != null ? _anchor : _localPlayer.Activities != null ? _localPlayer.Activities.NearestAnchor : null;
            if (anchor == null) return;

            _events?.Publish(new ActivitySelectionRequested(_localPlayer.PlayerId, anchor.AnchorId, activityId));
        }
    }
}
