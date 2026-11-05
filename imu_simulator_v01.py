# -*- coding: utf-8 -*-
"""
imu_simulator_v01.py
====================
九轴 IMU 模拟器（游戏开发输入生成器） —— 给本科生联调 Unity 用。

设计原则（见本科生任务安排文档）：
  - 模拟器 = 游戏开发输入生成器，不是科研级物理仿真。
  - 不模拟 gyro bias / hard-iron / soft-iron / 噪声 / 温漂 / 磁扰 / 饱和。
  - Unity 永远不知道数据是 SIM 还是 REAL，统一 JSON over UDP 格式。
  - Python 是科研数据【主记录器】；Unity 只把 game_event 回传，由 Python 落盘。

架构：
  SIM_AUTO / SIM_KEYBOARD  ->  Unified IMU JSON  ->  UDP:5005 -> Unity
                                                      |
                                                      +-> CSV (Python 主记录器)
  Unity -> UDP:5006 -> Python 接收 game_event / task_id -> 写入同一 CSV

切换 REAL 时，只需把 produce_packet() 换成 WT901 解析结果，其余代码共用。

运行：
  python imu_simulator_v01.py            # 默认 AUTO 模式
  python imu_simulator_v01.py KEYBOARD   # 键盘手动模式（Windows 下 A/D/W/S/Q/E/空格）
"""

import socket
import json
import time
import math
import sys
import threading

# ---------------------- 配置 ----------------------
UDP_IP = "127.0.0.1"
UDP_PORT_SEND = 5005      # Python -> Unity
UDP_PORT_RECV = 5006      # Unity -> Python (game_event 回传)
RATE_HZ = 50              # 输出频率
DT = 1.0 / RATE_HZ

MODE = "KEYBOARD" if len(sys.argv) > 1 and sys.argv[1].upper() == "KEYBOARD" else "AUTO"
SOURCE_TAG = "SIM_KEYBOARD" if MODE == "KEYBOARD" else "SIM_AUTO"

CSV_PATH = "imu_record_%s.csv" % time.strftime("%Y%m%d_%H%M%S")

# ---------------------- 线程安全状态（供回传写入） ----------------------
lock = threading.Lock()
current_event = "NONE"     # 来自 Unity 的瞬时事件快照
current_task_id = 0        # 来自 Unity 的当前任务
current_trial = 1

# ---------------------- UDP 发送 / 接收 ----------------------
sock_send = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock_recv = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock_recv.bind(("0.0.0.0", UDP_PORT_RECV))
sock_recv.settimeout(0.05)


def recv_loop():
    """接收 Unity 回传的 game_event / task_id，更新全局状态。"""
    global current_event, current_task_id, current_trial
    while True:
        try:
            data, _ = sock_recv.recvfrom(4096)
            msg = json.loads(data.decode("utf-8"))
            with lock:
                if "event" in msg:
                    current_event = msg["event"]
                if "task_id" in msg:
                    current_task_id = msg["task_id"]
                if "trial_id" in msg:
                    current_trial = msg["trial_id"]
        except socket.timeout:
            continue
        except Exception:
            continue


# ---------------------- 键盘输入（Windows 优先，跨平台回退） ----------------------
key_state = {"roll": 0.0, "pitch": 0.0, "yaw": 0.0, "shake": False}


def read_keyboard_windows():
    import msvcrt
    if msvcrt.kbhit():
        c = msvcrt.getwch().lower()
        return c
    return None


def read_keyboard_posix():
    import sys as _sys, select
    if select.select([_sys.stdin], [], [], 0)[0]:
        return _sys.stdin.read(1).lower()
    return None


def poll_keyboard():
    """非阻塞读取按键，更新 key_state。支持 A/D/W/S/Q/E/空格。"""
    c = None
    try:
        c = read_keyboard_windows()
    except Exception:
        try:
            c = read_keyboard_posix()
        except Exception:
            c = None
    if c is None:
        return
    if c == "a":
        key_state["roll"] = -20.0
    elif c == "d":
        key_state["roll"] = 20.0
    elif c == "w":
        key_state["pitch"] = 15.0
    elif c == "s":
        key_state["pitch"] = -15.0
    elif c == "q":
        key_state["yaw"] -= 3.0
    elif c == "e":
        key_state["yaw"] += 3.0
    elif c == " ":
        key_state["shake"] = True
    elif c == "r":
        key_state["roll"] = key_state["pitch"] = 0.0
        key_state["yaw"] = 0.0


# ---------------------- 数据包生成 ----------------------
frame_id = 0
start_time = time.time()
trial_start = start_time


