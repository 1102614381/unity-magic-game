import socket
import json


class UDPSender:

    def __init__(
        self,
        target_ip="127.0.0.1",
        target_port=5005
    ):

        self.target_ip = (
            target_ip
        )

        self.target_port = (
            target_port
        )

        self.socket = (
            socket.socket(
                socket.AF_INET,
                socket.SOCK_DGRAM
            )
        )

        print(
            "[UDP] Sender initialized."
        )

        print(
            f"[UDP] Target: "
            f"{self.target_ip}:"
            f"{self.target_port}"
        )

    # ========================================
    # Send one IMU frame
    # ========================================

    def send(
        self,
        imu_data
    ):

        data_dict = (
            imu_data.to_dict()
        )

        message = json.dumps(
            data_dict,
            ensure_ascii=False
        )

        self.socket.sendto(

            message.encode(
                "utf-8"
            ),

            (
                self.target_ip,
                self.target_port
            )
        )

    # ========================================
    # Close
    # ========================================

    def close(self):

        if self.socket:

            self.socket.close()

            print(
                "[UDP] Socket closed."
            )