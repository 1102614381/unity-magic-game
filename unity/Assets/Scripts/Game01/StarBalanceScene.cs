using MagicIsland.Core;
using MagicIsland.IMU;
using MagicIsland.UI;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.Game01
{
    /// <summary>
    /// G1 星星平衡岛 V3（低龄儿童版·深夜睡觉主题）：
    /// 判定/事件与 V1.1 完全一致；视觉改为 2D 深夜星空 + 睡帽星星 + 白云站点。
    /// 2D 精灵统一用 SpriteRenderer 且 rotation=identity（正面朝相机，绝不倒置）。
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
        [SerializeField] private bool enableVoice = true;
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
            // 2D 俯视：相机正对 XZ 平面，夜空贴图作为底图
            cam.transform.position = new Vector3(0f, 18f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.orthographic = true;
            cam.orthographicSize = 11f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.11f);

            // 深夜星空底图：Unity 的 Plane 默认就水平（法线朝上），切勿旋转，否则会竖起来
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "NightSkyGround";
            ground.transform.localScale = new Vector3(2.4f, 1f, 2.4f); // 24 x 24
            var groundRenderer = ground.GetComponent<Renderer>();
            var nightTex = Resources.Load<Texture2D>("Art/bg_night_sky");
            var groundMat = MakeUnlit(nightTex != null ? Color.white : new Color(0.04f, 0.05f, 0.13f));
            if (nightTex != null) groundMat.mainTexture = nightTex;
            groundRenderer.material = groundMat;
            Destroy(ground.GetComponent<Collider>());

            // 球：保留物理球体（不可见），视觉用睡帽星星，直径约 1.4 世界单位
            var ballGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballGo.name = "StarBall";
            ballGo.transform.position = new Vector3(0f, 0.5f, 0f);
            ballGo.transform.localScale = Vector3.one;
            ballGo.GetComponent<Renderer>().enabled = false;
            _ball = ballGo.AddComponent<Rigidbody>();
            _ball.mass = 1f;
            _ball.drag = 2.6f;
            _ball.angularDrag = 3f;
            _ball.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

            // 关键：精灵正对相机且正立。法线朝相机(+Y)，图片上方对齐相机的 up 方向。
            // 用 LookRotation(cam.forward 的反方向, cam.up) 保证绝不倒置。
            Quaternion faceCam = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);

            var starSprite = KidVisualFactory.LoadSprite("star_sleepy");
            if (starSprite == null) starSprite = KidVisualFactory.LoadSprite("prop_star_ball");
            var ballVisualGo = new GameObject("StarBallVisual");
            ballVisualGo.transform.SetParent(ballGo.transform, false);
            ballVisualGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            ballVisualGo.transform.rotation = faceCam;
            ballVisualGo.transform.localScale = Vector3.one * 1.4f;
            var ballSr = ballVisualGo.AddComponent<SpriteRenderer>();
            ballSr.sprite = starSprite;
            ballSr.sortingOrder = 10;
            if (starSprite == null)
            {
                ballGo.GetComponent<Renderer>().enabled = true;
                ballGo.GetComponent<Renderer>().material = MakeUnlit(KidVisualFactory.CandyYellow);
            }
            _ballVisual = ballVisualGo.transform;

            // 目标站点：白色云朵（下方判定圆柱沿用，视觉云朵比原黄圈更小更柔和）
            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "TargetRing";
            Destroy(ringGo.GetComponent<Collider>());
            ringGo.transform.localScale = new Vector3(targetRadius * 2f, 0.02f, targetRadius * 2f);
            ringGo.GetComponent<Renderer>().enabled = false;
            _ring = ringGo.transform;

            var ringVisualGo = new GameObject("CloudStationVisual");
            ringVisualGo.transform.SetParent(ringGo.transform, false);
            ringVisualGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
            ringVisualGo.transform.rotation = faceCam; // 正立
            // 云朵贴图宽≈512px ppu=512 → 1 张图=1 单位；缩到约 2.6 单位，明显小于原黄圈
            ringVisualGo.transform.localScale = Vector3.one * 2.6f;
            var ringSr = ringVisualGo.AddComponent<SpriteRenderer>();
            ringSr.sprite = KidVisualFactory.LoadSprite("cloud_station");
            ringSr.sortingOrder = 5;
            _ringVisual = ringVisualGo.transform;

            // 装饰：几颗小星星散落，营造夜空氛围（尺寸小，不遮挡玩法）
            AddFlatSprite(faceCam, new Vector3(-8f, 0f, -4f), "icon_star_medal", 0.7f, 2);
            AddFlatSprite(faceCam, new Vector3(8f, 0f, 3f), "icon_star_medal", 0.6f, 2);
            AddFlatSprite(faceCam, new Vector3(-6f, 0f, 7f), "icon_star_medal", 0.5f, 2);
            AddFlatSprite(faceCam, new Vector3(7f, 0f, -8f), "icon_star_medal", 0.55f, 2);
        }

        private void AddFlatSprite(Quaternion rot, Vector3 pos, string spriteName, float size, int order)
        {
            var go = new GameObject("Deco_" + spriteName);
            go.transform.position = pos + Vector3.up * 0.12f;
            go.transform.rotation = rot; // 正对相机且正立
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = KidVisualFactory.LoadSprite(spriteName);
            sr.color = new Color(1f, 1f, 1f, 0.95f);
            sr.sortingOrder = order;
        }

        private static Material MakeUnlit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        private void BuildHud()
        {
            var canvas = UIFactory.CreateCanvas("Canvas_G1", false);

            var starIcon = KidVisualFactory.LoadSprite("icon_star_medal");
            KidVisualFactory.CreateSpriteImage(canvas.transform, starIcon, 70f, 70f,
                                               new Vector2(0.5f, 1f), new Vector2(0f, -56f), "TitleStar");
            var title = UIFactory.CreateText(canvas.transform, "睡觉星星岛", 38, TextAnchor.UpperCenter,
                                             Color.white, false);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(400f, 66f);
            titleRt.anchoredPosition = new Vector2(0f, -104f);

            // 提示卡：魔法棒图标 + 极短文字
            var wandIcon = KidVisualFactory.LoadSprite("wand_magic");
            KidVisualFactory.CreateHintCard(canvas.transform, wandIcon, "飞到云朵上",
                                            new Color(0.35f, 0.45f, 0.82f),
                                            new Vector2(0.5f, 0f), new Vector2(0f, 52f), 620f, 120f);

            _holdCard = new GameObject("HoldBarBg", typeof(RectTransform), typeof(Image));
            _holdCard.transform.SetParent(canvas.transform, false);
            var holdBg = _holdCard.GetComponent<Image>();
            holdBg.color = new Color(0f, 0f, 0f, 0.25f);
            holdBg.raycastTarget = false;
            var hbRt = _holdCard.GetComponent<RectTransform>();
            hbRt.anchorMin = new Vector2(0.5f, 0.5f);
            hbRt.anchorMax = new Vector2(0.5f, 0.5f);
            hbRt.sizeDelta = new Vector2(360f, 30f);
            hbRt.anchoredPosition = new Vector2(0f, -170f);
            var fillGo = new GameObject("HoldBarFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(_holdCard.transform, false);
            _holdFill = fillGo.GetComponent<Image>();
            _holdFill.color = new Color(0.85f, 0.9f, 1f);
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

            _medal = KidVisualFactory.CreateSpriteImage(canvas.transform, starIcon, 180f, 180f,
                                                        new Vector2(0.5f, 0.72f), Vector2.zero, "Medal");
            _medal.gameObject.AddComponent<KidSpin>().degreesPerSecond = 50f;
            _medal.gameObject.SetActive(false);

            _result = UIFactory.CreateText(canvas.transform, string.Empty, 64, TextAnchor.MiddleCenter,
                                           Color.white, false);
            var resultRect = _result.rectTransform;
            resultRect.anchorMin = new Vector2(0.5f, 0.56f);
            resultRect.anchorMax = new Vector2(0.5f, 0.56f);
            resultRect.pivot = new Vector2(0.5f, 0.5f);
            resultRect.sizeDelta = new Vector2(1200f, 120f);

            _hud = UIFactory.CreateText(canvas.transform, string.Empty, 22, TextAnchor.LowerLeft,
                                        new Color(0.9f, 0.9f, 1f, 0.6f), false);
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

            if (index == 0 && enableVoice) KidVisualFactory.Speak("把星星送到云朵上睡觉觉");
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
                _ball.velocity = _ball.velocity.normalized * maxSpeed;
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

            // 云朵轻微呼吸（幅度很小，不会变大到遮挡）
            float pulse = 1f + Mathf.Sin(Time.time * 3f) * 0.03f;
            _ringVisual.localScale = Vector3.one * (2.6f * pulse);

            if (_ballVisual != null)
                _ballVisual.localPosition = new Vector3(0f, 0.1f + Mathf.Sin(Time.time * 4f) * 0.02f, 0f);

            if (_trialDone)
            {
                _celebrateTimer += Time.deltaTime;
                if (_medal != null && _medal.gameObject.activeSelf)
                {
                    float s = 1f + Mathf.Sin(_celebrateTimer * 8f) * 0.1f;
                    _medal.rectTransform.localScale = Vector3.one * s;
                }
            }

            UpdateHud();
            if (_trialDone && !_allDone && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                NextTrial();
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
            _result.text = "睡得香！";
            if (enableVoice) KidVisualFactory.Speak("太棒啦！星星睡着啦！");
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

        private void OnDestroy() { KidVisualFactory.ShutdownSpeech(); }
    }
}
