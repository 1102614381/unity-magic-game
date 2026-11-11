using System;
using UnityEngine;

namespace MagicIsland.Core
{
    /// <summary>
    /// 统一事件名称（需求说明书 7.5）。新增事件前先和研究生确认，不要各写各的。
    /// </summary>
    public static class GameEvents
    {
        // 公共事件
        public const string SessionStart = "SESSION_START";
        public const string SessionEnd = "SESSION_END";
        public const string TaskStart = "TASK_START";
        public const string TaskEnd = "TASK_END";
        public const string TrialStart = "TRIAL_START";
        public const string TrialEnd = "TRIAL_END";
        public const string TargetShow = "TARGET_SHOW";
        public const string ActionStart = "ACTION_START";
        public const string ActionEnd = "ACTION_END";
        public const string ResponseSubmit = "RESPONSE_SUBMIT";
        public const string TaskSuccess = "TASK_SUCCESS";
        public const string TaskFailed = "TASK_FAILED";
        public const string PromptGiven = "PROMPT_GIVEN";

        // 游戏1 专有
        public const string ObstacleCollision = "OBSTACLE_COLLISION";
        public const string TargetReached = "TARGET_REACHED";

        // 游戏2 专有
        public const string TargetFound = "TARGET_FOUND";
        public const string WrongSelection = "WRONG_SELECTION";

        // 游戏3 专有
        public const string OptionSelected = "OPTION_SELECTED";
        public const string ChoiceConfirmed = "CHOICE_CONFIRMED";

        // 游戏4 专有
        public const string SequenceShow = "SEQUENCE_SHOW";
        public const string SequenceInput = "SEQUENCE_INPUT";
        public const string SequenceCorrect = "SEQUENCE_CORRECT";
        public const string SequenceWrong = "SEQUENCE_WRONG";
    }

    /// <summary>
    /// 游戏事件记录器：写出 game_events.csv，列名与需求说明书 7.4 表格严格一致。
    /// 由 GameManager 自动创建（DontDestroyOnLoad），不需要手动挂载。
    /// </summary>
    public class GameEventManager : MonoBehaviour
    {
        public static GameEventManager Instance { get; private set; }

        public const string Header =
            "event_id,session_id,participant_id,trial_id,task_id,event_type,event_timestamp_ms," +
            "unity_elapsed_ms,target_id,response,is_correct,difficulty,prompt_level,score,extra";

        [Header("输出")]
        [Tooltip("关闭后只打 Console，不写 CSV（调试用）")]
        [SerializeField] private bool writeToDisk = true;
        [SerializeField] private bool echoToConsole = false;

        private string _filePath = string.Empty;
        private string _sessionId = string.Empty;
        private string _participantId = string.Empty;
        private string _trialId = string.Empty;
        private int _taskId;
        private string _taskPhase = string.Empty;
        private string _difficulty = string.Empty;
        private int _promptLevel;
        private string _score = string.Empty;

        private int _eventSeq;
        private float _sessionStartRealtime = -1f;

        public string FilePath => _filePath;
        public bool SessionActive => _sessionStartRealtime >= 0f;
        public float ElapsedMs => SessionActive
            ? (Time.realtimeSinceStartup - _sessionStartRealtime) * 1000f
            : 0f;

        public int TaskId
        {
            get => _taskId;
            set => _taskId = value;
        }

        public string TaskPhase
        {
            get => _taskPhase;
            set => _taskPhase = value;
        }

        public string TrialId
        {
            get => _trialId;
            set => _trialId = value;
        }

        public string Difficulty
        {
            get => _difficulty;
            set => _difficulty = value;
        }

        public int PromptLevel
        {
            get => _promptLevel;
            set => _promptLevel = value;
        }

        public string Score
        {
            get => _score;
            set => _score = value;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>开始一次采集会话：建立 game_events.csv 并记录 SESSION_START。</summary>
        public void BeginSession(string participantId, string sessionId, string sessionFolder)
        {
            _participantId = participantId ?? string.Empty;
            _sessionId = sessionId ?? string.Empty;
            _filePath = string.IsNullOrEmpty(sessionFolder)
                ? string.Empty
                : System.IO.Path.Combine(sessionFolder, "game_events.csv");

            _eventSeq = 0;
            _taskId = 0;
            _trialId = string.Empty;
            _taskPhase = string.Empty;
            _difficulty = string.Empty;
            _promptLevel = 0;
            _score = string.Empty;

            if (writeToDisk && !string.IsNullOrEmpty(_filePath))
            {
                CsvLogWriter.EnsureFile(_filePath, Header);
            }

            _sessionStartRealtime = Time.realtimeSinceStartup;
            Log(GameEvents.SessionStart);
            Debug.Log($"[GameEventManager] 会话开始 session={_sessionId} participant={_participantId}");
        }

        public void EndSession()
        {
            if (!SessionActive) return;
            Log(GameEvents.SessionEnd);
            _sessionStartRealtime = -1f;
        }

        /// <summary>
        /// 记录一条事件。
        /// event_timestamp_ms 为 Unity 侧 Unix 毫秒（联调阶段 Python 会另记自己的接收时刻，两者不默认同步）。
        /// </summary>
        public void Log(string eventType,
                        string targetId = "",
                        string response = "",
                        bool? isCorrect = null,
                        string extra = "")
        {
            if (string.IsNullOrEmpty(eventType)) return;

            long utcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            float elapsedMs = ElapsedMs;
            _eventSeq++;

            string eventId = $"{_sessionId}_E{_eventSeq:000000}";
            string correct = isCorrect.HasValue ? (isCorrect.Value ? "true" : "false") : "";
            string extraText = string.IsNullOrEmpty(_taskPhase) ? extra : $"{extra};phase={_taskPhase}";

            if (writeToDisk && !string.IsNullOrEmpty(_filePath))
            {
                CsvLogWriter.Append(_filePath,
                    eventId, _sessionId, _participantId, _trialId, _taskId, eventType,
                    utcMs, elapsedMs, targetId, response, correct,
                    _difficulty, _promptLevel, _score, extraText);
            }

            if (echoToConsole)
            {
                Debug.Log($"[Event] {eventType} task={_taskId} trial={_trialId} target={targetId} resp={response} correct={correct}");
            }
        }
    }
}
