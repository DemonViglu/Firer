using System;
using System.Collections.Generic;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// 显式活动注册表。由启动阶段注册，运行时不扫描 Player 组件，也不动态 AddComponent。
    /// </summary>
    public sealed class ActivityCatalog
    {
        private readonly Dictionary<string, ActivityDefinition> _definitions = new(StringComparer.Ordinal);

        public IReadOnlyCollection<ActivityDefinition> Definitions => _definitions.Values;

        public bool Register(ActivityDefinition definition)
        {
            if (definition == null || !definition.IsValid) return false;
            if (_definitions.ContainsKey(definition.ActivityId)) return false;

            _definitions.Add(definition.ActivityId, definition);
            return true;
        }

        public bool Replace(ActivityDefinition definition)
        {
            if (definition == null || !definition.IsValid) return false;
            _definitions[definition.ActivityId] = definition;
            return true;
        }

        public bool TryGet(string activityId, out ActivityDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(activityId) && _definitions.TryGetValue(activityId, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }
    }
}
