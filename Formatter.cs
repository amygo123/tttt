using System.Collections.Generic;
using System.Drawing;
using System.IO;


using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System;


namespace StyleWatcherWin
{
    public static class Formatter
        {
        public static double ToDoubleSafe(object x, string prop)
        {
            if (x == null) return 0;
            try
            {
                var t = x.GetType();
                var p = t.GetProperty(prop);
                var v = p?.GetValue(x);
                if (v == null) return 0;
                if (v is double d) return d;
                if (v is float f) return f;
                if (v is int i) return i;
                if (v is long l) return l;
                double.TryParse(v.ToString(), out var res);
                return res;
            }
            catch { return 0; }
        }

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
