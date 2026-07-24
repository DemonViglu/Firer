namespace Flow.Runtime
{
    public interface IFlowBlackboard
    {
        void Set<T>(string key, T value);
        T Get<T>(string key, T defaultValue = default);
        bool Has(string key);
        void Remove(string key);
        void Clear();
    }
}
