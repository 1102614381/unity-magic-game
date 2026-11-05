// PlayerController.cs
// ============================================================================
// 记忆游戏最小原型 —— 小球移动控制器（键盘模拟 Roll/Pitch）
// 挂到 Player（Sphere）上。
//
// 当前：用键盘模拟九轴"倾斜"输入（A/D=Roll 左右, W/S=Pitch 前后）。
// 延申：接真机时把 useIMU 勾上，并把 roll/pitch 来源换成 IMUReceiver。
// ============================================================================

using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("移动设置")]
    public float moveSpeed = 4.0f;        // 移动速度（米/秒）
    public float boundX = 4.0f;           // 活动边界（左右）
    public float boundZ = 4.0f;           // 活动边界（前后）

    [Header("输入模式")]
    [Tooltip("false=键盘模拟；true=接入 IMUReceiver 真实九轴")]
    public bool useIMU = false;

    // 真实接入时，把场景里挂了 IMUReceiver 的物体拖到这里
    public IMUReceiver imuReceiver;

    // 当前"倾斜量"，供 MemoryGame 读取（也方便调试）
    [HideInInspector] public float rollInput = 0f;   // 左右
    [HideInInspector] public float pitchInput = 0f;  // 前后

    void Update()
    {
        if (!useIMU)
        {
            // ---------- 键盘模拟（最小原型默认）----------
            // A/D -> Roll（左右）  W/S -> Pitch（前后）
            float rx = (Input.GetKey(KeyCode.D) ? 1f : 0f)
                     - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float pz = (Input.GetKey(KeyCode.W) ? 1f : 0f)
                     - (Input.GetKey(KeyCode.S) ? 1f : 0f);

            rollInput = rx;
            pitchInput = pz;
        }
        else
        {
            // ---------- 真机接入（延申点，本原型先不用）----------
            // 注意：IMUReceiver 当前已把 roll/pitch/yaw 写成旋转角(°)。
            // 这里直接把角度当作"倾斜量"驱动位移即可；
            // 正式版建议由研究者统一约定"零姿态回正后，角度=位移方向"。
            if (imuReceiver != null)
            {
                rollInput = Mathf.Clamp(imuReceiver.latestRoll / 30f, -1f, 1f);   // 30° 满偏
                pitchInput = Mathf.Clamp(imuReceiver.latestPitch / 30f, -1f, 1f);
            }
        }

        // 位移：Roll -> X（左右），Pitch -> Z（前后，向前为负 Z）
        Vector3 delta = new Vector3(rollInput, 0f, -pitchInput) * moveSpeed * Time.deltaTime;
        Vector3 pos = transform.position + delta;

        // 边界限制
        pos.x = Mathf.Clamp(pos.x, -boundX, boundX);
        pos.z = Mathf.Clamp(pos.z, -boundZ, boundZ);
        pos.y = transform.position.y; // 保持高度

        transform.position = pos;
    }
}
