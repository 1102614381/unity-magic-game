using MagicIsland.Core;
using MagicIsland.IMU;
using MagicIsland.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.Game01
{
    /// <summary>
    /// G1 星星平衡岛（阶段B 交互原型，V1.1 6.1）：
    /// Unity 3D 物理——球体 + Rigidbody，锁定垂直运动在 XZ 平面滚动；
    /// Roll/Pitch 连续施力，滚进目标圈并保持 2 秒即 HOLD_SUCCESS。
    /// 场景里只需一个空物体挂本脚本：地面/球/目标圈/HUD 全部由代码生成。
    /// </summary>
    public class StarBalanceScene : MonoBehaviour
    {
        [Header("判定")]
        [SerializeField] private float targetRadius = 1.5f;
        [SerializeField] private float holdSeconds = 2f;
        [SerializeField] private float playAreaHalf = 9f;
        [SerializeField] private int trialCount = 3;

        [Header("输入映射")]
        [SerializeField] private float forceScale = 14f;
        [SerializeField] private float maxSpeed = 6f;
        [Tooltip("真机方向相反时勾选调整")]
        [SerializeField] private bool invertRollForce;
        [SerializeField] private bool invertPitchForce;
        [Tooltip("方向键助力量（等效倾斜角），IMU 连接时也叠加生效，用来对抗自发滚动")]
        [SerializeField] private float keyboardAssistDegrees = 25f;

        private Rigidbody _ball;
        private Transform _ring;
        private Text _hud;
        private Text _result;
        private Image _medal;

        private Vector3 _ringCenter;
        private bool _inTarget;
        private bool _enteredOnce;
        private float _holdTimer;
        private int _outOfTargetCount;
        private float _trialStartTime;
        private int _trialIndex;
        private bool _trialDone;
        private bool _allDone;

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
            cam.transform.position = new Vector3(0f, 14f, -0.5f);
            cam.transform.rotation = Quaternion.Euler(88f, 0f, 0f);
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.84f, 0.92f, 0.85f);

            if (FindObjectOfType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                lightGo.AddComponent<Light>();
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f); // 20 x 20
            ground.GetComponent<Renderer>().material = MakeMat(new Color(0.88f, 0.84f, 0.72f));

            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "StarBall";
            ballGo.transform.position = new Vector3(0f, 0.5f, 0f);
            ballGo.transform.localScale = Vector3.one;
            ballGo.GetComponent<Renderer>().material = MakeMat(UIFactory.Primary);
            _ball = ballGo.AddComponent<Rigidbody>();
            _ball.mass = 1f;
            _ball.drag = 2.6f;
            _ball.angularDrag = 3f;
            _ball.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "TargetRing";
            Destroy(ringGo.GetComponent<Collider>()); // 只做视觉，判定用距离计算
            ringGo.transform.localScale = new Vector3(targetRadius * 2f, 0.02f, targetRadius * 2f);
            ringGo.GetComponent<Renderer>().material = MakeMat(new Color(0.95f, 0.72f, 0.18f));
            _ring = ringGo.transform;
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
            var canvas = UIFactory.CreateCanvas("Canvas_G1", false); // 不加背景，露出 3D 场景

            UIFactory.CreateText(canvas.transform, "G1 星星平衡岛", 54, TextAnchor.UpperCenter,
                                 UIFactory.PrimaryDark, false);

            _result = UIFactory.CreateText(canvas.transform, string.Empty, 64, TextAnchor.MiddleCenter,
                                           UIFactory.PrimaryDark, false);
            var resultRect = _result.rectTransform;
            resultRect.anchorMin = new Vector2(0.5f, 0.62f);
            resultRect.anchorMax = new Vector2(0.5f, 0.62f);
            resultRect.pivot = new Vector2(0.5f, 0.5f);
            resultRect.sizeDelta = new Vector2(1200f, 120f);

            var medalTex = Resources.Load<Texture2D>("Art/icon_star_medal");
            _medal = UIFactory.CreateImage(canvas.transform, "Medal", Color.white);
            if (medalTex != null)
            {
                _medal.sprite = Sprite.Create(medalTex, new Rect(0f, 0f, medalTex.width, medalTex.height),
                                              new Vector2(0.5f, 0.5f), 100f);
                _medal.preserveAspect = true;
            }
            var medalRect = _medal.rectTransform;
            medalRect.anchorMin = new Vector2(0.5f, 0.72f);
            medalRect.anchorMax = new Vector2(0.5f, 0.72f);
            medalRect.pivot = new Vector2(0.5f, 0.5f);
            medalRect.sizeDelta = new Vector2(140f, 140f);
            _medal.gameObject.SetActive(false);

            UIFactory.CreateText(canvas.transform, "倾斜魔法棒，把星星球滚进黄色圆圈，停住 2 秒", 34,
                                 TextAnchor.LowerCenter, UIFactory.TextDark, false);

            _hud = UIFactory.CreateText(canvas.transform, string.Empty, 28, TextAnchor.LowerLeft,
                                        UIFactory.TextMuted, false);
            var hudRect = _hud.rectTransform;
            hudRect.anchorMin = new Vector2(0f, 0f);
            hudRect.anchorMax = new Vector2(0f, 0f);
            hudRect.pivot = new Vector2(0f, 0f);
            hudRect.anchoredPosition = new Vector2(24f, 20f);
            hudRect.sizeDelta = new Vector2(800f, 150f);
        }

        private void StartTrial(int index)
        {
            _trialIndex = index;
            _trialDone = false;
            _allDone = false;
            _inTarget = false;
            _enteredOnce = false;
            _holdTimer = 0f;
            _outOfTargetCount = 0;
            _trialStartTime = Time.realtimeSinceStartup;

            // 新目标圈位置（避开出生点）
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(3.5f, playAreaHalf * 0.7f);
            _ringCenter = new Vector3(Mathf.Sin(angle) * dist, 0.05f, Mathf.Cos(angle) * dist);
            _ring.position = _ringCenter;

            _ball.velocity = Vector3.zero;
            _ball.transform.position = new Vector3(0f, 0.5f, 0f);

            _medal.gameObject.SetActive(false);
            _result.text = index == 0 ? string.Empty : "很好！再来一次";

            var events = GameEventManager.Instance;
            if (events != null)
            {
                events.TrialId = $"{events.TaskId}_T{index + 1}_{System.DateTime.Now:HHmmss}";
                events.Log(GameEvents.TargetShow, targetId: $"Ring_T{index + 1}");
            }
        }

        private void FixedUpdate()
        {
            if (_ball == null || _allDone || _trialDone)
            {
                if (_ball != null && _trialDone) _ball.velocity = Vector3.zero;
                return;
            }

            Vector3 force = ReadForce();
            _ball.AddForce(force);
            if (_ball.velocity.magnitude > maxSpeed)
            {
                _ball.velocity = _ball.velocity.normalized * maxSpeed;
            }
        }

        private Vector3 ReadForce()
        {
            Vector3 force = Vector3.zero;

            var imu = IMUReceiver.Instance;
            if (imu != null && imu.IsConnected) // 用 IsConnected：停掉发送器 1 秒后自动切回键盘
            {
                float roll = invertRollForce ? -imu.Roll : imu.Roll;
                float pitch = invertPitchForce ? -imu.Pitch : imu.Pitch;
                force += new Vector3(roll, 0f, pitch) * forceScale;
            }

            // 键盘助力：始终可用并与 IMU 叠加，保证玩家对自发滚动的球始终有控制权
            float x = 0f, z = 0f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.W)) z += 1f;
            if (Input.GetKey(KeyCode.S)) z -= 1f;
            force += new Vector3(x, 0f, z) * (keyboardAssistDegrees * forceScale);

            return force;
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

            if (_ball == null) return;

            var events = GameEventManager.Instance;
            Vector2 ballPos = new Vector2(_ball.transform.position.x, _ball.transform.position.z);
            Vector2 ringPos = new Vector2(_ringCenter.x, _ringCenter.z);
            bool inside = Vector2.Distance(ballPos, ringPos) <= targetRadius;

            if (inside && !_inTarget && !_trialDone)
            {
                _inTarget = true;
                if (events != null) events.Log(GameEvents.TargetEnter, targetId: $"Ring_T{_trialIndex + 1}");
            }
            else if (!inside && _inTarget)
            {
                _inTarget = false;
                if (_enteredOnce) _outOfTargetCount++;
                if (events != null) events.Log(GameEvents.TargetExit, targetId: $"Ring_T{_trialIndex + 1}");
                _holdTimer = 0f;
            }

            if (_inTarget)
            {
                _enteredOnce = true;
                _holdTimer += Time.deltaTime;
                if (_holdTimer >= holdSeconds) Success();
            }

            UpdateHud(inside);
            if (_trialDone && !_allDone && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            {
                NextTrial();
            }
        }

        private void Success()
        {
            _trialDone = true;
            _inTarget = false;
            float completionMs = (Time.realtimeSinceStartup - _trialStartTime) * 1000f;

            var events = GameEventManager.Instance;
            if (events != null)
            {
                events.Log(GameEvents.HoldSuccess, targetId: $"Ring_T{_trialIndex + 1}",
                           extra: $"hold_ms={holdSeconds * 1000f:0}");
                events.Log(GameEvents.TaskSuccess, isCorrect: true,
                           extra: $"trial={_trialIndex + 1}/{trialCount}");
            }

            TaskResultsWriter.Append(1, events != null ? events.TrialId : string.Empty, true, completionMs,
                                     (holdSeconds * 1000f).ToString("0"),
                                     _outOfTargetCount.ToString(),
                                     $"Ring_T{_trialIndex + 1}");

            _medal.gameObject.SetActive(true);
            _result.text = _trialIndex + 1 >= trialCount ? "本关完成！你真棒！" : "太棒了！获得一颗星";
        }

        private void NextTrial()
        {
            int next = _trialIndex + 1;
            if (next >= trialCount)
            {
                _allDone = true;
                return;
            }
            StartTrial(next);
        }

        private void UpdateHud(bool inside)
        {
            var imu = IMUReceiver.Instance;
            string conn = imu != null && imu.IsConnected ? "已连接" : "等待数据";
            string roll = imu != null ? imu.Roll.ToString("0.0") : "-";
            string pitch = imu != null ? imu.Pitch.ToString("0.0") : "-";
            string hold = inside && !_trialDone ? $"保持中 {_holdTimer:0.0}/{holdSeconds:0} 秒" : string.Empty;
            _hud.text = $"{conn}  roll={roll}  pitch={pitch}\n" +
                        $"第 {_trialIndex + 1}/{trialCount} 轮  出圈 {_outOfTargetCount} 次  {hold}\n" +
                        "WASD 推球  空格 下一轮  Esc 返回主页";
        }
    }
}
