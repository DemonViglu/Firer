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
            Fighting
        }

        private readonly float _rodFuelCost;
        private readonly int _catchesPerRod;
        private readonly float _fuelPerCatch;
        private readonly float _minimumBiteDelay;
        private readonly float _maximumBiteDelay;
        private readonly float _hookWindowSeconds;
        private readonly float _reelTensionGain;
        private readonly float _reelProgressGain;
        private readonly float _easeTensionDrop;
        private readonly float _easeProgressLoss;
        private readonly float _fishPullPerSecond;
        private readonly float _fishPullVariance;
        private readonly Random _random;

        private FishingState _state;
        private int _catches;
        private float _stateEndsAt;
        private float _fightElapsed;
        private float _tension;
        private float _catchProgress;
        private int _publishedTensionPercent = -1;
        private int _publishedProgressPercent = -1;

        public bool HasRod => _state != FishingState.None;
        public bool IsLineCast => _state == FishingState.WaitingForBite
                                  || _state == FishingState.BiteReady
                                  || _state == FishingState.Fighting;
        public bool IsFishBiting => _state == FishingState.BiteReady;
        public bool IsFighting => _state == FishingState.Fighting;
        public float Tension01 => _tension;
        public float CatchProgress01 => _catchProgress;
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
            float reelTensionGain = 0.2f,
            float reelProgressGain = 0.2f,
            float easeTensionDrop = 0.34f,
            float easeProgressLoss = 0.06f,
            float fishPullPerSecond = 0.035f,
            float fishPullVariance = 0.08f,
            Random random = null)
        {
            _rodFuelCost = Math.Max(0f, rodFuelCost);
            _catchesPerRod = Math.Max(1, catchesPerRod);
            _fuelPerCatch = Math.Max(0f, fuelPerCatch);
            _minimumBiteDelay = Math.Max(0.1f, minimumBiteDelay);
            _maximumBiteDelay = Math.Max(_minimumBiteDelay, maximumBiteDelay);
            _hookWindowSeconds = Math.Max(0.1f, hookWindowSeconds);
            _reelTensionGain = Math.Clamp(reelTensionGain, 0.01f, 1f);
            _reelProgressGain = Math.Clamp(reelProgressGain, 0.01f, 1f);
            _easeTensionDrop = Math.Clamp(easeTensionDrop, 0.01f, 1f);
            _easeProgressLoss = Math.Clamp(easeProgressLoss, 0f, 1f);
            _fishPullPerSecond = Math.Max(0f, fishPullPerSecond);
            _fishPullVariance = Math.Max(0f, fishPullVariance);
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
                "fishing.reel" => Reel(context, request.SessionRevision),
                "fishing.ease" => EaseLine(),
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
                _fightElapsed += deltaTime;
                var pullPulse = 0.5f + 0.5f * (float)Math.Sin(_fightElapsed * 3.7f);
                _tension = Math.Clamp(
                    _tension + deltaTime * (_fishPullPerSecond + _fishPullVariance * pullPulse),
                    0f,
                    1f);
                _catchProgress = Math.Max(0f, _catchProgress - deltaTime * 0.012f);

                if (_tension >= 1f)
                {
                    BreakLine("鱼线绷断了，重新抛竿吧");
                    return;
                }

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
            _catches,
            _catchesPerRod,
            _tension,
            _catchProgress,
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

        private ActivityActionResult Reel(IActivityContext context, uint sessionRevision)
        {
            if (_state == FishingState.BiteReady)
                return HookFish(context, sessionRevision);
            if (_state != FishingState.Fighting)
                return ActivityActionResult.Reject("There is no hooked fish to reel");

            RequestReelCue(context, sessionRevision);
            _tension = Math.Clamp(_tension + _reelTensionGain, 0f, 1f);
            _catchProgress = Math.Clamp(_catchProgress + _reelProgressGain, 0f, 1f);

            if (_tension >= 1f)
            {
                BreakLine("收线太猛，鱼线绷断了");
                return ActivityActionResult.Consume("Fishing line snapped");
            }
            if (_catchProgress < 1f)
            {
                Status = $"稳住鱼线：进度 {ToPercent(_catchProgress)}%，张力 {ToPercent(_tension)}%";
                MarkNetworkStateChanged();
                return ActivityActionResult.Consume("Fishing line reeled");
            }

            _catches++;
            if (_fuelPerCatch > 0f)
                context.Flame.Restore(_fuelPerCatch);

            if (_catches >= _catchesPerRod)
            {
                return ActivityActionResult.End(ActivityEndReason.Completed, "Fishing session completed");
            }

            _state = FishingState.ReadyToCast;
            ResetFightGauges();
            Status = $"钓到第 {_catches}/{_catchesPerRod} 条鱼，余火返还 {_fuelPerCatch:0}";
            MarkNetworkStateChanged();
            return ActivityActionResult.Consume("Fish reeled");
        }

        private ActivityActionResult HookFish(IActivityContext context, uint sessionRevision)
        {
            if (_state != FishingState.BiteReady)
                return ActivityActionResult.Reject("There is no fish to hook");

            _state = FishingState.Fighting;
            _fightElapsed = 0f;
            _tension = 0.28f;
            _catchProgress = 0.08f;
            RequestReelCue(context, sessionRevision);
            Status = "鱼已上钩！交替收线与放线，别让张力爆表";
            MarkNetworkStateChanged();
            return ActivityActionResult.Consume("Fish hooked");
        }

        private ActivityActionResult EaseLine()
        {
            if (_state != FishingState.Fighting)
                return ActivityActionResult.Reject("There is no hooked fish to ease");

            _tension = Math.Max(0f, _tension - _easeTensionDrop);
            _catchProgress = Math.Max(0f, _catchProgress - _easeProgressLoss);
            Status = $"放线缓冲：进度 {ToPercent(_catchProgress)}%，张力 {ToPercent(_tension)}%";
            MarkNetworkStateChanged();
            return ActivityActionResult.Consume("Fishing line eased");
        }

        private void BreakLine(string status)
        {
            _state = FishingState.ReadyToCast;
            ResetFightGauges();
            Status = status;
            MarkNetworkStateChanged();
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
            _publishedTensionPercent = ToPercent(_tension);
            _publishedProgressPercent = ToPercent(_catchProgress);
            NetworkStateRevision = NetworkStateRevision == uint.MaxValue
                ? 1u
                : NetworkStateRevision + 1u;
        }

        private void MarkNetworkStateChangedWhenGaugeMoves()
        {
            var tensionPercent = ToPercent(_tension);
            var progressPercent = ToPercent(_catchProgress);
            if (Math.Abs(tensionPercent - _publishedTensionPercent) < 3
                && Math.Abs(progressPercent - _publishedProgressPercent) < 3)
            {
                return;
            }

            MarkNetworkStateChanged();
        }

        private void ResetFightGauges()
        {
            _fightElapsed = 0f;
            _tension = 0f;
            _catchProgress = 0f;
            _publishedTensionPercent = 0;
            _publishedProgressPercent = 0;
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
        public int Catches { get; }
        public int CatchesPerRod { get; }
        public int TensionPercent { get; }
        public int CatchProgressPercent { get; }
        public string Status { get; }

        public FishingActivityStateSnapshot(
            bool hasRod,
            bool isLineCast,
            bool isFishBiting,
            bool isFighting,
            int catches,
            int catchesPerRod,
            float tension01,
            float catchProgress01,
            string status)
        {
            HasRod = hasRod;
            IsLineCast = isLineCast;
            IsFishBiting = isFishBiting;
            IsFighting = isFighting;
            Catches = Math.Max(0, catches);
            CatchesPerRod = Math.Max(1, catchesPerRod);
            TensionPercent = ToPercent(tension01);
            CatchProgressPercent = ToPercent(catchProgress01);
            Status = status ?? string.Empty;
        }

        public string Serialize()
        {
            var encodedStatus = Convert.ToBase64String(Encoding.UTF8.GetBytes(Status));
            return $"{(HasRod ? 1 : 0)}|{(IsLineCast ? 1 : 0)}|{(IsFishBiting ? 1 : 0)}|{(IsFighting ? 1 : 0)}|{Catches}|{CatchesPerRod}|{TensionPercent}|{CatchProgressPercent}|{encodedStatus}";
        }

        public static bool TryParse(string payload, out FishingActivityStateSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(payload)) return false;

            var parts = payload.Split('|');
            if (parts.Length != 9
                || !TryParseFlag(parts[0], out var hasRod)
                || !TryParseFlag(parts[1], out var isLineCast)
                || !TryParseFlag(parts[2], out var isFishBiting)
                || !TryParseFlag(parts[3], out var isFighting)
                || !int.TryParse(parts[4], out var catches)
                || !int.TryParse(parts[5], out var catchesPerRod)
                || !int.TryParse(parts[6], out var tensionPercent)
                || !int.TryParse(parts[7], out var catchProgressPercent))
            {
                return false;
            }

            try
            {
                var status = Encoding.UTF8.GetString(Convert.FromBase64String(parts[8]));
                snapshot = new FishingActivityStateSnapshot(
                    hasRod,
                    isLineCast,
                    isFishBiting,
                    isFighting,
                    catches,
                    catchesPerRod,
                    Math.Clamp(tensionPercent, 0, 100) / 100f,
                    Math.Clamp(catchProgressPercent, 0, 100) / 100f,
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
