using MagicIsland.Core;
using MagicIsland.IMU;
using MagicIsland.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.Game01
{
    /// <summary>
    /// G1 星星平衡岛（阶段B 交互原型 V2.0 · 低龄儿童版）：
    /// 判定与事件上报完全沿用 V1.1（球体 + Rigidbody、XZ 平面滚动、进圈保持 2 秒）；
    /// 视觉改为：花草地面 + 星星球贴图 + 发光星星目标圈 + 大图标提示 + 语音；
    /// HUD 只剩极简图形提示，debug 数值仅在编辑器显示。
    /// 场景里只需一个空物体挂本脚本。
    /// </summary>
    public class StarBalanceScene : MonoBehaviour
    {
        [Header("判定（勿改，与数据表一致）")]
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
        [Tooltip("方向键助力量（等效倾斜角），IMU 连接时也叠加生效")]
        [SerializeField] private float keyboardAssistDegrees = 25f;

        [Header("低龄儿童版选项")]
        [Tooltip("关闭语音时仅保留图形与动画提示")]
        [SerializeField] private bool enableVoice = true;
        [Tooltip("编辑器里显示 debug 数值（roll/pitch等），打包后自动隐藏")]
        [SerializeField] private bool showDebugInEditor = true;

        private Rigidbody _ball;
        private Transform _ring;
        private Transform _ringVisual;
        private Transform _ballVisual;
        private Text _hud;
        private Text _result;
        private Image _medal;
        private GameObject _holdCard;
        private Image _holdFill;

        private Vector3 _ringCenter;
        private bool _inTarget;
        private bool _enteredOnce;
        private float _holdTimer;
        private int _outOfTargetCount;
        private float _trialStartTime;
        private int _trialIndex;
        private bool _trialDone;
        private bool _allDone;
        private float _celebrateTimer;

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
            cam.backgroundColor = new Color(0.84f, 0.95f, 0.98f);

            if (FindObjectOfType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // 地面：先建物理平面，再贴花草图（贴图缺失时回退浅绿纯色）
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f); // 20 x 20
            var groundRenderer = ground.GetComponent<Renderer>();
            var meadowTex = Resources.Load<Texture2D>("Art/bg_meadow");
            if (meadowTex != null)
            {
                meadowTex.wrapMode = TextureWrapMode.Repeat;
                var groundMat = MakeMat(Color.white);
                groundMat.mainTexture = meadowTex;
                groundMat.mainTextureScale = new Vector2(4f, 4f);
                groundRenderer.material = groundMat;
            }
            else
            {
                groundRenderer.material = MakeMat(new Color(0.78f, 0.92f, 0.70f));
            }

            // 球：保留 Rigidbody 做物理，渲染换成星星球精灵（俯视相机看得见）
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "StarBall";
            ballGo.transform.position = new Vector3(0f, 0.5f, 0f);
            ballGo.transform.localScale = Vector3.one;
            ballGo.GetComponent<Renderer>().enabled = false; // 隐藏纯色球
            _ball = ballGo.AddComponent<Rigidbody>();
            _ball.mass = 1f;
            _ball.drag = 2.6f;
            _ball.angularDrag = 3f;
            _ball.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

            var starSprite = KidVisualFactory.LoadSprite("prop_star_ball");
            var ballVisualGo = new GameObject("StarBallVisual");
            ballVisualGo.transform.SetParent(ballGo.transform, false);
            ballVisualGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            ballVisualGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // 平躺在 XZ 平面朝上
            ballVisualGo.transform.localScale = Vector3.one * 1.5f;
            var ballSr = ballVisualGo.AddComponent<SpriteRenderer>();
            ballSr.sprite = starSprite;
            ballSr.sortingOrder = 10;
            if (starSprite == null)
            {
                // 兜底：画一个黄色圆
                ballGo.GetComponent<Renderer>().enabled = true;
                ballGo.GetComponent<Renderer>().material = MakeMat(KidVisualFactory.CandyYellow);
            }
            _ballVisual = ballVisualGo.transform;

            // 目标：判定用隐形圆柱（沿用原逻辑），视觉用发光星星贴片
            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "TargetRing";
            Destroy(ringGo.GetComponent<Collider>());
            ringGo.transform.localScale = new Vector3(targetRadius * 2f, 0.02f, targetRadius * 2f);
            ringGo.GetComponent<Renderer>().enabled = false;
            _ring = ringGo.transform;

            var ringVisualGo = new GameObject("TargetRingVisual");
            ringVisualGo.transform.SetParent(ringGo.transform, false);
            ringVisualGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            ringVisualGo.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ringVisualGo.transform.localScale = Vector3.one * 1.25f;
            var ringSr = ringVisualGo.AddComponent<SpriteRenderer>();
            ringSr.sprite = MakeGlowRingSprite(256);
            ringSr.color = KidVisualFactory.CandyYellow;
            ringSr.sortingOrder = 5;
            ringVisualGo.AddComponent<KidSpin>().degreesPerSecond = 18f;
            _ringVisual = ringVisualGo.transform;

            // 场景装饰：四角蘑菇（用现有贴图，若缺失则用彩色小球代替）
            AddDecoration(new Vector3(-7f, 0f, -7f), "prop_mushrooms", KidVisualFactory.CandyPink);
            AddDecoration(new Vector3(7f, 0f, -7f), "prop_mushrooms", KidVisualFactory.CandyPurple);
            AddDecoration(new Vector3(-7f, 0f, 7f), "prop_mushrooms", KidVisualFactory.CandyBlue);
            AddDecoration(new Vector3(7f, 0f, 7f), "prop_mushrooms", KidVisualFactory.CandyGreen);
        }

        private void AddDecoration(Vector3 pos, string spriteName, Color fallbackColor)
        {
            var go = new GameObject("Deco_" + spriteName);
            go.transform.position = pos + Vector3.up * 0.02f;
            go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            go.transform.localScale = Vector3.one * 1.6f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KidVisualFactory.LoadSprite(spriteName);
            sr.color = sr.sprite != null ? Color.white : fallbackColor;
            sr.sortingOrder = 3;
        }

        /// <summary>程序生成一个柔和发光圆环精灵（无外部依赖）。</summary>
        private static Sprite MakeGlowRingSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            float c = (size - 1) * 0.5f;
            float inner = size * 0.30f, outer = size * 0.46f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a;
                    if (d < inner || d > outer) a = 0f;
                    else
                    {
                        float mid = (inner + outer) * 0.5f;
                        a = 1f - Mathf.Abs(d - mid) / (outer - inner) * 0.7f;
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
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
            var canvas = UIFactory.CreateCanvas("Canvas_G1", false);

            // 极简标题：只留一个图标 + 2 个字（低龄儿童读得的字尽量少）
            var starIcon = KidVisualFactory.LoadSprite("icon_star_medal");
            KidVisualFactory.CreateSpriteImage(canvas.transform, starIcon, 90f, 90f,
                                               new Vector2(0.5f, 1f), new Vector2(0f, -70f), "TitleStar");
            var title = UIFactory.CreateText(canvas.transform, "星星岛", 40, TextAnchor.UpperCenter,
                                             KidVisualFactory.CandyPurple, false);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(400f, 70f);
            titleRt.anchoredPosition = new Vector2(0f, -125f);

            // 大图标提示条：星星 + 圈圈图案，配极短文字"滚进圈圈"
            KidVisualFactory.CreateHintCard(canvas.transform, starIcon, "滚进圈圈",
                                            KidVisualFactory.CandyPurple,
                                            new Vector2(0.5f, 0f), new Vector2(0f, 58f), 680f, 130f);

            // 保持进度条：进入圈圈后出现，用"填满"代替"保持 2 秒"的文字
            _holdCard = new GameObject("HoldBarBg", typeof(RectTransform), typeof(Image));
            _holdCard.transform.SetParent(canvas.transform, false);
            var holdBg = _holdCard.GetComponent<Image>();
            holdBg.color = new Color(0f, 0f, 0f, 0.25f);
            holdBg.raycastTarget = false;
            var hbRt = _holdCard.GetComponent<RectTransform>();
            hbRt.anchorMin = new Vector2(0.5f, 0.5f);
            hbRt.anchorMax = new Vector2(0.5f, 0.5f);
            hbRt.sizeDelta = new Vector2(420f, 34f);
            hbRt.anchoredPosition = new Vector2(0f, -190f);
            var fillGo = new GameObject("HoldBarFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_holdCard.transform, false);
            _holdFill = fillGo.GetComponent<Image>();
            _holdFill.color = KidVisualFactory.CandyYellow;
            _holdFill.raycastTarget = false;
            var fillRt = _holdFill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            _holdFill.type = Image.Type.Filled;
            _holdFill.fillMethod = Image.FillMethod.Horizontal;
            _holdFill.fillAmount = 0f;
            _holdCard.SetActive(false);

            // 成功/结果：大星星 + 短文字
            _medal = KidVisualFactory.CreateSpriteImage(canvas.transform, starIcon, 260f, 260f,
                                                        new Vector2(0.5f, 0.72f), Vector2.zero, "Medal");
            _medal.gameObject.AddComponent<KidSpin>().degreesPerSecond = 60f;
            _medal.gameObject.SetActive(false);

            _result = UIFactory.CreateText(canvas.transform, string.Empty, 72, TextAnchor.MiddleCenter,
                                           Color.white, false);
            var resultRect = _result.rectTransform;
            resultRect.anchorMin = new Vector2(0.5f, 0.56f);
            resultRect.anchorMax = new Vector2(0.5f, 0.56f);
            resultRect.pivot = new Vector2(0.5f, 0.5f);
            resultRect.sizeDelta = new Vector2(1200f, 120f);

            // debug：仅编辑器可见，打包后不显示
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
            _inTarget = false;
            _enteredOnce = false;
            _holdTimer = 0f;
            _outOfTargetCount = 0;
            _celebrateTimer = 0f;
            _trialStartTime = Time.realtimeSinceStartup;

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(3.5f, playAreaHalf * 0.7f);
            _ringCenter = new Vector3(Mathf.Sin(angle) * dist, 0.05f, Mathf.Cos(angle) * dist);
            _ring.position = _ringCenter;

            _ball.velocity = Vector3.zero;
            _ball.transform.position = new Vector3(0f, 0.5f, 0f);

            _medal.gameObject.SetActive(false);
            _holdCard.SetActive(false);
            _result.text = index == 0 ? string.Empty : "再来一次";
            if (index > 0 && enableVoice) KidVisualFactory.Speak("再来一次");

            var events = GameEventManager.Instance;
            if (events != null)
            {
                events.TrialId = $"{events.TaskId}_T{index + 1}_{System.DateTime.Now:HHmmss}";
                events.Log(GameEvents.TargetShow, targetId: $"Ring_T{index + 1}");
            }

            if (index == 0 && enableVoice) KidVisualFactory.Speak("把星星球滚进亮亮的圈圈里");
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
            if (imu != null && imu.IsConnected)
            {
                float roll = invertRollForce ? -imu.Roll : imu.Roll;
                float pitch = invertPitchForce ? -imu.Pitch : imu.Pitch;
                force += new Vector3(roll, 0f, pitch) * forceScale;
            }

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
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                var em = GameEventManager.Instance;
                if (em != null) em.Log(GameEvents.TaskEnd, extra: "abort=esc");
                KidVisualFactory.ShutdownSpeech();
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
                if (enableVoice) KidVisualFactory.Speak("停住，棒！");
            }
            else if (!inside && _inTarget)
            {
                _inTarget = false;
                if (_enteredOnce) _outOfTargetCount++;
                if (events != null) events.Log(GameEvents.TargetExit, targetId: $"Ring_T{_trialIndex + 1}");
                _holdTimer = 0f;
                _holdCard.SetActive(false);
            }

            if (_inTarget)
            {
                _enteredOnce = true;
                _holdTimer += Time.deltaTime;
                _holdCard.SetActive(true);
                _holdFill.fillAmount = Mathf.Clamp01(_holdTimer / holdSeconds);
                if (_holdTimer >= holdSeconds) Success();
            }

            // 目标圈：靠近时变大发光，给低龄儿童"找得到"的视觉反馈
            float dist = Vector2.Distance(ballPos, ringPos);
            float pulse = 1f + Mathf.Sin(Time.time * 5f) * 0.06f;
            float near = Mathf.Clamp01(1f - dist / 6f);
            _ringVisual.localScale = Vector3.one * (1.25f * pulse + near * 0.25f);

            // 星星球轻微上下浮动（视觉层，不影响物理位置与判定）
            if (_ballVisual != null)
                _ballVisual.localPosition = new Vector3(0f, 0.05f + Mathf.Sin(Time.time * 4f) * 0.03f, 0f);

            if (_trialDone)
            {
                _celebrateTimer += Time.deltaTime;
                if (_medal != null && _medal.gameObject.activeSelf)
                {
                    float s = 1f + Mathf.Sin(_celebrateTimer * 8f) * 0.12f;
                    _medal.rectTransform.localScale = Vector3.one * s;
                }
            }

            UpdateHud();
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

            _holdCard.SetActive(false);
            _medal.gameObject.SetActive(true);
            _result.text = "太棒啦！";
            if (enableVoice) KidVisualFactory.Speak("太棒啦！星星到家啦！");
        }

        private void NextTrial()
        {
            int next = _trialIndex + 1;
            if (next >= trialCount)
            {
                _allDone = true;
                _result.text = "全完成啦！";
                if (enableVoice) KidVisualFactory.Speak("全部完成，你真棒！");
                return;
            }
            StartTrial(next);
        }

        private void UpdateHud()
        {
            if (_hud == null || !_hud.gameObject.activeSelf) return;
            var imu = IMUReceiver.Instance;
            string conn = imu != null && imu.IsConnected ? "已连接" : "等待数据";
            string roll = imu != null ? imu.Roll.ToString("0.0") : "-";
            string pitch = imu != null ? imu.Pitch.ToString("0.0") : "-";
            _hud.text = $"{conn}  roll={roll}  pitch={pitch}\n" +
                        $"第 {_trialIndex + 1}/{trialCount} 轮  出圈 {_outOfTargetCount} 次  " +
                        $"保持 {_holdTimer:0.0}/{holdSeconds:0}";
        }

        private void OnDestroy()
        {
            KidVisualFactory.ShutdownSpeech();
        }
    }
}
