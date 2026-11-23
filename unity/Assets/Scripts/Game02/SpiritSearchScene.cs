using MagicIsland.Core;
using MagicIsland.IMU;
using MagicIsland.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.Game02
{
    /// <summary>
    /// G2 寻找魔法精灵 V3（低龄儿童版·白天平原主题）：
    /// 判定/事件与 V1.1 完全一致；视觉改为 3D 白天平原（绿草、小花、白云、大太阳）。
    /// 精灵、云朵、太阳都用 billboard 面向圆心玩家且严格正立（up=世界向上）。
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

        // 事件 targetId 沿用 V1.1 中文名（数据契约不变）
        private readonly string[] _spiritNames = { "蓝精灵", "红精灵", "黄精灵", "绿精灵", "紫精灵", "橙精灵" };
        private readonly string[] _spiritSprites =
        {
            "spirit_blue", "spirit_red", "spirit_yellow",
            "spirit_green", "spirit_purple", "spirit_orange"
        };
        private readonly Color[] _spiritColors =
        {
            new Color(0.45f, 0.78f, 1.00f),
            new Color(1.00f, 0.50f, 0.50f),
            new Color(1.00f, 0.85f, 0.35f),
            new Color(0.50f, 0.90f, 0.58f),
            new Color(0.75f, 0.58f, 0.98f),
            new Color(1.00f, 0.68f, 0.38f)
        };

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

        private float[] _spiritPhase;
        private Vector3[] _spiritBasePos;
        private float _targetGlow;
        private const float SpiritScale = 1.8f;

        private int TargetIndex => _trialIndex % _spiritNames.Length;

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
            cam.transform.position = new Vector3(0f, 1.6f, 0f);
            cam.transform.rotation = Quaternion.identity;
            cam.fieldOfView = 68f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.53f, 0.80f, 0.98f); // 明亮白天蓝天
            cam.farClipPlane = 250f;
            _cameraT = cam.transform;

            if (FindObjectOfType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            RenderSettings.ambientLight = new Color(0.85f, 0.90f, 0.95f);

            // ============ 3D 白天平原：绿色草地（不用梦幻岛贴图）============
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GrassPlain";
            ground.transform.localScale = new Vector3(8f, 1f, 8f); // 80 x 80
            ground.transform.position = new Vector3(0f, -0.6f, 0f);
            ground.GetComponent<Renderer>().material = MakeMat(new Color(0.48f, 0.78f, 0.38f));

            // 小草：绿色细锥，随机散布（不遮挡视线）
            for (int i = 0; i < 70; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(3f, 16f);
                var pos = new Vector3(Mathf.Sin(a) * r, -0.35f, Mathf.Cos(a) * r);
                AddGrass(pos, Random.Range(0.35f, 0.7f));
            }
            // 小花：五颜六色的小花（花心+花瓣），点缀平原
            Color[] petal = { new Color(1f,0.55f,0.62f), new Color(1f,0.9f,0.45f),
                              new Color(0.95f,0.6f,1f), Color.white, new Color(0.6f,0.8f,1f) };
            for (int i = 0; i < 40; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(3f, 15f);
                var pos = new Vector3(Mathf.Sin(a) * r, -0.58f, Mathf.Cos(a) * r);
                AddFlower(pos, petal[i % petal.Length]);
            }

            // ============ 天空装饰：白云 + 大太阳（billboard 面向圆心玩家）============
            float[] cloudAngles = { 15f, 75f, 135f, 195f, 255f, 315f };
            foreach (float ang in cloudAngles)
            {
                float a = ang * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 30f, 9f + Random.Range(-1f, 2f), Mathf.Cos(a) * 30f);
                AddSkySprite("Cloud_" + ang, "cloud_float", pos, 9f, 2);
            }
            // 大太阳：放在东北方向高空
            {
                float a = 50f * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 34f, 15f, Mathf.Cos(a) * 34f);
                AddSkySprite("BigSun", "sun_big", pos, 12f, 3);
            }

            // ============ 6 个精灵：半径 7 圆周排布，正立朝向圆心玩家 ============
            _spirits = new Transform[_spiritNames.Length];
            _spiritPhase = new float[_spiritNames.Length];
            _spiritBasePos = new Vector3[_spiritNames.Length];

            for (int i = 0; i < _spiritNames.Length; i++)
            {
                float angle = i * 360f / _spiritNames.Length;
                var go = new GameObject("Spirit_" + _spiritNames[i]);
                var pos = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad) * 7f, 1.0f,
                                      Mathf.Cos(angle * Mathf.Deg2Rad) * 7f);
                go.transform.position = pos;
                _spiritBasePos[i] = pos;
                _spiritPhase[i] = i * 0.8f;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = KidVisualFactory.LoadSprite(_spiritSprites[i]);
                sr.color = Color.white;
                sr.sortingOrder = 10;
                go.transform.localScale = Vector3.one * SpiritScale;
                // 只取水平朝向，up 用世界向上 → 严格正立、不歪不倒
                go.transform.rotation = FaceCenter(pos);

                if (sr.sprite == null)
                {
                    var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    cap.transform.SetParent(go.transform, false);
                    cap.GetComponent<Renderer>().material = MakeMat(_spiritColors[i]);
                }
                _spirits[i] = go.transform;
            }
        }

        /// <summary>只按水平方向朝圆心，保证图片正立（up=世界向上）。</summary>
        private static Quaternion FaceCenter(Vector3 worldPos)
        {
            Vector3 toCenter = new Vector3(-worldPos.x, 0f, -worldPos.z).normalized;
            if (toCenter.sqrMagnitude < 0.001f) toCenter = Vector3.forward;
            return Quaternion.LookRotation(toCenter, Vector3.up);
        }

        private void AddGrass(Vector3 pos, float h)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = "GrassBlade";
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.position = pos;
            g.transform.localScale = new Vector3(0.06f, h * 0.5f, 0.06f);
            g.transform.rotation = Quaternion.Euler(Random.Range(-12f,12f), 0f, Random.Range(-12f,12f));
            g.GetComponent<Renderer>().material = MakeMat(new Color(0.30f + Random.Range(0f,0.12f), 0.68f, 0.28f));
        }

        private void AddFlower(Vector3 pos, Color color)
        {
            // 茎
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "FlowerStem";
            Object.Destroy(stem.GetComponent<Collider>());
            stem.transform.position = pos + Vector3.up * 0.14f;
            stem.transform.localScale = new Vector3(0.025f, 0.14f, 0.025f);
            stem.GetComponent<Renderer>().material = MakeMat(new Color(0.25f, 0.6f, 0.25f));
            // 花瓣（一个小球当花冠）
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "FlowerHead";
            Object.Destroy(head.GetComponent<Collider>());
            head.transform.position = pos + Vector3.up * 0.32f;
            head.transform.localScale = Vector3.one * 0.19f;
            head.GetComponent<Renderer>().material = MakeMat(color);
        }

        private void AddSkySprite(string name, string spriteName, Vector3 pos, float size, int order)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.transform.rotation = FaceCenter(pos);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KidVisualFactory.LoadSprite(spriteName);
            sr.sortingOrder = order;
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

            var star = KidVisualFactory.LoadSprite("icon_star_medal");
            KidVisualFactory.CreateSpriteImage(canvas.transform, star, 60f, 60f,
                                               new Vector2(0.5f, 1f), new Vector2(0f, -50f), "TitleStar");
            var title = UIFactory.CreateText(canvas.transform, "找精灵", 34, TextAnchor.UpperCenter,
                                             Color.white, false);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(400f, 62f);
            titleRt.anchoredPosition = new Vector2(0f, -94f);

            // 目标提示卡：目标精灵大图标 + 极短文字
            var hint = KidVisualFactory.CreateHintCard(canvas.transform, null, "找它",
                                                       KidVisualFactory.CandyBlue,
                                                       new Vector2(0.5f, 0.88f), Vector2.zero, 440f, 150f);
            _targetIcon = KidVisualFactory.CreateSpriteImage(hint.transform, null, 110f, 110f,
                                                             new Vector2(0f, 0.5f), new Vector2(-160f, 0f),
                                                             "TargetSpiritIcon");

            _prompt = UIFactory.CreateText(canvas.transform, string.Empty, 28, TextAnchor.MiddleCenter,
                                           Color.white, false);
            var promptRect = _prompt.rectTransform;
            promptRect.anchorMin = new Vector2(0.5f, 0.88f);
            promptRect.anchorMax = new Vector2(0.5f, 0.88f);
            promptRect.sizeDelta = new Vector2(260f, 120f);
            promptRect.anchoredPosition = new Vector2(90f, 0f);
            _prompt.gameObject.SetActive(false);

            _feedback = UIFactory.CreateText(canvas.transform, string.Empty, 56, TextAnchor.MiddleCenter,
                                             Color.white, false);
            var feedbackRect = _feedback.rectTransform;
            feedbackRect.anchorMin = new Vector2(0.5f, 0.30f);
            feedbackRect.anchorMax = new Vector2(0.5f, 0.30f);
            feedbackRect.sizeDelta = new Vector2(1000f, 100f);

            _successCard = KidVisualFactory.CreateHintCard(canvas.transform, star, "找到啦",
                                                           KidVisualFactory.CandyGreen,
                                                           new Vector2(0.5f, 0.5f), new Vector2(0f, -60f),
                                                           480f, 140f);
            _successCard.SetActive(false);

            _hud = UIFactory.CreateText(canvas.transform, string.Empty, 22, TextAnchor.LowerLeft,
                                        new Color(1f, 1f, 1f, 0.75f), false);
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

            if (_targetIcon != null)
            {
                _targetIcon.sprite = KidVisualFactory.LoadSprite(_spiritSprites[TargetIndex]);
                _targetIcon.color = Color.white;
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
                    _targetGlow = Mathf.Lerp(_targetGlow, 1f, Time.deltaTime * 6f);
                    target.localScale = Vector3.one * (SpiritScale + _targetGlow * 0.25f);
                    if (_alignedTimer >= alignedHoldSeconds * 2f) FoundTarget(target);
                }
                else if (facing != null && facing != target && still)
                {
                    _alignedTimer = 0f;
                    _wrongTimer += Time.deltaTime;
                    _targetGlow = Mathf.Lerp(_targetGlow, 0f, Time.deltaTime * 6f);
                    target.localScale = Vector3.one * (SpiritScale + _targetGlow * 0.25f);
                    if (_wrongTimer >= wrongAlignSeconds && Time.time >= _nextWrongAllowedTime)
                    {
                        _nextWrongAllowedTime = Time.time + 2.5f;
                        _wrongTimer = 0f;
                        _wrongTargetCount++;
                        if (events != null)
                            events.Log(GameEvents.WrongTarget, targetId: facing.name, response: facing.name, isCorrect: false);
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
                    target.localScale = Vector3.one * (SpiritScale + _targetGlow * 0.25f);
                }
            }

            UpdateHud(yawRel);
        }

        private void AnimateSpirits()
        {
            for (int i = 0; i < _spirits.Length; i++)
            {
                if (_spirits[i] == null) continue;
                float bob = Mathf.Sin(Time.time * 2f + _spiritPhase[i]) * 0.14f;
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

        private void ApplyCameraYaw(float yawRel) { _cameraT.rotation = Quaternion.Euler(0f, yawRel, 0f); }

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
                if (angle < bestAngle) { bestAngle = angle; best = spirit; }
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

        private void OnDestroy() { KidVisualFactory.ShutdownSpeech(); }
    }
}
