using MagicIsland.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    /// <summary>
    /// V1.1 儿童入口（4.1）：「今天是谁来魔法岛玩呀？」+ 头像卡；
    /// 右上角齿轮进入研究者后台（4.2，PIN 防误触）。
    /// 替换第一阶段 StartSceneUI，挂在 Start 场景的空物体上。
    /// </summary>
    public class ChildEntrySceneUI : MonoBehaviour
    {
        [Header("研究者后台")]
        [Tooltip("演示用 PIN，正式部署前修改")]
        [SerializeField] private string researcherPin = "1234";

        [Header("头像卡绑定（留空则自动分配下一个编号）")]
        [SerializeField] private string avatarAParticipantId = "P001";
        [SerializeField] private string avatarBParticipantId = "P002";

        private InputField _pinInput;
        private Text _pinError;
        private GameObject _pinPanel;
        private GameObject _researcherPanel;
        private Text _researcherInfo;
        private InputField _researcherIdInput;

        private void Start()
        {
            BuildUI();
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("Canvas_ChildEntry");

            // 右上角齿轮（研究者入口）
            var gear = UIFactory.CreateButton(canvas.transform, "齿轮", 64f, ShowPinPanel, 30f);
            var gearRect = gear.GetComponent<RectTransform>();
            gearRect.anchorMin = new Vector2(1f, 1f);
            gearRect.anchorMax = new Vector2(1f, 1f);
            gearRect.pivot = new Vector2(1f, 1f);
            gearRect.anchoredPosition = new Vector2(-36f, -36f);
            gearRect.sizeDelta = new Vector2(96f, 64f);

            var content = UIFactory.CreateVerticalContainer(canvas.transform, 520f, 30f, 110f);
            UIFactory.CreateText(content, "今天是谁来魔法岛玩呀？", 72, TextAnchor.MiddleCenter, UIFactory.Primary);
            UIFactory.CreateText(content, "点一点你的头像，就可以出发啦", 32, TextAnchor.MiddleCenter, UIFactory.TextMuted);

            var row = UIFactory.CreateHorizontalRow(content, 60f, 330f);
            CreateAvatarCard(row, avatarAParticipantId, "Art/character_bunny", "小兔", "小兔");
            CreateAvatarCard(row, avatarBParticipantId, "Art/character_spirit_blue", "小蓝", "小蓝");

            BuildPinPanel(canvas.transform);
            BuildResearcherPanel(canvas.transform);
        }

        private void CreateAvatarCard(Transform parent, string participantId, string resourcePath,
                                      string displayName, string fallbackLabel)
        {
            var go = new GameObject("Avatar_" + displayName, typeof(RectTransform), typeof(Image),
                                    typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);

            var element = go.GetComponent<LayoutElement>();
            element.preferredHeight = 300f;

            var image = go.GetComponent<Image>();
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
            {
                image.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                             new Vector2(0.5f, 0.5f), 100f);
                image.preserveAspect = true;
                image.color = Color.white;
            }
            else
            {
                image.color = UIFactory.Primary; // 素材缺失时的兜底
            }

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            string pid = string.IsNullOrEmpty(participantId)
                ? ParticipantManager.Instance.SuggestNextParticipantId()
                : participantId;
            button.onClick.AddListener(() => EnterAsChild(pid, displayName));

            UIFactory.CreateText(go.transform, displayName, 30, TextAnchor.LowerCenter, Color.white, false);
        }

        private void EnterAsChild(string participantId, string displayName)
        {
            var pm = ParticipantManager.Instance;
            var events = GameEventManager.Instance;
            if (pm == null || events == null || GameManager.Instance == null)
            {
                Debug.LogError("[ChildEntrySceneUI] 全局管理器未就绪，请确认从 Start 场景进入。");
                return;
            }

            // 一次只接待一个孩子：已有活动会话则沿用，否则为该头像建新会话
            var info = pm.Current ?? pm.StartSession(participantId, string.Empty, string.Empty,
                                                     $"儿童入口:{displayName}");
            if (!events.SessionActive)
            {
                events.BeginSession(info.ParticipantId, info.SessionId, info.SessionFolder);
            }
            GameManager.Instance.GoToMainMenu();
        }

        private void BuildPinPanel(Transform canvas)
        {
            _pinPanel = new GameObject("PinPanel", typeof(RectTransform), typeof(Image));
            _pinPanel.transform.SetParent(canvas, false);
            Stretch(_pinPanel);
            _pinPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var content = UIFactory.CreateVerticalContainer(_pinPanel.transform, 420f, 26f, 320f);
            UIFactory.CreateText(content, "研究者入口", 56, TextAnchor.MiddleCenter, Color.white);
            _pinInput = UIFactory.CreateInputField(content, "请输入 PIN", 88f, 6);
            _pinInput.contentType = InputField.ContentType.Password;

            UIFactory.CreateButton(content, "确认", 92f, ConfirmPin, 36f);
            _pinError = UIFactory.CreateText(content, string.Empty, 28, TextAnchor.MiddleCenter,
                                             new Color(1f, 0.6f, 0.6f));
            _pinPanel.SetActive(false);
        }

        private void BuildResearcherPanel(Transform canvas)
        {
            _researcherPanel = new GameObject("ResearcherPanel", typeof(RectTransform), typeof(Image));
            _researcherPanel.transform.SetParent(canvas, false);
            Stretch(_researcherPanel);
            _researcherPanel.GetComponent<Image>().color = UIFactory.Background;

            var content = UIFactory.CreateVerticalContainer(_researcherPanel.transform, 420f, 24f, 120f);
            UIFactory.CreateText(content, "研究者后台", 60, TextAnchor.MiddleCenter, UIFactory.Primary);
            _researcherInfo = UIFactory.CreateText(content, CurrentSessionText(), 30,
                                                   TextAnchor.MiddleCenter, UIFactory.TextDark);
            _researcherIdInput = UIFactory.CreateInputField(content, "被试编号（留空自动分配 P00X）", 84f, 16);
            UIFactory.CreateButton(content, "建立新会话", 88f, StartSessionFromPanel, 34f);
            UIFactory.CreateButton(content, "结束当前会话", 88f, EndSessionFromPanel, 34f);
            UIFactory.CreateButton(content, "返回儿童入口", 88f, BackToChildEntry, 34f);
            _researcherPanel.SetActive(false);
        }

        private string CurrentSessionText()
        {
            var info = ParticipantManager.Instance != null ? ParticipantManager.Instance.Current : null;
            return info == null
                ? "当前没有活动会话"
                : $"当前会话：{info.ParticipantId} / {info.SessionId}";
        }

        private void ShowPinPanel()
        {
            if (_pinInput != null) _pinInput.text = string.Empty;
            if (_pinError != null) _pinError.text = string.Empty;
            _pinPanel.SetActive(true);
        }

        private void ConfirmPin()
        {
            if (_pinInput.text == researcherPin)
            {
                _pinPanel.SetActive(false);
                _researcherInfo.text = CurrentSessionText();
                _researcherPanel.SetActive(true);
            }
            else
            {
                _pinError.text = "PIN 不对，再想一想";
                _pinInput.text = string.Empty;
            }
        }

        private void StartSessionFromPanel()
        {
            var pm = ParticipantManager.Instance;
            var events = GameEventManager.Instance;
            if (pm == null || events == null) return;

            // 换被试前先收尾上一个会话
            events.EndSession();

            string id = _researcherIdInput != null ? _researcherIdInput.text : string.Empty;
            var info = pm.StartSession(id, string.Empty, string.Empty, "研究者后台建立");
            events.BeginSession(info.ParticipantId, info.SessionId, info.SessionFolder);

            _researcherIdInput.text = string.Empty;
            _researcherInfo.text = CurrentSessionText();
        }

        private void EndSessionFromPanel()
        {
            var events = GameEventManager.Instance;
            if (events != null) events.EndSession();
            ParticipantManager.Instance?.ClearCurrent();
            _researcherInfo.text = CurrentSessionText();
        }

        private void BackToChildEntry()
        {
            _researcherPanel.SetActive(false);
        }

        private static RectTransform Stretch(GameObject go)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
