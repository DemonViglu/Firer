using UnityEngine;

namespace DemonViglu.FirePlay.Core
{
    public interface IWorldObjectRegistry
    {
        bool TryResolve<T>(string stableId, out T component) where T : Component;
    }

    /// <summary>通过 StableSceneId 定位世界对象，不缓存 Unity 实例引用。</summary>
    public sealed class StableIdWorldObjectRegistry : IWorldObjectRegistry
    {
        public bool TryResolve<T>(string stableId, out T component) where T : Component
        {
            component = null;
            if (!StableSceneId.TryFind(stableId, out var stableSceneId) || stableSceneId == null)
                return false;

            component = stableSceneId.GetComponent<T>();
            return component != null;
        }
    }
}
