using System.Globalization;

namespace MagicIsland.IMU
{
    /// <summary>
    /// IMU JSON 帧解析器（不依赖 Newtonsoft / System.Text.Json，避免加包）。
    /// 只解析扁平对象，够用且容错：字段缺失取默认值，数值支持科学计数法。
    /// </summary>
    public static class ImuJsonParser
    {
        public static bool TryParse(string json, out ImuFrame frame)
        {
            frame = default;
            if (string.IsNullOrEmpty(json)) return false;

            bool any = false;

            if (TryGetString(json, "session_id", out var sessionId)) { frame.sessionId = sessionId; any = true; }
            if (TryGetString(json, "device_id", out var deviceId)) { frame.deviceId = deviceId; any = true; }
            if (TryGetLong(json, "frame_id", out var frameId)) { frame.frameId = frameId; any = true; }
            if (TryGetLong(json, "host_timestamp_ms", out var hostMs)) { frame.hostTimestampMs = hostMs; any = true; }
            if (TryGetFloat(json, "relative_time_s", out var rel)) { frame.relativeTimeS = rel; any = true; }
            if (TryGetString(json, "device_time", out var deviceTime)) { frame.deviceTime = deviceTime; }

            if (TryGetFloat(json, "ax", out var ax)) { frame.ax = ax; any = true; }
            if (TryGetFloat(json, "ay", out var ay)) { frame.ay = ay; any = true; }
            if (TryGetFloat(json, "az", out var az)) { frame.az = az; any = true; }
            if (TryGetFloat(json, "gx", out var gx)) { frame.gx = gx; any = true; }
            if (TryGetFloat(json, "gy", out var gy)) { frame.gy = gy; any = true; }
            if (TryGetFloat(json, "gz", out var gz)) { frame.gz = gz; any = true; }

            // 契约字段优先；兼容旧脚本可能发送的 mx/my/mz 写法
            if (TryGetFloat(json, "mx_raw", out var mx)) { frame.mxRaw = mx; any = true; }
            else if (TryGetFloat(json, "mx", out var mxAlt)) { frame.mxRaw = mxAlt; }
            if (TryGetFloat(json, "my_raw", out var my)) { frame.myRaw = my; any = true; }
            else if (TryGetFloat(json, "my", out var myAlt)) { frame.myRaw = myAlt; }
            if (TryGetFloat(json, "mz_raw", out var mz)) { frame.mzRaw = mz; any = true; }
            else if (TryGetFloat(json, "mz", out var mzAlt)) { frame.mzRaw = mzAlt; }

            if (TryGetFloat(json, "roll", out var roll)) { frame.roll = roll; any = true; }
            if (TryGetFloat(json, "pitch", out var pitch)) { frame.pitch = pitch; any = true; }
            if (TryGetFloat(json, "yaw", out var yaw)) { frame.yaw = yaw; any = true; }

            if (TryGetString(json, "source", out var source)) frame.source = source;
            if (TryGetString(json, "participant_id", out var pid)) frame.participantId = pid;
            if (TryGetString(json, "trial_id", out var tid)) frame.trialId = tid;
            if (TryGetInt(json, "task_id", out var taskId)) frame.taskId = taskId;
            if (TryGetString(json, "task_phase", out var phase)) frame.taskPhase = phase;

            return any;
        }

        // ---------------- 内部工具 ----------------

        private static bool TryGetToken(string json, string key, out string token)
        {
            token = null;
            string needle = "\"" + key + "\"";
            int keyIndex = json.IndexOf(needle, System.StringComparison.Ordinal);
            if (keyIndex < 0) return false;

            int colon = json.IndexOf(':', keyIndex + needle.Length);
            if (colon < 0) return false;

            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return false;

            if (json[i] == '"')
            {
                int end = i + 1;
                while (end < json.Length)
                {
                    if (json[end] == '\\') { end += 2; continue; }
                    if (json[end] == '"') break;
                    end++;
                }
                if (end >= json.Length) return false;
                token = json.Substring(i + 1, end - i - 1);
                return true;
            }

            int start = i;
            while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']' && !char.IsWhiteSpace(json[i]))
            {
                i++;
            }
            if (i == start) return false;
            token = json.Substring(start, i - start);
            return true;
        }

        private static bool TryGetString(string json, string key, out string value)
        {
            value = null;
            if (!TryGetToken(json, key, out var token)) return false;
            if (token == "null") return false;
            value = token;
            return true;
        }

        private static bool TryGetFloat(string json, string key, out float value)
        {
            value = 0f;
            if (!TryGetToken(json, key, out var token)) return false;
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryGetLong(string json, string key, out long value)
        {
            value = 0L;
            if (!TryGetToken(json, key, out var token)) return false;
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            {
                value = (long)d;
                return true;
            }
            return false;
        }

        private static bool TryGetInt(string json, string key, out int value)
        {
            value = 0;
            if (!TryGetLong(json, key, out var l)) return false;
            value = (int)l;
            return true;
        }
    }
}
