using DemonViglu.FirePlay.Player;
using UnityEngine;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// 将余火状态映射到玩家当前主火苗的视觉强度。
    /// 只写入 FlameColorState，Renderer 与 Light 仍由 FlameVisuals 统一表现。
    /// </summary>
    public sealed class FlameResourceVisualBridge : MonoBehaviour
    {
        [SerializeField] private FlameResourceController _resourceController;
        [SerializeField] private PlayerFlameController _playerFlameController;

        [Header("强度映射")]
        [SerializeField, Min(0.01f)] private float _receiverMinimumIntensity = 0.22f;
        [SerializeField, Min(0.01f)] private float _giverMaximumIntensity = 1.15f;
        [SerializeField, Range(0f, 1f)] private float _receiverOverrideVisualLevel = 0.15f;
        [SerializeField, Min(0f)] private float _responseSpeed = 5f;

        private float _visualFuel;
        private bool _initialized;

        private void Awake()
        {
            if (_resourceController == null)
            {
                _resourceController = GetComponentInChildren<FlameResourceController>(true);
            }

            if (_playerFlameController == null)
            {
                _playerFlameController = GetComponentInChildren<PlayerFlameController>(true);
            }

            if (_resourceController == null || _playerFlameController == null)
            {
                Debug.LogError("[FlameResourceVisualBridge] 缺少余火或玩家火苗控制器。", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            var resourceState = _resourceController.State;
            var activeFlame = _playerFlameController.ActiveFlame;
            if (resourceState == null || activeFlame == null)
            {
                return;
            }

            var targetVisualFuel = resourceState.IsReceiverOverride
                ? _receiverOverrideVisualLevel
                : resourceState.NormalizedFuel;

            if (!_initialized)
            {
                _visualFuel = targetVisualFuel;
                _initialized = true;
            }
            else
            {
                var blend = 1f - Mathf.Exp(-_responseSpeed * Time.deltaTime);
                _visualFuel = Mathf.Lerp(_visualFuel, targetVisualFuel, blend);
            }

            activeFlame.State.SetResourceLevel(_visualFuel);
            activeFlame.State.SetIntensity(Mathf.Lerp(
                _receiverMinimumIntensity,
                _giverMaximumIntensity,
                _visualFuel));
        }

        private void OnValidate()
        {
            _receiverMinimumIntensity = Mathf.Max(0.01f, _receiverMinimumIntensity);
            _giverMaximumIntensity = Mathf.Max(_receiverMinimumIntensity, _giverMaximumIntensity);
            _receiverOverrideVisualLevel = Mathf.Clamp01(_receiverOverrideVisualLevel);
            _responseSpeed = Mathf.Max(0f, _responseSpeed);
        }
    }
}
