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
        private InputAction _lookAction;
        private InputAction _interactAction;

        public Vector2 Move => _moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool SprintHeld => _sprintAction != null && _sprintAction.IsPressed();
        public bool ConstrictFlameHeld => _constrictFlameAction != null && _constrictFlameAction.IsPressed();
        public bool PlaceFirePressedThisFrame => _placeFireAction != null && _placeFireAction.WasPressedThisFrame();
        public Vector2 Look => _lookAction?.ReadValue<Vector2>() ?? Vector2.zero;
        public bool InteractPressedThisFrame => _interactAction != null && _interactAction.WasPressedThisFrame();

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

            _moveAction = _playerMap.FindAction("Move", throwIfNotFound: false);
            _sprintAction = _playerMap.FindAction("Sprint", throwIfNotFound: false);
            _constrictFlameAction = _playerMap.FindAction("ConstrictFlame", throwIfNotFound: false);
            _placeFireAction = _playerMap.FindAction("PlaceFire", throwIfNotFound: false);
            _lookAction = _playerMap.FindAction("Look", throwIfNotFound: false);
            _interactAction = _playerMap.FindAction("Interact", throwIfNotFound: false);
        }

        private void OnEnable()
        {
            _playerMap?.Enable();
        }

        private void OnDisable()
        {
            _playerMap?.Disable();
        }
    }
}
