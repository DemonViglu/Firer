using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.Core
{
    /// <summary>
    /// 为需要存档或未来同步的场景对象提供人工指定的稳定标识。
    /// 标识不依赖 GameObject 名称、层级路径或 Instance ID。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StableSceneId : MonoBehaviour
    {
        private static readonly Dictionary<string, StableSceneId> ActiveIds = new();

        [SerializeField] private string _value;
        [SerializeField] private bool _allowRuntimeAssignment;

        public string Value => _value;
        public bool IsValid => !string.IsNullOrWhiteSpace(_value);
        public bool AllowsRuntimeAssignment => _allowRuntimeAssignment;

        public static bool TryFind(string value, out StableSceneId stableSceneId)
        {
            stableSceneId = null;
            return !string.IsNullOrWhiteSpace(value) && ActiveIds.TryGetValue(value.Trim().ToLowerInvariant(), out stableSceneId);
        }

        /// <summary>
        /// 仅供运行时创建且需要持久化的对象在创建命令中显式分配 ID。
        /// 场景预置对象仍应在 Inspector 手动填写，不会自动生成。
        /// </summary>
        public bool TryAssignRuntimeValue(string value)
        {
            if (!_allowRuntimeAssignment || IsValid || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (ActiveIds.TryGetValue(normalized, out var existing) && existing != this)
            {
                Debug.LogError($"[StableSceneId] 运行时 ID 重复：{normalized}", this);
                return false;
            }

            _value = normalized;
            if (isActiveAndEnabled)
            {
                ActiveIds[_value] = this;
            }

            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveIds.Clear();
        }

        private void OnEnable()
        {
            if (!IsValid)
            {
                if (!_allowRuntimeAssignment)
                {
                    Debug.LogWarning("[StableSceneId] 未配置稳定 ID。", this);
                }
                return;
            }

            if (ActiveIds.TryGetValue(_value, out var existing) && existing != this)
            {
                Debug.LogError($"[StableSceneId] 场景中存在重复稳定 ID：{_value}", this);
                return;
            }

            ActiveIds[_value] = this;
        }

        private void OnDisable()
        {
            if (IsValid && ActiveIds.TryGetValue(_value, out var existing) && existing == this)
            {
                ActiveIds.Remove(_value);
            }
        }

        private void OnValidate()
        {
            if (_value != null)
            {
                _value = _value.Trim().ToLowerInvariant();
            }
        }
    }
}
