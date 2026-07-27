using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Save
{
    [Serializable]
    public sealed class CampfireRecord
    {
        public string id;
        public Vector3 position;
        public Quaternion rotation;
        public int level;
        public float totalContribution;
        public bool runtimeCreated;
        public string sourceSmallFireId;
    }
}
