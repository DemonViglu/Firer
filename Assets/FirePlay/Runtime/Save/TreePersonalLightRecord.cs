using System;
using UnityEngine;

namespace DemonViglu.FirePlay.Save
{
    [Serializable]
    public sealed class TreePersonalLightRecord
    {
        public string playerId;
        public Color color;
        public float positionSeed;
    }
}
