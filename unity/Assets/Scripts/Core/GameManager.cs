using MagicIsland.IMU;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagicIsland.Core
{
    /// <summary>
    /// 全局流程控制：启动页 → 被试信息 → 魔法岛主页 → 四个任务入口。
    /// 使用前无需手动摆 Manager 物体：Bootstrap 会在第一个场景加载前自动创建。
    /// </summary>
    [DefaultExecutionOrder(-300)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public const string SceneStart = "Start";
        public const string SceneParticipant = "Participant";
        public const string SceneMainMenu = "MainMenu";

        /// <summary>四个任务场景名，下标 +1 即 task_id。</summary>
        public static readonly string[] GameSceneNames = { "Game01", "Game02", "Game03", "Game04" };

        public int CurrentTaskId { get; private set; }

        /// <summary>第一个场景加载前自动搭好全局物体，减少场景搭建工作。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            var root = new GameObject("[MagicIsland]");
            root.AddComponent<GameManager>();
            root.AddComponent<GameEventManager>();
            root.AddComponent<ParticipantManager>();
            root.AddComponent<IMUReceiver>();
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
        }

        private void Update()
        {
            // 游戏中按 Esc 返回主页（儿童误触保护：只在任务内生效）
            if (CurrentTaskId > 0 && Input.GetKeyDown(KeyCode.Escape))
            {
                GoToMainMenu();
            }
        }

        public void GoToStart()
        {
            LoadScene(SceneStart);
        }

        public void GoToParticipant()
        {
            EndCurrentTask();
            LoadScene(SceneParticipant);
        }

        public void GoToMainMenu()
        {
            EndCurrentTask();
            LoadScene(SceneMainMenu);
        }

        /// <summary>进入指定任务（1–4）。场景缺失时给出明确提示，不会静默失败。</summary>
        public bool EnterGame(int taskId)
        {
            if (taskId < 1 || taskId > GameSceneNames.Length)
            {
                Debug.LogError($"[GameManager] 非法 taskId：{taskId}");
                return false;
            }
            if (!HasSession())
            {
                Debug.LogWarning("[GameManager] 尚未建立被试会话，请先填写被试信息。");
                GoToParticipant();
                return false;
            }

            string sceneName = GameSceneNames[taskId - 1];
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameManager] 场景 {sceneName} 不在 Build Settings 中，无法进入。");
                return false;
            }

            CurrentTaskId = taskId;
            var events = GameEventManager.Instance;
            events.TaskId = taskId;
            events.TaskPhase = "TASK_GUIDE";
            events.TrialId = $"{events.TaskId}_{System.DateTime.Now:HHmmss}";
            events.Log(GameEvents.TaskStart);

            SceneManager.LoadScene(sceneName);
            return true;
        }

        private void EndCurrentTask()
        {
            if (CurrentTaskId <= 0) return;

            var events = GameEventManager.Instance;
            events.Log(GameEvents.TaskEnd);
            events.TaskId = 0;
            events.TaskPhase = string.Empty;
            CurrentTaskId = 0;
        }

        private bool HasSession()
        {
            return ParticipantManager.Instance != null && ParticipantManager.Instance.Current != null;
        }

        private static bool LoadScene(string sceneName)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[GameManager] 场景 {sceneName} 不在 Build Settings 中。" +
                               "请打开 File > Build Settings，把 Start / Participant / MainMenu / Game01..04 加进去。");
                return false;
            }
            SceneManager.LoadScene(sceneName);
            return true;
        }
    }
}
