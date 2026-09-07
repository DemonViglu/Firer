using UnityEngine;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Registers the scene-authored UI root as the only FirePlay UI service.
    /// </summary>
    public sealed class FirePlayUiBootstrap : MonoBehaviour
    {
        [SerializeField] private FirePlayUiRoot _root;
        [SerializeField] private FirePlayUiCatalog _catalog;
        [SerializeField] private bool _showNetworkConnectionOnStart;

        private void Awake()
        {
            if (GameInstanceSubsystem.TryGet<IFirePlayUiService>() != null)
            {
                Debug.LogError("[FirePlayUiBootstrap] 场景已有 UI Service，请删除重复 Bootstrap。", this);
                enabled = false;
                return;
            }
            if (_root == null || _catalog == null)
            {
                Debug.LogError("[FirePlayUiBootstrap] 必须显式绑定 FirePlayUiRoot 与 Ui Catalog。", this);
                enabled = false;
                return;
            }

            if (!_root.Initialize(_catalog))
            {
                enabled = false;
                return;
            }

            GameInstanceSubsystem.Register<IFirePlayUiService>(_root);
        }

        private void Start()
        {
            if (!enabled) return;

            _root.Show(FirePlayUiIds.Hud);
            if (_showNetworkConnectionOnStart)
                ShowNetworkConnection();
        }

        public void ShowNetworkConnection()
        {
            GameInstanceSubsystem.TryGet<IFirePlayUiService>()?.Show(FirePlayUiIds.NetworkConnection);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(GameInstanceSubsystem.TryGet<IFirePlayUiService>(), _root))
                GameInstanceSubsystem.Unregister<IFirePlayUiService>();
        }
    }
}
