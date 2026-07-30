namespace DemonViglu.FirePlay.Player
{
    public enum PlayerIntentKind
    {
        Interact,
        Rest,
        RitualPrimary,
        RitualSecondary,
        Emote,
        PlaceFire,
        CancelPlacement,
        AddFire,
        TendFire,
        GatherEmber,
        StartPublicFire,
        DrawFire,
        ReclaimSmallFire,
        ContributeWorldTree,
        CycleTreeLightColor,
        Pause,
        LegacyWithdrawOrReclaim
    }

    /// <summary>Router 输出的低频语义意图；CommandExecutor 将在下一阶段消费。</summary>
    public sealed class PlayerIntentRequested : IGameEvent
    {
        public string PlayerId { get; }
        public PlayerIntentKind Kind { get; }
        public PlayerMode Mode { get; }
        public PlayerInteractTargetKind TargetKind { get; }
        public string TargetId { get; }

        public PlayerIntentRequested(string playerId, PlayerIntentKind kind, PlayerMode mode, PlayerInteractTargetKind targetKind, string targetId)
        {
            PlayerId = playerId;
            Kind = kind;
            Mode = mode;
            TargetKind = targetKind;
            TargetId = targetId;
        }
    }
}
