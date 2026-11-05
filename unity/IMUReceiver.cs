// IMUReceiver.cs
// ============================================================================
// Unity 接收 Python 发来的统一 IMU 数据，并把 game_event 回传 Python。
// 挂载到一个空的 GameObject 上即可（例如命名为 "IMUHub"）。
// ============================================================================

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class IMUReceiver : MonoBehaviour
{
    [Header("UDP Settings")]
    public int listenPort = 5005;

    [Header("Rotation Mapping")]
    [Tooltip("Roll -> Unity X")]
    public float rollScale = 1.0f;

    [Tooltip("Pitch -> Unity Z")]
    public float pitchScale = 1.0f;

    [Tooltip("Yaw -> Unity Y")]
    public float yawScale = 1.0f;

    [Header("Axis Sign")]
    public bool invertRoll = false;
    public bool invertPitch = false;
    public bool invertYaw = false;

    [Header("Smoothing")]
    [Range(0.0f, 30.0f)]
    public float smoothSpeed = 12.0f;

    [Header("Debug")]
    public bool showDebugLog = false;

    // =========================================
    // UDP
    // =========================================

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool running = false;

    // =========================================
    // Latest received IMU values
    // =========================================

    private readonly object dataLock = new object();

    private float latestRoll = 0.0f;
    private float latestPitch = 0.0f;
    private float latestYaw = 0.0f;

    private string latestSource = "";
    private string latestDeviceId = "";

    private bool hasReceivedData = false;

    // =========================================
    // Runtime rotation
    // =========================================

    private Quaternion targetRotation;

    // =========================================
    // JSON structure
    //
    // Must match Python UDP JSON keys.
    // Extra JSON fields are safely ignored.
    // =========================================

    [Serializable]
    public class IMUData
    {
        public string device_id;

        public int frame_id;

        public long host_timestamp_ms;

        public float ax;
        public float ay;
        public float az;

        public float gx;
        public float gy;
        public float gz;

        public float mx;
        public float my;
        public float mz;

        public float roll;
        public float pitch;
        public float yaw;

        public string motion_state;
        public string game_event;

        public int task_id;
        public int trial_id;

        public string zone;

        public string source;
    }

    // =========================================
    // Start
    // =========================================

    void Start()
    {
        targetRotation = transform.localRotation;

        StartUDP();

        Debug.Log(
            $"[IMUReceiver] Listening on UDP port {listenPort}"
        );
    }

    // =========================================
    // UDP Start
    // =========================================

    void StartUDP()
    {
        try
        {
            udpClient = new UdpClient(listenPort);

            running = true;

            receiveThread = new Thread(ReceiveLoop);

            receiveThread.IsBackground = true;

            receiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[IMUReceiver] UDP initialization failed: "
                + e.Message
            );
        }
    }

    // =========================================
    // UDP Receive Thread
    // =========================================

    void ReceiveLoop()
    {
        IPEndPoint remoteEP =
            new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data =
                    udpClient.Receive(ref remoteEP);

                string json =
                    Encoding.UTF8.GetString(data);

                IMUData imu =
                    JsonUtility.FromJson<IMUData>(json);

                if (imu == null)
                    continue;

                lock (dataLock)
                {
                    latestRoll = imu.roll;
                    latestPitch = imu.pitch;
                    latestYaw = imu.yaw;

                    latestSource = imu.source;
                    latestDeviceId = imu.device_id;

                    hasReceivedData = true;
                }

                if (showDebugLog)
                {
                    Debug.Log(
                        $"IMU RX | " +
                        $"Device={imu.device_id} | " +
                        $"Roll={imu.roll:F2} | " +
                        $"Pitch={imu.pitch:F2} | " +
                        $"Yaw={imu.yaw:F2} | " +
                        $"Source={imu.source}"
                    );
                }
            }
            catch (SocketException)
            {
                // Expected when socket closes.
            }
            catch (Exception e)
            {
                if (running)
                {
                    Debug.LogWarning(
                        "[IMUReceiver] Receive error: "
                        + e.Message
                    );
                }
            }
        }
    }

    // =========================================
    // Unity Update
    // =========================================

    void Update()
    {
        if (!hasReceivedData)
            return;

        float roll;
        float pitch;
        float yaw;

        lock (dataLock)
        {
            roll = latestRoll;
            pitch = latestPitch;
            yaw = latestYaw;
        }

        // =====================================
        // Optional axis inversion
        // =====================================

        if (invertRoll)
            roll = -roll;

        if (invertPitch)
            pitch = -pitch;

        if (invertYaw)
            yaw = -yaw;

        // =====================================
        // Requested mapping
        //
        // Roll  -> Unity X
        // Yaw   -> Unity Y
        // Pitch -> Unity Z
        //
        // Quaternion.Euler(x, y, z)
        // =====================================

        targetRotation =
            Quaternion.Euler(
                roll * rollScale,
                yaw * yawScale,
                pitch * pitchScale
            );

        // =====================================
        // Smooth cube rotation
        // =====================================

        if (smoothSpeed <= 0.0f)
        {
            transform.localRotation =
                targetRotation;
        }
        else
        {
            transform.localRotation =
                Quaternion.Slerp(
                    transform.localRotation,
                    targetRotation,
                    1.0f
                    - Mathf.Exp(
                        -smoothSpeed
                        * Time.deltaTime
                    )
                );
        }
    }

    // =========================================
    // Shutdown
    // =========================================

    void OnDestroy()
    {
        StopUDP();
    }

    void OnApplicationQuit()
    {
        StopUDP();
    }

    void StopUDP()
    {
        running = false;

        try
        {
            udpClient?.Close();
        }
        catch
        {
        }

        try
        {
            if (
                receiveThread != null
                && receiveThread.IsAlive
            )
            {
                receiveThread.Join(200);
            }
        }
        catch
        {
        }

        receiveThread = null;
        udpClient = null;
    }
}