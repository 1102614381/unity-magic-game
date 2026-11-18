using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace MagicIsland.Core
{
    /// <summary>
    /// Unity → Python 事件回传（V1.1 7.3，UDP 5006）。
    /// 发送失败不影响本地 CSV：本地 game_events.csv 始终是兜底，UDP 只是旁路。
    /// 失败只计数 + 首次警告，避免刷屏。
    /// </summary>
    public static class EventSender
    {
        private const string DefaultHost = "127.0.0.1";
        private const int DefaultPort = 5006;

        private static readonly object Gate = new object();
        private static UdpClient _client;
        private static string _host = DefaultHost;
        private static int _port = DefaultPort;
        private static bool _warned;

        public static int SentCount { get; private set; }
        public static int FailedCount { get; private set; }
        public static string LastError { get; private set; }

        public static void Configure(string host, int port)
        {
            lock (Gate)
            {
                _host = string.IsNullOrEmpty(host) ? DefaultHost : host;
                _port = port <= 0 ? DefaultPort : port;
                try { _client?.Close(); } catch { }
                _client = null;
            }
            Debug.Log($"[EventSender] 事件回传目标设置为 {_host}:{_port}");
        }

        public static void Send(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            lock (Gate)
            {
                try
                {
                    if (_client == null) _client = new UdpClient();
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    _client.Send(data, data.Length, _host, _port);
                    SentCount++;
                }
                catch (Exception e)
                {
                    FailedCount++;
                    LastError = e.Message;
                    if (!_warned)
                    {
                        _warned = true;
                        Debug.LogWarning(
                            $"[EventSender] UDP {_host}:{_port} 回传失败（不影响本地CSV，Python侧未启动时属正常）：{e.Message}");
                    }
                    try { _client?.Close(); } catch { }
                    _client = null;
                }
            }
        }
    }
}
