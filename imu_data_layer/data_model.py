from dataclasses import dataclass, asdict


@dataclass
class IMUData:
    """
    Unified IMU data structure.

    All data sources:
        SIM
        SIM_KEYBOARD
        WT901

    must finally be converted to this structure.
    """

    # ========================================
    # Device / frame information
    # ========================================

    device_id: str

    frame_id: int

    host_timestamp_ms: int

    # ========================================
    # Accelerometer
    # Unit: g
    # ========================================

    ax: float
    ay: float
    az: float

    # ========================================
    # Gyroscope
    # Unit: degree/s
    # ========================================

    gx: float
    gy: float
    gz: float

    # ========================================
    # Magnetometer
    # Unit: uT
    # ========================================

    mx: float
    my: float
    mz: float

    # ========================================
    # Orientation
    # Unit: degree
    # ========================================

    roll: float
    pitch: float
    yaw: float

    # ========================================
    # Research / game labels
    # ========================================

    motion_state: str = "UNKNOWN"

    game_event: str = "NONE"

    task_id: int = 0

    trial_id: int = 0

    zone: str = "NONE"

    # ========================================
    # Data source
    #
    # SIM
    # SIM_KEYBOARD
    # WT901
    # ========================================

    source: str = "SIM"

    def to_dict(self):

        return asdict(self)