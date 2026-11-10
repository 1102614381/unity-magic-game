using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.IMU
{
    /// <summary>
    /// IMU 测试场景演示：用 roll / pitch / yaw 驱动一个 Cube，验证「Python UDP → Unity」链路。
    /// 挂在一个空物体上，把 Cube 拖到 target 即可；没有 Python 时可用方向键本地演示。
    /// 快捷键：Z 校准零位，R 复位，方向键模拟倾斜（仅未收到 UDP 时生效）。
    /// </summary>
    public class ImuCubeDemo : MonoBehaviour
    {
        [Header("驱动目标")]
        [SerializeField] private Transform target;

        [Header("姿态映射")]
        [Tooltip("角度增益，1 = 与实际姿态角一致")]
        [SerializeField] private float gain = 1f;
        [Tooltip("旋转平滑系数，越大越跟手")]
        [SerializeField] private float smoothing = 12f;

        [Header("无数据时的键盘演示")]
        [SerializeField] private bool keyboardFallback = true;
        [SerializeField] private float keyboardSpeed = 70f;

        [Header("状态显示（可留空）")]
        [SerializeField] private Text statusLabel;

        private Vector3 _keyboardAngles;
        private float _refreshTimer;
        private int _lastFrameCount;
        private float _fpsTimer;
        private float _fps;

        private void Start()
        {
            if (target == null)
            {
                var go = GameObject.Find("Cube") ?? GameObject.CreatePrimitive(PrimitiveType.Cube);
                target = go.transform;
            }
        }

        private void Update()
        {
            var receiver = IMUReceiver.Instance;
            bool hasLiveData = receiver != null && receiver.IsConnected;

            if (hasLiveData)
            {
                var q = Quaternion.Euler(-receiver.Pitch * gain, receiver.Yaw * gain, -receiver.Roll * gain);
                target.localRotation = Quaternion.Slerp(target.localRotation, q, Time.deltaTime * smoothing);
            }
            else if (keyboardFallback)
            {
                float dt = Time.deltaTime;
                if (Input.GetKey(KeyCode.LeftArrow)) _keyboardAngles.z += keyboardSpeed * dt;
                if (Input.GetKey(KeyCode.RightArrow)) _keyboardAngles.z -= keyboardSpeed * dt;
                if (Input.GetKey(KeyCode.UpArrow)) _keyboardAngles.x -= keyboardSpeed * dt;
                if (Input.GetKey(KeyCode.DownArrow)) _keyboardAngles.x += keyboardSpeed * dt;

                var q = Quaternion.Euler(_keyboardAngles);
                target.localRotation = Quaternion.Slerp(target.localRotation, q, Time.deltaTime * smoothing);
            }

            if (receiver != null)
            {
                if (Input.GetKeyDown(KeyCode.Z)) receiver.CalibrateZero();
                if (Input.GetKeyDown(KeyCode.R))
                {
                    receiver.ResetZero();
                    _keyboardAngles = Vector3.zero;
                    target.localRotation = Quaternion.identity;
                }
            }

            UpdateStatus(receiver);
        }

        private void UpdateStatus(IMUReceiver receiver)
        {
            if (statusLabel == null) return;

            _refreshTimer += Time.deltaTime;
            if (_refreshTimer < 0.2f) return;
            _refreshTimer = 0f;

            if (receiver == null)
            {
                statusLabel.text = "IMUReceiver 未初始化";
                return;
            }

            if (receiver.FrameCount != _lastFrameCount)
            {
                _fps = (receiver.FrameCount - _lastFrameCount) / Mathf.Max(0.0001f, Time.time - _fpsTimer);
                _lastFrameCount = receiver.FrameCount;
                _fpsTimer = Time.time;
            }

            string state = receiver.IsConnected
                ? "已连接"
                : (receiver.HasFrame ? "信号中断" : "等待数据");

            statusLabel.text =
                $"UDP :{receiver.Port}   状态：{state}\n" +
                $"roll {receiver.RawFrame.roll,7:0.0}   pitch {receiver.RawFrame.pitch,7:0.0}   yaw {receiver.RawFrame.yaw,7:0.0}\n" +
                $"frame_id {receiver.RawFrame.frameId}   帧数 {receiver.FrameCount}   丢帧解析错误 {receiver.ParseErrorCount}\n" +
                $"source {receiver.RawFrame.source}   {_fps:0} 帧/秒\n" +
                "Z 校准零位    R 复位    方向键可本地演示";
        }
    }
}
