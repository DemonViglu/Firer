using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// FirePlay 输入资源的唯一运行时入口。
    /// 将 FirePlay.inputactions 资产拖入 Inspector 后，其他玩家组件只读取本类提供的值。
    /// </summary>
    public sealed class FirePlayPlayerInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset _actions;
        [SerializeField] private string _playerActionMapName = "Player";

        private InputActionMap _playerMap;
        private InputAction _moveAction;
        private InputAction _sprintAction;
        private InputAction _constrictFlameAction;
        private InputAction _placeFireAction;
        private InputAction _restAction;
        private InputAction _upgradeCampfireAction;
        private InputAction _lookAction;
        private InputAction _interactAction;
        private InputAction _emoteAction;
        private InputAction _cycleTreeLightColorAction;
        private InputAction _pauseAction;

        // Virtual controls are intentionally routed through the same input facade as
        // keyboard/gamepad. Gameplay systems never need to know whether a command
        // originated from a mobile UI button or a physical device.
        private Vector2 _virtualMove;
        private Vector2 _virtualLook;
        public event Action<RawPlayerInput> RawInputPerformed;

        public Vector2 Move => Vector2.ClampMagnitude((_moveAction?.ReadValue<Vector2>() ?? Vector2.zero) + _virtualMove, 1f);
        public bool SprintHeld => _sprintAction != null && _sprintAction.IsPressed();
        public bool ConstrictFlameHeld => _constrictFlameAction != null && _constrictFlameAction.IsPressed();
        public Vector2 Look
        {
            get
            {
                var look = (_lookAction?.ReadValue<Vector2>() ?? Vector2.zero) + _virtualLook;
                _virtualLook = Vector2.zero;
                return look;
            }
        }

        private void Awake()
        {
            if (_actions == null)
            {
                Debug.LogError("[FirePlayPlayerInput] 未指定 Input Action Asset。", this);
                enabled = false;
                return;
            }

            _playerMap = _actions.FindActionMap(_playerActionMapName, throwIfNotFound: false);
            if (_playerMap == null)
            {
                Debug.LogError($"[FirePlayPlayerInput] 找不到 Action Map: {_playerActionMapName}", this);
                enabled = false;
                return;
            }

            _moveAction = FindRequiredAction("Move");
            _sprintAction = FindRequiredAction("Sprint");
            _constrictFlameAction = FindRequiredAction("ConstrictFlame");
            _placeFireAction = FindRequiredAction("PlaceFire");
            _restAction = FindRequiredAction("Rest");
            _upgradeCampfireAction = FindRequiredAction("UpgradeCampfire");
            _lookAction = FindRequiredAction("Look");
            _interactAction = FindRequiredAction("Interact");
            _emoteAction = FindRequiredAction("Emote");
            _cycleTreeLightColorAction = FindRequiredAction("CycleTreeLightColor");
            _pauseAction = FindRequiredAction("Pause");
        }

        private void OnEnable()
        {
            SubscribePerformedCallbacks();
            _playerMap?.Enable();
        }

        private void OnDisable()
        {
            _playerMap?.Disable();
            UnsubscribePerformedCallbacks();
        }

        private InputAction FindRequiredAction(string actionName)
        {
            var action = _playerMap.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                Debug.LogError($"[FirePlayPlayerInput] Action Map '{_playerActionMapName}' 缺少动作：{actionName}。", this);
            }

            return action;
        }

        public void SetVirtualMove(Vector2 value)
        {
            _virtualMove = Vector2.ClampMagnitude(value, 1f);
        }

        public void AddVirtualLookDelta(Vector2 delta)
        {
            _virtualLook += delta;
        }

        public void RequestVirtualPlaceFire() => Queue(RawPlayerInput.PlaceFire);
        public void RequestVirtualCancelPlacement() => Queue(RawPlayerInput.CancelPlacement);
        public void RequestVirtualRest() => Queue(RawPlayerInput.Rest);
        public void RequestVirtualUpgradeCampfire() => Queue(RawPlayerInput.UpgradeCampfire);
        public void RequestVirtualTendFire() => Queue(RawPlayerInput.TendFire);
        public void RequestVirtualAddFire() => Queue(RawPlayerInput.AddFire);
        public void RequestVirtualGatherEmber() => Queue(RawPlayerInput.GatherEmber);
        public void RequestVirtualStartPublicFire() => Queue(RawPlayerInput.StartPublicFire);
        public void RequestVirtualDrawFire() => Queue(RawPlayerInput.DrawFire);
        public void RequestVirtualReclaimSmallFire() => Queue(RawPlayerInput.ReclaimSmallFire);
        public void RequestVirtualContributeWorldTree() => Queue(RawPlayerInput.ContributeWorldTree);
        public void RequestVirtualInteract() => Queue(RawPlayerInput.Interact);
        public void RequestVirtualEmote() => Queue(RawPlayerInput.Emote);
        public void RequestVirtualCycleTreeLightColor() => Queue(RawPlayerInput.CycleTreeLightColor);

        private void SubscribePerformedCallbacks()
        {
            Subscribe(_placeFireAction);
            Subscribe(_restAction);
            Subscribe(_upgradeCampfireAction);
            Subscribe(_interactAction);
            Subscribe(_emoteAction);
            Subscribe(_cycleTreeLightColorAction);
            Subscribe(_pauseAction);
        }

        private void UnsubscribePerformedCallbacks()
        {
            Unsubscribe(_placeFireAction);
            Unsubscribe(_restAction);
            Unsubscribe(_upgradeCampfireAction);
            Unsubscribe(_interactAction);
            Unsubscribe(_emoteAction);
            Unsubscribe(_cycleTreeLightColorAction);
            Unsubscribe(_pauseAction);
        }

        private void Subscribe(InputAction action)
        {
            if (action == null) return;
            action.performed += OnDiscreteActionPerformed;
        }

        private void Unsubscribe(InputAction action)
        {
            if (action != null) action.performed -= OnDiscreteActionPerformed;
        }

        private void OnDiscreteActionPerformed(InputAction.CallbackContext context)
        {
            var action = context.action;
            if (action == _placeFireAction) Queue(RawPlayerInput.PlaceFire);
            else if (action == _restAction) Queue(RawPlayerInput.Rest);
            else if (action == _upgradeCampfireAction) Queue(RawPlayerInput.UpgradeCampfire);
            else if (action == _interactAction) Queue(RawPlayerInput.Interact);
            else if (action == _emoteAction) Queue(RawPlayerInput.Emote);
            else if (action == _cycleTreeLightColorAction) Queue(RawPlayerInput.CycleTreeLightColor);
            else if (action == _pauseAction) Queue(RawPlayerInput.Pause);
        }

        private void Queue(RawPlayerInput input)
        {
            RawInputPerformed?.Invoke(input);
        }
    }
}
