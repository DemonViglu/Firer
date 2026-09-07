using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.UI
{
    public static class FirePlayUiIds
    {
        public const string Hud = "FirePlayHud";
        public const string ActivitySelection = "ActivitySelectionForms";
        public const string Marshmallow = "MarshmallowActivityForms";
        public const string Fishing = "FishingActivityForms";
        public const string Guitar = "GuitarActivityForms";
        public const string Emote = "EmoteActivityForms";
        public const string NetworkConnection = "NetworkConnectionForms";
    }

    public enum FirePlayUiLayer
    {
        Hud,
        Screen,
        Modal
    }

    public enum FirePlayUiStackPolicy
    {
        Normal,
        Stack,
        HideOther
    }

    [CreateAssetMenu(fileName = "FirePlayUiCatalog", menuName = "FirePlay/UI Catalog")]
    public sealed class FirePlayUiCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string _uiId;
            [SerializeField] private GameObject _prefab;
            [SerializeField] private FirePlayUiLayer _layer;
            [SerializeField] private bool _modal;
            [SerializeField] private bool _blocksInput;
            [SerializeField] private FirePlayUiStackPolicy _stackPolicy;

            public string UiId => _uiId;
            public GameObject Prefab => _prefab;
            public FirePlayUiLayer Layer => _layer;
            public bool Modal => _modal;
            public bool BlocksInput => _blocksInput;
            public FirePlayUiStackPolicy StackPolicy => _stackPolicy;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();

        private Dictionary<string, Entry> _byId;

        public IReadOnlyList<Entry> Entries => _entries;

        public bool TryGet(string uiId, out Entry entry)
        {
            EnsureIndex();
            entry = null;
            return !string.IsNullOrWhiteSpace(uiId) && _byId.TryGetValue(uiId, out entry);
        }

        public bool Validate(out string error)
        {
            EnsureIndex();
            if (_byId.Count != _entries.Length)
            {
                error = "Ui Catalog 包含空白或重复 UiId。";
                return false;
            }

            foreach (var entry in _entries)
            {
                if (entry.Prefab == null)
                {
                    error = $"Ui Catalog 的 {entry.UiId} 未绑定 Prefab。";
                    return false;
                }

                if (entry.Prefab.GetComponentInChildren<FirePlayUiView>(true) == null)
                {
                    error = $"Ui Catalog 的 {entry.UiId} Prefab 缺少 FirePlayUiView。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void OnEnable() => _byId = null;
        private void OnValidate() => _byId = null;

        private void EnsureIndex()
        {
            if (_byId != null) return;

            _byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.UiId))
                    continue;

                _byId.TryAdd(entry.UiId.Trim(), entry);
            }
        }
    }
}
