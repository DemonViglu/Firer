using DemonViglu.FirePlay.Network;
using SUIFW;
using UnityEngine;
using UnityEngine.UI;

namespace DemonViglu.FirePlay.UI
{
    /// <summary>
    /// Minimal Android/desktop direct-connect form. It depends only on the
    /// session-control contract and never accesses NGO or Unity Transport.
    /// </summary>
    public sealed class NetworkConnectionForms : BaseUIForms
    {
        private const string AddressPreferenceKey = "fireplay.network.address";
        private const string PortPreferenceKey = "fireplay.network.port";

        [SerializeField] private InputField _addressInput;
        [SerializeField] private InputField _portInput;
        [SerializeField] private Text _statusText;
        [SerializeField] private Button _connectButton;
        [SerializeField] private Text _connectButtonText;

        private IFirePlayNetworkSessionControl _session;
        private IEventPublisher _events;
        private bool _subscribed;

        private void Awake()
        {
            if (_addressInput != null)
                _addressInput.contentType = InputField.ContentType.Standard;
            if (_portInput != null)
            {
                _portInput.contentType = InputField.ContentType.IntegerNumber;
                _portInput.characterLimit = 5;
            }
        }

        public override void Display()
        {
            base.Display();
            ResolveSession();
            PopulateEndpoint();
            Bind();
            RefreshStatus();
        }

        public override void Hiding()
        {
            Unbind();
            base.Hiding();
        }

        private void ResolveSession()
        {
            _session = GameInstanceSubsystem.TryGet<IFirePlayNetworkSessionControl>();
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
        }

        private void PopulateEndpoint()
        {
            if (_session == null)
                return;

            if (_addressInput != null)
            {
                _addressInput.text = PlayerPrefs.GetString(
                    AddressPreferenceKey,
                    _session.ServerAddress);
            }
            if (_portInput != null)
            {
                var savedPort = PlayerPrefs.GetInt(PortPreferenceKey, _session.Port);
                _portInput.text = Mathf.Clamp(savedPort, 1, ushort.MaxValue).ToString();
            }
        }

        private void Bind()
        {
            Unbind();
            _connectButton?.onClick.AddListener(OnConnectOrDisconnectClicked);
            if (_events != null)
            {
                _events.Subscribe<FirePlayNetworkStateChanged>(OnNetworkStateChanged);
                _subscribed = true;
            }
        }

        private void Unbind()
        {
            _connectButton?.onClick.RemoveListener(OnConnectOrDisconnectClicked);
            if (_subscribed && _events != null)
                _events.Unsubscribe<FirePlayNetworkStateChanged>(OnNetworkStateChanged);
            _subscribed = false;
        }

        private void OnConnectOrDisconnectClicked()
        {
            ResolveSession();
            if (_session == null)
            {
                SetStatus("网络会话入口未就绪");
                return;
            }

            if (_session.IsRunning)
            {
                _session.Shutdown();
                RefreshStatus();
                return;
            }

            var address = _addressInput != null ? _addressInput.text : string.Empty;
            if (_portInput == null || !ushort.TryParse(_portInput.text, out var port) || port == 0)
            {
                SetStatus("端口必须是 1 到 65535");
                return;
            }
            if (!_session.ConfigureEndpoint(address, port))
            {
                SetStatus("服务器地址或端口无效");
                return;
            }

            PlayerPrefs.SetString(AddressPreferenceKey, _session.ServerAddress);
            PlayerPrefs.SetInt(PortPreferenceKey, _session.Port);
            PlayerPrefs.Save();
            SetStatus("正在连接...");
            if (!_session.StartClient())
                RefreshStatus();
        }

        private void OnNetworkStateChanged(FirePlayNetworkStateChanged change)
        {
            if (change != null)
                RefreshStatus();
        }

        private void RefreshStatus()
        {
            ResolveSession();
            if (_session == null)
            {
                SetStatus("网络会话入口未就绪");
                SetButton("连接", false);
                return;
            }

            var status = _session.State switch
            {
                FirePlayNetworkState.Starting => "正在启动网络...",
                FirePlayNetworkState.Started when _session.Mode == FirePlayNetworkMode.Client => "等待服务器确认...",
                FirePlayNetworkState.Started => $"{_session.Mode} 已启动",
                FirePlayNetworkState.ClientConnected when _session.Mode == FirePlayNetworkMode.Client => "已连接服务器",
                FirePlayNetworkState.ClientConnected => "客户端已连接",
                FirePlayNetworkState.ClientDisconnected => string.IsNullOrWhiteSpace(_session.LastReason)
                    ? "连接已断开"
                    : _session.LastReason,
                FirePlayNetworkState.StartFailed => string.IsNullOrWhiteSpace(_session.LastReason)
                    ? "网络启动失败"
                    : _session.LastReason,
                FirePlayNetworkState.Stopped => "尚未连接",
                _ => _session.State.ToString()
            };
            SetStatus(status);
            SetButton(_session.IsRunning ? "断开" : "连接", true);

            var editable = !_session.IsRunning;
            if (_addressInput != null) _addressInput.interactable = editable;
            if (_portInput != null) _portInput.interactable = editable;
        }

        private void SetStatus(string value)
        {
            if (_statusText != null)
                _statusText.text = value ?? string.Empty;
        }

        private void SetButton(string label, bool interactable)
        {
            if (_connectButtonText != null)
                _connectButtonText.text = label ?? string.Empty;
            if (_connectButton != null)
                _connectButton.interactable = interactable;
        }

        private void OnDisable()
        {
            Unbind();
        }
    }
}
