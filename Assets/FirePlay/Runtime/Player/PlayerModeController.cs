using System;
using DemonViglu.FirePlay.Activity;
using UnityEngine;

namespace DemonViglu.FirePlay.Player
{
    public enum PlayerMode
    {
        Exploring,
        Placing,
        Resting
    }

    /// <summary>Player 输入模式的唯一事实来源；模式只约束输入，不承载玩法状态。</summary>
    public sealed class PlayerModeController : MonoBehaviour, IActivityPlayerStateView
    {
        public PlayerMode CurrentMode { get; private set; } = PlayerMode.Exploring;
        public string ModeId => CurrentMode.ToString();
        public bool IsExploring => CurrentMode == PlayerMode.Exploring;
        public bool IsPlacing => CurrentMode == PlayerMode.Placing;
        public bool IsResting => CurrentMode == PlayerMode.Resting;

        public bool HasState(string stateId)
        {
            if (string.IsNullOrWhiteSpace(stateId)) return false;
            return stateId.Equals("exploring", StringComparison.OrdinalIgnoreCase) && IsExploring
                || stateId.Equals("placing", StringComparison.OrdinalIgnoreCase) && IsPlacing
                || stateId.Equals("resting", StringComparison.OrdinalIgnoreCase) && IsResting
                || stateId.Equals(ModeId, StringComparison.OrdinalIgnoreCase);
        }

        public bool TryEnter(PlayerMode mode)
        {
            if (CurrentMode != PlayerMode.Exploring)
            {
                return false;
            }

            CurrentMode = mode;
            return true;
        }

        public void Exit(PlayerMode mode)
        {
            if (CurrentMode == mode)
            {
                CurrentMode = PlayerMode.Exploring;
            }
        }
    }
}
