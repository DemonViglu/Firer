namespace DemonViglu.FirePlay.Player
{
    /// <summary>
    /// 活动自己的语义动作处理契约。
    /// UI、输入和网络层只提交 ActivityActionRequested，具体活动决定如何消费。
    /// </summary>
    public interface IActivityActionHandler
    {
        string ActivityId { get; }
        bool TryHandle(ActivityActionRequested request);
    }
}
