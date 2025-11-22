using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace StyleWatcherWin
{
    internal static class Formatter
    {
        public static string Prettify(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            // 新接口返回形如：{"msg":"...","code":200}
            // 这里优先从 JSON 中取 msg 字段，兼容旧的纯文本返回
            string payload = raw;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("msg", out var msgProp) &&
                        msgProp.ValueKind == JsonValueKind.String)
                    {
                        payload = msgProp.GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // 不是合法 JSON 时，按旧逻辑走
            }

            var s = (payload ?? string.Empty)
                .Replace("\\n", "\n")
                .Replace("\r\n", "\n");

            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].Trim();
            s = string.Join("\n", lines);
            s = Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }
    }
}