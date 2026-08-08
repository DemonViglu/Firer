using System;

namespace DemonViglu.FirePlay.Network
{
    /// <summary>
    /// Pure launch-data parser shared by desktop builds and future connection UI.
    /// Unknown process arguments are ignored so Unity and platform flags remain valid.
    /// </summary>
    public readonly struct FirePlayNetworkLaunchOptions
    {
        public bool HasModeOverride { get; }
        public FirePlayNetworkMode Mode { get; }
        public bool HasServerAddressOverride { get; }
        public string ServerAddress { get; }
        public bool HasListenAddressOverride { get; }
        public string ListenAddress { get; }
        public bool HasPortOverride { get; }
        public ushort Port { get; }

        public bool HasAnyOverride => HasModeOverride
            || HasServerAddressOverride
            || HasListenAddressOverride
            || HasPortOverride;

        private FirePlayNetworkLaunchOptions(
            bool hasModeOverride,
            FirePlayNetworkMode mode,
            bool hasServerAddressOverride,
            string serverAddress,
            bool hasListenAddressOverride,
            string listenAddress,
            bool hasPortOverride,
            ushort port)
        {
            HasModeOverride = hasModeOverride;
            Mode = mode;
            HasServerAddressOverride = hasServerAddressOverride;
            ServerAddress = serverAddress ?? string.Empty;
            HasListenAddressOverride = hasListenAddressOverride;
            ListenAddress = listenAddress ?? string.Empty;
            HasPortOverride = hasPortOverride;
            Port = port;
        }

        public static bool TryParse(string[] arguments, out FirePlayNetworkLaunchOptions options, out string error)
        {
            var hasMode = false;
            var mode = FirePlayNetworkMode.None;
            var hasServerAddress = false;
            var serverAddress = string.Empty;
            var hasListenAddress = false;
            var listenAddress = string.Empty;
            var hasPort = false;
            ushort port = 0;

            arguments ??= Array.Empty<string>();
            for (var index = 0; index < arguments.Length; index++)
            {
                if (TryReadOption(arguments, ref index, "fireplay-mode", out var modeValue))
                {
                    if (!TryParseMode(modeValue, out mode))
                    {
                        options = default;
                        error = $"Invalid fireplay mode: {modeValue}";
                        return false;
                    }
                    hasMode = true;
                    continue;
                }

                if (TryReadOption(arguments, ref index, "fireplay-address", out var addressValue))
                {
                    if (!IsValidAddressText(addressValue))
                    {
                        options = default;
                        error = "Invalid fireplay server address";
                        return false;
                    }
                    hasServerAddress = true;
                    serverAddress = addressValue.Trim();
                    continue;
                }

                if (TryReadOption(arguments, ref index, "fireplay-listen-address", out var listenValue))
                {
                    if (!IsValidAddressText(listenValue))
                    {
                        options = default;
                        error = "Invalid fireplay listen address";
                        return false;
                    }
                    hasListenAddress = true;
                    listenAddress = listenValue.Trim();
                    continue;
                }

                if (!TryReadOption(arguments, ref index, "fireplay-port", out var portValue))
                    continue;

                if (!ushort.TryParse(portValue, out port) || port == 0)
                {
                    options = default;
                    error = $"Invalid fireplay port: {portValue}";
                    return false;
                }
                hasPort = true;
            }

            options = new FirePlayNetworkLaunchOptions(
                hasMode,
                mode,
                hasServerAddress,
                serverAddress,
                hasListenAddress,
                listenAddress,
                hasPort,
                port);
            error = string.Empty;
            return true;
        }

        private static bool TryReadOption(
            string[] arguments,
            ref int index,
            string optionName,
            out string value)
        {
            value = string.Empty;
            var argument = arguments[index];
            if (string.IsNullOrWhiteSpace(argument))
                return false;

            var normalizedName = optionName.TrimStart('-');
            var trimmed = argument.Trim();
            var withoutPrefix = trimmed.TrimStart('-');
            if (withoutPrefix.StartsWith(normalizedName + "=", StringComparison.OrdinalIgnoreCase))
            {
                value = withoutPrefix[(normalizedName.Length + 1)..];
                return true;
            }
            if (!string.Equals(withoutPrefix, normalizedName, StringComparison.OrdinalIgnoreCase))
                return false;
            if (index + 1 >= arguments.Length)
            {
                value = string.Empty;
                return true;
            }

            value = arguments[++index];
            return true;
        }

        private static bool TryParseMode(string value, out FirePlayNetworkMode mode)
        {
            mode = FirePlayNetworkMode.None;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            switch (value.Trim().ToLowerInvariant())
            {
                case "manual":
                case "none":
                    return true;
                case "host":
                    mode = FirePlayNetworkMode.Host;
                    return true;
                case "server":
                    mode = FirePlayNetworkMode.Server;
                    return true;
                case "client":
                    mode = FirePlayNetworkMode.Client;
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsValidAddressText(string value) =>
            !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 253;
    }
}
