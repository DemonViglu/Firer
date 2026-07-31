using DemonViglu.FirePlay.Player;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>活动专属 UI／输入 Presenter；不把 SUIFW 或窗口栈放入 ActivitySession。</summary>
    public interface IActivityPresenter
    {
        string ActivityId { get; }
        void Present(ActivitySessionSnapshot session, ActivityUIOrchestrator orchestrator);
        void Close();
    }
}
