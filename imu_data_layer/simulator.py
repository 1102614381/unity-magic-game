import math
import time

from data_model import IMUData


class IMUSimulator:

    def __init__(self):

        self.start_time = time.time()
        self.frame_id = 0

    def get_data(self):

        # --------------------------------
        # 当前运行时间
        # --------------------------------
        current_time = time.time()

        t = current_time - self.start_time

        self.frame_id += 1

        # --------------------------------
        # 模拟姿态角
        # --------------------------------

        # Roll:
        # ±30°
        # 0.2 Hz
        roll = 30.0 * math.sin(
            2.0 * math.pi * 0.2 * t
        )

        # Pitch:
        # ±20°
        # 0.15 Hz
        pitch = 20.0 * math.sin(
            2.0 * math.pi * 0.15 * t
        )

        # Yaw:
        # 持续旋转
        # 20 degree/s
        yaw = (20.0 * t) % 360.0

        # --------------------------------
        # 模拟 Accelerometer
        # --------------------------------

        ax = 0.10 * math.sin(
            2.0 * math.pi * 0.5 * t
        )

        ay = 0.10 * math.cos(
            2.0 * math.pi * 0.5 * t
        )

        # 静态情况下接近 1 g
        az = 1.0

        # --------------------------------
        # 模拟 Gyroscope
        # --------------------------------

        # roll 的近似角速度
        gx = (
            30.0
            * 2.0
            * math.pi
            * 0.2
            * math.cos(
                2.0 * math.pi * 0.2 * t
            )
        )

        # pitch 的近似角速度
        gy = (
            20.0
            * 2.0
            * math.pi
            * 0.15
            * math.cos(
                2.0 * math.pi * 0.15 * t
            )
        )

        # yaw 持续旋转
        gz = 20.0

        # --------------------------------
        # 模拟 Magnetometer
        # --------------------------------

        # 暂时使用较稳定的模拟磁场
        mx = 25.0
        my = 5.0
        mz = 40.0

        # --------------------------------
        # 时间戳
        # --------------------------------

        host_timestamp_ms = int(
            current_time * 1000
        )

        # --------------------------------
        # 构建统一 IMU 数据对象
        # --------------------------------

        data = IMUData(

            frame_id=self.frame_id,

            host_timestamp_ms=
            host_timestamp_ms,

            ax=ax,
            ay=ay,
            az=az,

            gx=gx,
            gy=gy,
            gz=gz,

            mx=mx,
            my=my,
            mz=mz,

            roll=roll,
            pitch=pitch,
            yaw=yaw,

            motion_state="UNKNOWN",
            game_event="NONE",

            task_id=0,
            trial_id=0,

            zone="NONE",

            source="SIM"
        )

        return data