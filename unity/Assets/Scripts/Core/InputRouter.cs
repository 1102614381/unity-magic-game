using System;
using MagicIsland.IMU;
using UnityEngine;

namespace MagicIsland.Core
{
    /// <summary>离散方向（对应魔法棒向哪个方向倾斜/转动越过阈值）。</summary>
    public enum StickDirection { Forward, Back, Left, Right }

    /// <summary>
    /// 统一离散输入路由（V1.1 第五章）：把连续姿态角切成离散方向事件。
    /// 状态机 NEUTRAL → TRIGGERED(越阈值立即反馈) → LOCKED → 回中解锁 → 下一次触发。
    /// G3/G4（欢婷）依赖本类；G2 使用 Yaw 连续值，不经过这里。
    /// 无手环时键盘方向键可作演示输入（越阈后自动解锁）。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class InputRouter : MonoBehaviour
    {
        public static InputRouter Instance { get; private set; }

        [Header("阈值（度）")]
        [Tooltip("越过大角度即触发")]
        [SerializeField] private float triggerDegrees = 30f;
        [Tooltip("小于该角度视为回中，解锁")]
        [SerializeField] private float returnDegrees = 12f;
        [Tooltip("键盘演示输入的自动解锁时间（秒）")]
        [SerializeField] private float keyAutoReleaseSeconds = 0.35f;

        /// <summary>越阈值立即触发一次。</summary>
        public event Action<StickDirection> DirectionTriggered;

        /// <summary>回中解锁时触发一次。</summary>
        public event Action<StickDirection> DirectionReleased;

        /// <summary>当前锁定的方向；null 表示 NEUTRAL。</summary>
        public StickDirection? LockedDirection { get; private set; }

        private float _keyReleaseTime;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            PollKeyboard();
            PollImu();
        }

        private void PollImu()
        {
            var imu = IMUReceiver.Instance;
            if (imu == null || !imu.HasFrame) return;

            float roll = imu.Roll;    // 约定：左负右正（与研究者确认）
            float pitch = imu.Pitch;  // 约定：前负后正（与研究者确认）

            if (LockedDirection.HasValue)
            {
                bool centered = Mathf.Abs(roll) < returnDegrees && Mathf.Abs(pitch) < returnDegrees;
                if (centered)
                {
                    var dir = LockedDirection.Value;
                    LockedDirection = null;
                    DirectionReleased?.Invoke(dir);
                }
                return;
            }

            // 取绝对值更大的轴作为动作方向，避免斜推时两轴同时越阈
            StickDirection? candidate = null;
            if (Mathf.Abs(roll) >= triggerDegrees || Mathf.Abs(pitch) >= triggerDegrees)
            {
                candidate = Mathf.Abs(roll) >= Mathf.Abs(pitch)
                    ? (roll > 0f ? StickDirection.Right : StickDirection.Left)
                    : (pitch > 0f ? StickDirection.Back : StickDirection.Forward);
            }

            if (candidate.HasValue)
            {
                LockedDirection = candidate.Value;
                DirectionTriggered?.Invoke(LockedDirection.Value);
            }
        }

        private void PollKeyboard()
        {
            if (LockedDirection.HasValue)
            {
                if (Time.unscaledTime >= _keyReleaseTime)
                {
                    var dir = LockedDirection.Value;
                    LockedDirection = null;
                    DirectionReleased?.Invoke(dir);
                }
                return;
            }

            StickDirection? key = null;
            if (Input.GetKeyDown(KeyCode.UpArrow)) key = StickDirection.Forward;
            else if (Input.GetKeyDown(KeyCode.DownArrow)) key = StickDirection.Back;
            else if (Input.GetKeyDown(KeyCode.LeftArrow)) key = StickDirection.Left;
            else if (Input.GetKeyDown(KeyCode.RightArrow)) key = StickDirection.Right;

            if (key.HasValue)
            {
                LockedDirection = key.Value;
                _keyReleaseTime = Time.unscaledTime + Mathf.Max(0.1f, keyAutoReleaseSeconds);
                DirectionTriggered?.Invoke(key.Value);
            }
        }
    }
}
