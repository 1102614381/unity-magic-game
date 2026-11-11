using MagicIsland.Core;
using MagicIsland.IMU;
using UnityEngine;
using UnityEngine.UI;

namespace MagicIsland.UI
{
    /// <summary>
    /// 关卡占位场景：证明「主菜单 → 关卡 → 返回」流程已通，同时把实时 IMU 数据显示出来。
    /// 第二阶段把本脚本换成真正的关卡逻辑（G1/G2 由彦祖实现）。
    /// </summary>
    public class PlaceholderGameScene : MonoBehaviour
    {
        [SerializeField] private int taskId = 1;
        [SerializeField] private bool buildUIInCode = true;

        private Text _imuText;
        private float _timer;

        private void Start()
        {
            // 如果是从编辑器直接打开本场景，补一次任务开始事件（保证事件序列完整）
            var events = GameEventManager.Instance;
            if (events != null && events.TaskId == 0)
            {
                events.TaskId = taskId;
                events.TaskPhase = "PLACEHOLDER";
                events.Log(GameEvents.TaskStart);
            }

            if (buildUIInCode) BuildUI();
        }

        private void Update()
        {
            if (_imuText == null || IMUReceiver.Instance == null) return;

            _timer += Time.deltaTime;
            if (_timer < 0.1f) return;
            _timer = 0f;

            var receiver = IMUReceiver.Instance;
            string state = receiver.IsConnected ? "已连接" : "等待数据";
            _imuText.text = state +
                $"   roll {receiver.Roll,6:0.0}   pitch {receiver.Pitch,6:0.0}   yaw {receiver.Yaw,6:0.0}\n" +
                $"frame_id {receiver.RawFrame.frameId}   共 {receiver.FrameCount} 帧";
        }

        public void BackToMainMenu()
        {
            GameManager.Instance.GoToMainMenu();
        }

        private void BuildUI()
        {
            string[] names = { "星星平衡岛", "寻找魔法精灵", "帮助森林伙伴", "魔法记忆森林" };
            string title = taskId >= 1 && taskId <= names.Length
                ? $"G{taskId}  {names[taskId - 1]}"
                : $"G{taskId}";

            var canvas = UIFactory.CreateCanvas("Canvas_Game0" + taskId);
            var content = UIFactory.CreateVerticalContainer(canvas.transform, 420f, 24f, 80f);

            UIFactory.CreateText(content, title, 62, TextAnchor.MiddleCenter, UIFactory.Primary);
            UIFactory.CreateText(content, "关卡开发中 · 主骨架与 IMU 链路已就绪", 30,
                                 TextAnchor.MiddleCenter, UIFactory.TextMuted);

            _imuText = UIFactory.CreateText(content, "IMU 数据等待中", 28, TextAnchor.MiddleCenter, UIFactory.TextDark);

            UIFactory.CreateButton(content, "返回魔法岛", 96f, BackToMainMenu, 32f);
            UIFactory.CreateText(content, "按 Esc 也可以返回主页", 24, TextAnchor.MiddleCenter, UIFactory.TextMuted);
        }
    }
}
