using System;
using System.IO;
using UnityEngine;

namespace DemonViglu.FirePlay.Save
{
    public sealed class LocalSaveRepository
    {
        private const string FileName = "fireplay-demo.json";
        public string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public bool TrySave(FirePlaySaveData data, out string error)
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                Debug.Log($"Save to {SavePath}");
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public bool TryLoad(out FirePlaySaveData data, out string error)
        {
            data = null;
            if (!File.Exists(SavePath))
            {
                error = "No save file";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<FirePlaySaveData>(File.ReadAllText(SavePath));
                if (data == null || (data.version != 2 && data.version != FirePlaySaveData.CurrentVersion))
                {
                    error = "Unsupported save version";
                    return false;
                }
                Debug.Log($"Load from {SavePath}");
                data.campfires ??= new();
                data.worldTree ??= new();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
