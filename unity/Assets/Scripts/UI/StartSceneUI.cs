using System.Collections;
using MagicIsland.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    /// <summary>
    /// 启动页：显示标题，点击「开始」或按空格/回车后自动跳转到被试信息页。
    /// 场景里只需要一个空物体挂上本脚本（UI 由代码生成，方便先跑通流程）。
    /// </summary>
    public class StartSceneUI : MonoBehaviour
    {
        [Header("界面")]
        [SerializeField] private bool buildUIInCode = true;

        [Header("自动跳转")]
        [Tooltip("开启后停留指定秒数自动进入被试信息页；儿童实操时建议关闭，由研究者点击")]
        [SerializeField] private bool autoJump = false;
        [SerializeField] private float autoJumpSeconds = 3f;

        private bool _jumping;

        private void Start()
        {
            if (buildUIInCode) BuildUI();
            if (autoJump && autoJumpSeconds > 0f) StartCoroutine(AutoJumpRoutine());
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Enter();
            }
        }

        public void Enter()
        {
            if (_jumping) return;
            _jumping = true;
            GameManager.Instance.GoToParticipant();
        }

        private IEnumerator AutoJumpRoutine()
        {
            yield return new WaitForSeconds(autoJumpSeconds);
            Enter();
        }

        private void BuildUI()
        {
            var canvas = UIFactory.CreateCanvas("Canvas_Start");
            var content = UIFactory.CreateVerticalContainer(canvas.transform, 520f, 28f, 120f);

            UIFactory.CreateText(content, "魔法岛大冒险", 88, TextAnchor.MiddleCenter, UIFactory.Primary);
            UIFactory.CreateText(content, "握好魔法棒，我们出发啦", 34, TextAnchor.MiddleCenter, UIFactory.TextMuted);

            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(content, false);
            var spacerElement = spacer.GetComponent<LayoutElement>();
            spacerElement.minHeight = 40f;
            spacerElement.preferredHeight = 40f;

            UIFactory.CreateButton(content, "开始", 120f, Enter, 42f);

            UIFactory.CreateText(content, "按空格键也可以开始", 26, TextAnchor.MiddleCenter, UIFactory.TextMuted);

            var version = UIFactory.CreateText(canvas.transform, "魔法岛大冒险 · 第一阶段骨架", 24,
                                               TextAnchor.LowerCenter, UIFactory.TextMuted);
            var rect = version.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 30f);
            rect.sizeDelta = new Vector2(900f, 40f);
        }
    }
}
