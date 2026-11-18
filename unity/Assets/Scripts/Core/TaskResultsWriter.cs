using System.IO;
using UnityEngine;

namespace MagicIsland.Core
{
    /// <summary>
    /// task_results.csv 写入器（V1.1 7.6，每试次一行）。
    /// 列序固定（用不到的列留空）：
    /// task_id,trial_id,participant_id,session_id,success,completion_time_ms,
    /// hold_duration_ms,out_of_target_count,target_id,wrong_target_count,alignment_time_ms,extra
    /// </summary>
    public static class TaskResultsWriter
    {
        public const string Header =
            "task_id,trial_id,participant_id,session_id,success,completion_time_ms," +
            "hold_duration_ms,out_of_target_count,target_id,wrong_target_count,alignment_time_ms,extra";

        public static void Append(int taskId, string trialId, bool success, float completionTimeMs,
                                  string holdDurationMs = "", string outOfTargetCount = "",
                                  string targetId = "", string wrongTargetCount = "",
                                  string alignmentTimeMs = "", string extra = "")
        {
            var pm = ParticipantManager.Instance;
            var info = pm != null ? pm.Current : null;
            if (info == null)
            {
                Debug.LogWarning("[TaskResultsWriter] 没有活动会话，试次结果未落盘。");
                return;
            }

            string filePath = Path.Combine(info.SessionFolder, "task_results.csv");
            CsvLogWriter.EnsureFile(filePath, Header);
            CsvLogWriter.Append(filePath,
                taskId, trialId ?? string.Empty, info.ParticipantId, info.SessionId,
                success, completionTimeMs,
                holdDurationMs ?? string.Empty, outOfTargetCount ?? string.Empty,
                targetId ?? string.Empty, wrongTargetCount ?? string.Empty,
                alignmentTimeMs ?? string.Empty, extra ?? string.Empty);
        }
    }
}
