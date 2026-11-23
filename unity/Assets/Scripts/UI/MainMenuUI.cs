using MagicIsland.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    /// <summary>
    /// 魔法岛主页：4 座岛入口 + 进度星数占位。
    /// 本周四个关卡先指向占位场景，第二阶段再替换成正式关卡。
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        [Header("界面")]
        [SerializeField] private bool buildUIInCode = true;

        private Text _sessionText;
        private Text _messageText;

        private void Start()
        {
            if (buildUIInCode) BuildUI();
            RefreshHeader();
        }

        private void RefreshHeader()
        {
            if (_sessionText == null) return;

            var participant = ParticipantManager.Instance != null ? ParticipantManager.Instance.Current : null;
            _sessionText.text = participant == null
                ? "尚未建立会话（请返回档案页填写）"
                : $"被试 {participant.ParticipantId}   会话 {participant.SessionId}";
        }

        public void EnterGame(int taskId)
        {
            bool ok = GameManager.Instance.EnterGame(taskId);
            if (!ok && _messageText != null)
            {
                _messageText.text = $"任务 {taskId} 场景还没建好，请检查 Build Settings 中的场景列表。";
                _messageText.color = UIFactory.PrimaryDark;
            }
        }

        public void EndSessionAndBack()
        {
            GameEventManager.Instance.EndSession();
            ParticipantManager.Instance.ClearCurrent();
            GameManager.Instance.GoToParticipant();
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("Canvas_MainMenu", false);

            // 梦幻岛地图作为整个选关界面的背景（四座小岛就是四个游戏的入口）
            var bgSprite = KidVisualFactory.LoadSprite("bg_island_map");
            if (bgSprite != null)
            {
                var bgGo = new GameObject("MenuBackground", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                bgGo.transform.SetParent(canvas.transform, false);
                var bgRect = bgGo.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                var bgImg = bgGo.GetComponent<UnityEngine.UI.Image>();
                bgImg.sprite = bgSprite;
                bgImg.preserveAspect = false; // 铺满整个屏幕
                bgImg.color = Color.white;
                bgImg.raycastTarget = false;
            }
            else
            {
                UIFactory.CreateImage(canvas.transform, "Background", UIFactory.Background);
            }

            // 顶部标题栏（半透明白，保证圆润标题字在背景上清晰）
            var header = new GameObject("Header", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            header.transform.SetParent(canvas.transform, false);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0f, 196f);
            headerRt.anchoredPosition = Vector2.zero;
            var headerImg = header.GetComponent<UnityEngine.UI.Image>();
            headerImg.color = new Color(1f, 1f, 1f, 0.74f);
            headerImg.raycastTarget = false;

            var title = UIFactory.CreateText(header.transform, "魔法岛", 78, TextAnchor.MiddleCenter,
                                             UIFactory.Primary, false);
            title.rectTransform.sizeDelta = new Vector2(0f, 108f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -22f);

            _sessionText = UIFactory.CreateText(header.transform, "", 30, TextAnchor.MiddleCenter,
                                                UIFactory.TextDark, false);
            _sessionText.rectTransform.sizeDelta = new Vector2(0f, 60f);
            _sessionText.rectTransform.anchoredPosition = new Vector2(0f, -134f);

            // 四个游戏入口：直接贴在背景图对应的四座小岛上（坐标按 1080x1080 原图归一化）
            // 左上星星岛 / 右上精灵岛 / 左下蘑菇岛 / 右下动物岛
            CreateIslandButton(canvas.transform, new Vector2(0.215f, 0.80f), "G1  星星平衡岛", 1);
            CreateIslandButton(canvas.transform, new Vector2(0.785f, 0.80f), "G2  寻找魔法精灵", 2);
            CreateIslandButton(canvas.transform, new Vector2(0.215f, 0.47f), "G3  帮助森林伙伴", 3);
            CreateIslandButton(canvas.transform, new Vector2(0.785f, 0.47f), "G4  魔法记忆森林", 4);

            _messageText = UIFactory.CreateText(canvas.transform, "", 28, TextAnchor.MiddleCenter,
                                                UIFactory.PrimaryDark, false);
            _messageText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _messageText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _messageText.rectTransform.sizeDelta = new Vector2(1000f, 70f);
            _messageText.rectTransform.anchoredPosition = new Vector2(0f, 132f);

            // 底部返回按钮
            var endBtn = UIFactory.CreateButton(canvas.transform, "返回档案页", 92f, EndSessionAndBack, 34f);
            var endRt = endBtn.GetComponent<RectTransform>();
            endRt.anchorMin = new Vector2(0.5f, 0f);
            endRt.anchorMax = new Vector2(0.5f, 0f);
            endRt.pivot = new Vector2(0.5f, 0f);
            endRt.sizeDelta = new Vector2(560f, 92f);
            endRt.anchoredPosition = new Vector2(0f, 32f);
        }

        /// <summary>在指定的小岛位置创建可点击的游戏入口（半透明圆润标签浮在岛上）。</summary>
        private void CreateIslandButton(Transform parent, Vector2 anchor, string label, int taskId)
        {
            var go = new GameObject("IslandButton_" + taskId, typeof(RectTransform),
                                    typeof(UnityEngine.UI.Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(370f, 118f);
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.30f, 0.22f, 0.58f, 0.88f); // 半透明紫色圆润底，白字清晰

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.92f, 0.88f, 1f);
            colors.pressedColor = new Color(0.78f, 0.72f, 0.92f);
            colors.selectedColor = Color.white;
            btn.colors = colors;
            btn.onClick.AddListener(() => EnterGame(taskId));

            var text = UIFactory.CreateText(go.transform, label, 42, TextAnchor.MiddleCenter, Color.white, false);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }
    }
}
