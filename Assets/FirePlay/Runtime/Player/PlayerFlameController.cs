using System;
using System.Collections.Generic;
using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 玩家拥有火焰的逻辑入口。
    /// 火焰是独立世界对象，不要求成为 Player 的子物体；本组件只维护归属与当前主火苗。
    /// </summary>
    public sealed class PlayerFlameController : MonoBehaviour
    {
        [SerializeField] private List<FlameBrush> _ownedFlames = new();
        [SerializeField] private FlameBrush _activeFlame;

        public IReadOnlyList<FlameBrush> OwnedFlames => _ownedFlames;
        public FlameBrush ActiveFlame => _activeFlame;

        public event Action<FlameBrush> ActiveFlameChanged;

        private void Awake()
        {
            RemoveMissingFlames();

            if (_activeFlame == null && _ownedFlames.Count > 0)
            {
                _activeFlame = _ownedFlames[0];
            }

        }

        private void Start()
        {
            // Network-spawned Players receive their Flame during
            // NetworkBehaviour.OnNetworkSpawn, which runs after Awake.
            if (_activeFlame == null)
                Debug.LogWarning("[PlayerFlameController] 玩家当前没有可用火苗。", this);
        }

        /// <summary>
        /// 将火苗加入玩家的拥有列表。重复注册会被忽略。
        /// </summary>
        public void AddFlame(FlameBrush flame, bool makeActive = false)
        {
            if (flame == null || _ownedFlames.Contains(flame))
            {
                return;
            }

            _ownedFlames.Add(flame);
            if (_activeFlame == null || makeActive)
            {
                SetActiveFlame(flame);
            }
        }

        /// <summary>
        /// 移除火苗；可用于火种被放置到世界、献入篝火或暂时参与协作时。
        /// </summary>
        public bool RemoveFlame(FlameBrush flame)
        {
            if (flame == null || !_ownedFlames.Remove(flame))
            {
                return false;
            }

            if (_activeFlame == flame)
            {
                _activeFlame = _ownedFlames.Count > 0 ? _ownedFlames[0] : null;
                ActiveFlameChanged?.Invoke(_activeFlame);
            }

            return true;
        }

        public bool SetActiveFlame(FlameBrush flame)
        {
            if (flame == null || !_ownedFlames.Contains(flame) || _activeFlame == flame)
            {
                return false;
            }

            _activeFlame = flame;
            ActiveFlameChanged?.Invoke(_activeFlame);
            return true;
        }

        private void OnValidate()
        {
            RemoveMissingFlames();
            if (_activeFlame == null && _ownedFlames.Count > 0)
            {
                _activeFlame = _ownedFlames[0];
            }
        }

        private void RemoveMissingFlames()
        {
            _ownedFlames.RemoveAll(flame => flame == null);
        }
    }
}
