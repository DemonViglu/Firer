using System;
using System.Collections.Generic;

namespace DemonViglu.FirePlay.Save
{
    [Serializable]
    public sealed class FirePlaySaveData
    {
        public const int CurrentVersion = 3;
        public int version = CurrentVersion;
        public List<CampfireRecord> campfires = new();
        public TreeProgressData worldTree = new();
    }
}
