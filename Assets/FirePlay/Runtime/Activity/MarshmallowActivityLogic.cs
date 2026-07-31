using System;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Pure multi-action marshmallow activity. It owns only the gameplay state;
    /// the marshmallow prop, animation and dedicated UI belong to the
    /// presentation layer that receives this state from the activity host.
    /// </summary>
    public sealed class MarshmallowActivityLogic : IActivityLogic, IActivityTickable
    {
        public const string ActivityId = "marshmallow";

        private readonly float _materializeFuelCost;
        private readonly int _turnsRequired;
        private readonly float _needleCyclesPerSecond;
        private readonly float _perfectZoneWidth;
        private readonly float _targetEdgePadding;
        private readonly float _perfectEatFuelRefund;
        private readonly float _toastedEatFuelRefund;
        private readonly float _scorchedEatFuelRefund;
        private readonly Random _random;

        private MarshmallowRoastState _roast;
        private MarshmallowActivityResult? _completedResult;

        public bool HasMaterialized => _roast != null;
        public bool IsRoasting => _roast != null && !_roast.IsReadyToEat;
        public bool IsReadyToEat => _roast != null && _roast.IsReadyToEat;
        public int CompletedTurns => _roast?.CompletedTurns ?? 0;
        public int PerfectTurns => _roast?.PerfectTurns ?? 0;
        public float NeedlePosition => _roast?.NeedlePosition ?? 0f;
        public float TargetCenter => _roast?.TargetCenter ?? 0f;
        public MarshmallowActivityResult? CompletedResult => _completedResult;

        public MarshmallowActivityLogic(
            float materializeFuelCost = 3f,
            int turnsRequired = 2,
            float needleCyclesPerSecond = 0.35f,
            float perfectZoneWidth = 0.24f,
            float targetEdgePadding = 0.15f,
            float perfectEatFuelRefund = 2f,
            float toastedEatFuelRefund = 1f,
            float scorchedEatFuelRefund = 0f,
            Random random = null)
        {
            _materializeFuelCost = Math.Max(0f, materializeFuelCost);
            _turnsRequired = Math.Max(1, turnsRequired);
            _needleCyclesPerSecond = Math.Max(0.05f, needleCyclesPerSecond);
            _perfectZoneWidth = Clamp(perfectZoneWidth, 0.05f, 0.8f);
            _targetEdgePadding = Clamp(targetEdgePadding, 0f, 0.5f - _perfectZoneWidth * 0.5f);
            _perfectEatFuelRefund = Math.Max(0f, perfectEatFuelRefund);
            _toastedEatFuelRefund = Math.Max(0f, toastedEatFuelRefund);
            _scorchedEatFuelRefund = Math.Max(0f, scorchedEatFuelRefund);
            _random = random ?? new Random();
        }

        public ActivityStartCheck CheckStart(IActivityContext context)
        {
            if (context == null || context.ActivityId != ActivityId)
                return ActivityStartCheck.Deny("Marshmallow context is invalid");
            if (context.Flame == null)
                return ActivityStartCheck.Deny("Marshmallow needs a flame resource");
            return ActivityStartCheck.Allow();
        }

        public IActivityStartOperation PrepareStart(IActivityContext context)
        {
            return new ActivityStartOperation(
                () =>
                {
                    ResetState();
                    return true;
                },
                ResetState);
        }

        public ActivityActionResult HandleAction(IActivityContext context, ActivityActionRequest request)
        {
            if (context == null || context.Flame == null)
                return ActivityActionResult.Reject("Marshmallow needs a flame resource");

            return request.ActionId switch
            {
                "marshmallow.materialize" => Materialize(context),
                "marshmallow.turn" => Turn(),
                "marshmallow.eat" => Eat(context),
                "activity.exit" => ActivityActionResult.End(ActivityEndReason.Requested, "Marshmallow activity exited"),
                _ => ActivityActionResult.Reject("Unknown marshmallow action")
            };
        }

        public void Tick(IActivityContext context, float deltaTime)
        {
            if (_roast != null && !_roast.IsReadyToEat)
                _roast.Advance(deltaTime);
        }

        public void End(IActivityContext context, ActivityEndReason reason)
        {
            ResetState();
        }

        private ActivityActionResult Materialize(IActivityContext context)
        {
            if (_roast != null)
                return ActivityActionResult.Reject("Marshmallow is already materialized");
            if (!context.Flame.TryConsume(_materializeFuelCost))
                return ActivityActionResult.Reject("Not enough flame to materialize marshmallow");

            _roast = new MarshmallowRoastState(
                _turnsRequired,
                _needleCyclesPerSecond,
                _perfectZoneWidth,
                _targetEdgePadding,
                _random);
            _completedResult = null;
            return ActivityActionResult.Consume("Marshmallow materialized");
        }

        private ActivityActionResult Turn()
        {
            if (_roast == null)
                return ActivityActionResult.Reject("Materialize the marshmallow first");
            if (_roast.IsReadyToEat)
                return ActivityActionResult.Reject("Marshmallow is ready to eat");

            var perfect = _roast.TryTurn();
            if (_roast.IsReadyToEat)
            {
                _completedResult = CreateResult(_roast);
                return ActivityActionResult.Consume(perfect
                    ? "Perfect final turn; marshmallow is ready"
                    : "Final turn; marshmallow is ready");
            }

            return ActivityActionResult.Consume(perfect ? "Perfect turn" : "Turn accepted");
        }

        private ActivityActionResult Eat(IActivityContext context)
        {
            if (_roast == null || !_roast.IsReadyToEat || !_completedResult.HasValue)
                return ActivityActionResult.Reject("Marshmallow is not ready to eat");

            var result = _completedResult.Value;
            if (result.FuelRefund > 0f)
                context.Flame.Restore(result.FuelRefund);

            return ActivityActionResult.End(ActivityEndReason.Completed, "Marshmallow eaten");
        }

        private MarshmallowActivityResult CreateResult(MarshmallowRoastState roast)
        {
            var quality = roast.IsPerfect
                ? MarshmallowRoastQuality.Perfect
                : roast.PerfectTurns == 0
                    ? MarshmallowRoastQuality.Scorched
                    : MarshmallowRoastQuality.Toasted;
            var refund = quality switch
            {
                MarshmallowRoastQuality.Perfect => _perfectEatFuelRefund,
                MarshmallowRoastQuality.Toasted => _toastedEatFuelRefund,
                _ => _scorchedEatFuelRefund
            };
            return new MarshmallowActivityResult(
                quality,
                roast.PerfectTurns,
                roast.TurnsRequired,
                refund);
        }

        private void ResetState()
        {
            _roast = null;
            _completedResult = null;
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Min(Math.Max(value, minimum), maximum);
        }
    }

    public enum MarshmallowRoastQuality
    {
        Scorched,
        Toasted,
        Perfect
    }

    public readonly struct MarshmallowActivityResult
    {
        public MarshmallowRoastQuality Quality { get; }
        public int PerfectTurns { get; }
        public int TurnsRequired { get; }
        public float FuelRefund { get; }

        public MarshmallowActivityResult(
            MarshmallowRoastQuality quality,
            int perfectTurns,
            int turnsRequired,
            float fuelRefund)
        {
            Quality = quality;
            PerfectTurns = perfectTurns;
            TurnsRequired = turnsRequired;
            FuelRefund = fuelRefund;
        }
    }

    /// <summary>不依赖 Unity 的烘烤时序状态。</summary>
    public sealed class MarshmallowRoastState
    {
        private readonly int _turnsRequired;
        private readonly float _needleCyclesPerSecond;
        private readonly float _perfectZoneWidth;
        private readonly float _targetEdgePadding;
        private readonly Random _random;

        public int CompletedTurns { get; private set; }
        public int PerfectTurns { get; private set; }
        public float NeedlePosition { get; private set; }
        public float TargetCenter { get; private set; }
        public bool IsReadyToEat => CompletedTurns >= _turnsRequired;
        public bool IsPerfect => IsReadyToEat && PerfectTurns == _turnsRequired;
        public int TurnsRequired => _turnsRequired;

        public MarshmallowRoastState(
            int turnsRequired,
            float needleCyclesPerSecond,
            float perfectZoneWidth,
            float targetEdgePadding,
            Random random)
        {
            _turnsRequired = Math.Max(1, turnsRequired);
            _needleCyclesPerSecond = Math.Max(0.05f, needleCyclesPerSecond);
            _perfectZoneWidth = Clamp(perfectZoneWidth, 0.05f, 0.8f);
            _targetEdgePadding = Clamp(targetEdgePadding, 0f, 0.5f - _perfectZoneWidth * 0.5f);
            _random = random ?? new Random();
            ResetRound();
        }

        public void Advance(float deltaTime)
        {
            if (!IsReadyToEat && deltaTime > 0f)
                NeedlePosition = (NeedlePosition + deltaTime * _needleCyclesPerSecond) % 1f;
        }

        public bool TryTurn()
        {
            if (IsReadyToEat) return false;

            var isPerfect = Math.Abs(NeedlePosition - TargetCenter) <= _perfectZoneWidth * 0.5f;
            CompletedTurns++;
            if (isPerfect) PerfectTurns++;
            if (!IsReadyToEat) ResetRound();
            return isPerfect;
        }

        private void ResetRound()
        {
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
