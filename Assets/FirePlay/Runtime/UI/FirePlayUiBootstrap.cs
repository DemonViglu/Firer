using SUIFW;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// 启动 SUIFW 的 Resources Canvas。将本组件放在每个可游玩的场景的任意常驻对象上；
    /// UIManager 会保留 Canvas，因此后续场景重复调用不会生成第二套根节点。
    /// </summary>
    public sealed class FirePlayUiBootstrap : MonoBehaviour
    {
        [SerializeField] private bool _showNetworkConnectionOnStart;

        private void Awake()
        {
            UIManager.GetInstance();
            ConfigureUiInput();
        }

        private void Start()
        {
            if (_showNetworkConnectionOnStart)
                ShowNetworkConnection();
        }

        public void ShowNetworkConnection()
        {
            UIManager.GetInstance().ShowUIForms("NetworkConnectionForms");
        }

        private static void ConfigureUiInput()
        {
            var eventSystem = UnityEngine.Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                Debug.LogWarning("[FirePlayUiBootstrap] SUIFW Canvas did not provide an EventSystem; mobile UI will be display-only.");
                return;
            }

            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                Debug.LogError(
                    "[FirePlayUiBootstrap] SUIFW Canvas 的 EventSystem 缺少显式 InputSystemUIInputModule；不会在运行时动态补组件。",
                    eventSystem);
                return;
            }

            if (inputModule.actionsAsset == null)
            {
                Debug.LogError(
                    "[FirePlayUiBootstrap] InputSystemUIInputModule 缺少 UI Actions Asset；请在 Canvas Prefab 中显式绑定。",
                    inputModule);
            }
        }
    }
}
