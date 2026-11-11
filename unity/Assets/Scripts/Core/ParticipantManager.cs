using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MagicIsland.Core
{
    /// <summary>一次采集会话的基本信息（匿名，不含姓名）。</summary>
    public class ParticipantInfo
    {
        public string ParticipantId;
        public string SessionId;
        public string SessionFolder;

        public string ParticipantCsv => Path.Combine(SessionFolder, "participant_info.csv");
    }

    /// <summary>
    /// 被试与会话管理：建立 Dataset/P001/Session_001 目录并写入 participant_info.csv。
    /// 目录结构与需求说明书第九章一致。
    /// </summary>
    public class ParticipantManager : MonoBehaviour
    {
        public static ParticipantManager Instance { get; private set; }

        [Header("数据根目录")]
        [Tooltip("留空则使用 Application.persistentDataPath/Dataset；也可以填绝对路径，例如 D:/Dataset")]
        [SerializeField] private string datasetRootOverride = string.Empty;

        public ParticipantInfo Current { get; private set; }

        public string DatasetRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(datasetRootOverride)) return datasetRootOverride;
                return Path.Combine(Application.persistentDataPath, "Dataset");
            }
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

        /// <summary>建立会话目录并写被试信息。participantId 为空时自动分配下一个编号。</summary>
        public ParticipantInfo StartSession(string participantId, string age, string group, string notes)
        {
            participantId = SanitizeId(participantId);
            if (string.IsNullOrEmpty(participantId)) participantId = SuggestNextParticipantId();

            string participantFolder = Path.Combine(DatasetRoot, participantId);
            Directory.CreateDirectory(participantFolder);

            string sessionId = NextSessionId(participantFolder);
            string sessionFolder = Path.Combine(participantFolder, sessionId);
            Directory.CreateDirectory(sessionFolder);

            var info = new ParticipantInfo
            {
                ParticipantId = participantId,
                SessionId = sessionId,
                SessionFolder = sessionFolder
            };

            CsvLogWriter.EnsureFile(info.ParticipantCsv,
                "participant_id,session_id,age,group,notes,created_at");
            CsvLogWriter.Append(info.ParticipantCsv,
                participantId, sessionId,
                age ?? string.Empty, group ?? string.Empty, notes ?? string.Empty,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            Current = info;
            Debug.Log($"[ParticipantManager] 新会话：{sessionFolder}");
            return info;
        }

        public void ClearCurrent()
        {
            Current = null;
        }

        /// <summary>扫描 Dataset 目录，给出下一个被试编号（P001、P002…）。</summary>
        public string SuggestNextParticipantId()
        {
            int max = 0;
            try
            {
                if (Directory.Exists(DatasetRoot))
                {
                    foreach (var dir in Directory.GetDirectories(DatasetRoot))
                    {
                        string name = Path.GetFileName(dir);
                        var m = Regex.Match(name, @"^P(\d+)$", RegexOptions.IgnoreCase);
                        if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > max) max = n;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ParticipantManager] 扫描被试目录失败：{e.Message}");
            }
            return "P" + (max + 1).ToString("000");
        }

        /// <summary>预览该被试下一次会话编号，供界面提示用。</summary>
        public string PredictNextSessionId(string participantId)
        {
            participantId = SanitizeId(participantId);
            if (string.IsNullOrEmpty(participantId)) return "Session_001";
            string folder = Path.Combine(DatasetRoot, participantId);
            return NextSessionId(folder);
        }

        private static string NextSessionId(string participantFolder)
        {
            int max = 0;
            try
            {
                if (Directory.Exists(participantFolder))
                {
                    foreach (var dir in Directory.GetDirectories(participantFolder))
                    {
                        string name = Path.GetFileName(dir);
                        var m = Regex.Match(name, @"^Session_(\d+)$", RegexOptions.IgnoreCase);
                        if (m.Success && int.TryParse(m.Groups[1].Value, out int n) && n > max) max = n;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ParticipantManager] 扫描会话目录失败：{e.Message}");
            }
            return "Session_" + (max + 1).ToString("000");
        }

        /// <summary>只保留字母、数字、下划线、短横线，避免目录名非法。</summary>
        private static string SanitizeId(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            return Regex.Replace(raw.Trim(), @"[^A-Za-z0-9_\-]", string.Empty);
        }
    }
}
