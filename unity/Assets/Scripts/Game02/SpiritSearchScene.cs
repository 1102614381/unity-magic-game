using MagicIsland.Core;
using MagicIsland.IMU;
using MagicIsland.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.Game02
{
    /// <summary>
    /// G2 寻找魔法精灵（阶段B 交互原型，V1.1 6.2）：
    /// Yaw 相对转向搜索——任务开始记录 yaw 参考零点，Mathf.DeltaAngle 处理 ±180° 跳变；
    /// 对准判定 = 角度差小于阈值 且 角速度低于静止阈值（双条件），保持后确认目标。
    /// 场景里只需一个空物体挂本脚本：精灵/相机/HUD 全部由代码生成。
    /// </summary>
    public class SpiritSearchScene : MonoBehaviour
    {
        [Header("判定阈值")]
        [SerializeField] private float alignedDegrees = 12f;
        [Tooltip("低于该角速度(度/秒)视为“停住”")]
        [SerializeField] private float stillDegreesPerSecond = 15f;
        [SerializeField] private float alignedHoldSeconds = 1f;
        [SerializeField] private float wrongAlignSeconds = 1f;
        [SerializeField] private int trialCount = 3;

        private readonly Color[] _spiritColors =
        {
            new Color(0.25f, 0.55f, 0.95f), // 蓝
            new Color(0.90f, 0.30f, 0.28f), // 红
            new Color(0.95f, 0.78f, 0.20f), // 黄
            new Color(0.35f, 0.75f, 0.35f), // 绿
            new Color(0.62f, 0.40f, 0.85f), // 紫
            new Color(0.95f, 0.55f, 0.25f), // 橙
        };
        private readonly string[] _spiritNames = { "蓝精灵", "红精灵", "黄精灵", "绿精灵", "紫精灵", "橙精灵" };

        private Transform _cameraT;
        private Transform[] _spirits;
        private Text _prompt;
        private Text _hud;
        private Text _feedback;

        private float _yawRef;
        private float _keyYawOffset; // A/D 键盘转向叠加量（每轮重置）
        private float _prevYawRel;
        private float _angVelSmooth;
        private float _alignedTimer;
        private float _wrongTimer;
        private float _nextWrongAllowedTime; // 找错记录的冷却时间戳
        private bool _alignedLogged;
        private float _alignedAtMs = -1f;
        private int _wrongTargetCount;
        private float _trialStartTime;
        private int _trialIndex;
        private bool _trialDone;
        private bool _allDone;

        private int TargetIndex => _trialIndex % _spirits.Length;

        private void Start()
        {
            BuildWorld();
            BuildHud();
            StartTrial(0);
        }

        private void BuildWorld()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cam = camGo.GetComponent<Camera>();
            }
            cam.transform.position = new Vector3(0f, 1.4f, 0f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.88f, 0.95f);
            _cameraT = cam.transform;

            if (FindObjectOfType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.0f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            _spirits = new Transform[_spiritColors.Length];
            for (int i = 0; i < _spiritColors.Length; i++)
            {
                float angle = i * 360f / _spiritColors.Length;
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.name = "Spirit_" + _spiritNames[i];
                go.transform.position = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 7f, 1.0f,
                                                    Mathf.Cos(angle * Mathf.Deg2Rad) * 7f);
                go.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);
                go.GetComponent<Renderer>().material = MakeMat(_spiritColors[i]);

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                labelGo.transform.rotation = Quaternion.LookRotation(
                    new Vector3(go.transform.position.x, 0f, go.transform.position.z)); // 朝外放，从圆心看字才是正的
                var label = labelGo.AddComponent<TextMesh>();
                label.text = _spiritNames[i];
                label.fontSize = 48;
                label.characterSize = 0.25f;
                label.anchor = TextAnchor.MiddleCenter;
                if (UIFactory.DefaultFont != null) label.font = UIFactory.DefaultFont;

                _spirits[i] = go.transform;
            }
        }

        private static Material MakeMat(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas("Canvas_G2", false); // 不加背景，露出 3D 场景

            UIFactory.CreateText(canvas.transform, "G2 寻找魔法精灵", 48, TextAnchor.UpperCenter,
                                 UIFactory.PrimaryDark, false);

            _prompt = UIFactory.CreateText(canvas.transform, string.Empty, 60, TextAnchor.MiddleCenter,
                                           UIFactory.PrimaryDark, false);
            var promptRect = _prompt.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0.86f);
            promptRect.anchorMax = new Vector2(0.5f, 0.86f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.sizeDelta = new Vector2(1400f, 100f);

            _feedback = UIFactory.CreateText(canvas.transform, string.Empty, 44, TextAnchor.MiddleCenter,
                                             new Color(0.85f, 0.55f, 0.10f), false);
            var feedbackRect = _feedback.rectTransform;
            feedbackRect.anchorMin = new Vector2(0.5f, 0.30f);
            feedbackRect.anchorMax = new Vector2(0.5f, 0.30f);
            feedbackRect.pivot = new Vector2(0.5f, 0.5f);
            feedbackRect.sizeDelta = new Vector2(1200f, 80f);

            _hud = UIFactory.CreateText(canvas.transform, string.Empty, 28, TextAnchor.LowerLeft,
                                        UIFactory.TextMuted, false);
            var hudRect = _hud.rectTransform;
            hudRect.anchorMin = new Vector2(0f, 0f);
            hudRect.anchorMax = new Vector2(0f, 0f);
            hudRect.pivot = new Vector2(0f, 0f);
            hudRect.anchoredPosition = new Vector2(24f, 20f);
            hudRect.sizeDelta = new Vector2(900f, 130f);
        }

        private void StartTrial(int index)
        {
            _trialIndex = index;
            _trialDone = false;
            _allDone = false;
            _alignedTimer = 0f;
            _wrongTimer = 0f;
            _nextWrongAllowedTime = 0f;
            _alignedLogged = false;
            _alignedAtMs = -1f;
            _trialStartTime = Time.realtimeSinceStartup;

            // 任务开始保存 yaw 参考零点（V1.1 6.2）
            var imu = IMUReceiver.Instance;
            _yawRef = imu != null && imu.HasFrame ? imu.Yaw : 0f;
            _keyYawOffset = 0f;
            _prevYawRel = 0f;

            string colorName = _spiritNames[TargetIndex];
            _prompt.text = $"找到【{colorName}】的精灵";
            _feedback.text = string.Empty;

            var events = GameEventManager.Instance;
            if (events != null)
            {
                events.TrialId = $"{events.TaskId}_T{index + 1}_{System.DateTime.Now:HHmmss}";
                events.Log(GameEvents.TargetShow, targetId: _spirits[TargetIndex].name);
            }
        }

        private void Update()
        {
            // Esc 中止本关返回主页（记录 TASK_END abort，保证事件闭环）
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                var em = GameEventManager.Instance;
                if (em != null) em.Log(GameEvents.TaskEnd, extra: "abort=esc");
                GameManager.Instance.GoToMainMenu();
                return;
            }

            float yawRel = ReadYawRelative();
            ApplyCameraYaw(yawRel);
            UpdateAngularVelocity(yawRel);

            var events = GameEventManager.Instance;
            Transform target = _spirits[TargetIndex];

            if (!_allDone && !_trialDone)
            {
                Transform facing = FindFacingSpirit();
                float dyaw = Mathf.Abs(Mathf.DeltaAngle(yawRel, FacingYaw(target))); // DeltaAngle 处理 ±360 绕圈
                bool still = _angVelSmooth < stillDegreesPerSecond;

                if (facing == target && dyaw < alignedDegrees && still)
                {
                    _alignedTimer += Time.deltaTime;
                    _wrongTimer = 0f;
                    if (!_alignedLogged && _alignedTimer >= alignedHoldSeconds)
                    {
                        _alignedLogged = true;
                        _alignedAtMs = (Time.realtimeSinceStartup - _trialStartTime) * 1000f;
                        if (events != null)
                        {
                            events.Log(GameEvents.TargetAligned, targetId: target.name);
                            events.Log(GameEvents.HoldStart, targetId: target.name);
                        }
                    }
                    if (_alignedTimer >= alignedHoldSeconds)
                    {
                        _feedback.text = "对准了！保持住……";
                        if (_alignedTimer >= alignedHoldSeconds * 2f) FoundTarget(target);
                    }
                }
                else if (facing != null && facing != target && still)
                {
                    _alignedTimer = 0f;
                    _wrongTimer += Time.deltaTime;
                    // 连续盯错 wrongAlignSeconds 才记一次，且两次记录间有冷却，避免刷屏
                    if (_wrongTimer >= wrongAlignSeconds && Time.time >= _nextWrongAllowedTime)
                    {
                        _nextWrongAllowedTime = Time.time + 2.5f;
                        _wrongTimer = 0f;
                        _wrongTargetCount++;
                        if (events != null)
                        {
                            events.Log(GameEvents.WrongTarget, targetId: facing.name, response: facing.name,
                                       isCorrect: false);
                        }
                        _feedback.text = "看一看提示，再找找哦";
                    }
                }
                else
                {
                    _alignedTimer = 0f;
                    _wrongTimer = 0f;
                    if (!_trialDone && _feedback.text == "对准了！保持住……") _feedback.text = string.Empty;
                }
            }

            UpdateHud(yawRel);
        }

        private float ReadYawRelative()
        {
            var imu = IMUReceiver.Instance;
            if (imu != null && imu.IsConnected) // 用 IsConnected：停掉发送器 1 秒后自动切回键盘
            {
                // 相对参考零点的连续角；DeltaAngle 保证 ±180° 跳变时结果仍在 ±180 内
                // A/D 键盘转向叠加在 IMU 之上，保证玩家始终能主动转到位（demo 摆幅有限）
                if (Input.GetKey(KeyCode.A)) _keyYawOffset += 70f * Time.deltaTime;
                if (Input.GetKey(KeyCode.D)) _keyYawOffset -= 70f * Time.deltaTime;
                return Mathf.DeltaAngle(_yawRef, imu.Yaw) + _keyYawOffset;
            }

            // 无手环时键盘 A/D 演示
            if (Input.GetKey(KeyCode.A)) _yawRef += 60f * Time.deltaTime;
            if (Input.GetKey(KeyCode.D)) _yawRef -= 60f * Time.deltaTime;
            return Mathf.Repeat(-_yawRef + 180f, 360f) - 180f;
        }

        private void ApplyCameraYaw(float yawRel)
        {
            _cameraT.rotation = Quaternion.Euler(0f, yawRel, 0f);
        }

        private void UpdateAngularVelocity(float yawRel)
        {
            float delta = Mathf.DeltaAngle(_prevYawRel, yawRel);
            float vel = Mathf.Abs(delta) / Mathf.Max(0.0001f, Time.deltaTime);
            _angVelSmooth = Mathf.Lerp(_angVelSmooth, vel, 0.35f);
            _prevYawRel = yawRel;
        }

        private Transform FindFacingSpirit()
        {
            Vector3 fwd = _cameraT.forward;
            fwd.y = 0f;
            fwd.Normalize();

            Transform best = null;
            float bestAngle = 999f;
            foreach (var spirit in _spirits)
            {
                Vector3 dir = spirit.position - _cameraT.position;
                dir.y = 0f;
                dir.Normalize();
                float angle = Vector3.Angle(fwd, dir);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = spirit;
                }
            }
            return bestAngle <= alignedDegrees ? best : null;
        }

        private float FacingYaw(Transform spirit)
        {
            Vector3 dir = spirit.position - _cameraT.position;
            return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        }

        private void FoundTarget(Transform target)
        {
            _trialDone = true;
            float completionMs = (Time.realtimeSinceStartup - _trialStartTime) * 1000f;

            var events = GameEventManager.Instance;
            if (events != null)
            {
                events.Log(GameEvents.TargetFound, targetId: target.name, response: target.name, isCorrect: true);
                events.Log(GameEvents.TaskSuccess, targetId: target.name, isCorrect: true,
                           extra: $"trial={_trialIndex + 1}/{trialCount}");
            }

            TaskResultsWriter.Append(2, events != null ? events.TrialId : string.Empty, true, completionMs,
                                     targetId: target.name,
                                     wrongTargetCount: _wrongTargetCount.ToString(),
                                     alignmentTimeMs: _alignedAtMs >= 0f ? _alignedAtMs.ToString("0") : "");

            _feedback.text = "找到啦！你真棒！";
            StartCoroutine(NextTrialAfterDelay());
        }

        private System.Collections.IEnumerator NextTrialAfterDelay()
        {
            yield return new WaitForSeconds(1.6f);
            int next = _trialIndex + 1;
            if (next >= trialCount)
            {
                _allDone = true;
                _prompt.text = "本关完成！精灵们都找到啦";
                _feedback.text = string.Empty;
            }
            else
            {
                _wrongTargetCount = 0;
                StartTrial(next);
            }
        }

        private void UpdateHud(float yawRel)
        {
            var imu = IMUReceiver.Instance;
            string conn = imu != null && imu.IsConnected ? "已连接" : "键盘 A/D 演示";
            _hud.text = $"{conn}  yaw={yawRel:0.0}  角速度={_angVelSmooth:0}°/s\n" +
                        $"第 {_trialIndex + 1}/{trialCount} 轮  找错 {_wrongTargetCount} 次\n" +
                        "A/D 转身  Esc 返回主页";
        }
    }
}
