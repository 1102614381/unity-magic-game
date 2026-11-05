import socket
import json


UDP_IP = "127.0.0.1"

UDP_PORT = 5005


sock = socket.socket(
    socket.AF_INET,
    socket.SOCK_DGRAM
)

sock.bind(
    (
        UDP_IP,
        UDP_PORT
    )
)


print(
    "===================================="
)

print(
    "UDP Test Receiver"
)

print(
    f"Listening on "
    f"{UDP_IP}:{UDP_PORT}"
)

print(
    "Press Ctrl+C to stop."
)

print(
    "===================================="
)


try:

    while True:

        data, address = (
            sock.recvfrom(
                65535
            )
        )

        message = (
            data.decode(
                "utf-8"
            )
        )

        imu = json.loads(
            message
        )

        print(

            f"\r"

            f"Device: "
            f"{imu['device_id']} | "

            f"Frame: "
            f"{imu['frame_id']:06d} | "

            f"Roll: "
            f"{imu['roll']:7.2f} | "

            f"Pitch: "
            f"{imu['pitch']:7.2f} | "

            f"Yaw: "
            f"{imu['yaw']:7.2f} | "

            f"Source: "
            f"{imu['source']}",

            end=""
        )

except KeyboardInterrupt:

    print(
        "\nReceiver stopped."
    )

finally:

    sock.close()