using System;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Pure multi-action marshmallow activity. It owns only the gameplay state;
    /// the marshmallow prop, animation and dedicated UI belong to the
    /// presentation layer that receives this state from the activity host.
    /// </summary>
    public sealed class MarshmallowActivityLogic : IActivityLogic, IActivityTickable,
        IActivityPresentationLifecycle, IActivityNetworkStateProvider
    {
        public const string ActivityId = "marshmallow";
        public const string RoastingStateId = "marshmallow.roasting";
        public const string GiveActionId = "marshmallow.give";
        public const string ReceiveVfxCueId = "marshmallow.receive.vfx";

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
        private int _publishedNeedlePercent = -1;

        public bool HasMaterialized => _roast != null;
        public bool IsRoasting => _roast != null && !_roast.IsReadyToEat;
        public bool IsReadyToEat => _roast != null && _roast.IsReadyToEat;
        public int CompletedTurns => _roast?.CompletedTurns ?? 0;
        public int PerfectTurns => _roast?.PerfectTurns ?? 0;
        public float NeedlePosition => _roast?.NeedlePosition ?? 0f;
        public float TargetCenter => _roast?.TargetCenter ?? 0f;
        public float PerfectZoneWidth => _perfectZoneWidth;
        public MarshmallowActivityResult? CompletedResult => _completedResult;
        public int TurnsRequired => _roast?.TurnsRequired ?? _turnsRequired;
        public uint NetworkStateRevision { get; private set; }

        public bool TryPeekGift(
            string sourcePlayerId,
            string eventId,
            out MarshmallowGift gift,
            out string reason)
        {
            if (_roast == null || !_roast.IsReadyToEat || !_completedResult.HasValue)
            {
                gift = default;
                reason = "Marshmallow is not ready to give";
                return false;
            }

            var result = _completedResult.Value;
            gift = new MarshmallowGift(
                sourcePlayerId,
                eventId,
                result.Quality,
                result.FuelRefund);
            reason = string.Empty;
            return true;
        }

        public bool ConsumeGift()
        {
            if (_roast == null || !_roast.IsReadyToEat || !_completedResult.HasValue)
                return false;
            ResetState();
            return true;
        }

        public bool TryGive(
            string sourcePlayerId,
            string eventId,
            Func<MarshmallowGift, string> accept,
            out string reason)
        {
            if (!TryPeekGift(sourcePlayerId, eventId, out var gift, out reason))
                return false;
            if (accept == null)
            {
                reason = "Marshmallow gift receiver is unavailable";
                return false;
            }
            reason = accept(gift) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(reason))
                return false;
            ResetState();
            return true;
        }

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
            {
                _roast.Advance(deltaTime);
                MarkNetworkStateChangedWhenNeedleMoves();
            }
        }

        public void End(IActivityContext context, ActivityEndReason reason)
        {
            ResetState();
        }

        public string CaptureNetworkState() => new MarshmallowActivityStateSnapshot(
            HasMaterialized,
            IsReadyToEat,
            CompletedTurns,
            PerfectTurns,
            TurnsRequired,
            _completedResult.HasValue,
            _completedResult?.Quality ?? MarshmallowRoastQuality.Scorched,
            NeedlePosition,
            TargetCenter,
            PerfectZoneWidth).Serialize();

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
                sessionRevision));
            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationState,
                context.PlayerId,
                ActivityId,
                string.Empty,
                RoastingStateId,
                active: true,
                sessionRevision));

            if (!string.IsNullOrWhiteSpace(context.AnchorId))
            {
                context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                    ActivityPlayerRequestKind.LookTarget,
                    context.PlayerId,
                    ActivityId,
                    context.AnchorId,
                    string.Empty,
                    active: true,
                    sessionRevision));
            }
        }

        public void OnPresentationEnded(IActivityContext context, uint sessionRevision, ActivityEndReason reason)
        {
            if (context?.Presentation == null) return;

            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.LookTarget,
                context.PlayerId,
                ActivityId,
                context.AnchorId,
                string.Empty,
                active: false,
                sessionRevision));
            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.MovementLock,
                context.PlayerId,
                ActivityId,
                string.Empty,
                string.Empty,
                active: false,
                sessionRevision));
            context.Presentation.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationState,
                context.PlayerId,
                ActivityId,
                string.Empty,
                RoastingStateId,
                active: false,
                sessionRevision));
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
            MarkNetworkStateChanged();
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
                MarkNetworkStateChanged();
                return ActivityActionResult.Consume(perfect
                    ? "Perfect final turn; marshmallow is ready"
                    : "Final turn; marshmallow is ready");
            }

            MarkNetworkStateChanged();
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
            MarkNetworkStateChanged();
        }

        private void MarkNetworkStateChanged()
        {
            _publishedNeedlePercent = ToPercent(NeedlePosition);
            NetworkStateRevision = NetworkStateRevision == uint.MaxValue
                ? 1u
                : NetworkStateRevision + 1u;
        }

        private void MarkNetworkStateChangedWhenNeedleMoves()
        {
            var needlePercent = ToPercent(NeedlePosition);
            if (Math.Abs(needlePercent - _publishedNeedlePercent) < 3)
                return;
            MarkNetworkStateChanged();
        }

        private static int ToPercent(float value) =>
            (int)Math.Round(Clamp(value, 0f, 1f) * 100f);

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

    /// <summary>
    /// Marshmallow-owned wire payload. PlayerActivityHost and the network layer
    /// transport this string without knowing any marshmallow fields.
    /// </summary>
    public readonly struct MarshmallowActivityStateSnapshot
    {
        public bool HasMaterialized { get; }
        public bool IsReadyToEat { get; }
        public int CompletedTurns { get; }
        public int PerfectTurns { get; }
        public int TurnsRequired { get; }
        public bool HasResult { get; }
        public MarshmallowRoastQuality Quality { get; }
        public int NeedlePercent { get; }
        public int TargetCenterPercent { get; }
        public int PerfectZonePercent { get; }

        public MarshmallowActivityStateSnapshot(
            bool hasMaterialized,
            bool isReadyToEat,
            int completedTurns,
            int perfectTurns,
            int turnsRequired,
            bool hasResult,
            MarshmallowRoastQuality quality,
            float needlePosition,
            float targetCenter,
            float perfectZoneWidth)
        {
            HasMaterialized = hasMaterialized;
            IsReadyToEat = isReadyToEat;
            CompletedTurns = Math.Max(0, completedTurns);
            PerfectTurns = Math.Max(0, perfectTurns);
            TurnsRequired = Math.Max(1, turnsRequired);
            HasResult = hasResult;
            Quality = quality;
            NeedlePercent = ToPercent(needlePosition);
            TargetCenterPercent = ToPercent(targetCenter);
            PerfectZonePercent = Math.Max(1, ToPercent(perfectZoneWidth));
        }

        public string Serialize() =>
            $"{(HasMaterialized ? 1 : 0)}|{(IsReadyToEat ? 1 : 0)}|{CompletedTurns}|{PerfectTurns}|{TurnsRequired}|{(HasResult ? 1 : 0)}|{(int)Quality}|{NeedlePercent}|{TargetCenterPercent}|{PerfectZonePercent}";

        public static bool TryParse(string payload, out MarshmallowActivityStateSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(payload)) return false;

            var parts = payload.Split('|');
            if (parts.Length != 10
                || !TryParseFlag(parts[0], out var hasMaterialized)
                || !TryParseFlag(parts[1], out var isReadyToEat)
                || !int.TryParse(parts[2], out var completedTurns)
                || !int.TryParse(parts[3], out var perfectTurns)
                || !int.TryParse(parts[4], out var turnsRequired)
                || !TryParseFlag(parts[5], out var hasResult)
                || !int.TryParse(parts[6], out var qualityValue)
                || !Enum.IsDefined(typeof(MarshmallowRoastQuality), qualityValue)
                || !int.TryParse(parts[7], out var needlePercent)
                || !int.TryParse(parts[8], out var targetCenterPercent)
                || !int.TryParse(parts[9], out var perfectZonePercent))
            {
                return false;
            }

            snapshot = new MarshmallowActivityStateSnapshot(
                hasMaterialized,
                isReadyToEat,
                completedTurns,
                perfectTurns,
                turnsRequired,
                hasResult,
                (MarshmallowRoastQuality)qualityValue,
                ClampPercent(needlePercent),
                ClampPercent(targetCenterPercent),
                ClampPercent(perfectZonePercent));
            return true;
        }

        private static bool TryParseFlag(string value, out bool result)
        {
            result = value == "1";
            return result || value == "0";
        }

        private static float ClampPercent(int value) =>
            Math.Clamp(value, 0, 100) / 100f;

        private static int ToPercent(float value) =>
            (int)Math.Round(Math.Clamp(value, 0f, 1f) * 100f);
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
