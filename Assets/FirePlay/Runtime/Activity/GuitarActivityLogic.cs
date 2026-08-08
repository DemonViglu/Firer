using System;

namespace DemonViglu.FirePlay.Activity
{
    /// <summary>
    /// Independent Anywhere guitar activity. Each of the 21 keys is a
    /// semantic action, so a future guitar UI can bind buttons, keyboard
    /// input or network messages without changing the activity core.
    /// </summary>
    public sealed class GuitarActivityLogic : IActivityLogic, IActivityPresentationLifecycle,
        IActivityNetworkStateProvider
    {
        public const string ActivityId = "guitar";
        public const int KeyCount = 21;
        private const string PlayingStateId = "guitar.playing";

        public int LastKeyIndex { get; private set; }
        public int PlayedKeyCount { get; private set; }
        public uint NetworkStateRevision { get; private set; }

        public ActivityStartCheck CheckStart(IActivityContext context)
        {
            return context != null && context.ActivityId == ActivityId
                ? ActivityStartCheck.Allow()
                : ActivityStartCheck.Deny("Guitar context is invalid");
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
            if (context == null || context.ActivityId != ActivityId)
                return ActivityActionResult.Reject("Guitar context is invalid");

            if (request.ActionId == "activity.exit")
                return ActivityActionResult.End(ActivityEndReason.Requested, "Guitar activity exited");

            if (!TryParseKey(request.ActionId, out var keyIndex))
                return ActivityActionResult.Reject("Unknown guitar key action");

            LastKeyIndex = keyIndex;
            PlayedKeyCount++;
            MarkNetworkStateChanged();
            context.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationCue,
                context.PlayerId,
                ActivityId,
                string.Empty,
                "guitar.play",
                active: true,
                sessionRevision: request.SessionRevision));
            context.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationCue,
                context.PlayerId,
                ActivityId,
                string.Empty,
                $"guitar.key.{keyIndex:00}",
                active: true,
                sessionRevision: request.SessionRevision));
            return ActivityActionResult.Consume($"Guitar key {keyIndex:00} played");
        }

        public void OnPresentationStarted(IActivityContext context, uint sessionRevision)
        {
            context?.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationCue,
                context.PlayerId,
                ActivityId,
                string.Empty,
                "guitar.begin",
                active: true,
                sessionRevision: sessionRevision));
            context?.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.MovementLock,
                context.PlayerId,
                ActivityId,
                string.Empty,
                string.Empty,
                active: true,
                sessionRevision: sessionRevision));
            context?.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationState,
                context.PlayerId,
                ActivityId,
                string.Empty,
                PlayingStateId,
                active: true,
                sessionRevision: sessionRevision));
        }

        public void OnPresentationEnded(IActivityContext context, uint sessionRevision, ActivityEndReason reason)
        {
            context?.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.AnimationState,
                context.PlayerId,
                ActivityId,
                string.Empty,
                PlayingStateId,
                active: false,
                sessionRevision: sessionRevision));
            context?.Presentation?.RequestPlayer(new ActivityPlayerRequest(
                ActivityPlayerRequestKind.MovementLock,
                context.PlayerId,
                ActivityId,
                string.Empty,
                string.Empty,
                active: false,
                sessionRevision: sessionRevision));
        }

        public void End(IActivityContext context, ActivityEndReason reason)
        {
            ResetState();
        }

        public string CaptureNetworkState() =>
            new GuitarActivityStateSnapshot(LastKeyIndex, PlayedKeyCount).Serialize();

        public static string GetKeyActionId(int keyIndex)
        {
            if (keyIndex < 1 || keyIndex > KeyCount)
                throw new ArgumentOutOfRangeException(nameof(keyIndex));
            return $"guitar.key.{keyIndex:00}";
        }

        private static bool TryParseKey(string actionId, out int keyIndex)
        {
            keyIndex = 0;
            if (string.IsNullOrWhiteSpace(actionId)
                || !actionId.StartsWith("guitar.key.", StringComparison.Ordinal)
                || !int.TryParse(actionId.Substring("guitar.key.".Length), out keyIndex))
            {
                return false;
            }

            return keyIndex >= 1 && keyIndex <= KeyCount;
        }

        private void ResetState()
        {
            LastKeyIndex = 0;
            PlayedKeyCount = 0;
            MarkNetworkStateChanged();
        }

        private void MarkNetworkStateChanged()
        {
            NetworkStateRevision = NetworkStateRevision == uint.MaxValue
                ? 1u
                : NetworkStateRevision + 1u;
        }
    }

    /// <summary>Guitar-owned wire state; the network layer treats it as opaque.</summary>
    public readonly struct GuitarActivityStateSnapshot
    {
        public int LastKeyIndex { get; }
        public int PlayedKeyCount { get; }

        public GuitarActivityStateSnapshot(int lastKeyIndex, int playedKeyCount)
        {
            LastKeyIndex = lastKeyIndex;
            PlayedKeyCount = Math.Max(0, playedKeyCount);
        }

        public string Serialize() => $"{LastKeyIndex}|{PlayedKeyCount}";

        public static bool TryParse(string payload, out GuitarActivityStateSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(payload)) return false;
            var parts = payload.Split('|');
            if (parts.Length != 2
                || !int.TryParse(parts[0], out var lastKeyIndex)
                || !int.TryParse(parts[1], out var playedKeyCount)
                || lastKeyIndex < 0
                || lastKeyIndex > GuitarActivityLogic.KeyCount
                || playedKeyCount < 0
                || (playedKeyCount == 0 && lastKeyIndex != 0)
                || (playedKeyCount > 0 && lastKeyIndex == 0))
            {
                return false;
            }

            snapshot = new GuitarActivityStateSnapshot(lastKeyIndex, playedKeyCount);
            return true;
        }
    }
}
