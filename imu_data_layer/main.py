import time

from keyboard_simulator import (
    KeyboardIMUSimulator
)

from csv_logger import (
    CSVLogger
)

from udp_sender import (
    UDPSender
)


# ========================================
# Configuration
# ========================================

SAMPLE_RATE_HZ = 50

SAMPLE_INTERVAL = (
    1.0
    / SAMPLE_RATE_HZ
)


# ========================================
# UDP Configuration
# ========================================

UNITY_IP = "127.0.0.1"

UNITY_PORT = 5005


def main():

    print(
        "=============================================="
    )

    print(
        "MagicIMU Data Layer V0.3"
    )

    print(
        "Keyboard IMU Simulator + UDP"
    )

    print(
        "Device ID: WT5500011658"
    )

    print(
        f"Sample Rate: "
        f"{SAMPLE_RATE_HZ} Hz"
    )

    print(
        "=============================================="
    )

    print(
        "Keyboard Controls:"
    )

    print(
        "A / D     : Roll left / right"
    )

    print(
        "W / S     : Pitch forward / backward"
    )

    print(
        "Q / E     : Yaw left / right"
    )

    print(
        "SPACE     : Shake / swing"
    )

    print(
        "R         : Reset orientation"
    )

    print(
        "ESC       : Stop"
    )

    print(
        "=============================================="
    )

    # ========================================
    # Create input source
    # ========================================

    imu = (
        KeyboardIMUSimulator()
    )

    # ========================================
    # Create CSV logger
    # ========================================

    logger = CSVLogger(
        output_dir="data"
    )

    # ========================================
    # Create UDP sender
    # ========================================

    udp = UDPSender(

        target_ip=
        UNITY_IP,

        target_port=
        UNITY_PORT
    )

    try:

        while imu.running:

            loop_start = (
                time.perf_counter()
            )

            # ==================================
            # Get IMU data
            # ==================================

            data = (
                imu.get_data()
            )

            # ==================================
            # Save CSV
            # ==================================

            logger.write(
                data
            )

            # ==================================
            # Send UDP → Unity
            # ==================================

            udp.send(
                data
            )

            # ==================================
            # Console
            # ==================================

            print(

                f"\r"

                f"Device "
                f"{data.device_id} | "

                f"Frame "
                f"{data.frame_id:06d} | "

                f"R "
                f"{data.roll:7.2f}° | "

                f"P "
                f"{data.pitch:7.2f}° | "

                f"Y "
                f"{data.yaw:7.2f}° | "

                f"M "
                f"["
                f"{data.mx:6.1f}, "
                f"{data.my:6.1f}, "
                f"{data.mz:6.1f}"
                f"] | "

                f"{data.motion_state:8s}",

                end=""
            )

            # ==================================
            # Maintain sample rate
            # ==================================

            elapsed = (
                time.perf_counter()
                - loop_start
            )

            sleep_time = (
                SAMPLE_INTERVAL
                - elapsed
            )

            if sleep_time > 0:

                time.sleep(
                    sleep_time
                )

    except KeyboardInterrupt:

        print(
            "\nCtrl+C received."
        )

    finally:

        print(
            "\nStopping..."
        )

        imu.close()

        udp.close()

        logger.close()

        print(
            "Program terminated."
        )


if __name__ == "__main__":

    main()