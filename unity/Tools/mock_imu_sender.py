# -*- coding: utf-8 -*-
"""
开发自测用的 IMU 模拟发送器（正式数据仍由研究生的 imu_data_layer 提供）。

按需求说明书 7.3 契约发送 JSON 帧到 Unity 的 UDP 端口（默认 5005）。
没有真机、Python 数据层也还没跑起来时，用它对通 Unity 侧链路。

用法：
    python mock_imu_sender.py                     # 键盘模式
    python mock_imu_sender.py --demo              # 自动摆动，无需操作
    python mock_imu_sender.py --port 5005 --rate 50

键盘：
    A / D    roll 左右          W / S    pitch 前后
    Q / E    yaw 左右           空格     回中
    ESC      退出
"""

import argparse
import json
import math
import socket
import sys
import time

try:
    import msvcrt
except ImportError:
    msvcrt = None


class State:
    def __init__(self):
        self.roll = 0.0
        self.pitch = 0.0
        self.yaw = 0.0


def read_keys(state, step):
    """Windows 下非阻塞读键，返回 False 表示要退出。"""
    if msvcrt is None:
        return True

    while msvcrt.kbhit():
        ch = msvcrt.getch()
        try:
            key = ch.decode("utf-8", errors="ignore").lower()
        except Exception:
            key = ""

        if key == "a":
            state.roll -= step
        elif key == "d":
            state.roll += step
        elif key == "w":
            state.pitch -= step
        elif key == "s":
            state.pitch += step
        elif key == "q":
            state.yaw -= step
        elif key == "e":
            state.yaw += step
        elif key == " ":
            state.roll = state.pitch = state.yaw = 0.0
        elif ch in (b"\x1b",):
            return False

    state.roll = max(-45.0, min(45.0, state.roll))
    state.pitch = max(-45.0, min(45.0, state.pitch))
    state.yaw = max(-180.0, min(180.0, state.yaw))
    return True


def demo_state(state, t):
    state.roll = 25.0 * math.sin(t * 1.2)
    state.pitch = 15.0 * math.sin(t * 0.8)
    state.yaw = 40.0 * math.sin(t * 0.4)


def main():
    parser = argparse.ArgumentParser(description="IMU 模拟发送器（开发自测）")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5005)
    parser.add_argument("--rate", type=float, default=50.0, help="发送频率 Hz")
    parser.add_argument("--device", default="WT5500011658")
    parser.add_argument("--participant", default="P001")
    parser.add_argument("--trial", default="T001")
    parser.add_argument("--task", type=int, default=1)
    parser.add_argument("--phase", default="PLACEHOLDER")
    parser.add_argument("--demo", action="store_true", help="自动摆动，无需键盘")
    args = parser.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    state = State()
    key_step = 3.0

    print("=" * 56)
    print(f"目标 {args.host}:{args.port}   频率 {args.rate} Hz   设备 {args.device}")
    print("A/D 左右  W/S 前后  Q/E 转向  空格 回中  ESC 退出")
    if msvcrt is None:
        print("当前系统不支持键盘读取，请加 --demo 使用自动摆动模式")
    print("=" * 56)

    start = time.time()
    frame_id = 0

    try:
        while True:
            t = time.time() - start

            if args.demo:
                demo_state(state, t)
            elif not read_keys(state, key_step):
                break

            frame = {
                "session_id": f"{args.participant}_DEFAULT",
                "device_id": args.device,
                "frame_id": frame_id,
                "host_timestamp_ms": int(time.time() * 1000),
                "relative_time_s": round(t, 3),
                "device_time": time.strftime("%H:%M:%S"),
                "ax": round(0.02 * math.sin(math.radians(state.pitch)), 4),
                "ay": round(0.02 * math.sin(math.radians(state.roll)), 4),
                "az": round(1.0 * math.cos(math.radians(state.pitch)), 4),
                "gx": 0.0, "gy": 0.0, "gz": 0.0,
                "mx_raw": 120.0, "my_raw": -340.0, "mz_raw": 410.0,
                "roll": round(state.roll, 2),
                "pitch": round(state.pitch, 2),
                "yaw": round(state.yaw, 2),
                "source": "SIM_KEYBOARD",
                "participant_id": args.participant,
                "trial_id": args.trial,
                "task_id": args.task,
                "task_phase": args.phase,
            }

            sock.sendto(json.dumps(frame).encode("utf-8"), (args.host, args.port))
            frame_id += 1

            if frame_id % int(max(args.rate, 1)) == 0:
                sys.stdout.write(
                    f"\r发送 {frame_id} 帧   roll {state.roll:6.1f}  pitch {state.pitch:6.1f}  yaw {state.yaw:6.1f}")
                sys.stdout.flush()

            time.sleep(1.0 / max(args.rate, 1.0))
    except KeyboardInterrupt:
        pass
    finally:
        sock.close()
        print("\n已停止发送。")


if __name__ == "__main__":
    main()