def produce_packet_auto(t):
    """AUTO 模式：自动循环 TILT -> ROTATE -> SHAKE -> STATIC，便于回归调试。"""
    global frame_id
    cycle = 30.0  # 每阶段约 7.5s，共 30s 一轮
    phase_t = t % cycle
    if phase_t < 7.5:
        motion = "TILT"
        roll = 30.0 * math.sin(2 * math.pi * 0.2 * t)
        pitch = 20.0 * math.sin(2 * math.pi * 0.15 * t)
        yaw = 0.0
        ax = 0.1 * math.sin(2 * math.pi * t)
        ay = 0.1 * math.cos(2 * math.pi * t)
        az = 1.0
        gx = gy = gz = 0.0
    elif phase_t < 15.0:
        motion = "ROTATE"
        roll = pitch = 0.0
        yaw = (t * 20.0) % 360.0
        ax = ay = 0.0
        az = 1.0
        gx = gy = 0.0
        gz = 20.0
    elif phase_t < 22.5:
        motion = "SHAKE"
        roll = pitch = 0.0
        yaw = 0.0
        ax = 0.6 * math.sin(2 * math.pi * 5.0 * t)
        ay = 0.6 * math.cos(2 * math.pi * 5.0 * t)
        az = 1.0
        gx = 200.0 * math.sin(2 * math.pi * 5.0 * t)
        gy = 150.0 * math.cos(2 * math.pi * 5.0 * t)
        gz = 0.0
    else:
        motion = "STATIC"
        roll = pitch = yaw = 0.0
        ax = ay = 0.0
        az = 1.0
        gx = gy = gz = 0.0
    return build_packet(roll, pitch, yaw, ax, ay, az, gx, gy, gz, motion)


def produce_packet_keyboard():
    """KEYBOARD 模式：A/D roll, W/S pitch, Q/E yaw, 空格 shake。"""
    global frame_id
    poll_keyboard()
    roll = key_state["roll"]
    pitch = key_state["pitch"]
    yaw = key_state["yaw"] % 360.0
    if key_state["shake"]:
        motion = "SHAKE"
        ax = 0.6 * math.sin(2 * math.pi * 5.0 * time.time())
        gx = 200.0 * math.sin(2 * math.pi * 5.0 * time.time())
        gy = 150.0 * math.cos(2 * math.pi * 5.0 * time.time())
        key_state["shake"] = False
    else:
        motion = "TILT" if (abs(roll) > 1 or abs(pitch) > 1) else ("ROTATE" if abs(yaw) > 1 else "STATIC")
        ax = ay = 0.0
        az = 1.0
        gx = gy = gz = 0.0
    return build_packet(roll, pitch, yaw, ax, ay, az, gx, gy, gz, motion)


def build_packet(roll, pitch, yaw, ax, ay, az, gx, gy, gz, motion):
    global frame_id
    frame_id += 1
    device_ts = int((time.time() - start_time) * 1000)  # 模拟设备时间戳
    host_ts = int(time.time() * 1000)
    # 磁场：模拟恒定本底（真实模式由 WT901 提供）
    mx, my, mz = 25.0, 5.0, 40.0
    with lock:
        ev = current_event
        tk = current_task_id
        tr = current_trial
    return {
        "frame_id": frame_id,
        "device_timestamp_ms": device_ts,
        "host_timestamp_ms": host_ts,
        "ax": round(ax, 4), "ay": round(ay, 4), "az": round(az, 4),
        "gx": round(gx, 3), "gy": round(gy, 3), "gz": round(gz, 3),
        "mx": mx, "my": my, "mz": mz,
        "roll": round(roll, 3), "pitch": round(pitch, 3), "yaw": round(yaw, 3),
        "motion_state": motion,
        "phase": motion,
        "event": ev,
        "task_id": tk,
        "trial_id": tr,
        "source": SOURCE_TAG,
    }


# ---------------------- 主循环 ----------------------
def main():
    # 启动回传接收线程
    t_recv = threading.Thread(target=recv_loop, daemon=True)
    t_recv.start()

    # 写 CSV（Python 主记录器）
    csv_fields = ["frame_id", "device_timestamp_ms", "host_timestamp_ms",
                  "ax", "ay", "az", "gx", "gy", "gz", "mx", "my", "mz",
                  "roll", "pitch", "yaw", "motion_state", "phase",
                  "event", "task_id", "trial_id", "source"]
    f = open(CSV_PATH, "w", encoding="utf-8", newline="")
    f.write(",".join(csv_fields) + "\n")

    print("=" * 60)
    print("IMU Simulator v0.1  |  MODE=%s  SOURCE=%s" % (MODE, SOURCE_TAG))
    print("发送 -> Unity  UDP %s:%d  (50 Hz JSON)" % (UDP_IP, UDP_PORT_SEND))
    print("接收 <- Unity  UDP :%d  (game_event 回传)" % UDP_PORT_RECV)
    print("主记录 CSV -> %s" % CSV_PATH)
    if MODE == "KEYBOARD":
        print("键盘: A/D=roll  W/S=pitch  Q/E=yaw  空格=shake  R=复位")
    print("=" * 60)

    try:
        while True:
            t = time.time() - start_time
            pkt = produce_packet_keyboard() if MODE == "KEYBOARD" else produce_packet_auto(t)
            sock_send.sendto(json.dumps(pkt).encode("utf-8"), (UDP_IP, UDP_PORT_SEND))
            # 落盘
            f.write(",".join(str(pkt[k]) for k in csv_fields) + "\n")
            f.flush()
            time.sleep(DT)
    except KeyboardInterrupt:
        print("\n[停止] 已保存 %d 帧到 %s" % (frame_id, CSV_PATH))
        f.close()


if __name__ == "__main__":
    main()
