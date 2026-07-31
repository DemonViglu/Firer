namespace DemonViglu.FirePlay.Activity
{
    /// <summary>活动可以从哪里开始；是否可用仍由规则系统决定。</summary>
    public enum ActivityScope
    {
        Anywhere,
        Anchor,
        Targeted
    }

    /// <summary>同一 Anchor 上多个玩家的状态隔离方式。</summary>
    public enum ActivityParticipationMode
    {
        Independent,
        SharedGroup,
        TargetedInteraction
    }

    public enum ActivitySessionPhase
    {
        Requested,
        Preflight,
        Running,
        Suspended,
        Switching,
        Interrupted,
        Completed,
        Ended
    }

    public enum ActivityEndReason
    {
        Requested,
        Switched,
        LeftAnchor,
        StateChanged,
        TargetUnavailable,
        ResourceUnavailable,
        AuthorityRejected,
        Completed,
        OwnerDisabled
    }
}
