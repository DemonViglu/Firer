using UnityEngine;

namespace DemonViglu.FirePlay.Core
{
    public interface IWorldCommandVersioned
    {
        uint CommandVersion { get; }
    }

    public interface IWorldObjectRegistry
    {
        bool TryResolve<T>(string stableId, out T component) where T : Component;
        bool TryGetCommandVersion(string stableId, out uint version);
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

        public bool TryGetCommandVersion(string stableId, out uint version)
        {
            version = 0;
            if (!StableSceneId.TryFind(stableId, out var stableSceneId) || stableSceneId == null)
                return false;

            foreach (var behaviour in stableSceneId.GetComponents<MonoBehaviour>())
            {
                if (behaviour is not IWorldCommandVersioned versioned) continue;
                version = versioned.CommandVersion;
                return true;
            }
            return false;
        }
    }
}
