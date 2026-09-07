namespace DemonViglu.FirePlay.UI
{
    public interface IFirePlayUiService
    {
        bool Show(string uiId);
        bool Hide(string uiId);
        bool CloseOrReturn(string uiId);
        bool Back();
        bool IsShown(string uiId);
        bool TryGetView(string uiId, out FirePlayUiView view);
    }
}
