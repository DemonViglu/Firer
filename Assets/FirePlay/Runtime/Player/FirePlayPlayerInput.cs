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
        private bool _virtualPlaceFirePressed;
        private bool _virtualCancelPlacementPressed;
        private bool _virtualRestPressed;
        private bool _virtualUpgradeCampfirePressed;
        private bool _virtualTendFirePressed;
        private bool _virtualAddFirePressed;
        private bool _virtualGatherEmberPressed;
        private bool _virtualStartPublicFirePressed;
        private bool _virtualDrawFirePressed;
        private bool _virtualReclaimSmallFirePressed;
        private bool _virtualContributeWorldTreePressed;
        private bool _virtualInteractPressed;
        private bool _virtualEmotePressed;
        private bool _virtualCycleTreeLightColorPressed;

        public Vector2 Move => Vector2.ClampMagnitude((_moveAction?.ReadValue<Vector2>() ?? Vector2.zero) + _virtualMove, 1f);
        public bool SprintHeld => _sprintAction != null && _sprintAction.IsPressed();
        public bool ConstrictFlameHeld => _constrictFlameAction != null && _constrictFlameAction.IsPressed();
        public bool PlaceFirePressedThisFrame => ConsumeVirtualPress(ref _virtualPlaceFirePressed) || (_placeFireAction != null && _placeFireAction.WasPressedThisFrame());
        public bool CancelPlacementPressedThisFrame => ConsumeVirtualPress(ref _virtualCancelPlacementPressed);
        public bool RestPressedThisFrame => ConsumeVirtualPress(ref _virtualRestPressed) || (_restAction != null && _restAction.WasPressedThisFrame());
        public bool UpgradeCampfirePressedThisFrame => ConsumeVirtualPress(ref _virtualUpgradeCampfirePressed) || (_upgradeCampfireAction != null && _upgradeCampfireAction.WasPressedThisFrame());
        public bool TendFirePressedThisFrame => ConsumeVirtualPress(ref _virtualTendFirePressed);
        public bool AddFirePressedThisFrame => ConsumeVirtualPress(ref _virtualAddFirePressed);
        public bool GatherEmberPressedThisFrame => ConsumeVirtualPress(ref _virtualGatherEmberPressed);
        public bool StartPublicFirePressedThisFrame => ConsumeVirtualPress(ref _virtualStartPublicFirePressed);
        public bool DrawFirePressedThisFrame => ConsumeVirtualPress(ref _virtualDrawFirePressed);
        public bool ReclaimSmallFirePressedThisFrame => ConsumeVirtualPress(ref _virtualReclaimSmallFirePressed);
        public bool ContributeWorldTreePressedThisFrame => ConsumeVirtualPress(ref _virtualContributeWorldTreePressed);
        public Vector2 Look
        {
            get
            {
                var look = (_lookAction?.ReadValue<Vector2>() ?? Vector2.zero) + _virtualLook;
                _virtualLook = Vector2.zero;
                return look;
            }
        }
        public bool InteractPressedThisFrame => ConsumeVirtualPress(ref _virtualInteractPressed) || (_interactAction != null && _interactAction.WasPressedThisFrame());
        public bool EmotePressedThisFrame => ConsumeVirtualPress(ref _virtualEmotePressed) || (_emoteAction != null && _emoteAction.WasPressedThisFrame());
        public bool CycleTreeLightColorPressedThisFrame => ConsumeVirtualPress(ref _virtualCycleTreeLightColorPressed) || (_cycleTreeLightColorAction != null && _cycleTreeLightColorAction.WasPressedThisFrame());
        public bool PausePressedThisFrame => _pauseAction != null && _pauseAction.WasPressedThisFrame();

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
            _playerMap?.Enable();
        }

        private void OnDisable()
        {
            _playerMap?.Disable();
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

        public void RequestVirtualPlaceFire() => _virtualPlaceFirePressed = true;
        public void RequestVirtualCancelPlacement() => _virtualCancelPlacementPressed = true;
        public void RequestVirtualRest() => _virtualRestPressed = true;
        public void RequestVirtualUpgradeCampfire() => _virtualUpgradeCampfirePressed = true;
        public void RequestVirtualTendFire() => _virtualTendFirePressed = true;
        public void RequestVirtualAddFire() => _virtualAddFirePressed = true;
        public void RequestVirtualGatherEmber() => _virtualGatherEmberPressed = true;
        public void RequestVirtualStartPublicFire() => _virtualStartPublicFirePressed = true;
        public void RequestVirtualDrawFire() => _virtualDrawFirePressed = true;
        public void RequestVirtualReclaimSmallFire() => _virtualReclaimSmallFirePressed = true;
        public void RequestVirtualContributeWorldTree() => _virtualContributeWorldTreePressed = true;
        public void RequestVirtualInteract() => _virtualInteractPressed = true;
        public void RequestVirtualEmote() => _virtualEmotePressed = true;
        public void RequestVirtualCycleTreeLightColor() => _virtualCycleTreeLightColorPressed = true;

        private static bool ConsumeVirtualPress(ref bool pressed)
        {
            if (!pressed)
            {
                return false;
            }

            pressed = false;
            return true;
        }
    }
}
