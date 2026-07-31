using System;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Pure fishing activity state. The activity is anchored, but its rules and
    /// resource transaction are independent from RestInteraction and scene objects.
    /// </summary>
    public sealed class FishingActivityLogic : IActivityLogic, IActivityTickable
    {
        public const string ActivityId = "fishing";

        private enum FishingState
        {
            None,
            ReadyToCast,
            WaitingForBite,
            BiteReady
        }

        private readonly float _rodFuelCost;
        private readonly int _catchesPerRod;
        private readonly float _fuelPerCatch;
        private readonly float _minimumBiteDelay;
        private readonly float _maximumBiteDelay;
        private readonly float _hookWindowSeconds;
        private readonly Random _random;

        private FishingState _state;
        private int _catches;
        private float _stateEndsAt;

        public bool HasRod => _state != FishingState.None;
        public bool IsLineCast => _state == FishingState.WaitingForBite || _state == FishingState.BiteReady;
        public bool IsFishBiting => _state == FishingState.BiteReady;
        public int Catches => _catches;
        public int CatchesPerRod => _catchesPerRod;
        public string Status { get; private set; } = "准备钓鱼";

        public FishingActivityLogic(
            float rodFuelCost = 30f,
            int catchesPerRod = 4,
            float fuelPerCatch = 8f,
            float minimumBiteDelay = 1.5f,
            float maximumBiteDelay = 4f,
            float hookWindowSeconds = 2f,
            Random random = null)
        {
            _rodFuelCost = Math.Max(0f, rodFuelCost);
            _catchesPerRod = Math.Max(1, catchesPerRod);
            _fuelPerCatch = Math.Max(0f, fuelPerCatch);
            _minimumBiteDelay = Math.Max(0.1f, minimumBiteDelay);
            _maximumBiteDelay = Math.Max(_minimumBiteDelay, maximumBiteDelay);
            _hookWindowSeconds = Math.Max(0.1f, hookWindowSeconds);
            _random = random ?? new Random();
        }

        public ActivityStartCheck CheckStart(IActivityContext context)
        {
            if (context == null || context.ActivityId != ActivityId)
                return ActivityStartCheck.Deny("Fishing context is invalid");
            if (context.Location == null)
                return ActivityStartCheck.Deny("Fishing needs a location anchor");
            if (context.Flame == null)
                return ActivityStartCheck.Deny("Fishing needs a flame resource");
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
                return ActivityActionResult.Reject("Fishing needs a flame resource");

            return request.ActionId switch
            {
                "fishing.primary" => HandlePrimary(context),
                "fishing.materialize" => Materialize(context),
                "fishing.cast" => Cast(),
                "fishing.reel" => Reel(context),
                "activity.exit" => ActivityActionResult.End(ActivityEndReason.Requested, "Fishing activity exited"),
                _ => ActivityActionResult.Reject("Unknown fishing action")
            };
        }

        public void Tick(IActivityContext context, float deltaTime)
        {
            if (_state == FishingState.WaitingForBite && deltaTime > 0f)
            {
                _stateEndsAt -= deltaTime;
                if (_stateEndsAt <= 0f)
                {
                    _state = FishingState.BiteReady;
                    _stateEndsAt = _hookWindowSeconds;
                    Status = "有鱼咬钩了，快收线";
                }
            }
            else if (_state == FishingState.BiteReady && deltaTime > 0f)
            {
                _stateEndsAt -= deltaTime;
                if (_stateEndsAt <= 0f)
                {
                    _state = FishingState.ReadyToCast;
                    Status = "鱼儿游走了，再试一次吧";
                }
            }
        }

        public void End(IActivityContext context, ActivityEndReason reason)
        {
            ResetState();
        }

        private ActivityActionResult HandlePrimary(IActivityContext context)
        {
            if (!HasRod) return Materialize(context);
            return Cast();
        }

        private ActivityActionResult Materialize(IActivityContext context)
        {
            if (HasRod)
                return ActivityActionResult.Reject("Fishing rod is already materialized");
            if (!context.Flame.TryConsume(_rodFuelCost))
                return ActivityActionResult.Reject("Not enough flame to materialize fishing rod");

            _state = FishingState.ReadyToCast;
            _catches = 0;
            Status = "鱼竿准备好了，可以抛竿";
            return ActivityActionResult.Consume("Fishing rod materialized");
        }

        private ActivityActionResult Cast()
        {
            if (!HasRod)
                return ActivityActionResult.Reject("Materialize the fishing rod first");
            if (_state != FishingState.ReadyToCast)
                return ActivityActionResult.Reject("The fishing line is already cast");

            _state = FishingState.WaitingForBite;
            _stateEndsAt = _minimumBiteDelay
                + (float)_random.NextDouble() * (_maximumBiteDelay - _minimumBiteDelay);
            Status = "鱼线随着水波轻轻漂着……";
            return ActivityActionResult.Consume("Fishing line cast");
        }

        private ActivityActionResult Reel(IActivityContext context)
        {
            if (!HasRod || !IsFishBiting)
                return ActivityActionResult.Reject("There is no fish to reel");

            _catches++;
            if (_fuelPerCatch > 0f)
                context.Flame.Restore(_fuelPerCatch);

            if (_catches >= _catchesPerRod)
            {
                return ActivityActionResult.End(ActivityEndReason.Completed, "Fishing session completed");
            }

            _state = FishingState.ReadyToCast;
            Status = $"钓到第 {_catches}/{_catchesPerRod} 条鱼，余火返还 {_fuelPerCatch:0}";
            return ActivityActionResult.Consume("Fish reeled");
        }

        private void ResetState()
        {
            _state = FishingState.None;
            _catches = 0;
            _stateEndsAt = 0f;
            Status = "准备钓鱼";
        }
    }
}
