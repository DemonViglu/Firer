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
    public sealed class PlayerModeController : MonoBehaviour
    {
        public PlayerMode CurrentMode { get; private set; } = PlayerMode.Exploring;
        public bool IsExploring => CurrentMode == PlayerMode.Exploring;
        public bool IsPlacing => CurrentMode == PlayerMode.Placing;
        public bool IsResting => CurrentMode == PlayerMode.Resting;

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
