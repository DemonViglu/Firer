using System;
using System.Text;

namespace DemonViglu.FirePlay.Network
{
    /// <summary>
    /// FirePlay realtime-session compatibility handshake.
    /// This is deliberately not authentication and carries no player/gameplay data.
    /// </summary>
    public static class FirePlayNetworkProtocol
    {
        public const int CurrentVersion = 1;
        public const int MaximumPayloadBytes = 64;

        private const string ProtocolName = "fireplay.realtime";
        private static readonly byte[] CurrentPayload =
            Encoding.UTF8.GetBytes($"{ProtocolName}|{CurrentVersion}");

        public static byte[] CreatePayload()
        {
            var payload = new byte[CurrentPayload.Length];
            Buffer.BlockCopy(CurrentPayload, 0, payload, 0, CurrentPayload.Length);
            return payload;
        }

        public static bool TryValidate(byte[] payload, out string rejectionReason)
        {
            if (payload == null || payload.Length == 0)
            {
                rejectionReason = "客户端缺少 FirePlay 实时联机协议握手";
                return false;
            }

            if (payload.Length > MaximumPayloadBytes)
            {
                rejectionReason = "客户端握手数据超过 FirePlay 协议上限";
                return false;
            }

            var value = Encoding.UTF8.GetString(payload);
            var expected = $"{ProtocolName}|{CurrentVersion}";
            if (string.Equals(value, expected, StringComparison.Ordinal))
            {
                rejectionReason = string.Empty;
                return true;
            }

            rejectionReason = value.StartsWith($"{ProtocolName}|", StringComparison.Ordinal)
                ? $"FirePlay 联机协议版本不兼容（Host={CurrentVersion}）"
                : "客户端不是兼容的 FirePlay 实时联机版本";
            return false;
        }
    }
}
