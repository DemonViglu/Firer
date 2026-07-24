using System.Collections.Generic;

namespace Flow.Runtime
{
    public class FlowBlackboard : IFlowBlackboard
    {
        private Dictionary<string, object> m_Data = new Dictionary<string, object>();

        public void Set<T>(string key, T value)
        {
            m_Data[key] = value;
        }

        public T Get<T>(string key, T defaultValue = default)
        {
            if (m_Data.TryGetValue(key, out var val) && val is T typed)
            {
                return typed;
            }
            return defaultValue;
        }

        public bool Has(string key)
        {
            return m_Data.ContainsKey(key);
        }

        public void Remove(string key)
        {
            m_Data.Remove(key);
        }

        public void Clear()
        {
            m_Data.Clear();
        }
    }
}
