using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace StyleWatcherWin
{
internal static class Formatter
    {
        public static string Prettify(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            var s = raw.Replace("\\n", "\n").Replace("\r\n", "\n");
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].Trim();
            s = string.Join("\n", lines);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\n{3,}", "\n\n");
            return s.Trim();
        }
    }
}
