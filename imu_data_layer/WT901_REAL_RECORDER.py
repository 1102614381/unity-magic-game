#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
============================================================
MagicIMU Data Layer V0.3
Real WT901WIFI Recorder
============================================================

Device:
    WitMotion WT901WIFI

Device ID:
    WT5500011658

Data flow:

    WT901WIFI
        ↓ UDP :1399
      Python
       /   \
      /     \
 RAW UDP   Decoded IMU
    ↓          ↓
   CSV        CSV

Purpose:
    1. Verify real WT901 communication
    2. Preserve every original UDP packet
    3. Decode Acc / Gyro / Mag / Roll / Pitch / Yaw
    4. Verify actual packet rate
    5. Prepare for later Unity UDP output

Stop:
    Ctrl+C

============================================================
"""

import socket
import csv
import os
import struct
import time
from datetime import datetime


# ============================================================
# 1. USER CONFIGURATION
# ============================================================

DEVICE_ID = "WT5500011658"

# WT901 sends UDP packets to PC port 1399
UDP_IP = "0.0.0.0"
UDP_PORT = 1399

# ------------------------------------------------------------
# Save location
#
# You can change this later.
# ------------------------------------------------------------

SAVE_ROOT = r"D:\yyh\jiuzhou-yyh\3-jiuzhou-yyh-unity\data"

# Safety maximum recording duration.
# Ctrl+C can stop recording earlier.
MAX_RECORD_TIME_S = 300.0

# Print status every N seconds
STATUS_INTERVAL_S = 1.0

# Expected WT901 Wi-Fi packet length
EXPECTED_PACKET_LENGTH = 54


# ============================================================
# 2. WT901 54-BYTE DECODER
# ============================================================

class WT901Wifi54Decoder:
    """
    Decoder based on the real 54-byte WT901WIFI packets
    previously validated in the user's acquisition system.

    Packet structure:

        0..11       ASCII device ID
        12          year offset from 2000
        13          month
        14          day
        15          hour
        16          minute
        17          second
        18..19      millisecond uint16 LE

        20..25      Acc X/Y/Z
        26..31      Gyro X/Y/Z
        32..37      Mag X/Y/Z
        38..43      Roll/Pitch/Yaw

        44..51      additional WT901 fields
        52..53      CR LF
    """

    EXPECTED_LEN = 54

    @staticmethod
    def _i16(data, offset):
        return struct.unpack_from(
            "<h",
            data,
            offset
        )[0]

    @staticmethod
    def _u16(data, offset):
        return struct.unpack_from(
            "<H",
            data,
            offset
        )[0]

    def decode(self, packet):

        # ----------------------------------------------------
        # Basic validation
        # ----------------------------------------------------

        if packet is None:
            return None

        if len(packet) != self.EXPECTED_LEN:
            return None

        if packet[0:2] != b"WT":
            return None

        try:

            # =================================================
            # Device ID
            # =================================================

            device_header = (
                packet[0:12]
                .decode(
                    "ascii",
                    errors="replace"
                )
            )

            # =================================================
            # Device internal time
            # =================================================

            year = 2000 + packet[12]

            month = packet[13]

            day = packet[14]

            hour = packet[15]

            minute = packet[16]

            second = packet[17]

            millisecond = self._u16(
                packet,
                18
            )

            device_time = (
                f"{year:04d}-"
                f"{month:02d}-"
                f"{day:02d} "
                f"{hour:02d}:"
                f"{minute:02d}:"
                f"{second:02d}."
                f"{millisecond:03d}"
            )

            # =================================================
            # Accelerometer RAW
            # =================================================

            ax_raw = self._i16(
                packet,
                20
            )

            ay_raw = self._i16(
                packet,
                22
            )

            az_raw = self._i16(
                packet,
                24
            )

            # WT901 acceleration range:
            # ±16 g

            ax_g = (
                ax_raw
                / 32768.0
                * 16.0
            )

            ay_g = (
                ay_raw
                / 32768.0
                * 16.0
            )

            az_g = (
                az_raw
                / 32768.0
                * 16.0
            )

            # =================================================
            # Gyroscope RAW
            # =================================================

            gx_raw = self._i16(
                packet,
                26
            )

            gy_raw = self._i16(
                packet,
                28
            )

            gz_raw = self._i16(
                packet,
                30
            )

            # WT901 gyro range:
            # ±2000 degree/s

            gx_dps = (
                gx_raw
                / 32768.0
                * 2000.0
            )

            gy_dps = (
                gy_raw
                / 32768.0
                * 2000.0
            )

            gz_dps = (
                gz_raw
                / 32768.0
                * 2000.0
            )

            # =================================================
            # Magnetometer
            #
            # IMPORTANT:
            # Existing validated code only confirms these
            # as raw magnetometer counts.
            #
            # We DO NOT silently convert them to uT here.
            # =================================================

            mx_raw = self._i16(
                packet,
                32
            )

            my_raw = self._i16(
                packet,
                34
            )

            mz_raw = self._i16(
                packet,
                36
            )

            # =================================================
            # Orientation
            # =================================================

            roll_raw = self._i16(
                packet,
                38
            )

            pitch_raw = self._i16(
                packet,
                40
            )

            yaw_raw = self._i16(
                packet,
                42
            )

            roll_deg = (
                roll_raw
                / 32768.0
                * 180.0
            )

            pitch_deg = (
                pitch_raw
                / 32768.0
                * 180.0
            )

            yaw_deg = (
                yaw_raw
                / 32768.0
                * 180.0
            )

            # =================================================
            # Result
            # =================================================

            return {

                "device_id":
                    device_header,

                "device_time":
                    device_time,

                # Acc
                "ax_g": ax_g,
                "ay_g": ay_g,
                "az_g": az_g,

                # Gyro
                "gx_dps": gx_dps,
                "gy_dps": gy_dps,
                "gz_dps": gz_dps,

                # Magnetometer RAW
                "mx_raw": mx_raw,
                "my_raw": my_raw,
                "mz_raw": mz_raw,

                # Orientation
                "roll_deg": roll_deg,
                "pitch_deg": pitch_deg,
                "yaw_deg": yaw_deg
            }

        except (
            ValueError,
            struct.error,
            IndexError
        ):

            return None


# ============================================================
# 3. OUTPUT DIRECTORY
# ============================================================

def create_output_directory():

    os.makedirs(
        SAVE_ROOT,
        exist_ok=True
    )

    timestamp = (
        datetime.now()
        .strftime(
            "%Y-%m-%d_%H-%M-%S"
        )
    )

    folder_name = (
        timestamp
        + "_"
        + DEVICE_ID
        + "_REAL"
    )

    output_dir = os.path.join(
        SAVE_ROOT,
        folder_name
    )

    os.makedirs(
        output_dir,
        exist_ok=True
    )

    return output_dir


# ============================================================
# 4. MAIN
# ============================================================

def main():

    print()
    print("=" * 72)

    print(
        "MagicIMU Data Layer V0.3"
    )

    print(
        "REAL WT901WIFI Recorder"
    )

    print("=" * 72)

    print()

    print(
        "Target device:",
        DEVICE_ID
    )

    print(
        "UDP listen:",
        f"{UDP_IP}:{UDP_PORT}"
    )

    print()

    print(
        "Waiting for REAL WT901 data..."
    )

    print(
        "Press Ctrl+C to stop."
    )

    print()

    # ========================================================
    # Output directory
    # ========================================================

    output_dir = (
        create_output_directory()
    )

    raw_path = os.path.join(
        output_dir,
        "WT901_raw_udp.csv"
    )

    decoded_path = os.path.join(
        output_dir,
        "WT901_decoded_imu.csv"
    )

    summary_path = os.path.join(
        output_dir,
        "recording_summary.txt"
    )

    print(
        "Output directory:"
    )

    print(
        output_dir
    )

    print()

    # ========================================================
    # Create decoder
    # ========================================================

    decoder = (
        WT901Wifi54Decoder()
    )

    # ========================================================
    # UDP socket
    # ========================================================

    sock = socket.socket(
        socket.AF_INET,
        socket.SOCK_DGRAM
    )

    # Increase receive buffer
    try:

        sock.setsockopt(
            socket.SOL_SOCKET,
            socket.SO_RCVBUF,
            4 * 1024 * 1024
        )

    except OSError:

        pass

    try:

        sock.bind(
            (
                UDP_IP,
                UDP_PORT
            )
        )

    except OSError as exc:

        print()
        print(
            "ERROR:"
        )

        print(
            "Cannot bind UDP port",
            UDP_PORT
        )

        print(
            repr(exc)
        )

        print()

        print(
            "Check whether another WT901 "
            "program is already using port 1399."
        )

        return

    sock.settimeout(
        0.2
    )

    # ========================================================
    # CSV files
    # ========================================================

    raw_file = open(
        raw_path,
        "w",
        newline="",
        encoding="utf-8-sig"
    )

    decoded_file = open(
        decoded_path,
        "w",
        newline="",
        encoding="utf-8-sig"
    )

    raw_writer = csv.writer(
        raw_file
    )

    decoded_writer = csv.writer(
        decoded_file
    )

    # ========================================================
    # RAW CSV header
    # ========================================================

    raw_writer.writerow([

        "packet_index",

        "pc_timestamp",

        "relative_time_s",

        "source_ip",

        "source_port",

        "packet_length",

        "device_id",

        "accepted_device",

        "packet_hex"
    ])

    # ========================================================
    # Decoded CSV header
    # ========================================================

    decoded_writer.writerow([

        "frame_id",

        "device_id",

        "source",

        # PC time
        "host_timestamp_ms",

        "pc_timestamp",

        "relative_time_s",

        # WT901 internal time
        "device_time",

        # Accelerometer
        "ax_g",
        "ay_g",
        "az_g",

        # Gyroscope
        "gx_dps",
        "gy_dps",
        "gz_dps",

        # Magnetometer
        "mx_raw",
        "my_raw",
        "mz_raw",

        # Orientation
        "roll_deg",
        "pitch_deg",
        "yaw_deg",

        # Reserved for future game system
        "motion_state",

        "game_event",

        "task_id",

        "trial_id",

        "zone"
    ])

    raw_file.flush()

    decoded_file.flush()

    # ========================================================
    # Runtime variables
    # ========================================================

    start_perf = (
        time.perf_counter()
    )

    start_wall = (
        datetime.now()
    )

    packet_count = 0

    decoded_count = 0

    rejected_device_count = 0

    invalid_packet_count = 0

    packet_times = []

    last_status_time = (
        start_perf
    )

    first_valid_device_seen = False

    # ========================================================
    # Main receive loop
    # ========================================================

    try:

        while True:

            current_perf = (
                time.perf_counter()
            )

            elapsed = (
                current_perf
                - start_perf
            )

            # ------------------------------------------------
            # Safety timeout
            # ------------------------------------------------

            if elapsed >= MAX_RECORD_TIME_S:

                print()
                print(
                    "Maximum recording time reached."
                )

                break

            # ------------------------------------------------
            # Receive UDP
            # ------------------------------------------------

            try:

                packet, addr = (
                    sock.recvfrom(
                        4096
                    )
                )

            except socket.timeout:

                continue

            packet_perf = (
                time.perf_counter()
            )

            packet_wall = (
                datetime.now()
            )

            relative_time_s = (
                packet_perf
                - start_perf
            )

            packet_count += 1

            packet_times.append(
                packet_perf
            )

            # ------------------------------------------------
            # Read device ID from packet header
            # ------------------------------------------------

            if len(packet) >= 12:

                try:

                    packet_device_id = (
                        packet[:12]
                        .decode(
                            "ascii",
                            errors="replace"
                        )
                    )

                except Exception:

                    packet_device_id = (
                        "UNKNOWN"
                    )

            else:

                packet_device_id = (
                    "UNKNOWN"
                )

            # ------------------------------------------------
            # Is this our selected device?
            # ------------------------------------------------

            accepted_device = (
                packet_device_id
                == DEVICE_ID
            )

            # ------------------------------------------------
            # ALWAYS preserve RAW UDP packet
            # ------------------------------------------------

            raw_writer.writerow([

                packet_count,

                packet_wall.strftime(
                    "%Y-%m-%d "
                    "%H:%M:%S.%f"
                ),

                f"{relative_time_s:.9f}",

                addr[0],

                addr[1],

                len(packet),

                packet_device_id,

                int(
                    accepted_device
                ),

                packet.hex()
            ])

            # ------------------------------------------------
            # Ignore other WT901 devices for decoded stream
            # ------------------------------------------------

            if not accepted_device:

                rejected_device_count += 1

                continue

            # ------------------------------------------------
            # Decode packet
            # ------------------------------------------------

            decoded = (
                decoder.decode(
                    packet
                )
            )

            if decoded is None:

                invalid_packet_count += 1

                continue

            decoded_count += 1

            # ------------------------------------------------
            # First successful packet message
            # ------------------------------------------------

            if not first_valid_device_seen:

                print()
                print(
                    "SUCCESS:"
                )

                print(
                    "Real WT901 detected."
                )

                print(
                    "Device:",
                    decoded["device_id"]
                )

                print(
                    "Source:",
                    f"{addr[0]}:{addr[1]}"
                )

                print(
                    "Packet length:",
                    len(packet)
                )

                print()

                first_valid_device_seen = (
                    True
                )

            # ------------------------------------------------
            # Host timestamp
            # ------------------------------------------------

            host_timestamp_ms = int(
                time.time()
                * 1000
            )

            # ------------------------------------------------
            # Decoded CSV
            # ------------------------------------------------

            decoded_writer.writerow([

                decoded_count,

                decoded["device_id"],

                "WT901",

                host_timestamp_ms,

                packet_wall.strftime(
                    "%Y-%m-%d "
                    "%H:%M:%S.%f"
                ),

                f"{relative_time_s:.9f}",

                decoded["device_time"],

                # Accelerometer
                f"{decoded['ax_g']:.9f}",
                f"{decoded['ay_g']:.9f}",
                f"{decoded['az_g']:.9f}",

                # Gyroscope
                f"{decoded['gx_dps']:.9f}",
                f"{decoded['gy_dps']:.9f}",
                f"{decoded['gz_dps']:.9f}",

                # Magnetometer
                decoded["mx_raw"],
                decoded["my_raw"],
                decoded["mz_raw"],

                # Orientation
                f"{decoded['roll_deg']:.9f}",
                f"{decoded['pitch_deg']:.9f}",
                f"{decoded['yaw_deg']:.9f}",

                # Reserved fields
                "UNKNOWN",

                "NONE",

                0,

                0,

                "NONE"
            ])

            # ------------------------------------------------
            # Flush periodically
            # ------------------------------------------------

            if packet_count % 200 == 0:

                raw_file.flush()

                decoded_file.flush()

            # ------------------------------------------------
            # Console display
            # ------------------------------------------------

            if (
                packet_perf
                - last_status_time
                >= STATUS_INTERVAL_S
            ):

                duration = (
                    packet_perf
                    - start_perf
                )

                if duration > 0:

                    packet_rate = (
                        decoded_count
                        / duration
                    )

                else:

                    packet_rate = 0.0

                print(

                    f"\r"

                    f"Device "
                    f"{decoded['device_id']} | "

                    f"Frames "
                    f"{decoded_count:06d} | "

                    f"Rate "
                    f"{packet_rate:6.1f} Hz | "

                    f"A "
                    f"["
                    f"{decoded['ax_g']:6.3f}, "
                    f"{decoded['ay_g']:6.3f}, "
                    f"{decoded['az_g']:6.3f}"
                    f"] g | "

                    f"G "
                    f"["
                    f"{decoded['gx_dps']:7.1f}, "
                    f"{decoded['gy_dps']:7.1f}, "
                    f"{decoded['gz_dps']:7.1f}"
                    f"] °/s | "

                    f"M "
                    f"["
                    f"{decoded['mx_raw']:6d}, "
                    f"{decoded['my_raw']:6d}, "
                    f"{decoded['mz_raw']:6d}"
                    f"] | "

                    f"RPY "
                    f"["
                    f"{decoded['roll_deg']:6.1f}, "
                    f"{decoded['pitch_deg']:6.1f}, "
                    f"{decoded['yaw_deg']:6.1f}"
                    f"]°",

                    end=""
                )

                last_status_time = (
                    packet_perf
                )

    except KeyboardInterrupt:

        print()
        print()
        print(
            "Ctrl+C detected."
        )

    finally:

        # ====================================================
        # Close files and socket
        # ====================================================

        raw_file.flush()
        decoded_file.flush()

        raw_file.close()
        decoded_file.close()

        sock.close()

        final_elapsed = (
            time.perf_counter()
            - start_perf
        )

        # ====================================================
        # Packet statistics
        # ====================================================

        if (
            decoded_count > 0
            and final_elapsed > 0
        ):

            mean_rate = (
                decoded_count
                / final_elapsed
            )

        else:

            mean_rate = 0.0

        # ----------------------------------------------------
        # Estimate packet interval statistics
        # ----------------------------------------------------

        intervals = []

        if len(packet_times) >= 2:

            intervals = [

                packet_times[i]
                - packet_times[i - 1]

                for i in range(
                    1,
                    len(packet_times)
                )

                if (
                    packet_times[i]
                    - packet_times[i - 1]
                ) > 0
            ]

        if intervals:

            mean_interval_ms = (
                sum(intervals)
                / len(intervals)
                * 1000.0
            )

            max_interval_ms = (
                max(intervals)
                * 1000.0
            )

        else:

            mean_interval_ms = 0.0
            max_interval_ms = 0.0

        # ====================================================
        # Summary file
        # ====================================================

        with open(
            summary_path,
            "w",
            encoding="utf-8"
        ) as f:

            f.write(
                "MagicIMU Data Layer V0.3\n"
            )

            f.write(
                "REAL WT901WIFI Recorder\n"
            )

            f.write(
                "=" * 60
                + "\n\n"
            )

            f.write(
                f"Device ID: "
                f"{DEVICE_ID}\n"
            )

            f.write(
                f"Recording start: "
                f"{start_wall}\n"
            )

            f.write(
                f"Duration: "
                f"{final_elapsed:.3f} s\n"
            )

            f.write(
                f"Total UDP packets: "
                f"{packet_count}\n"
            )

            f.write(
                f"Decoded target frames: "
                f"{decoded_count}\n"
            )

            f.write(
                f"Rejected other-device packets: "
                f"{rejected_device_count}\n"
            )

            f.write(
                f"Invalid target packets: "
                f"{invalid_packet_count}\n"
            )

            f.write(
                f"Mean decoded rate: "
                f"{mean_rate:.3f} Hz\n"
            )

            f.write(
                f"Mean UDP interval: "
                f"{mean_interval_ms:.3f} ms\n"
            )

            f.write(
                f"Maximum UDP interval: "
                f"{max_interval_ms:.3f} ms\n"
            )

            f.write(
                "\nMagnetometer note:\n"
            )

            f.write(
                "mx_raw/my_raw/mz_raw are preserved "
                "as WT901 raw magnetic counts.\n"
            )

            f.write(
                "No unsupported uT conversion "
                "has been applied.\n"
            )

        # ====================================================
        # Final report
        # ====================================================

        print()
        print()
        print("=" * 72)

        print(
            "Recording finished"
        )

        print("=" * 72)

        print(
            f"Device: {DEVICE_ID}"
        )

        print(
            f"Duration: "
            f"{final_elapsed:.2f} s"
        )

        print(
            f"UDP packets: "
            f"{packet_count}"
        )

        print(
            f"Decoded target frames: "
            f"{decoded_count}"
        )

        print(
            f"Mean rate: "
            f"{mean_rate:.2f} Hz"
        )

        print(
            f"Invalid packets: "
            f"{invalid_packet_count}"
        )

        print(
            f"Other-device packets: "
            f"{rejected_device_count}"
        )

        print()

        print(
            "Generated files:"
        )

        print(
            "1.",
            raw_path
        )

        print(
            "2.",
            decoded_path
        )

        print(
            "3.",
            summary_path
        )

        print("=" * 72)


# ============================================================
# ENTRY POINT
# ============================================================

if __name__ == "__main__":

    main()