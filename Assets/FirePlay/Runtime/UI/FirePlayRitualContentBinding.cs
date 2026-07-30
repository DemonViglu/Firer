using UnityEngine;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>把一个 UI 内容根绑定到 Ritual ID；新增仪式只需在 Prefab 添加绑定。</summary>
    public sealed class FirePlayRitualContentBinding : MonoBehaviour
    {
        [SerializeField] private string _ritualId;
        public string RitualId => _ritualId;

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
