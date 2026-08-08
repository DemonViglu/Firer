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
        private bool _acceptInput = true;

        // Virtual controls are intentionally routed through the same input facade as
        // keyboard/gamepad. Gameplay systems never need to know whether a command
        // originated from a mobile UI button or a physical device.
        private Vector2 _virtualMove;
        private Vector2 _virtualLook;
        public event Action<RawPlayerInput> RawInputPerformed;

        public bool AcceptInput => _acceptInput;
        public Vector2 Move => !_acceptInput
            ? Vector2.zero
            : Vector2.ClampMagnitude((_moveAction?.ReadValue<Vector2>() ?? Vector2.zero) + _virtualMove, 1f);
        public bool SprintHeld => _acceptInput && _sprintAction != null && _sprintAction.IsPressed();
        public bool ConstrictFlameHeld => _acceptInput && _constrictFlameAction != null && _constrictFlameAction.IsPressed();
        public Vector2 Look
        {
            get
            {
                if (!_acceptInput)
                {
                    _virtualLook = Vector2.zero;
                    return Vector2.zero;
                }

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
            _lookAction = FindRequiredAction("Look");

            // Only Move and Look are part of the base Player contract. Sprint
            // and all discrete actions belong to optional feature modules.
            _sprintAction = FindOptionalAction("Sprint");
            _constrictFlameAction = FindOptionalAction("ConstrictFlame");
            _placeFireAction = FindOptionalAction("PlaceFire");
            _restAction = FindOptionalAction("Rest");
            _upgradeCampfireAction = FindOptionalAction("UpgradeCampfire");
            _interactAction = FindOptionalAction("Interact");
            _emoteAction = FindOptionalAction("Emote");
            _cycleTreeLightColorAction = FindOptionalAction("CycleTreeLightColor");
            _pauseAction = FindOptionalAction("Pause");
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

        private InputAction FindOptionalAction(string actionName)
        {
            return _playerMap.FindAction(actionName, throwIfNotFound: false);
        }

        public void SetVirtualMove(Vector2 value)
        {
            _virtualMove = _acceptInput ? Vector2.ClampMagnitude(value, 1f) : Vector2.zero;
        }

        public void AddVirtualLookDelta(Vector2 delta)
        {
            if (_acceptInput)
                _virtualLook += delta;
        }

        /// <summary>
        /// Network ownership gate. The action map remains configured on the
        /// object, but non-owned Players cannot expose input values or enqueue
        /// local intents. This avoids disabling a shared InputActionAsset for
        /// the actual local Player when remote instances exist.
        /// </summary>
        public void SetLocalControl(bool enabled)
        {
            _acceptInput = enabled;
            if (!enabled)
            {
                _virtualMove = Vector2.zero;
                _virtualLook = Vector2.zero;
            }
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
            if (!_acceptInput) return;
            RawInputPerformed?.Invoke(input);
        }
    }
}
