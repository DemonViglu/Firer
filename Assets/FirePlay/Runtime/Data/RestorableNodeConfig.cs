using UnityEngine;

namespace DemonViglu.FirePlay.Data
{
    /// <summary>
    /// 单类可复苏节点共享的玩法和表现参数。
    /// </summary>
    [CreateAssetMenu(menuName = "DemonViglu/FirePlay/Restorable Node Config", fileName = "RestorableNodeConfig_")]
    public sealed class RestorableNodeConfig : ScriptableObject
    {
        [Header("复苏条件")]
        [SerializeField] private Color _requiredColor = new(0.96f, 0.65f, 0.78f, 1f);
        [SerializeField, Range(0f, 1f)] private float _minimumColorMatch = 0.75f;

        [Header("复苏表现")]
        [SerializeField, Min(0.01f)] private float _restoreDuration = 1.8f;
        [SerializeField] private Color _inkColor = new(0.24f, 0.25f, 0.27f, 1f);
        [SerializeField] private Color _restoredColor = new(0.78f, 0.67f, 0.56f, 1f);
        [SerializeField] private Color _bloomColor = new(0.96f, 0.65f, 0.78f, 1f);

        public Color RequiredColor => _requiredColor;
        public float MinimumColorMatch => _minimumColorMatch;
        public float RestoreDuration => _restoreDuration;
        public Color InkColor => _inkColor;
        public Color RestoredColor => _restoredColor;
        public Color BloomColor => _bloomColor;
    }
}
