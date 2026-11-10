using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MagicIsland.Core
{
    /// <summary>
    /// 极简 CSV 追加写入器。
    /// 每次写入立即落盘，崩溃最多丢最后一行，适合科研数据记录。
    /// 所有事件 / 被试信息 / 试次结果统一走这里，保证格式一致。
    /// </summary>
    public static class CsvLogWriter
    {
        private static readonly object Gate = new object();

        /// <summary>文件不存在或为空时写入表头。</summary>
        public static void EnsureFile(string filePath, string headerLine)
        {
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(filePath) || new FileInfo(filePath).Length == 0)
                {
                    File.WriteAllText(filePath, headerLine + "\n", new UTF8Encoding(false));
                }
            }
        }

        /// <summary>追加一行（自动转义逗号、引号、换行）。</summary>
        public static void Append(string filePath, params object[] values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(values[i]));
            }
            AppendLine(filePath, sb.ToString());
        }

        public static void AppendLine(string filePath, string line)
        {
            lock (Gate)
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(filePath, line + "\n", new UTF8Encoding(false));
            }
        }

        private static string Escape(object value)
        {
            if (value == null) return string.Empty;

            string s;
            if (value is float f) s = f.ToString("R", CultureInfo.InvariantCulture);
            else if (value is double d) s = d.ToString("R", CultureInfo.InvariantCulture);
            else if (value is bool b) s = b ? "true" : "false";
            else if (value is IFormattable formattable) s = formattable.ToString(null, CultureInfo.InvariantCulture);
            else s = value.ToString();

            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
            {
                s = "\"" + s.Replace("\"", "\"\"") + "\"";
            }
            return s;
        }
    }
}
