using System.Collections.Generic;
using DemonViglu.FirePlay.Core;
using DemonViglu.FirePlay.World;
using UnityEngine;

namespace DemonViglu.FirePlay.Save
{
    public sealed class CampfirePersistence : MonoBehaviour
    {
        [SerializeField] private Campfire _campfirePrefab;
        [SerializeField] private bool _loadOnStart = true;
        private readonly LocalSaveRepository _repository = new();

        public string Status { get; private set; } = "Not loaded";

        private void Start()
        {
            if (_loadOnStart)
            {
                LoadNow();
            }
        }

        [ContextMenu("Save Now")]
        public void SaveNow()
        {
            var data = new FirePlaySaveData();
            foreach (var campfire in Campfire.ActiveInstances)
            {
                data.campfires.Add(campfire.CreateRecord());
            }

            Status = _repository.TrySave(data, out var error)
                ? $"Saved {data.campfires.Count} campfires"
                : $"Save failed: {error}";
            Debug.Log($"[CampfirePersistence] {Status}", this);
        }

        [ContextMenu("Load Now")]
        public void LoadNow()
        {
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

            Campfire.ClearRuntimeInstances();
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

                var instance = Instantiate(_campfirePrefab, record.position, record.rotation);
                if (instance.RestoreRuntime(record))
                {
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
    }
}
