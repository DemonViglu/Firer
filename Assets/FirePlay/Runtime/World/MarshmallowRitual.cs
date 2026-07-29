using System;
using DemonViglu.FirePlay.Flame;
using UnityEngine;

namespace DemonViglu.FirePlay.World
{
    /// <summary>烤棉花停留仪式；消耗少量余火拟造棉花糖，手持表现由 Player 管理。</summary>
    [DefaultExecutionOrder(100)]
    public sealed class MarshmallowRitual : RestLookTargetRitual
    {
        [SerializeField, Min(0f)] private float _materializeFuelCost = 3f;
        [SerializeField, Min(1)] private int _turnsRequired = 2;
        [SerializeField, Min(0.05f)] private float _needleCyclesPerSecond = 0.35f;
        [SerializeField, Range(0.05f, 0.8f)] private float _perfectZoneWidth = 0.24f;
        [SerializeField, Range(0f, 0.5f)] private float _targetEdgePadding = 0.15f;
        [Header("Result Rewards")]
        [SerializeField, Min(0f)] private float _perfectEatFuelRefund = 2f;
        [SerializeField, Min(0f)] private float _toastedEatFuelRefund = 1f;
        [SerializeField, Min(0f)] private float _scorchedEatFuelRefund;

        public float MaterializeFuelCost => _materializeFuelCost;
        public int TurnsRequired => _turnsRequired;
        public float NeedleCyclesPerSecond => _needleCyclesPerSecond;
        public float PerfectZoneWidth => _perfectZoneWidth;
        public float TargetEdgePadding => _targetEdgePadding;
        public bool IsCampfireBurning => TryGetComponent<Campfire>(out var campfire) && !campfire.IsExtinguished;

        protected override bool RotatePlayerTowardsTarget => true;
        protected override bool UsesExternalCamera => true;

        public bool TryMaterialize(FlameResourceController resourceController, out string status)
        {
            if (!IsCampfireBurning)
            {
                status = "火焰熄灭了，先添一把火吧";
                return false;
            }

            if (resourceController == null || !resourceController.TryConsume(_materializeFuelCost))
            {
                status = "余火不够啦，先去找一点暖意吧";
                return false;
            }

            status = "一颗软乎乎的棉花糖出现了";
            return true;
        }

        public MarshmallowResult CreateResult(MarshmallowRoastSession session)
        {
            if (session == null || !session.IsReadyToEat)
            {
                return default;
            }

            var quality = session.IsPerfect
                ? MarshmallowRoastQuality.Perfect
                : session.PerfectTurns == 0
                    ? MarshmallowRoastQuality.Scorched
                    : MarshmallowRoastQuality.Toasted;
            var refund = quality switch
            {
                MarshmallowRoastQuality.Perfect => _perfectEatFuelRefund,
                MarshmallowRoastQuality.Toasted => _toastedEatFuelRefund,
                _ => _scorchedEatFuelRefund
            };
            return new MarshmallowResult(quality, session.PerfectTurns, session.TurnsRequired, refund);
        }

        private void OnValidate()
        {
            _materializeFuelCost = Mathf.Max(0f, _materializeFuelCost);
            _turnsRequired = Mathf.Max(1, _turnsRequired);
            _needleCyclesPerSecond = Mathf.Max(0.05f, _needleCyclesPerSecond);
            _perfectZoneWidth = Mathf.Clamp(_perfectZoneWidth, 0.05f, 0.8f);
            _targetEdgePadding = Mathf.Clamp(_targetEdgePadding, 0f, 0.5f - _perfectZoneWidth * 0.5f);
            _perfectEatFuelRefund = Mathf.Max(0f, _perfectEatFuelRefund);
            _toastedEatFuelRefund = Mathf.Max(0f, _toastedEatFuelRefund);
            _scorchedEatFuelRefund = Mathf.Max(0f, _scorchedEatFuelRefund);
        }
    }

    public enum MarshmallowRoastQuality
    {
        Scorched,
        Toasted,
        Perfect
    }

    /// <summary>一次完成烤制的不可变结果。当前仅本局使用，未来可供分享或异步赠礼消费。</summary>
    public readonly struct MarshmallowResult
    {
        public MarshmallowRoastQuality Quality { get; }
        public int PerfectTurns { get; }
        public int TurnsRequired { get; }
        public float FuelRefund { get; }

        public MarshmallowResult(MarshmallowRoastQuality quality, int perfectTurns, int turnsRequired, float fuelRefund)
        {
            Quality = quality;
            PerfectTurns = perfectTurns;
            TurnsRequired = turnsRequired;
            FuelRefund = fuelRefund;
        }
    }

    /// <summary>
    /// 一次烤棉花糖的纯玩法状态：不读取输入、不显示 UI，也不持有场景对象。
    /// Player 表现层只需推进它、请求翻面并读取结果。
    /// </summary>
    public sealed class MarshmallowRoastSession
    {
        private readonly int _turnsRequired;
        private readonly float _needleCyclesPerSecond;
        private readonly float _perfectZoneWidth;
        private readonly float _targetEdgePadding;
        private readonly System.Random _random;

        public int CompletedTurns { get; private set; }
        public int PerfectTurns { get; private set; }
        public float NeedlePosition { get; private set; }
        public float TargetCenter { get; private set; }
        public bool IsReadyToEat => CompletedTurns >= _turnsRequired;
        public bool IsRoasting => !IsReadyToEat;
        public bool IsPerfect => IsReadyToEat && PerfectTurns == _turnsRequired;
        public int TurnsRequired => _turnsRequired;

        public MarshmallowRoastSession(
            int turnsRequired,
            float needleCyclesPerSecond,
            float perfectZoneWidth,
            float targetEdgePadding,
            System.Random random = null)
        {
            _turnsRequired = Math.Max(1, turnsRequired);
            _needleCyclesPerSecond = Math.Max(0.05f, needleCyclesPerSecond);
            _perfectZoneWidth = Clamp(perfectZoneWidth, 0.05f, 0.8f);
            _targetEdgePadding = Clamp(targetEdgePadding, 0f, 0.5f - _perfectZoneWidth * 0.5f);
            _random = random ?? new System.Random();
            ResetRound();
        }

        public void Advance(float deltaTime)
        {
            if (!IsRoasting || deltaTime <= 0f)
            {
                return;
            }

            NeedlePosition = (NeedlePosition + deltaTime * _needleCyclesPerSecond) % 1f;
        }

        public bool TryTurn()
        {
            if (!IsRoasting)
            {
                return false;
            }

            var wasPerfect = Math.Abs(NeedlePosition - TargetCenter) <= _perfectZoneWidth * 0.5f;
            CompletedTurns++;
            if (wasPerfect)
            {
                PerfectTurns++;
            }

            if (IsRoasting)
            {
                ResetRound();
            }

            return wasPerfect;
        }

        private void ResetRound()
        {
            // Every new side starts at the left edge and gets a fresh golden zone.
            NeedlePosition = 0f;
            var halfWidth = _perfectZoneWidth * 0.5f;
            var minimum = _targetEdgePadding + halfWidth;
            var maximum = 1f - _targetEdgePadding - halfWidth;
            TargetCenter = minimum + (float)_random.NextDouble() * Math.Max(0f, maximum - minimum);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }
    }
}
