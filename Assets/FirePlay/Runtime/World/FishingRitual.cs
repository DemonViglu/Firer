using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>
    /// 湖边停留仪式的地点配置。玩家侧 FishingInteraction 负责输入、等待与表现，
    /// 本组件只提供可调的余火经济与节奏参数。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class FishingRitual : RestLookTargetRitual
    {
        [Header("Rod materialization")]
        [SerializeField, Min(0f)] private float _rodFuelCost = 30f;
        [SerializeField, Min(1)] private int _catchesPerRod = 4;
        [SerializeField, Min(0f)] private float _fuelPerCatch = 8f;

        [Header("Bite timing")]
        [SerializeField, Min(0.1f)] private float _minimumBiteDelay = 1.5f;
        [SerializeField, Min(0.1f)] private float _maximumBiteDelay = 4f;
        [SerializeField, Min(0.1f)] private float _hookWindowSeconds = 2f;

        public float RodFuelCost => _rodFuelCost;
        public int CatchesPerRod => _catchesPerRod;
        public float FuelPerCatch => _fuelPerCatch;
        public float MinimumBiteDelay => _minimumBiteDelay;
        public float MaximumBiteDelay => _maximumBiteDelay;
        public float HookWindowSeconds => _hookWindowSeconds;
        public override string RestHint => "这里适合坐下来，静静钓一会儿鱼";
        public override string ShortRestHint => " · 这里可以坐下钓鱼";

        protected override bool RotatePlayerTowardsTarget => true;
        protected override bool UsesFishingCamera => true;

        public bool TryMaterializeRod(FlameResourceController resourceController, out string status)
        {
            if (resourceController == null || !resourceController.TryConsume(_rodFuelCost))
            {
                status = "余火不够啦，先去找一点暖意吧";
                return false;
            }

            status = "一根小鱼竿在手中慢慢成形";
            return true;
        }

        private void OnValidate()
        {
            _rodFuelCost = Mathf.Max(0f, _rodFuelCost);
            _catchesPerRod = Mathf.Max(1, _catchesPerRod);
            _fuelPerCatch = Mathf.Max(0f, _fuelPerCatch);
            _minimumBiteDelay = Mathf.Max(0.1f, _minimumBiteDelay);
            _maximumBiteDelay = Mathf.Max(_minimumBiteDelay, _maximumBiteDelay);
            _hookWindowSeconds = Mathf.Max(0.1f, _hookWindowSeconds);
        }
    }

    /// <summary>一次收线成功的不可变结果；未来背包、分享或异步痕迹可直接消费。</summary>
    public readonly struct FishingResult
    {
        public int CatchIndex { get; }
        public int CatchesPerRod { get; }
        public float FuelRefund { get; }

        public FishingResult(int catchIndex, int catchesPerRod, float fuelRefund)
        {
            CatchIndex = catchIndex;
            CatchesPerRod = catchesPerRod;
            FuelRefund = fuelRefund;
        }
    }
}
