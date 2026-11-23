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

            // 梦幻岛地图作为整个选关界面的背景（四座岛入口浮在图上）
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

            // 半透明白色面板，让文字在背景图上依然清晰
            var veil = UIFactory.CreateImage(canvas.transform, "ContentVeil", new Color(1f, 1f, 1f, 0.55f));
            veil.raycastTarget = false;

            var content = UIFactory.CreateVerticalContainer(canvas.transform, 320f, 20f, 60f);

            UIFactory.CreateText(content, "魔法岛", 66, TextAnchor.MiddleCenter, UIFactory.Primary);
            _sessionText = UIFactory.CreateText(content, "", 26, TextAnchor.MiddleCenter, UIFactory.TextMuted);

            UIFactory.CreateText(content, "选择一座岛开始今天的冒险", 30, TextAnchor.MiddleCenter, UIFactory.TextDark);

            var row1 = UIFactory.CreateHorizontalRow(content, 28f, 190f);
            UIFactory.CreateButton(row1, "G1  星星平衡岛", 170f, () => EnterGame(1), 34f);
            UIFactory.CreateButton(row1, "G2  寻找魔法精灵", 170f, () => EnterGame(2), 34f);

            var row2 = UIFactory.CreateHorizontalRow(content, 28f, 190f);
            UIFactory.CreateButton(row2, "G3  帮助森林伙伴", 170f, () => EnterGame(3), 34f);
            UIFactory.CreateButton(row2, "G4  魔法记忆森林", 170f, () => EnterGame(4), 34f);

            _messageText = UIFactory.CreateText(content, "", 24, TextAnchor.MiddleCenter, UIFactory.TextMuted);

            UIFactory.CreateButton(content, "结束本次会话，返回档案页", 86f, EndSessionAndBack, 28f);
        }
    }
}
