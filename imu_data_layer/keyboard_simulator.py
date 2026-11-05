import math
import time
import threading

from pynput import keyboard

from data_model import IMUData


# ============================================
# Device information
# ============================================

DEVICE_ID = "WT5500011658"


class KeyboardIMUSimulator:

    def __init__(self):

        self.start_time = time.time()

        self.last_time = time.perf_counter()

        self.frame_id = 0

        # ========================================
        # Orientation
        # ========================================

        self.roll = 0.0
        self.pitch = 0.0
        self.yaw = 0.0

        # degree / second

        self.roll_speed = 45.0
        self.pitch_speed = 45.0
        self.yaw_speed = 60.0

        self.roll_limit = 60.0
        self.pitch_limit = 60.0

        # ========================================
        # Keyboard state
        # ========================================

        self.keys_pressed = set()

        self.running = True

        self.lock = threading.Lock()

        # ========================================
        # Shake
        # ========================================

        self.shake_active = False

        self.shake_start_time = 0.0

        self.shake_duration = 0.50

        # ========================================
        # Simulated world magnetic field
        #
        # Only for interface/game development.
        # NOT for scientific magnetic simulation.
        # ========================================

        self.mag_world_x = 25.0
        self.mag_world_y = 5.0
        self.mag_world_z = 40.0

        # ========================================
        # Keyboard listener
        # ========================================

        self.listener = keyboard.Listener(
            on_press=self._on_press,
            on_release=self._on_release
        )

        self.listener.start()

    # ============================================
    # Keyboard callbacks
    # ============================================

    def _on_press(self, key):

        with self.lock:

            try:

                char = key.char.lower()

                self.keys_pressed.add(char)

                if char == "r":

                    self.roll = 0.0
                    self.pitch = 0.0
                    self.yaw = 0.0

                    print(
                        "\n[Keyboard] Orientation reset."
                    )

            except AttributeError:

                if key == keyboard.Key.space:

                    self.shake_active = True

                    self.shake_start_time = (
                        time.perf_counter()
                    )

                elif key == keyboard.Key.esc:

                    self.running = False

    def _on_release(self, key):

        with self.lock:

            try:

                char = key.char.lower()

                if char in self.keys_pressed:

                    self.keys_pressed.remove(char)

            except AttributeError:

                pass

    # ============================================
    # Utility
    # ============================================

    @staticmethod
    def _clamp(
        value,
        min_value,
        max_value
    ):

        return max(
            min_value,
            min(max_value, value)
        )

    @staticmethod
    def _wrap_yaw(yaw):

        while yaw >= 180.0:

            yaw -= 360.0

        while yaw < -180.0:

            yaw += 360.0

        return yaw

    # ============================================
    # Gravity model
    # ============================================

    def _calculate_gravity(self):

        roll_rad = math.radians(
            self.roll
        )

        pitch_rad = math.radians(
            self.pitch
        )

        ax = -math.sin(
            pitch_rad
        )

        ay = (
            math.sin(roll_rad)
            * math.cos(pitch_rad)
        )

        az = (
            math.cos(roll_rad)
            * math.cos(pitch_rad)
        )

        return (
            ax,
            ay,
            az
        )

    # ============================================
    # Magnetic-field model
    # ============================================

    def _calculate_magnetic_field(self):

        roll = math.radians(
            self.roll
        )

        pitch = math.radians(
            self.pitch
        )

        yaw = math.radians(
            self.yaw
        )

        cr = math.cos(roll)
        sr = math.sin(roll)

        cp = math.cos(pitch)
        sp = math.sin(pitch)

        cy = math.cos(yaw)
        sy = math.sin(yaw)

        # R = Rz * Ry * Rx

        r11 = cy * cp

        r12 = (
            cy * sp * sr
            - sy * cr
        )

        r13 = (
            cy * sp * cr
            + sy * sr
        )

        r21 = sy * cp

        r22 = (
            sy * sp * sr
            + cy * cr
        )

        r23 = (
            sy * sp * cr
            - cy * sr
        )

        r31 = -sp

        r32 = cp * sr

        r33 = cp * cr

        wx = self.mag_world_x
        wy = self.mag_world_y
        wz = self.mag_world_z

        # body = R^T * world

        mx = (
            r11 * wx
            + r21 * wy
            + r31 * wz
        )

        my = (
            r12 * wx
            + r22 * wy
            + r32 * wz
        )

        mz = (
            r13 * wx
            + r23 * wy
            + r33 * wz
        )

        return (
            mx,
            my,
            mz
        )

    # ============================================
    # Main data generation
    # ============================================

    def get_data(self):

        current_time = (
            time.perf_counter()
        )

        dt = (
            current_time
            - self.last_time
        )

        self.last_time = (
            current_time
        )

        if dt > 0.1:

            dt = 0.1

        self.frame_id += 1

        gx = 0.0
        gy = 0.0
        gz = 0.0

        # ========================================
        # Keyboard → orientation
        # ========================================

        with self.lock:

            # A / D → Roll

            if "a" in self.keys_pressed:

                self.roll -= (
                    self.roll_speed
                    * dt
                )

                gx = -self.roll_speed

            if "d" in self.keys_pressed:

                self.roll += (
                    self.roll_speed
                    * dt
                )

                gx = self.roll_speed

            # W / S → Pitch

            if "w" in self.keys_pressed:

                self.pitch += (
                    self.pitch_speed
                    * dt
                )

                gy = self.pitch_speed

            if "s" in self.keys_pressed:

                self.pitch -= (
                    self.pitch_speed
                    * dt
                )

                gy = -self.pitch_speed

            # Q / E → Yaw

            if "q" in self.keys_pressed:

                self.yaw -= (
                    self.yaw_speed
                    * dt
                )

                gz = -self.yaw_speed

            if "e" in self.keys_pressed:

                self.yaw += (
                    self.yaw_speed
                    * dt
                )

                gz = self.yaw_speed

        # ========================================
        # Angle limits
        # ========================================

        self.roll = self._clamp(
            self.roll,
            -self.roll_limit,
            self.roll_limit
        )

        self.pitch = self._clamp(
            self.pitch,
            -self.pitch_limit,
            self.pitch_limit
        )

        self.yaw = self._wrap_yaw(
            self.yaw
        )

        # ========================================
        # Accelerometer
        # ========================================

        ax, ay, az = (
            self._calculate_gravity()
        )

        # ========================================
        # Shake simulation
        # ========================================

        if self.shake_active:

            shake_t = (
                current_time
                - self.shake_start_time
            )

            if (
                shake_t
                <= self.shake_duration
            ):

                frequency = 8.0

                signal = math.sin(
                    2.0
                    * math.pi
                    * frequency
                    * shake_t
                )

                ax += (
                    0.8
                    * signal
                )

                ay += (
                    0.6
                    * math.sin(
                        2.0
                        * math.pi
                        * 7.0
                        * shake_t
                    )
                )

                az += (
                    0.5
                    * math.sin(
                        2.0
                        * math.pi
                        * 9.0
                        * shake_t
                    )
                )

                gx += (
                    150.0
                    * signal
                )

                gy += (
                    120.0
                    * signal
                )

                gz += (
                    100.0
                    * signal
                )

            else:

                self.shake_active = False

        # ========================================
        # Magnetometer
        # ========================================

        mx, my, mz = (
            self._calculate_magnetic_field()
        )

        # ========================================
        # Motion-state label
        # ========================================

        motion_state = (
            "STATIC"
        )

        if self.shake_active:

            motion_state = (
                "DYNAMIC"
            )

        elif abs(gz) > 1.0:

            motion_state = (
                "ROTATE"
            )

        elif (
            abs(gx) > 1.0
            or abs(gy) > 1.0
        ):

            motion_state = (
                "TILT"
            )

        # ========================================
        # Host timestamp
        # ========================================

        timestamp_ms = int(
            time.time()
            * 1000
        )

        # ========================================
        # Unified IMU frame
        # ========================================

        data = IMUData(

            device_id=DEVICE_ID,

            frame_id=self.frame_id,

            host_timestamp_ms=
            timestamp_ms,

            ax=ax,
            ay=ay,
            az=az,

            gx=gx,
            gy=gy,
            gz=gz,

            mx=mx,
            my=my,
            mz=mz,

            roll=self.roll,
            pitch=self.pitch,
            yaw=self.yaw,

            motion_state=
            motion_state,

            game_event="NONE",

            task_id=0,

            trial_id=0,

            zone="NONE",

            source="SIM_KEYBOARD"
        )

        return data

    # ============================================
    # Close
    # ============================================

    def close(self):

        self.running = False

        if self.listener:

            self.listener.stop()