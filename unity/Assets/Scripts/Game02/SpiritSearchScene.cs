using MagicIsland.Core;
using MagicIsland.IMU;
using MagicIsland.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.Game02
{
    /// <summary>
    /// G2 寻找魔法精灵（阶段B 交互原型 V2.0 · 低龄儿童版）：
    /// 判定与事件上报完全沿用 V1.1（Yaw 相对转向 + 角速度静止 + 保持确认）；
    /// 视觉改为：彩虹云朵天空 + 可爱小精灵贴图染 6 色 + 漂浮动画 + 大图标 + 语音；
    /// 去掉精灵头顶的文字标签（低龄儿童不识字，靠颜色识别）。
    /// 场景里只需一个空物体挂本脚本。
    /// </summary>
    public class SpiritSearchScene : MonoBehaviour
    {
        [Header("判定（勿改，与数据表一致）")]
        [SerializeField] private float alignedDegrees = 12f;
        [Tooltip("低于该角速度(度/秒)视为“停住”")]
        [SerializeField] private float stillDegreesPerSecond = 15f;
        [SerializeField] private float alignedHoldSeconds = 1f;
        [SerializeField] private float wrongAlignSeconds = 1f;
        [SerializeField] private int trialCount = 3;

        [Header("低龄儿童版选项")]
        [SerializeField] private bool enableVoice = true;
        [SerializeField] private bool showDebugInEditor = true;

        private readonly Color[] _spiritColors =
        {
            new Color(0.35f, 0.72f, 1.00f), // 蓝
            new Color(1.00f, 0.45f, 0.45f), // 红
            new Color(1.00f, 0.84f, 0.30f), // 黄
            new Color(0.45f, 0.90f, 0.55f), // 绿
            new Color(0.75f, 0.52f, 0.98f), // 紫
            new Color(1.00f, 0.62f, 0.32f), // 橙
        };
        // 事件 targetId 沿用 V1.1 的中文名（数据契约不变）；文字标签本身已移除，孩子只看颜色
        private readonly string[] _spiritNames = { "蓝精灵", "红精灵", "黄精灵", "绿精灵", "紫精灵", "橙精灵" };

        private Transform _cameraT;
        private Transform[] _spirits;
        private Image _targetIcon;
        private Text _prompt;
        private Text _feedback;
        private Text _hud;
        private GameObject _successCard;

        private float _yawRef;
        private float _keyYawOffset;
        private float _prevYawRel;
        private float _angVelSmooth;
        private float _alignedTimer;
        private float _wrongTimer;
        private float _nextWrongAllowedTime;
        private bool _alignedLogged;
        private float _alignedAtMs = -1f;
        private int _wrongTargetCount;
        private float _trialStartTime;
        private int _trialIndex;
        private bool _trialDone;
        private bool _allDone;

        // 视觉动画用
        private float[] _spiritPhase;
        private Vector3[] _spiritBasePos;
        private float _targetGlow;

        private int TargetIndex => _trialIndex % _spiritColors.Length;

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
            cam.backgroundColor = new Color(0.86f, 0.93f, 1.00f);
            _cameraT = cam.transform;

            // 彩虹云朵天空：用一张面向相机的平面贴在远处，随相机转
            var skyTex = Resources.Load<Texture2D>("Art/bg_sky_clouds");
            if (skyTex != null)
            {
                var skyGo = new GameObject("SkyBackdrop");
                skyGo.transform.SetParent(_cameraT, false);
                skyGo.transform.localPosition = new Vector3(0f, 0f, 60f);
                var skySr = skyGo.AddComponent<SpriteRenderer>();
                skySr.sprite = Sprite.Create(skyTex, new Rect(0f, 0f, skyTex.width, skyTex.height),
                                             new Vector2(0.5f, 0.5f), 100f);
                skySr.sortingOrder = -100;
                skyGo.transform.localScale = Vector3.one * 12f;
            }

            if (FindObjectOfType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.05f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // 地面：柔和草地（俯视看不到多少，但给场景底部一点颜色）
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(4f, 1f, 4f);
            var meadow = Resources.Load<Texture2D>("Art/bg_meadow");
            var gm = MakeMat(meadow != null ? Color.white : new Color(0.78f, 0.92f, 0.70f));
            if (meadow != null)
            {
                meadow.wrapMode = TextureWrapMode.Repeat;
                gm.mainTexture = meadow;
                gm.mainTextureScale = new Vector2(6f, 6f);
            }
            ground.GetComponent<Renderer>().material = gm;
            ground.transform.position = new Vector3(0f, -0.6f, 0f);

            // 精灵：可爱小精灵贴图染 6 色，去掉文字标签
            var spiritSprite = KidVisualFactory.LoadSprite("character_spirit_white");
            _spirits = new Transform[_spiritColors.Length];
            _spiritPhase = new float[_spiritColors.Length];
            _spiritBasePos = new Vector3[_spiritColors.Length];

            for (int i = 0; i < _spiritColors.Length; i++)
            {
                float angle = i * 360f / _spiritColors.Length;
                var go = new GameObject("Spirit_" + _spiritNames[i]);
                var pos = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 7f, 1.0f,
                                      Mathf.Cos(angle * Mathf.Deg2Rad) * 7f);
                go.transform.position = pos;
                _spiritBasePos[i] = pos;
                _spiritPhase[i] = i * 0.8f;

                if (spiritSprite != null)
                {
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = spiritSprite;
                    sr.color = _spiritColors[i];
                    sr.sortingOrder = 10;
                    go.transform.localScale = Vector3.one * 2.2f;
                    // 让精灵面朝圆心（玩家在中心，看到的是精灵正面）
                    go.transform.rotation = Quaternion.LookRotation(-pos.normalized, Vector3.up);
                }
                else
                {
                    // 兜底：彩色胶囊
                    var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    cap.transform.SetParent(go.transform, false);
                    cap.transform.localScale = new Vector3(0.8f, 1.0f, 0.8f);
                    cap.GetComponent<Renderer>().material = MakeMat(_spiritColors[i]);
                }
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
            var canvas = UIFactory.CreateCanvas("Canvas_G2", false);

            // 标题极简化
            var star = KidVisualFactory.LoadSprite("icon_star_medal");
            KidVisualFactory.CreateSpriteImage(canvas.transform, star, 80f, 80f,
                                               new Vector2(0.5f, 1f), new Vector2(0f, -64f), "TitleStar");
            var title = UIFactory.CreateText(canvas.transform, "找精灵", 38, TextAnchor.UpperCenter,
                                             KidVisualFactory.CandyPurple, false);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(400f, 66f);
            titleRt.anchoredPosition = new Vector2(0f, -112f);

            // 目标提示：一个大色块图标 + 极短文字"找它"
            var hint = KidVisualFactory.CreateHintCard(canvas.transform, null, "找它",
                                                       KidVisualFactory.CandyBlue,
                                                       new Vector2(0.5f, 0.86f), Vector2.zero, 420f, 110f);
            _targetIcon = KidVisualFactory.CreateSpriteImage(hint.transform, null, 84f, 84f,
                                                             new Vector2(0f, 0.5f), new Vector2(210f, 0f),
                                                             "TargetColorIcon");
            // 把图标放到卡片左侧（覆盖在默认位置之外）
            _targetIcon.rectTransform.anchoredPosition = new Vector2(-170f, 0f);

            _prompt = UIFactory.CreateText(canvas.transform, string.Empty, 30, TextAnchor.MiddleCenter,
                                           Color.white, false);
            var promptRect = _prompt.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0.86f);
            promptRect.anchorMax = new Vector2(0.5f, 0.86f);
            promptRect.sizeDelta = new Vector2(300f, 100f);
            promptRect.anchoredPosition = new Vector2(60f, 0f);
            _prompt.gameObject.SetActive(false); // 文字提示由语音+图标替代

            // 反馈：找到时的绿色卡片（对/错用颜色，不用长句）
            _feedback = UIFactory.CreateText(canvas.transform, string.Empty, 60, TextAnchor.MiddleCenter,
                                             Color.white, false);
            var feedbackRect = _feedback.rectTransform;
            feedbackRect.anchorMin = new Vector2(0.5f, 0.32f);
            feedbackRect.anchorMax = new Vector2(0.5f, 0.32f);
            feedbackRect.sizeDelta = new Vector2(1000f, 100f);

            _successCard = KidVisualFactory.CreateHintCard(canvas.transform, star, "找到啦",
                                                           KidVisualFactory.CandyGreen,
                                                           new Vector2(0.5f, 0.5f), new Vector2(0f, -60f),
                                                           520f, 150f);
            _successCard.SetActive(false);

            _hud = UIFactory.CreateText(canvas.transform, string.Empty, 22, TextAnchor.LowerLeft,
                                        new Color(0.3f, 0.3f, 0.3f, 0.6f), false);
            var hudRect = _hud.rectTransform;
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.zero;
            hudRect.pivot = Vector2.zero;
            hudRect.anchoredPosition = new Vector2(16f, 12f);
            hudRect.sizeDelta = new Vector2(700f, 120f);
#if !UNITY_EDITOR
            _hud.gameObject.SetActive(false);
#else
            _hud.gameObject.SetActive(showDebugInEditor);
#endif
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
            _targetGlow = 0f;
            _trialStartTime = Time.realtimeSinceStartup;
            _successCard.SetActive(false);

            var imu = IMUReceiver.Instance;
            _yawRef = imu != null && imu.HasFrame ? imu.Yaw : 0f;
            _keyYawOffset = 0f;
            _prevYawRel = 0f;

            // 目标图标 = 该颜色的小精灵图（染成目标色），孩子看图就知道找谁
            var spiritSprite = KidVisualFactory.LoadSprite("character_spirit_white");
            if (_targetIcon != null)
            {
                _targetIcon.sprite = spiritSprite;
                _targetIcon.color = _spiritColors[TargetIndex];
            }
            _prompt.text = "找" + _spiritNames[TargetIndex];
            _feedback.text = string.Empty;

            if (enableVoice) KidVisualFactory.Speak("找" + _spiritNames[TargetIndex]);

            var events = GameEventManager.Instance;
            if (events != null)
            {
                events.TrialId = $"{events.TaskId}_T{index + 1}_{System.DateTime.Now:HHmmss}";
                events.Log(GameEvents.TargetShow, targetId: _spirits[TargetIndex].name);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                var em = GameEventManager.Instance;
                if (em != null) em.Log(GameEvents.TaskEnd, extra: "abort=esc");
                KidVisualFactory.ShutdownSpeech();
                GameManager.Instance.GoToMainMenu();
                return;
            }

            float yawRel = ReadYawRelative();
            ApplyCameraYaw(yawRel);
            UpdateAngularVelocity(yawRel);
            AnimateSpirits();

            var events = GameEventManager.Instance;
            Transform target = _spirits[TargetIndex];

            if (!_allDone && !_trialDone)
            {
                Transform facing = FindFacingSpirit();
                float dyaw = Mathf.Abs(Mathf.DeltaAngle(yawRel, FacingYaw(target)));
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
                    // 对准后目标精灵放大发光（动画替代"对准了保持住"文字）
                    _targetGlow = Mathf.Lerp(_targetGlow, 1f, Time.deltaTime * 6f);
                    target.localScale = Vector3.one * (2.2f + _targetGlow * 0.5f);
                    if (_alignedTimer >= alignedHoldSeconds * 2f) FoundTarget(target);
                }
                else if (facing != null && facing != target && still)
                {
                    _alignedTimer = 0f;
                    _wrongTimer += Time.deltaTime;
                    _targetGlow = Mathf.Lerp(_targetGlow, 0f, Time.deltaTime * 6f);
                    target.localScale = Vector3.one * (2.2f + _targetGlow * 0.5f);
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
                        // 找错：轻微摇头动画 + 语音，不用文字
                        _feedback.color = KidVisualFactory.CandyYellow;
                        _feedback.text = "？";
                        if (enableVoice) KidVisualFactory.Speak("再找找看");
                    }
                }
                else
                {
                    _alignedTimer = 0f;
                    _wrongTimer = 0f;
                    _targetGlow = Mathf.Lerp(_targetGlow, 0f, Time.deltaTime * 6f);
                    target.localScale = Vector3.one * (2.2f + _targetGlow * 0.5f);
                }
            }

            UpdateHud(yawRel);
        }

        private void AnimateSpirits()
        {
            for (int i = 0; i < _spirits.Length; i++)
            {
                if (_spirits[i] == null) continue;
                float bob = Mathf.Sin(Time.time * 2f + _spiritPhase[i]) * 0.12f;
                _spirits[i].position = _spiritBasePos[i] + Vector3.up * bob;
            }
        }

        private float ReadYawRelative()
        {
            var imu = IMUReceiver.Instance;
            if (imu != null && imu.IsConnected)
            {
                if (Input.GetKey(KeyCode.A)) _keyYawOffset += 70f * Time.deltaTime;
                if (Input.GetKey(KeyCode.D)) _keyYawOffset -= 70f * Time.deltaTime;
                return Mathf.DeltaAngle(_yawRef, imu.Yaw) + _keyYawOffset;
            }

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
                string name = target.name;
                events.Log(GameEvents.TargetFound, targetId: name, response: name, isCorrect: true);
                events.Log(GameEvents.TaskSuccess, targetId: name, isCorrect: true,
                           extra: $"trial={_trialIndex + 1}/{trialCount}");
            }

            TaskResultsWriter.Append(2, events != null ? events.TrialId : string.Empty, true, completionMs,
                                     targetId: target.name,
                                     wrongTargetCount: _wrongTargetCount.ToString(),
                                     alignmentTimeMs: _alignedAtMs >= 0f ? _alignedAtMs.ToString("0") : "");

            _feedback.text = string.Empty;
            _successCard.SetActive(true);
            if (enableVoice) KidVisualFactory.Speak("找到啦！你真棒！");
            StartCoroutine(NextTrialAfterDelay());
        }

        private System.Collections.IEnumerator NextTrialAfterDelay()
        {
            yield return new WaitForSeconds(1.6f);
            int next = _trialIndex + 1;
            if (next >= trialCount)
            {
                _allDone = true;
                _successCard.SetActive(false);
                _feedback.color = KidVisualFactory.CandyGreen;
                _feedback.text = "全找到啦！";
                if (enableVoice) KidVisualFactory.Speak("全部找到，你真棒！");
            }
            else
            {
                _wrongTargetCount = 0;
                StartTrial(next);
            }
        }

        private void UpdateHud(float yawRel)
        {
            if (_hud == null || !_hud.gameObject.activeSelf) return;
            var imu = IMUReceiver.Instance;
            string conn = imu != null && imu.IsConnected ? "已连接" : "键盘 A/D 演示";
            _hud.text = $"{conn}  yaw={yawRel:0.0}  角速度={_angVelSmooth:0}°/s\n" +
                        $"第 {_trialIndex + 1}/{trialCount} 轮  找错 {_wrongTargetCount} 次";
        }

        private void OnDestroy()
        {
            KidVisualFactory.ShutdownSpeech();
        }
    }
}
