using System;

namespace DemonViglu.FirePlay.Flame
{
    /// <summary>
    /// 不持有 Unity 场景引用的余火运行时状态。
    /// 使用进入/退出两个阈值形成回差，避免模式在边界反复切换。
    /// </summary>
    public sealed class FlameResourceState
    {
        private readonly float _receiverEnterFuel;
        private readonly float _giverEnterFuel;

        public FlameResourceState(
            float initialFuel,
            float maxFuel,
            float receiverEnterFuel,
            float giverEnterFuel)
        {
            MaxFuel = Math.Max(0.01f, maxFuel);
            _receiverEnterFuel = Clamp(receiverEnterFuel, 0f, MaxFuel);
            _giverEnterFuel = Clamp(giverEnterFuel, _receiverEnterFuel, MaxFuel);
            CurrentFuel = Clamp(initialFuel, 0f, MaxFuel);
            Mode = CurrentFuel <= _receiverEnterFuel ? FlameMode.Receiver : FlameMode.Giver;
        }

        public float CurrentFuel { get; private set; }
        public float MaxFuel { get; }
        public float NormalizedFuel => CurrentFuel / MaxFuel;
        public FlameMode Mode { get; private set; }
        public bool IsReceiverOverride { get; private set; }

        public event Action<float, float> FuelChanged;
        public event Action<FlameMode> ModeChanged;

        public bool TryConsume(float amount)
        {
            if (amount < 0f || CurrentFuel + 0.0001f < amount)
            {
                return false;
            }

            if (amount == 0f) return true;
            SetFuel(CurrentFuel - amount);
            return true;
        }

        /// <summary>用于夜间衰减、冲刺等连续消耗；余额不足时消耗至零。</summary>
        public bool ConsumeUpTo(float amount)
        {
            if (amount <= 0f || CurrentFuel <= 0f)
            {
                return false;
            }

            SetFuel(CurrentFuel - amount);
            return true;
        }

        public bool Restore(float amount)
        {
            if (amount <= 0f || CurrentFuel >= MaxFuel)
            {
                return false;
            }

            SetFuel(CurrentFuel + amount);
            return true;
        }

        /// <summary>
        /// Applies an exact value received from an authoritative snapshot.
        /// The same clamp, fuel notification and hysteresis rules used by
        /// normal gameplay mutations remain in force.
        /// </summary>
        public void ApplySnapshot(float currentFuel)
        {
            SetFuel(currentFuel);
        }

        public void SetReceiverOverride(bool active)
        {
            if (IsReceiverOverride == active)
            {
                return;
            }

            IsReceiverOverride = active;
            RefreshMode();
        }

        private void SetFuel(float value)
        {
            var previousFuel = CurrentFuel;
            CurrentFuel = Clamp(value, 0f, MaxFuel);

            if (Math.Abs(CurrentFuel - previousFuel) < 0.0001f)
            {
                return;
            }

            FuelChanged?.Invoke(previousFuel, CurrentFuel);
            RefreshMode();
        }

        private void RefreshMode()
        {
            var nextMode = Mode;

            if (IsReceiverOverride)
            {
                nextMode = FlameMode.Receiver;
            }
            else if (Mode == FlameMode.Giver && CurrentFuel <= _receiverEnterFuel)
            {
                nextMode = FlameMode.Receiver;
            }
            else if (Mode == FlameMode.Receiver && CurrentFuel >= _giverEnterFuel)
            {
                nextMode = FlameMode.Giver;
            }

            if (nextMode == Mode)
            {
                return;
            }

            Mode = nextMode;
            ModeChanged?.Invoke(Mode);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }
    }
}
