using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 大树个人光点颜色的原型选择器。
    /// 键盘仅用于验证；移动端 UI 可直接调用 WorldTreeContribution.SelectPersonalLightColor。
    /// </summary>
    public sealed class TreeLightColorSelector : MonoBehaviour
    {
        [SerializeField] private FirePlayPlayerInput _input;
        [SerializeField] private PlayerInteraction _interaction;
        [SerializeField] private PlayerModeController _modeController;
        [SerializeField] private Color[] _availableColors =
        {
            new(1f, 0.45f, 0.2f),
            new(1f, 0.82f, 0.25f),
            new(0.3f, 0.8f, 1f),
            new(0.8f, 0.45f, 1f)
        };

        private int _selectedIndex;

        private void Awake()
        {
            _input ??= GetComponent<FirePlayPlayerInput>();
            _interaction ??= GetComponent<PlayerInteraction>();
            _modeController ??= GetComponent<PlayerModeController>();
        }

        private void Update()
        {
            var worldTree = _interaction != null ? _interaction.NearestWorldTree : null;
            if (_input == null || (_modeController != null && !_modeController.IsExploring) || worldTree == null || worldTree.HasLocalContribution || !_input.CycleTreeLightColorPressedThisFrame)
            {
                return;
            }

            if (_availableColors == null || _availableColors.Length == 0)
            {
                return;
            }

            _selectedIndex = (_selectedIndex + 1) % _availableColors.Length;
            worldTree.SelectPersonalLightColor(_availableColors[_selectedIndex]);
        }
    }
}
