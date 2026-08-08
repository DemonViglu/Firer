using System.Collections.Generic;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.World;
using Unity.Netcode;
using UnityEngine;

namespace DemonViglu.FirePlay.Save
{
    public sealed class CampfirePersistence : MonoBehaviour
    {
        [SerializeField] private Campfire _campfirePrefab;
        [SerializeField] private bool _loadOnStart = true;
        private readonly LocalSaveRepository _repository = new();
        private readonly List<CampfireRecord> _retiredRuntimeCampfires = new();
        private bool _isLoading;

        public string Status { get; private set; } = "Not loaded";

        private void Start()
        {
            if (!HasWorldWriteAuthority())
            {
                Status = "Client mirror: persistence disabled";
                return;
            }

            if (_loadOnStart)
            {
                LoadNow();
            }
        }

        private void OnEnable()
        {
            Campfire.StateChanged += SaveAfterCampfireChanged;
            Campfire.Retired += SaveAfterCampfireRetired;
        }

        private void OnDisable()
        {
            Campfire.StateChanged -= SaveAfterCampfireChanged;
            Campfire.Retired -= SaveAfterCampfireRetired;
        }

        [ContextMenu("Save Now")]
        public void SaveNow()
        {
            if (!HasWorldWriteAuthority())
            {
                Status = "Client mirror: save skipped";
                return;
            }

            var data = new FirePlaySaveData();
            data.campfires.AddRange(_retiredRuntimeCampfires);
            foreach (var campfire in Campfire.ActiveInstances)
            {
                data.campfires.Add(campfire.CreateRecord());
            }
            var worldTree = FindAnyObjectByType<WorldTreeContribution>();
            if (worldTree != null)
            {
                data.worldTree = worldTree.CreateRecord();
            }

            Status = _repository.TrySave(data, out var error)
                ? $"Saved {data.campfires.Count} campfires"
                : $"Save failed: {error}";
            Debug.Log($"[CampfirePersistence] {Status}", this);
        }

        [ContextMenu("Load Now")]
        public void LoadNow()
        {
            if (!HasWorldWriteAuthority())
            {
                Status = "Client mirror: load skipped";
                return;
            }

            if (_campfirePrefab == null)
            {
                Status = "Load failed: Missing CampFire Prefab";
                Debug.LogError($"[CampfirePersistence] {Status}", this);
                return;
            }

            if (!_repository.TryLoad(out var data, out var error))
            {
                Status = error == "No save file" ? "No save file" : $"Load failed: {error}";
                return;
            }

            _isLoading = true;
            try
            {
                Campfire.ClearRuntimeInstances();
                _retiredRuntimeCampfires.Clear();
                var worldTree = FindAnyObjectByType<WorldTreeContribution>();
                worldTree?.ApplySavedState(data.worldTree);
                var loaded = 0;
                foreach (var record in data.campfires)
                {
                    if (!record.runtimeCreated)
                    {
                        if (StableSceneId.TryFind(record.id, out var sceneId))
                        {
                            sceneId.GetComponent<Campfire>()?.ApplySavedState(record);
                        }
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(record.sourceSmallFireId) && StableSceneId.TryFind(record.sourceSmallFireId, out var sourceId))
                    {
                        var sourceFire = sourceId.GetComponent<SmallFire>();
                        if (sourceFire != null)
                        {
                            sourceFire.gameObject.SetActive(false);
                        }
                    }

                    if (record.retired)
                    {
                        _retiredRuntimeCampfires.Add(record);
                        Campfire.RegisterRetiredRecord(record);
                        continue;
                    }

                    var instance = Instantiate(_campfirePrefab, record.position, record.rotation);
                    if (instance.RestoreRuntime(record))
                    {
                        SpawnNetworkCampfireIfNeeded(instance);
                        loaded++;
                    }
                    else
                    {
                        Destroy(instance.gameObject);
                    }
                }

                Status = $"Loaded {loaded} campfires";
                Debug.Log($"[CampfirePersistence] {Status}", this);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void SaveAfterCampfireChanged(Campfire _)
        {
            if (!_isLoading)
            {
                SaveNow();
            }
        }

        private void SaveAfterCampfireRetired(Campfire campfire)
        {
            if (_isLoading || campfire == null)
            {
                return;
            }

            _retiredRuntimeCampfires.RemoveAll(record => record.id == campfire.CampfireId);
            _retiredRuntimeCampfires.Add(campfire.CreateRecord());
            SaveNow();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        private static bool HasWorldWriteAuthority()
        {
            var manager = NetworkManager.Singleton;
            return manager == null || !manager.IsListening || manager.IsServer;
        }

        private static void SpawnNetworkCampfireIfNeeded(Campfire campfire)
        {
            var manager = NetworkManager.Singleton;
            if (campfire == null || manager == null || !manager.IsListening || !manager.IsServer)
                return;

            if (campfire.TryGetComponent<NetworkObject>(out var networkObject) && !networkObject.IsSpawned)
                networkObject.Spawn(true);
        }
    }
}
