using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    /// <summary>
    /// 低龄儿童视觉/语音工具（G1、G2 共用）。
    /// 目标：少字、多图、多动画、有语音；所有 API 在资源缺失时安全降级，不报错。
    /// 语音走 Windows 内置 SAPI（System.Speech），编辑器/PC 可用；
    /// 用反射调用，避免工程缺少程序集引用时编译不过。
    /// </summary>
    public static class KidVisualFactory
    {
        // ---------- 配色：低龄儿童偏好的明亮柔和色 ----------
        public static readonly Color CandyPink = new Color(1.00f, 0.62f, 0.72f);
        public static readonly Color CandyYellow = new Color(1.00f, 0.84f, 0.35f);
        public static readonly Color CandyBlue = new Color(0.45f, 0.78f, 1.00f);
        public static readonly Color CandyGreen = new Color(0.55f, 0.88f, 0.60f);
        public static readonly Color CandyPurple = new Color(0.74f, 0.58f, 0.96f);

        // ---------- 资源加载 ----------
        /// <summary>
        /// 从 Resources/Art 加载精灵；不存在返回 null。
        /// pixelsPerUnit 默认取图片宽度，使每张图 ≈ 1 个世界单位（避免 1024px 图变成 10 单位撑爆场景）。
        /// 需要精确控制大小时可显式传入。
        /// </summary>
        public static Sprite LoadSprite(string name, float pixelsPerUnit = 0f)
        {
            var tex = Resources.Load<Texture2D>("Art/" + name);
            if (tex == null) return null;
            if (pixelsPerUnit <= 0f) pixelsPerUnit = tex.width;
            return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                 new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        /// <summary>创建一张 2D 图片（UI），带可选点击回调。</summary>
        public static Image CreateSpriteImage(Transform parent, Sprite sprite, float width, float height,
                                              Vector2 anchor, Vector2 position, string name = "KidImage")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.preserveAspect = true;
            }
            img.color = Color.white;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = position;
            return img;
        }

        /// <summary>
        /// 大图标 + 极短文字的提示卡（替代整段说明文字）。
        /// 低龄儿童主要看图，文字只留 2-4 个字做辅助。
        /// </summary>
        public static GameObject CreateHintCard(Transform parent, Sprite icon, string shortText,
                                                Color cardColor, Vector2 anchor, Vector2 position,
                                                float width = 720f, float height = 180f)
        {
            var card = new GameObject("HintCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            var bg = card.GetComponent<Image>();
            bg.color = new Color(cardColor.r, cardColor.g, cardColor.b, 0.92f);
            bg.raycastTarget = false;
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = position;

            if (icon != null)
            {
                CreateSpriteImage(card.transform, icon, 120f, 120f,
                                  new Vector2(0f, 0.5f), new Vector2(-width * 0.5f + 90f, 0f), "HintIcon");
            }

            if (!string.IsNullOrEmpty(shortText))
            {
                var t = UIFactory.CreateText(card.transform, shortText, 52, TextAnchor.MiddleCenter,
                                             Color.white, false);
                var trt = t.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(icon != null ? 150f : 20f, 0f);
                trt.offsetMax = new Vector2(-20f, 0f);
            }

            // 轻微呼吸动画：让提示卡"活"起来，吸引低龄儿童注意
            var bob = card.AddComponent<KidBob>();
            bob.amplitude = 8f;
            bob.speed = 2.2f;
            return card;
        }

        /// <summary>
        /// 竖排提示牌：图标在上、文字竖排在下，贴在界面最左边。
        /// 适合低龄儿童——整块立在左侧，不遮挡中间玩法区。
        /// </summary>
        public static GameObject CreateVerticalHintCard(Transform parent, Sprite icon, string text,
                                                        Color cardColor, Vector2 anchor, Vector2 position,
                                                        float width = 150f, float height = 460f,
                                                        float fontSize = 44f)
        {
            var card = new GameObject("VerticalHintCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(parent, false);
            var bg = card.GetComponent<Image>();
            bg.color = new Color(cardColor.r, cardColor.g, cardColor.b, 0.9f);
            bg.raycastTarget = false;
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = position;

            // 顶部图标
            if (icon != null)
            {
                CreateSpriteImage(card.transform, icon, 110f, 110f,
                                  new Vector2(0.5f, 1f), new Vector2(0f, -78f), "VerticalHintIcon");
            }

            // 竖排文字（每字一行）
            if (!string.IsNullOrEmpty(text))
            {
                var t = UIFactory.CreateText(card.transform, UIFactory.ToVertical(text), fontSize,
                                             TextAnchor.UpperCenter, Color.white, false);
                var trt = t.rectTransform;
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.offsetMin = new Vector2(0f, 24f);
                trt.offsetMax = new Vector2(0f, -160f);
                t.lineSpacing = 1.15f;
            }

            var bob = card.AddComponent<KidBob>();
            bob.amplitude = 6f;
            bob.speed = 2f;
            return card;
        }

        // ---------- 语音 ----------
        private static object _speechSynth;
        private static MethodInfo _speakMethod;
        private static MethodInfo _selectVoiceMethod;
        private static bool _speechTried;

        /// <summary>
        /// 中文语音播报（Windows SAPI）。无可用语音引擎时静默跳过，绝不影响游戏流程。
        /// 只说短句，符合低龄儿童"听指令"的习惯。
        /// </summary>
        public static void Speak(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                EnsureSpeech();
                if (_speakMethod != null && _speechSynth != null)
                {
                    _speakMethod.Invoke(_speechSynth, new object[] { text });
                }
            }
            catch (Exception)
            {
                // 语音不可用（非 Windows / 无中文语音包）时静默降级为纯视觉提示
            }
        }

        private static void EnsureSpeech()
        {
            if (_speechTried) return;
            _speechTried = true;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                var type = Type.GetType("System.Speech.Synthesis.SpeechSynthesizer, System.Speech");
                if (type == null) return;
                _speechSynth = Activator.CreateInstance(type);
                _speakMethod = type.GetMethod("Speak", new[] { typeof(string) });

                // 优先选中文语音（Microsoft Huihui / Yaoyao / Kangkang）
                _selectVoiceMethod = type.GetMethod("SelectVoice", new[] { typeof(string) });
                if (_selectVoiceMethod != null && _speechSynth != null)
                {
                    string[] voices = { "Microsoft Huihui Desktop", "Microsoft Yaoyao Desktop",
                                        "Microsoft Kangkang Desktop", "Huihui", "Yaoyao" };
                    foreach (var v in voices)
                    {
                        try { _selectVoiceMethod.Invoke(_speechSynth, new object[] { v }); break; }
                        catch { /* 该语音不存在，试下一个 */ }
                    }
                }

                // 语速放慢，适合低龄儿童
                var rateProp = type.GetProperty("Rate");
                if (rateProp != null && rateProp.CanWrite) rateProp.SetValue(_speechSynth, -2, null);
            }
            catch (Exception)
            {
                _speechSynth = null;
                _speakMethod = null;
            }
#endif
        }

        /// <summary>关闭语音（场景销毁时可调用，释放 COM 资源）。</summary>
        public static void ShutdownSpeech()
        {
            if (_speechSynth is IDisposable d)
            {
                try { d.Dispose(); } catch { }
            }
            _speechSynth = null;
            _speakMethod = null;
            _speechTried = false;
        }
    }

    /// <summary>上下浮动动画：给图标/精灵加"呼吸感"。</summary>
    public class KidBob : MonoBehaviour
    {
        public float amplitude = 10f;
        public float speed = 2f;
        private Vector3 _basePos;
        private float _t;

        private void Start() { _basePos = transform.localPosition; _t = UnityEngine.Random.Range(0f, 6f); }
        private void Update()
        {
            _t += Time.deltaTime * speed;
            transform.localPosition = _basePos + new Vector3(0f, Mathf.Sin(_t) * amplitude, 0f);
        }
    }

    /// <summary>自转动画：给星星/奖章加"闪亮"感。</summary>
    public class KidSpin : MonoBehaviour
    {
        public float degreesPerSecond = 40f;
        private void Update() { transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime); }
    }
}
