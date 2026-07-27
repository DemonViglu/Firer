using System;
using System.Collections.Generic;
using UnityEngine;

namespace DemonViglu.FirePlay.Save
{
    [Serializable]
    public sealed class TreeProgressData
    {
        public bool hasContributed;
        public float totalContribution;
        public int contributionCount;
        public Color personalLightColor = Color.white;
        public List<TreePersonalLightRecord> personalLights = new();
    }
}
