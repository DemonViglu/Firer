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
        private void Awake()
        {
            UIManager.GetInstance();
            ConfigureUiInput();
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
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            }

            // Creates the standard Pointer/Touch actions owned by this module. It
            // replaces the disabled legacy StandaloneInputModule and never reads
            // UnityEngine.Input in an Input System-only project.
            inputModule.AssignDefaultActions();
        }
    }
}
