using MagicIsland.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    /// <summary>
    /// 被试信息录入页：只收匿名编号，不采集姓名，符合需求说明书的匿名管理要求。
    /// 确认后建立 Dataset/P00X/Session_00N 目录并开始记录会话事件。
    /// </summary>
    public class ParticipantSceneUI : MonoBehaviour
    {
        [Header("界面")]
        [SerializeField] private bool buildUIInCode = true;

        private InputField _participantInput;
        private InputField _ageInput;
        private InputField _groupInput;
        private InputField _notesInput;
        private Text _previewText;
        private bool _confirmed;

        private void Start()
        {
            if (buildUIInCode) BuildUI();
            RefreshPreview();
        }

        public void Confirm()
        {
            if (_confirmed) return;   // 防止连点建出两个会话目录
            _confirmed = true;

            string id = _participantInput != null ? _participantInput.text : string.Empty;
            string age = _ageInput != null ? _ageInput.text : string.Empty;
            string group = _groupInput != null ? _groupInput.text : string.Empty;
            string notes = _notesInput != null ? _notesInput.text : string.Empty;

            var info = ParticipantManager.Instance.StartSession(id, age, group, notes);
            GameEventManager.Instance.BeginSession(info.ParticipantId, info.SessionId, info.SessionFolder);

            GameManager.Instance.GoToMainMenu();
        }

        public void Back()
        {
            GameManager.Instance.GoToStart();
        }

        private void RefreshPreview()
        {
            if (_previewText == null || ParticipantManager.Instance == null) return;

            string currentId = _participantInput != null ? _participantInput.text : string.Empty;
            string nextId = string.IsNullOrEmpty(currentId)
                ? ParticipantManager.Instance.SuggestNextParticipantId()
                : currentId;
            string sessionId = ParticipantManager.Instance.PredictNextSessionId(nextId);

            _previewText.text = $"下一次会话将保存到：Dataset / {nextId} / {sessionId}";
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("Canvas_Participant");
            var content = UIFactory.CreateVerticalContainer(canvas.transform, 520f, 16f, 60f);

            UIFactory.CreateText(content, "被试信息", 60, TextAnchor.MiddleCenter, UIFactory.Primary);
            UIFactory.CreateText(content, "只填匿名编号，不要填写姓名等身份信息", 28,
                                 TextAnchor.MiddleCenter, UIFactory.TextMuted);

            string suggestedId = ParticipantManager.Instance != null
                ? ParticipantManager.Instance.SuggestNextParticipantId()
                : "P001";

            UIFactory.CreateText(content, "被试编号（留空自动分配）", 26, TextAnchor.MiddleLeft, UIFactory.TextMuted);
            _participantInput = UIFactory.CreateInputField(content, suggestedId, 76f, 16);
            _participantInput.text = suggestedId;
            _participantInput.onValueChanged.AddListener(_ => RefreshPreview());

            UIFactory.CreateText(content, "年龄", 26, TextAnchor.MiddleLeft, UIFactory.TextMuted);
            _ageInput = UIFactory.CreateInputField(content, "例如 7", 76f, 4);

            UIFactory.CreateText(content, "组别 / 备注（可选）", 26, TextAnchor.MiddleLeft, UIFactory.TextMuted);
            _groupInput = UIFactory.CreateInputField(content, "例如 实验组 / 对照组", 76f, 24);
            _notesInput = UIFactory.CreateInputField(content, "其他需要记录的说明", 76f, 64);

            _previewText = UIFactory.CreateText(content, "", 24, TextAnchor.MiddleCenter, UIFactory.TextMuted);

            var row = UIFactory.CreateHorizontalRow(content, 24f, 110f);
            UIFactory.CreateButton(row, "返回", 100f, Back, 32f);
            UIFactory.CreateButton(row, "开始采集", 100f, Confirm, 32f);
        }
    }
}
