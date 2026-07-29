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

        public Vector2 Move => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool SprintHeld => _sprintAction != null && _sprintAction.IsPressed();
        public bool ConstrictFlameHeld => _constrictFlameAction != null && _constrictFlameAction.IsPressed();
        public bool PlaceFirePressedThisFrame => _placeFireAction != null && _placeFireAction.WasPressedThisFrame();
        public bool RestPressedThisFrame => _restAction != null && _restAction.WasPressedThisFrame();
        public bool UpgradeCampfirePressedThisFrame => _upgradeCampfireAction != null && _upgradeCampfireAction.WasPressedThisFrame();
        public Vector2 Look => _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool InteractPressedThisFrame => _interactAction != null && _interactAction.WasPressedThisFrame();
        public bool EmotePressedThisFrame => _emoteAction != null && _emoteAction.WasPressedThisFrame();
        public bool CycleTreeLightColorPressedThisFrame => _cycleTreeLightColorAction != null && _cycleTreeLightColorAction.WasPressedThisFrame();
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
    }
}
