using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// 允许玩家在安全区域按住输入，暂时收束火焰进入 Receiver。
    /// 不改变余火数值，松开输入或离开安全区域即解除。
    /// </summary>
    public sealed class FlameContractionController : MonoBehaviour
    {
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private PlayerModeController _modeController;

        private void Awake()
        {
            if (_input == null)
            {
                _input = GetComponent<FirePlayPlayerInput>();
            }

            if (_resourceController == null)
            {
                _resourceController = GetComponent<FlameResourceController>();
            }
            _modeController ??= GetComponent<PlayerModeController>();

            if (_input == null || _resourceController == null)
            {
                Debug.LogError("[FlameContractionController] 缺少输入或余火控制器。", this);
                enabled = false;
            }
        }

        private void Update()
        {
            _resourceController.SetReceiverOverride((_modeController == null || _modeController.IsExploring) && _input.ConstrictFlameHeld);
        }

        private void OnDisable()
        {
            _resourceController?.SetReceiverOverride(false);
        }
    }
}
