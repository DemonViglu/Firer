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

        public string Value => _value;
        public bool IsValid => !string.IsNullOrWhiteSpace(_value);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveIds.Clear();
        }

        private void OnEnable()
        {
            if (!IsValid)
            {
                Debug.LogWarning("[StableSceneId] 未配置稳定 ID。", this);
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
