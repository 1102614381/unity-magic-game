using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MagicIsland.IMU
{
    /// <summary>
    /// 全组唯一的 IMU 输入入口（需求说明书第十一章：不得各自实现 UDP 监听器）。
    /// 后台线程收 UDP → 主线程解析 → 广播 OnFrame 事件。
    /// 帧字段见 ImuFrame；姿态输出已做零位校准、极性反转与死区处理。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class IMUReceiver : MonoBehaviour
    {
        public static IMUReceiver Instance { get; private set; }

        [Header("网络")]
        [SerializeField] private int listenPort = 5005;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float connectionTimeoutSeconds = 1.0f;

        [Header("姿态处理")]
        [Tooltip("真机装反时勾选，等价于取负")]
        [SerializeField] private bool invertRoll;
        [SerializeField] private bool invertPitch;
        [SerializeField] private bool invertYaw;
        [Tooltip("绝对值小于该角度的姿态输出为 0，抑制手抖与静止漂移")]
        [SerializeField] private float deadZoneDegrees = 2f;

        /// <summary>每收到一帧解析成功后触发（主线程）。</summary>
        public event Action<ImuFrame> FrameReceived;

        /// <summary>连接状态变化时触发（主线程）。</summary>
        public event Action<bool> ConnectionChanged;

        public int Port => listenPort;
        public ImuFrame RawFrame { get; private set; }
        public bool HasFrame { get; private set; }
        public bool IsConnected { get; private set; }
        public int FrameCount { get; private set; }
        public int ParseErrorCount { get; private set; }
        public string LastError { get; private set; }

        /// <summary>经零位校准 + 极性处理 + 死区后的姿态角（度）。</summary>
        public float Roll => Apply(RawFrame.roll - _zeroRoll, invertRoll);
        public float Pitch => Apply(RawFrame.pitch - _zeroPitch, invertPitch);
        public float Yaw => Apply(RawFrame.yaw - _zeroYaw, invertYaw);

        private readonly ConcurrentQueue<string> _queue = new ConcurrentQueue<string>();
        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;

        private float _startRealtime;
        private float _lastFrameRealtime = -999f;
        private float _zeroRoll, _zeroPitch, _zeroYaw;
        private long _lastFrameId = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _startRealtime = Time.realtimeSinceStartup;
            if (autoStart) StartListening();
        }

        private void Update()
        {
            // 每帧最多处理一定数量的积压帧，避免突发数据卡住主线程
            const int budget = 64;
            for (int i = 0; i < budget; i++)
            {
                if (!_queue.TryDequeue(out string json)) break;

                if (!ImuJsonParser.TryParse(json, out var frame))
                {
                    ParseErrorCount++;
                    continue;
                }

                frame.unityRecvTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                frame.unityElapsedMs = (Time.realtimeSinceStartup - _startRealtime) * 1000f;

                if (!HasFrame) CalibrateZero(frame);

                // 用 frame_id 粗判丢包（仅提示，不作为数据完整性依据）
                if (_lastFrameId >= 0 && frame.frameId > _lastFrameId + 1)
                {
                    // 设备重启或 Python 重连时会跳变，这里只记录不报警
                }
                _lastFrameId = frame.frameId;

                RawFrame = frame;
                HasFrame = true;
                FrameCount++;
                _lastFrameRealtime = Time.realtimeSinceStartup;

                FrameReceived?.Invoke(frame);
            }

            bool connected = HasFrame &&
                             (Time.realtimeSinceStartup - _lastFrameRealtime) <= connectionTimeoutSeconds;
            if (connected != IsConnected)
            {
                IsConnected = connected;
                ConnectionChanged?.Invoke(connected);
            }
        }

        public void StartListening(int? port = null)
        {
            if (port.HasValue) listenPort = port.Value;
            if (_running) StopListening();

            try
            {
                _client = new UdpClient(new IPEndPoint(IPAddress.Any, listenPort));
                _client.Client.ReceiveBufferSize = 1 << 20;

                _running = true;
                _thread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "MagicIsland-IMUReceiver"
                };
                _thread.Start();
                Debug.Log($"[IMUReceiver] 开始监听 UDP 0.0.0.0:{listenPort}");
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogError($"[IMUReceiver] 监听端口 {listenPort} 失败：{e.Message}" +
                               "（端口被占用或权限不足）");
            }
        }

        public void StopListening()
        {
            _running = false;
            try { _client?.Close(); } catch { /* 忽略关闭异常 */ }
            _client = null;

            if (_thread != null && _thread.IsAlive) _thread.Join(200);
            _thread = null;
        }

        /// <summary>把当前姿态记为零位（儿童举起魔法棒的初始姿势）。</summary>
        public void CalibrateZero()
        {
            if (HasFrame) CalibrateZero(RawFrame);
        }

        /// <summary>清除零位偏移。</summary>
        public void ResetZero()
        {
            _zeroRoll = _zeroPitch = _zeroYaw = 0f;
        }

        private void CalibrateZero(ImuFrame frame)
        {
            _zeroRoll = frame.roll;
            _zeroPitch = frame.pitch;
            _zeroYaw = frame.yaw;
            Debug.Log($"[IMUReceiver] 零位已校准 roll={_zeroRoll:0.0} pitch={_zeroPitch:0.0} yaw={_zeroYaw:0.0}");
        }

        private float Apply(float value, bool invert)
        {
            if (invert) value = -value;
            if (Mathf.Abs(value) < deadZoneDegrees) return 0f;
            return value;
        }

        private void ListenLoop()
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);

            while (_running && _client != null)
            {
                try
                {
                    byte[] data = _client.Receive(ref remote);
                    if (data != null && data.Length > 0)
                    {
                        _queue.Enqueue(Encoding.UTF8.GetString(data));
                    }
                }
                catch (SocketException)
                {
                    if (!_running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception e)
                {
                    LastError = e.Message;
                    if (!_running) break;
                }
            }
        }

        private void OnDestroy()
        {
            StopListening();
            if (Instance == this) Instance = null;
        }
    }
}
