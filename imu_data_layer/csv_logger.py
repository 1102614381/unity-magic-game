import csv
import os

from datetime import datetime


class CSVLogger:

    def __init__(
        self,
        output_dir="data"
    ):

        os.makedirs(
            output_dir,
            exist_ok=True
        )

        current_time = (
            datetime.now()
        )

        filename = (
            current_time.strftime(
                "imu_%Y_%m_%d_%H_%M_%S.csv"
            )
        )

        self.file_path = (
            os.path.join(
                output_dir,
                filename
            )
        )

        self.file = open(
            self.file_path,
            mode="w",
            newline="",
            encoding="utf-8"
        )

        self.fieldnames = [

            "device_id",

            "frame_id",

            "host_timestamp_ms",

            "ax",
            "ay",
            "az",

            "gx",
            "gy",
            "gz",

            "mx",
            "my",
            "mz",

            "roll",
            "pitch",
            "yaw",

            "motion_state",

            "game_event",

            "task_id",

            "trial_id",

            "zone",

            "source"
        ]

        self.writer = (
            csv.DictWriter(
                self.file,
                fieldnames=
                self.fieldnames
            )
        )

        self.writer.writeheader()

        print(
            "[CSV] Recording to:"
        )

        print(
            self.file_path
        )

    def write(
        self,
        imu_data
    ):

        data_dict = (
            imu_data.to_dict()
        )

        self.writer.writerow(
            data_dict
        )

    def close(self):

        if self.file:

            self.file.flush()

            self.file.close()

            print(
                "[CSV] File saved."
            )