using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace DemonViglu.FirePlay.Network
{
    /// <summary>
    /// NGO 的唯一启动边界。
    ///
    /// 这个组件不创建 NetworkManager、Transport 或 Player，所有引用必须在 Inspector 中显式配置。
    /// 当前阶段只验证 Host/Server/Client 的连接生命周期；Player、Activity 和 Flame 不在这里处理。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class FirePlayNetworkBootstrap : MonoBehaviour, IFirePlayNetworkSessionControl
    {
        public enum AutoStartMode
        {
            Manual,
            Host,
            Server,
            Client
        }

        [Header("显式引用")]
        [SerializeField] private NetworkManager _networkManager;
        [SerializeField] private UnityTransport _transport;
        [SerializeField] private NetworkObject _worldStatePrefab;

        [Header("本地测试")]
        [SerializeField] private AutoStartMode _autoStart = AutoStartMode.Manual;
        [SerializeField] private string _serverAddress = "127.0.0.1";
        [SerializeField] private string _listenAddress = "0.0.0.0";
        [SerializeField] private ushort _port = 7777;
        [SerializeField] private bool _allowCommandLineOverrides = true;

        [Header("连接准入")]
        [SerializeField, Min(1)] private int _maximumPlayers = 4;

        private IEventPublisher _events;
        private FirePlayNetworkMode _mode;
        private bool _subscribed;
        private bool _ownsConnectionApprovalCallback;
        private NetworkObject _spawnedWorldState;
        private readonly HashSet<ulong> _approvedPendingClients = new();

        public FirePlayNetworkMode Mode => _mode;
        public FirePlayNetworkState State { get; private set; } = FirePlayNetworkState.Stopped;
        public bool IsRunning => _networkManager != null && _networkManager.IsListening;
        public AutoStartMode ConfiguredAutoStart => _autoStart;
        public string ServerAddress => _serverAddress;
        public string ListenAddress => _listenAddress;
        public ushort Port => _port;
        public int MaximumPlayers => _maximumPlayers;
        public string LastReason { get; private set; } = string.Empty;

        private void Awake()
        {
            if (_networkManager == null)
            {
                _networkManager = GetComponent<NetworkManager>();
            }

            if (_transport == null)
            {
                _transport = GetComponent<UnityTransport>();
            }

            if (_networkManager == null || _transport == null)
            {
                Debug.LogError(
                    "[FirePlayNetworkBootstrap] 需要在同一 GameObject 上显式配置 NetworkManager 和 UnityTransport。" ,
                    this);
                enabled = false;
                return;
            }

            ApplyCommandLineOverrides();

            if (_networkManager.NetworkConfig.NetworkTransport != _transport)
            {
                Debug.LogError(
                    "[FirePlayNetworkBootstrap] NetworkManager.NetworkConfig.NetworkTransport 必须指向同一对象上的 UnityTransport；不会自动修复。",
                    this);
                enabled = false;
                return;
            }

            if (_networkManager.NetworkConfig.PlayerPrefab == null)
            {
                Debug.LogError(
                    "[FirePlayNetworkBootstrap] NetworkManager.PlayerPrefab 未绑定有效 GameObject；不会启动网络。",
                    this);
                enabled = false;
                return;
            }

            if (!_networkManager.NetworkConfig.ConnectionApproval)
            {
                Debug.LogError(
                    "[FirePlayNetworkBootstrap] NetworkConfig.ConnectionApproval 必须显式开启；不会自动修改场景配置。",
                    this);
                enabled = false;
                return;
            }

            if (_maximumPlayers < 1)
            {
                Debug.LogError("[FirePlayNetworkBootstrap] Maximum Players 必须至少为 1。", this);
                enabled = false;
                return;
            }

            if (!_networkManager.NetworkConfig.PlayerPrefab.TryGetComponent<NetworkObject>(out _))
            {
                Debug.LogError(
                    "[FirePlayNetworkBootstrap] NetworkManager.PlayerPrefab 必须挂载 NetworkObject；不会启动网络。",
                    this);
                enabled = false;
                return;
            }

            if (_worldStatePrefab == null
                || !_worldStatePrefab.TryGetComponent<FirePlayNetworkWorldState>(out _))
            {
                Debug.LogError(
                    "[FirePlayNetworkBootstrap] World State Prefab 必须显式绑定含 FirePlayNetworkWorldState 的 NetworkObject。",
                    this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!enabled || _networkManager == null || _subscribed)
            {
                return;
            }

            var approvalCallback = _networkManager.ConnectionApprovalCallback;
            if (approvalCallback != null && approvalCallback != OnConnectionApproval)
            {
                Debug.LogError(
                    "[FirePlayNetworkBootstrap] ConnectionApprovalCallback 已被其他组件占用；Bootstrap 是唯一连接准入边界。",
                    this);
                enabled = false;
                return;
            }

            _networkManager.ConnectionApprovalCallback = OnConnectionApproval;
            _ownsConnectionApprovalCallback = true;

            var existing = GameInstanceSubsystem.TryGet<IFirePlayNetworkSessionControl>();
            if (existing == null)
                GameInstanceSubsystem.Register<IFirePlayNetworkSessionControl>(this);
            else if (!ReferenceEquals(existing, this))
            {
                Debug.LogError("[FirePlayNetworkBootstrap] 场景中只能注册一个网络会话入口。", this);
                enabled = false;
                return;
            }

            _networkManager.OnConnectionEvent += OnConnectionEvent;
            _subscribed = true;
        }

        private void Start()
        {
            if (!enabled || _autoStart == AutoStartMode.Manual)
            {
                return;
            }

            switch (_autoStart)
            {
                case AutoStartMode.Host:
                    StartHost();
                    break;
                case AutoStartMode.Server:
                    StartServer();
                    break;
                case AutoStartMode.Client:
                    StartClient();
                    break;
            }
        }

        private void OnDisable()
        {
            if (_networkManager != null && _subscribed)
            {
                _networkManager.OnConnectionEvent -= OnConnectionEvent;
                _subscribed = false;
            }
            if (_networkManager != null
                && _ownsConnectionApprovalCallback
                && _networkManager.ConnectionApprovalCallback == OnConnectionApproval)
            {
                _networkManager.ConnectionApprovalCallback = null;
            }
            _ownsConnectionApprovalCallback = false;
            _approvedPendingClients.Clear();
            if (ReferenceEquals(GameInstanceSubsystem.TryGet<IFirePlayNetworkSessionControl>(), this))
                GameInstanceSubsystem.Unregister<IFirePlayNetworkSessionControl>();
        }

        public bool StartHost()
        {
            if (_networkManager == null)
                return false;
            return StartMode(FirePlayNetworkMode.Host, _networkManager.StartHost);
        }

        public bool StartServer()
        {
            if (_networkManager == null)
                return false;
            return StartMode(FirePlayNetworkMode.Server, _networkManager.StartServer);
        }

        public bool StartClient()
        {
            if (_networkManager == null)
                return false;
            return StartMode(FirePlayNetworkMode.Client, _networkManager.StartClient);
        }

        /// <summary>Runtime connection UI may configure the endpoint before starting NGO.</summary>
        public bool ConfigureEndpoint(string serverAddress, ushort port, string listenAddress = "0.0.0.0")
        {
            if (IsRunning || string.IsNullOrWhiteSpace(serverAddress) || serverAddress.Trim().Length > 253
                || string.IsNullOrWhiteSpace(listenAddress) || listenAddress.Trim().Length > 253
                || port == 0)
            {
                return false;
            }

            _serverAddress = serverAddress.Trim();
            _listenAddress = listenAddress.Trim();
            _port = port;
            return true;
        }

        public void Shutdown()
        {
            if (_networkManager == null || !_networkManager.IsListening)
            {
                return;
            }

            _networkManager.Shutdown();
            _spawnedWorldState = null;
            _approvedPendingClients.Clear();
            var previousMode = _mode;
            _mode = FirePlayNetworkMode.None;
            Publish(
                FirePlayNetworkState.Stopped,
                previousMode,
                reason: "NetworkManager shutdown");
            Debug.Log("[FirePlayNetworkBootstrap] 网络会话已停止。", this);
        }

        private bool StartMode(FirePlayNetworkMode mode, System.Func<bool> start)
        {
            if (!enabled || _networkManager == null || _transport == null)
            {
                return false;
            }

            if (_networkManager.IsListening)
            {
                Debug.LogWarning("[FirePlayNetworkBootstrap] NetworkManager 已经在运行，忽略重复启动。", this);
                return false;
            }

            if (!ValidateEndpoint())
            {
                return false;
            }

            _approvedPendingClients.Clear();
            _networkManager.NetworkConfig.ConnectionData = FirePlayNetworkProtocol.CreatePayload();

            _mode = mode;
            Publish(FirePlayNetworkState.Starting, mode);

            bool started;
            try
            {
                started = start();
            }
            catch (Exception exception)
            {
                var reason = $"NetworkManager.Start... 抛出异常：{exception.GetType().Name}";
                Publish(FirePlayNetworkState.StartFailed, mode, reason: reason);
                Debug.LogException(exception, this);
                _mode = FirePlayNetworkMode.None;
                _approvedPendingClients.Clear();
                return false;
            }
            if (!started)
            {
                var reason = "NetworkManager.Start... 返回 false";
                Publish(FirePlayNetworkState.StartFailed, mode, reason: reason);
                Debug.LogError($"[FirePlayNetworkBootstrap] {mode} 启动失败：{reason}。", this);
                _mode = FirePlayNetworkMode.None;
                _approvedPendingClients.Clear();
                return false;
            }

            if ((mode == FirePlayNetworkMode.Host || mode == FirePlayNetworkMode.Server)
                && !TrySpawnAuthorityWorldState(out var worldStateReason))
            {
                _networkManager.Shutdown();
                Publish(FirePlayNetworkState.StartFailed, mode, reason: worldStateReason);
                Debug.LogError($"[FirePlayNetworkBootstrap] {mode} 启动失败：{worldStateReason}。", this);
                _mode = FirePlayNetworkMode.None;
                _approvedPendingClients.Clear();
                return false;
            }

            Publish(FirePlayNetworkState.Started, mode);
            Debug.Log($"[FirePlayNetworkBootstrap] {mode} 已启动，地址={_serverAddress}，端口={_port}。", this);
            return true;
        }

        private bool TrySpawnAuthorityWorldState(out string reason)
        {
            reason = string.Empty;
            if (_networkManager == null || !_networkManager.IsServer)
            {
                reason = "World State 只能由 Host/Server 生成";
                return false;
            }

            if (_spawnedWorldState != null && _spawnedWorldState.IsSpawned)
                return true;

            NetworkObject instance = null;
            try
            {
                instance = Instantiate(_worldStatePrefab);
                instance.name = _worldStatePrefab.name;
                instance.Spawn(destroyWithScene: true);
                _spawnedWorldState = instance;
                Debug.Log("[FirePlayNetworkBootstrap] Host 已生成 Network World State。", instance);
                return true;
            }
            catch (Exception exception)
            {
                if (instance != null)
                    Destroy(instance.gameObject);
                Debug.LogException(exception, this);
                reason = "Network World State 生成失败";
                return false;
            }
        }

        private void OnConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            response.Pending = false;
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.PlayerPrefabHash = null;
            response.Position = null;
            response.Rotation = null;
            response.Reason = string.Empty;

            // NGO invokes approval for the Host's own local player during StartHost.
            if (_networkManager != null
                && _networkManager.IsHost
                && request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true;
                response.CreatePlayerObject = true;
                return;
            }

            if (!FirePlayNetworkProtocol.TryValidate(request.Payload, out var rejectionReason))
            {
                response.Reason = rejectionReason;
                Debug.LogWarning(
                    $"[FirePlayNetworkBootstrap] 拒绝 clientId={request.ClientNetworkId}：{rejectionReason}。",
                    this);
                return;
            }

            var connectedCount = _networkManager != null
                ? _networkManager.ConnectedClientsIds.Count
                : 0;
            if (connectedCount + _approvedPendingClients.Count >= _maximumPlayers)
            {
                response.Reason = $"房间人数已满（上限 {_maximumPlayers}）";
                Debug.LogWarning(
                    $"[FirePlayNetworkBootstrap] 拒绝 clientId={request.ClientNetworkId}：{response.Reason}。",
                    this);
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
            _approvedPendingClients.Add(request.ClientNetworkId);
            Debug.Log(
                $"[FirePlayNetworkBootstrap] 准入 clientId={request.ClientNetworkId}，协议={FirePlayNetworkProtocol.CurrentVersion}。",
                this);
        }

        private bool ValidateEndpoint()
        {
            if (_port == 0)
            {
                Debug.LogError("[FirePlayNetworkBootstrap] 端口不能为 0。", this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_serverAddress))
            {
                Debug.LogError("[FirePlayNetworkBootstrap] Server Address 不能为空。", this);
                return false;
            }

            _transport.SetConnectionData(_serverAddress, _port, _listenAddress);
            return true;
        }

        private void ApplyCommandLineOverrides()
        {
            if (!_allowCommandLineOverrides)
                return;

            if (!FirePlayNetworkLaunchOptions.TryParse(
                    Environment.GetCommandLineArgs(),
                    out var options,
                    out var error))
            {
                Debug.LogError($"[FirePlayNetworkBootstrap] 启动参数无效：{error}。", this);
                enabled = false;
                return;
            }
            if (!options.HasAnyOverride)
                return;

            if (options.HasModeOverride)
            {
                _autoStart = options.Mode switch
                {
                    FirePlayNetworkMode.Host => AutoStartMode.Host,
                    FirePlayNetworkMode.Server => AutoStartMode.Server,
                    FirePlayNetworkMode.Client => AutoStartMode.Client,
                    _ => AutoStartMode.Manual
                };
            }
            if (options.HasServerAddressOverride)
                _serverAddress = options.ServerAddress;
            if (options.HasListenAddressOverride)
                _listenAddress = options.ListenAddress;
            if (options.HasPortOverride)
                _port = options.Port;

            Debug.Log(
                $"[FirePlayNetworkBootstrap] 已应用启动参数：mode={_autoStart}, address={_serverAddress}, listen={_listenAddress}, port={_port}。",
                this);
        }

        private void OnConnectionEvent(NetworkManager manager, ConnectionEventData data)
        {
            _approvedPendingClients.Remove(data.ClientId);

            var state = data.EventType switch
            {
                ConnectionEvent.ClientConnected => FirePlayNetworkState.ClientConnected,
                ConnectionEvent.ClientDisconnected => FirePlayNetworkState.ClientDisconnected,
                ConnectionEvent.PeerConnected => FirePlayNetworkState.PeerConnected,
                ConnectionEvent.PeerDisconnected => FirePlayNetworkState.PeerDisconnected,
                _ => FirePlayNetworkState.StartFailed
            };

            var reason = data.EventType == ConnectionEvent.ClientDisconnected
                         && _mode == FirePlayNetworkMode.Client
                         && !string.IsNullOrWhiteSpace(manager.DisconnectReason)
                ? manager.DisconnectReason
                : data.EventType.ToString();
            Debug.Log(
                $"[FirePlayNetworkBootstrap] {data.EventType}，clientId={data.ClientId}，mode={_mode}，reason={reason}。",
                this);
            Publish(state, _mode, data.ClientId, reason);
        }

        private void Publish(
            FirePlayNetworkState state,
            FirePlayNetworkMode mode,
            ulong clientId = 0,
            string reason = null)
        {
            State = state;
            LastReason = reason ?? string.Empty;
            _events ??= GameInstanceSubsystem.GetOrCreate<IEventPublisher>(() => new GameEventBus());
            _events.Publish(new FirePlayNetworkStateChanged(state, mode, clientId, reason));
        }
    }
}
