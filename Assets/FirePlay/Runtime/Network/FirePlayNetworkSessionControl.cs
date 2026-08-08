namespace DemonViglu.FirePlay.Network
{
    /// <summary>
    /// UI-facing connection boundary. Presentation code may configure and
    /// start a session without referencing NGO or Unity Transport.
    /// </summary>
    public interface IFirePlayNetworkSessionControl
    {
        FirePlayNetworkMode Mode { get; }
        FirePlayNetworkState State { get; }
        bool IsRunning { get; }
        string ServerAddress { get; }
        string ListenAddress { get; }
        ushort Port { get; }
        string LastReason { get; }

        bool ConfigureEndpoint(string serverAddress, ushort port, string listenAddress = "0.0.0.0");
        bool StartHost();
        bool StartServer();
        bool StartClient();
        void Shutdown();
    }
}
