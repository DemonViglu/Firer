using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Global activity definition list. A definition is authored once here and
    /// can be referenced by any number of ActivityAnchorNode instances.
    /// </summary>
    [CreateAssetMenu(fileName = "ActivityCatalog", menuName = "FirePlay/Activity/Catalog")]
    public sealed class ActivityCatalogAsset : ScriptableObject
    {
        [SerializeField] private ActivityDefinitionAsset[] _definitions = Array.Empty<ActivityDefinitionAsset>();

        public ActivityDefinitionAsset[] Definitions => _definitions ?? Array.Empty<ActivityDefinitionAsset>();

        public ActivityCatalog CreateRuntimeCatalog()
        {
            var catalog = new ActivityCatalog();
            foreach (var asset in Definitions)
            {
                if (asset == null) continue;
                catalog.Register(asset.CreateRuntimeDefinition());
            }

            return catalog;
        }

        private void OnValidate()
        {
            if (_definitions == null) _definitions = Array.Empty<ActivityDefinitionAsset>();
        }
    }
}
