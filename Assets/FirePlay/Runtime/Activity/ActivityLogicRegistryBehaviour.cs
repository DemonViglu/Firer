using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Unity adapter for the pure ActivityLogicRegistry. Put one registry in
    /// the scene and add one focused factory per activity. PlayerActivityHost
    /// references only this adapter, so adding a new activity does not change
    /// an existing activity factory or the Player prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActivityLogicRegistryBehaviour : MonoBehaviour, IActivityLogicFactory
    {
        [SerializeField] private MonoBehaviour[] _factories = System.Array.Empty<MonoBehaviour>();

        public IActivityLogic Create(ActivityDefinition definition)
        {
            if (definition == null || _factories == null)
                return null;

            foreach (var candidate in _factories)
            {
                if (candidate is IActivityLogicFactory factory)
                {
                    var logic = factory.Create(definition);
                    if (logic != null)
                        return logic;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            if (_factories == null)
                _factories = System.Array.Empty<MonoBehaviour>();
        }
    }
}
