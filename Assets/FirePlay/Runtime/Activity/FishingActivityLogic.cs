using System;
using System.Text;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Pure fishing activity state. The activity is anchored, but its rules and
    /// resource transaction are independent from RestInteraction and scene objects.
    /// </summary>
    public sealed class FishingActivityLogic : IActivityLogic, IActivityTickable,
        IActivityPresentationLifecycle, IActivityNetworkStateProvider
    {
        public const string ActivityId = "fishing";
        public const string FishingStateId = "fishing";
        public const string FishingReelCueId = "fishing.reel";

        private enum FishingState
        {
            None,
            ReadyToCast,
            WaitingForBite,
            BiteReady,
            Fighting,
            CatchReady
        }

        private readonly float _rodFuelCost;
        private readonly int _catchesPerRod;
        private readonly float _fuelPerCatch;
        private readonly float _minimumBiteDelay;
        private readonly float _maximumBiteDelay;
        private readonly float _hookWindowSeconds;
        private readonly float _catchZoneSize;
        private readonly float _liftAcceleration;
        private readonly float _gravityAcceleration;
        private readonly float _catchBarDrag;
        private readonly float _maximumCatchBarSpeed;
        private readonly float _fishMoveSpeed;
        private readonly float _minimumFishTargetDuration;
        private readonly float _maximumFishTargetDuration;
        private readonly float _catchGainPerSecond;
        private readonly float _catchLossPerSecond;
        private readonly Random _random;

        private FishingState _state;
        private int _catches;
        private float _stateEndsAt;
        private float _fishPosition;
        private float _fishTarget;
        private float _fishTargetTime;
        private float _catchBarPosition;
        private float _catchBarVelocity;
        private float _catchProgress;
        private bool _liftHeld;
        private int _publishedFishPositionPercent = -1;
        private int _publishedCatchBarPositionPercent = -1;
        private int _publishedProgressPercent = -1;

        public bool HasRod => _state != FishingState.None;
        public bool IsLineCast => _state == FishingState.WaitingForBite
                                  || _state == FishingState.BiteReady
                                  || _state == FishingState.Fighting
                                  || _state == FishingState.CatchReady;
        public bool IsFishBiting => _state == FishingState.BiteReady;
        public bool IsFighting => _state == FishingState.Fighting;
        public bool IsCatchReady => _state == FishingState.CatchReady;
        public bool IsLiftHeld => _liftHeld;
        public float FishPosition01 => _fishPosition;
        public float CatchBarPosition01 => _catchBarPosition;
        public float CatchZoneSize01 => _catchZoneSize;
        public float CatchProgress01 => _catchProgress;
        public bool IsFishInsideCatchBar => IsInsideCatchBar();
        public int Catches => _catches;
        public int CatchesPerRod => _catchesPerRod;
        public string Status { get; private set; } = "准备钓鱼";
        public uint NetworkStateRevision { get; private set; }

        public FishingActivityLogic(
            float rodFuelCost = 30f,
            int catchesPerRod = 4,
            float fuelPerCatch = 8f,
            float minimumBiteDelay = 1.5f,
            float maximumBiteDelay = 4f,
            float hookWindowSeconds = 2f,
            float catchZoneSize = 0.28f,
            float liftAcceleration = 2.25f,
            float gravityAcceleration = 1.65f,
            float catchBarDrag = 2.8f,
            float maximumCatchBarSpeed = 0.9f,
            float fishMoveSpeed = 0.52f,
            float minimumFishTargetDuration = 0.45f,
            float maximumFishTargetDuration = 1.15f,
            float catchGainPerSecond = 0.34f,
            float catchLossPerSecond = 0.2f,
            Random random = null)
        {
            _rodFuelCost = Math.Max(0f, rodFuelCost);
            _catchesPerRod = Math.Max(1, catchesPerRod);
            _fuelPerCatch = Math.Max(0f, fuelPerCatch);
            _minimumBiteDelay = Math.Max(0.1f, minimumBiteDelay);
            _maximumBiteDelay = Math.Max(_minimumBiteDelay, maximumBiteDelay);
            _hookWindowSeconds = Math.Max(0.1f, hookWindowSeconds);
            _catchZoneSize = Math.Clamp(catchZoneSize, 0.1f, 0.75f);
            _liftAcceleration = Math.Max(0.1f, liftAcceleration);
            _gravityAcceleration = Math.Max(0.1f, gravityAcceleration);
            _catchBarDrag = Math.Max(0f, catchBarDrag);
            _maximumCatchBarSpeed = Math.Max(0.1f, maximumCatchBarSpeed);
            _fishMoveSpeed = Math.Max(0.05f, fishMoveSpeed);
            _minimumFishTargetDuration = Math.Max(0.1f, minimumFishTargetDuration);
            _maximumFishTargetDuration = Math.Max(_minimumFishTargetDuration, maximumFishTargetDuration);
            _catchGainPerSecond = Math.Max(0.01f, catchGainPerSecond);
            _catchLossPerSecond = Math.Max(0.01f, catchLossPerSecond);
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
                "fishing.primary" => HandlePrimary(context, request.SessionRevision),
                "fishing.materialize" => Materialize(context),
                "fishing.cast" => Cast(),
                "fishing.lift.start" => SetLiftHeld(context, request.SessionRevision, true),
                "fishing.lift.stop" => SetLiftHeld(context, request.SessionRevision, false),
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
                    Status = "有鱼咬钩了，快提竿";
                    MarkNetworkStateChanged();
                }
            }
            else if (_state == FishingState.BiteReady && deltaTime > 0f)
            {
                _stateEndsAt -= deltaTime;
                if (_stateEndsAt <= 0f)
                {
                    _state = FishingState.ReadyToCast;
                    Status = "鱼儿游走了，再试一次吧";
                    MarkNetworkStateChanged();
                }
            }
            else if (_state == FishingState.Fighting && deltaTime > 0f)
            {
                AdvanceFish(deltaTime);
                AdvanceCatchBar(deltaTime);

                var fishInside = IsInsideCatchBar();
                _catchProgress = Math.Clamp(
                    _catchProgress + deltaTime * (fishInside ? _catchGainPerSecond : -_catchLossPerSecond),
                    0f,
                    1f);

                if (_catchProgress >= 1f)
                {
                    _state = FishingState.CatchReady;
                    _liftHeld = false;
                    _catchBarVelocity = 0f;
                    Status = "抓住了！点击收获完成这次钓鱼";
                    MarkNetworkStateChanged();
                    return;
                }

                if (_catchProgress <= 0f)
                {
                    EscapeFish();
                    return;
                }

                Status = fishInside
                    ? $"保持覆盖：捕获 {ToPercent(_catchProgress)}%"
                    : $"鱼游出了绿色区域：捕获 {ToPercent(_catchProgress)}%";
                MarkNetworkStateChangedWhenGaugeMoves();
            }
        }

        public void End(IActivityContext context, ActivityEndReason reason)
        {
            ResetState();
        }

        public string CaptureNetworkState() => new FishingActivityStateSnapshot(
            HasRod,
            IsLineCast,
            IsFishBiting,
            IsFighting,
            IsCatchReady,
            _catches,
            _catchesPerRod,
            _fishPosition,
            _catchBarPosition,
            _catchZoneSize,
            _catchProgress,
            _liftHeld,
            Status).Serialize();

        public void OnPresentationStarted(IActivityContext context, uint sessionRevision)
        {
            if (context?.Presentation == null) return;

            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.MovementLock,
                context.PlayerId,
                ActivityId,
                string.Empty,
                string.Empty,
                active: true,
                sessionRevision: sessionRevision));
            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationState,
                context.PlayerId,
                ActivityId,
                string.Empty,
                FishingStateId,
                active: true,
                sessionRevision: sessionRevision));
        }

        public void OnPresentationEnded(IActivityContext context, uint sessionRevision, ActivityEndReason reason)
        {
            if (context?.Presentation == null) return;

            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.MovementLock,
                context.PlayerId,
                ActivityId,
                string.Empty,
                string.Empty,
                active: false,
                sessionRevision: sessionRevision));
            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationState,
                context.PlayerId,
                ActivityId,
                string.Empty,
                FishingStateId,
                active: false,
                sessionRevision: sessionRevision));
        }

        private ActivityActionResult HandlePrimary(IActivityContext context, uint sessionRevision)
        {
            if (!HasRod) return Materialize(context);
            if (_state == FishingState.BiteReady) return HookFish(context, sessionRevision);
            if (_state == FishingState.CatchReady) return CollectCatch(context);
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
            MarkNetworkStateChanged();
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
            ResetFightGauges();
            Status = "鱼线随着水波轻轻漂着……";
            MarkNetworkStateChanged();
            return ActivityActionResult.Consume("Fishing line cast");
        }

        private ActivityActionResult SetLiftHeld(
            IActivityContext context,
            uint sessionRevision,
            bool held)
        {
            if (_state != FishingState.Fighting)
                return ActivityActionResult.Reject("There is no hooked fish to control");
            if (_liftHeld == held)
                return ActivityActionResult.Consume(held ? "Catch bar already rising" : "Catch bar already falling");

            _liftHeld = held;
            if (held)
                RequestReelCue(context, sessionRevision);
            MarkNetworkStateChanged();
            return ActivityActionResult.Consume(held ? "Catch bar rising" : "Catch bar falling");
        }

        private ActivityActionResult HookFish(IActivityContext context, uint sessionRevision)
        {
            if (_state != FishingState.BiteReady)
                return ActivityActionResult.Reject("There is no fish to hook");

            _state = FishingState.Fighting;
            _fishPosition = 0.62f;
            _fishTarget = 0.76f;
            _fishTargetTime = NextFishTargetDuration();
            _catchBarPosition = 0.32f;
            _catchBarVelocity = 0f;
            _catchProgress = 0.35f;
            _liftHeld = false;
            RequestReelCue(context, sessionRevision);
            Status = "鱼已上钩！按住让绿色区域上升，松开让它下落";
            MarkNetworkStateChanged();
            return ActivityActionResult.Consume("Fish hooked");
        }

        private ActivityActionResult CollectCatch(IActivityContext context)
        {
            if (_state != FishingState.CatchReady)
                return ActivityActionResult.Reject("No fish is ready to collect");

            _catches++;
            if (_fuelPerCatch > 0f)
                context.Flame.Restore(_fuelPerCatch);

            if (_catches >= _catchesPerRod)
                return ActivityActionResult.End(ActivityEndReason.Completed, "Fishing session completed");

            _state = FishingState.ReadyToCast;
            ResetFightGauges();
            Status = $"钓到第 {_catches}/{_catchesPerRod} 条鱼，余火返还 {_fuelPerCatch:0}";
            MarkNetworkStateChanged();
            return ActivityActionResult.Consume("Fish collected");
        }

        private void RequestReelCue(IActivityContext context, uint sessionRevision)
        {
            context.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationCue,
                context.PlayerId,
                ActivityId,
                string.Empty,
                FishingReelCueId,
                active: true,
                sessionRevision: sessionRevision));
        }

        private void AdvanceFish(float deltaTime)
        {
            _fishTargetTime -= deltaTime;
            if (_fishTargetTime <= 0f || Math.Abs(_fishPosition - _fishTarget) <= 0.015f)
            {
                _fishTarget = 0.06f + (float)_random.NextDouble() * 0.88f;
                _fishTargetTime = NextFishTargetDuration();
            }

            _fishPosition = MoveTowards(
                _fishPosition,
                _fishTarget,
                _fishMoveSpeed * deltaTime);
        }

        private void AdvanceCatchBar(float deltaTime)
        {
            var acceleration = _liftHeld ? _liftAcceleration : -_gravityAcceleration;
            _catchBarVelocity += acceleration * deltaTime;
            _catchBarVelocity *= (float)Math.Exp(-_catchBarDrag * deltaTime);
            _catchBarVelocity = Math.Clamp(
                _catchBarVelocity,
                -_maximumCatchBarSpeed,
                _maximumCatchBarSpeed);

            _catchBarPosition += _catchBarVelocity * deltaTime;
            var halfZone = _catchZoneSize * 0.5f;
            var minimum = halfZone;
            var maximum = 1f - halfZone;
            if (_catchBarPosition < minimum)
            {
                _catchBarPosition = minimum;
                _catchBarVelocity = Math.Max(0f, -_catchBarVelocity * 0.18f);
            }
            else if (_catchBarPosition > maximum)
            {
                _catchBarPosition = maximum;
                _catchBarVelocity = Math.Min(0f, -_catchBarVelocity * 0.18f);
            }
        }

        private bool IsInsideCatchBar()
        {
            if (_state != FishingState.Fighting && _state != FishingState.CatchReady)
                return false;

            const float fishHalfSize = 0.025f;
            return Math.Abs(_fishPosition - _catchBarPosition)
                   <= _catchZoneSize * 0.5f + fishHalfSize;
        }

        private void EscapeFish()
        {
            _state = FishingState.ReadyToCast;
            ResetFightGauges();
            Status = "鱼儿挣脱了，重新抛竿再试一次吧";
            MarkNetworkStateChanged();
        }

        private float NextFishTargetDuration()
        {
            return _minimumFishTargetDuration
                   + (float)_random.NextDouble()
                   * (_maximumFishTargetDuration - _minimumFishTargetDuration);
        }

        private void ResetState()
        {
            _state = FishingState.None;
            _catches = 0;
            _stateEndsAt = 0f;
            ResetFightGauges();
            Status = "准备钓鱼";
            MarkNetworkStateChanged();
        }

        private void MarkNetworkStateChanged()
        {
            _publishedFishPositionPercent = ToPercent(_fishPosition);
            _publishedCatchBarPositionPercent = ToPercent(_catchBarPosition);
            _publishedProgressPercent = ToPercent(_catchProgress);
            NetworkStateRevision = NetworkStateRevision == uint.MaxValue
                ? 1u
                : NetworkStateRevision + 1u;
        }

        private void MarkNetworkStateChangedWhenGaugeMoves()
        {
            var fishPositionPercent = ToPercent(_fishPosition);
            var catchBarPositionPercent = ToPercent(_catchBarPosition);
            var progressPercent = ToPercent(_catchProgress);
            if (Math.Abs(fishPositionPercent - _publishedFishPositionPercent) < 2
                && Math.Abs(catchBarPositionPercent - _publishedCatchBarPositionPercent) < 2
                && Math.Abs(progressPercent - _publishedProgressPercent) < 3)
            {
                return;
            }

            MarkNetworkStateChanged();
        }

        private void ResetFightGauges()
        {
            _fishPosition = 0.5f;
            _fishTarget = 0.5f;
            _fishTargetTime = 0f;
            _catchBarPosition = _catchZoneSize * 0.5f;
            _catchBarVelocity = 0f;
            _catchProgress = 0f;
            _liftHeld = false;
            _publishedFishPositionPercent = ToPercent(_fishPosition);
            _publishedCatchBarPositionPercent = ToPercent(_catchBarPosition);
            _publishedProgressPercent = 0;
        }

        private static float MoveTowards(float current, float target, float maximumDelta)
        {
            if (Math.Abs(target - current) <= maximumDelta)
                return target;
            return current + Math.Sign(target - current) * maximumDelta;
        }

        private static int ToPercent(float value) =>
            (int)Math.Round(Math.Clamp(value, 0f, 1f) * 100f);
    }

    /// <summary>
    /// Fishing-owned wire payload. This schema stays beside Fishing rather
    /// than leaking fishing fields into PlayerActivityHost or NGO code.
    /// </summary>
    public readonly struct FishingActivityStateSnapshot
    {
        public bool HasRod { get; }
        public bool IsLineCast { get; }
        public bool IsFishBiting { get; }
        public bool IsFighting { get; }
        public bool IsCatchReady { get; }
        public int Catches { get; }
        public int CatchesPerRod { get; }
        public int FishPositionPercent { get; }
        public int CatchBarPositionPercent { get; }
        public int CatchZoneSizePercent { get; }
        public int CatchProgressPercent { get; }
        public bool IsLiftHeld { get; }
        public bool IsFishInsideCatchBar =>
            Math.Abs(FishPositionPercent - CatchBarPositionPercent)
            <= CatchZoneSizePercent * 0.5f + 2.5f;
        public string Status { get; }

        public FishingActivityStateSnapshot(
            bool hasRod,
            bool isLineCast,
            bool isFishBiting,
            bool isFighting,
            bool isCatchReady,
            int catches,
            int catchesPerRod,
            float fishPosition01,
            float catchBarPosition01,
            float catchZoneSize01,
            float catchProgress01,
            bool isLiftHeld,
            string status)
        {
            HasRod = hasRod;
            IsLineCast = isLineCast;
            IsFishBiting = isFishBiting;
            IsFighting = isFighting;
            IsCatchReady = isCatchReady;
            Catches = Math.Max(0, catches);
            CatchesPerRod = Math.Max(1, catchesPerRod);
            FishPositionPercent = ToPercent(fishPosition01);
            CatchBarPositionPercent = ToPercent(catchBarPosition01);
            CatchZoneSizePercent = Math.Clamp(ToPercent(catchZoneSize01), 10, 75);
            CatchProgressPercent = ToPercent(catchProgress01);
            IsLiftHeld = isLiftHeld;
            Status = status ?? string.Empty;
        }

        public string Serialize()
        {
            var encodedStatus = Convert.ToBase64String(Encoding.UTF8.GetBytes(Status));
            return $"{(HasRod ? 1 : 0)}|{(IsLineCast ? 1 : 0)}|{(IsFishBiting ? 1 : 0)}|{(IsFighting ? 1 : 0)}|{(IsCatchReady ? 1 : 0)}|{Catches}|{CatchesPerRod}|{FishPositionPercent}|{CatchBarPositionPercent}|{CatchZoneSizePercent}|{CatchProgressPercent}|{(IsLiftHeld ? 1 : 0)}|{encodedStatus}";
        }

        public static bool TryParse(string payload, out FishingActivityStateSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(payload)) return false;

            var parts = payload.Split('|');
            if (parts.Length != 13
                || !TryParseFlag(parts[0], out var hasRod)
                || !TryParseFlag(parts[1], out var isLineCast)
                || !TryParseFlag(parts[2], out var isFishBiting)
                || !TryParseFlag(parts[3], out var isFighting)
                || !TryParseFlag(parts[4], out var isCatchReady)
                || !int.TryParse(parts[5], out var catches)
                || !int.TryParse(parts[6], out var catchesPerRod)
                || !int.TryParse(parts[7], out var fishPositionPercent)
                || !int.TryParse(parts[8], out var catchBarPositionPercent)
                || !int.TryParse(parts[9], out var catchZoneSizePercent)
                || !int.TryParse(parts[10], out var catchProgressPercent)
                || !TryParseFlag(parts[11], out var isLiftHeld))
            {
                return false;
            }

            try
            {
                var status = Encoding.UTF8.GetString(Convert.FromBase64String(parts[12]));
                snapshot = new FishingActivityStateSnapshot(
                    hasRod,
                    isLineCast,
                    isFishBiting,
                    isFighting,
                    isCatchReady,
                    catches,
                    catchesPerRod,
                    Math.Clamp(fishPositionPercent, 0, 100) / 100f,
                    Math.Clamp(catchBarPositionPercent, 0, 100) / 100f,
                    Math.Clamp(catchZoneSizePercent, 10, 75) / 100f,
                    Math.Clamp(catchProgressPercent, 0, 100) / 100f,
                    isLiftHeld,
                    status);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool TryParseFlag(string value, out bool result)
        {
            result = value == "1";
            return result || value == "0";
        }

        private static int ToPercent(float value) =>
            (int)Math.Round(Math.Clamp(value, 0f, 1f) * 100f);
    }
}
