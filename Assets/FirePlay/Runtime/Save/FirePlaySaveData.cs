using System;
using System.Collections.Generic;

namespace DemonViglu.FirePlay.Save
{
    [Serializable]
    public sealed class FirePlaySaveData
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public List<CampfireRecord> campfires = new();
    }
}
