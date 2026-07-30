using System;

namespace DemonViglu.FirePlay.World
{
    [Serializable]
    public struct CampfireAuthoritySnapshot
    {
        public int Level;
        public float TotalContribution;
        public float Warmth;

        public CampfireAuthoritySnapshot(int level, float totalContribution, float warmth)
        {
            Level = level;
            TotalContribution = totalContribution;
            Warmth = warmth;
        }
    }

    /// <summary>不依赖 Unity 场景、视觉或时间 API 的篝火权威数值状态。</summary>
    public sealed class CampfireAuthorityState
    {
        private readonly float[] _levelThresholds;

        public int Level { get; private set; }
        public float TotalContribution { get; private set; }
        public float Warmth { get; private set; }
        public float MaximumWarmth { get; }
        public float WarmthDecayPerSecond { get; }
        public float WarmthPerTend { get; }
        public float TendFuelCost { get; }
        public float EmergencyWithdrawFuel { get; }
        public float EmergencyWithdrawWarmthCost { get; }
        public int MaximumLevel => Math.Max(0, _levelThresholds.Length - 1);
        public bool IsExtinguished => Warmth <= 0.001f;
        public bool NeedsTending => Warmth < MaximumWarmth - 0.001f;
        public bool IsMaxLevel => Level >= MaximumLevel;
        public float EstimatedBurnSeconds => IsExtinguished
            ? 0f
            : WarmthDecayPerSecond <= 0f ? float.PositiveInfinity : Warmth / WarmthDecayPerSecond;

        public CampfireAuthorityState(
            float[] levelThresholds,
            float maximumWarmth,
            float warmthDecayPerSecond,
            float warmthPerTend,
            float tendFuelCost,
            float emergencyWithdrawFuel,
            float emergencyWithdrawWarmthCost,
            CampfireAuthoritySnapshot initial)
        {
            _levelThresholds = levelThresholds is { Length: > 0 } ? (float[])levelThresholds.Clone() : new[] { 0f };
            MaximumWarmth = Math.Max(1f, maximumWarmth);
            WarmthDecayPerSecond = Math.Max(0f, warmthDecayPerSecond);
            WarmthPerTend = Math.Max(0f, warmthPerTend);
            TendFuelCost = Math.Max(0f, tendFuelCost);
            EmergencyWithdrawFuel = Math.Max(0f, emergencyWithdrawFuel);
            EmergencyWithdrawWarmthCost = Math.Max(0f, emergencyWithdrawWarmthCost);
            Restore(initial);
        }

        public CampfireAuthoritySnapshot CreateSnapshot() => new(Level, TotalContribution, Warmth);

        public void Restore(CampfireAuthoritySnapshot snapshot)
        {
            Level = Clamp(snapshot.Level, 0, MaximumLevel);
            TotalContribution = Math.Max(0f, snapshot.TotalContribution);
            Warmth = Clamp(snapshot.Warmth, 0f, MaximumWarmth);
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || Warmth <= 0f) return;
            Warmth = Math.Max(0f, Warmth - WarmthDecayPerSecond * deltaTime);
        }

        public bool CanTend(float availableFuel) => NeedsTending && availableFuel >= TendFuelCost;

        public bool ApplyTend()
        {
            if (!NeedsTending) return false;
            var previousLevel = Level;
            TotalContribution += TendFuelCost;
            Warmth = Math.Min(MaximumWarmth, Warmth + WarmthPerTend);
            Level = Math.Max(Level, GetLevelForContribution(TotalContribution));
            return Level > previousLevel;
        }

        public bool CanWithdraw(float playerFuel, float playerMaximumFuel) =>
            !IsExtinguished && playerFuel <= playerMaximumFuel - EmergencyWithdrawFuel && Warmth >= EmergencyWithdrawWarmthCost;

        public bool ApplyWithdraw()
        {
            if (Warmth < EmergencyWithdrawWarmthCost) return false;
            Warmth = Math.Max(0f, Warmth - EmergencyWithdrawWarmthCost);
            return true;
        }

        private int GetLevelForContribution(float contribution)
        {
            var level = 0;
            for (var index = 1; index < _levelThresholds.Length; index++)
            {
                if (contribution < _levelThresholds[index]) break;
                level = index;
            }
            return level;
        }

        private static int Clamp(int value, int minimum, int maximum) => Math.Min(Math.Max(value, minimum), maximum);
        private static float Clamp(float value, float minimum, float maximum) => Math.Min(Math.Max(value, minimum), maximum);
    }
}
