using UnityEngine;

namespace DemonViglu.FirePlay.Data
{
    [CreateAssetMenu(menuName = "DemonViglu/FirePlay/Campfire Config", fileName = "CampfireConfig_")]
    public sealed class CampfireConfig : ScriptableObject
    {
        [SerializeField] private float[] _upgradeCosts = { 25f, 35f, 50f };

        public int MaximumLevel => _upgradeCosts != null ? _upgradeCosts.Length : 0;

        public float GetUpgradeCost(int currentLevel)
        {
            if (_upgradeCosts == null || currentLevel < 0 || currentLevel >= _upgradeCosts.Length)
            {
                return 0f;
            }

            return Mathf.Max(0f, _upgradeCosts[currentLevel]);
        }

        private void OnValidate()
        {
            if (_upgradeCosts == null)
            {
                _upgradeCosts = System.Array.Empty<float>();
                return;
            }

            for (var index = 0; index < _upgradeCosts.Length; index++)
            {
                _upgradeCosts[index] = Mathf.Max(0.01f, _upgradeCosts[index]);
            }
        }
    }
}