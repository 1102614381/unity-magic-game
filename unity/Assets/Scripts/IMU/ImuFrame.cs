using System;

namespace MagicIsland.IMU
{
    /// <summary>
    /// 一帧 IMU 数据，字段与需求说明书 7.2 / 7.3 完全对应。
    /// 注意：磁场是 mx_raw / my_raw / mz_raw 原始计数，未标定前不得当作 µT。
    /// </summary>
    [Serializable]
    public struct ImuFrame
    {
        // ---- Python → Unity 契约字段 ----
        public string sessionId;
        public string deviceId;
        public long frameId;
        public long hostTimestampMs;      // Python 接收时间（Unix 毫秒）
        public float relativeTimeS;       // 采集开始后的相对秒
        public string deviceTime;         // 设备内部时间（若有）
        public float ax, ay, az;          // 加速度，g
        public float gx, gy, gz;          // 角速度，°/s
        public float mxRaw, myRaw, mzRaw; // 磁力计原始计数
        public float roll, pitch, yaw;    // 设备输出姿态角，°
        public string source;             // WT901 / SIM_KEYBOARD
        public string participantId;
        public string trialId;
        public int taskId;
        public string taskPhase;

        // ---- Unity 侧补充字段 ----
        public long unityRecvTimestampMs; // Unity 收到该帧的 Unix 毫秒
        public float unityElapsedMs;      // Unity 会话内相对接收时间

        public bool IsValid => !string.IsNullOrEmpty(deviceId) || frameId > 0;
    }
}
