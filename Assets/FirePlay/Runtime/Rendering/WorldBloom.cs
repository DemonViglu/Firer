using System;
using System.Collections;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.Data;
using UnityEngine;

namespace DemonViglu.FirePlay.Rendering
{
    /// <summary>
    /// 将可复苏节点的游戏状态写入各 Renderer 的材质属性块。
    /// 不创建材质实例，因此同一份材质可被多个节点安全复用。
    /// </summary>
    public sealed class WorldBloom : MonoBehaviour
    {
        [SerializeField] private Renderer[] _targetRenderers;
        [SerializeField, Range(0f, 1f)] private float _litAmount;

        private MaterialPropertyBlock _propertyBlock;
        private Coroutine _restoreCoroutine;

        public float LitAmount => _litAmount;
        public bool IsRestoring => _restoreCoroutine != null;

        private void Awake()
        {
            if (_targetRenderers == null || _targetRenderers.Length == 0)
            {
                _targetRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            }

            _propertyBlock = new MaterialPropertyBlock();
            ApplyProperties(Color.black, Color.white, Color.white);
        }

        public bool BeginRestore(RestorableNodeConfig config, Action onCompleted = null)
        {
            if (config == null || _restoreCoroutine != null || _litAmount >= 1f)
            {
                return false;
            }

            _restoreCoroutine = StartCoroutine(RestoreRoutine(config, onCompleted));
            return true;
        }

        private IEnumerator RestoreRoutine(RestorableNodeConfig config, Action onCompleted)
        {
            var startAmount = _litAmount;
            var elapsed = 0f;

            while (elapsed < config.RestoreDuration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / config.RestoreDuration);
                _litAmount = Mathf.SmoothStep(startAmount, 1f, progress);
                ApplyProperties(config.InkColor, config.RestoredColor, config.BloomColor);
                yield return null;
            }

            _litAmount = 1f;
            ApplyProperties(config.InkColor, config.RestoredColor, config.BloomColor);
            _restoreCoroutine = null;
            onCompleted?.Invoke();
        }

        private void ApplyProperties(Color inkColor, Color baseColor, Color bloomColor)
        {
            foreach (var targetRenderer in _targetRenderers)
            {
                if (targetRenderer == null)
                {
                    continue;
                }

                targetRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetFloat(FirePlayShaderPropertyIds.LitAmount, _litAmount);
                _propertyBlock.SetColor(FirePlayShaderPropertyIds.InkColor, inkColor);
                _propertyBlock.SetColor(FirePlayShaderPropertyIds.BaseColor, baseColor);
                _propertyBlock.SetColor(FirePlayShaderPropertyIds.BloomColor, bloomColor);
                targetRenderer.SetPropertyBlock(_propertyBlock);
            }
        }
    }
}
