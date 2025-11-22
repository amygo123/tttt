using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace StyleWatcherWin
{
    public class SaleRecord
    {
        public DateTime Date { get; set; }
        public string   Channel { get; set; } = "";
        public string   Shop    { get; set; } = "";
        public string   Name    { get; set; } = "";
        public string   Size    { get; set; } = "";
        public string   Color   { get; set; } = "";
        public int      Qty     { get; set; }
    }

    public class ParsedPayload
    {
        public string Title { get; set; } = "";
        public string Yesterday { get; set; } = "";
        public int?   Sum7d { get; set; }
        public List<SaleRecord> Records { get; set; } = new List<SaleRecord>();
    }

    public static class PayloadParser
    {
        // “标题: 昨日...” 行
        static readonly Regex RxTitle = new Regex(
            @"^(?<title>.+?)(?:[:：]\s*)(?<yest>昨日[^\n]*)$",
            RegexOptions.Compiled);

        // “标题 近7天销量汇总：593” 行
        static readonly Regex RxSum = new Regex(
            @"(?<title>.+?)\s+近7天销量汇总[:：]\s*(?<sum>\d+)",
            RegexOptions.Compiled);

        // 旧格式：yyyy-MM-dd 名称 尺码 颜色: 99件
        static readonly Regex RxLineLegacy = new Regex(
            @"^(?<date>20\d{2}-\d{2}-\d{2})\s+(?<rest>.+?)\s*[:：]\s*(?<qty>\d+)\s*件$",
            RegexOptions.Compiled);

        // 新格式：渠道 店铺 yyyy-MM-dd 名称 尺码 颜色: 99件
        static readonly Regex RxLineWithChannel = new Regex(
            @"^(?<channel>\S+)\s+(?<shop>.+?)\s+(?<date>20\d{2}-\d{2}-\d{2})\s+(?<rest>.+?)\s*[:：]\s*(?<qty>\d+)\s*件$",
            RegexOptions.Compiled);

        public static ParsedPayload Parse(string text)
        {
            var result = new ParsedPayload();
            if (string.IsNullOrWhiteSpace(text))
                return result;

            // 统一换行
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = normalized
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToList();

            if (lines.Count == 0)
                return result;

            // 标题 + 昨日
            var mTitle = RxTitle.Match(lines[0]);
            if (mTitle.Success)
            {
                result.Title = mTitle.Groups["title"].Value.Trim();
                result.Yesterday = mTitle.Groups["yest"].Value.Trim();
            }
            else
            {
                result.Title = lines[0];
            }

            // 近 7 天汇总
            foreach (var line in lines.Skip(1))
            {
                var mSum = RxSum.Match(line);
                if (mSum.Success)
                {
                    if (int.TryParse(mSum.Groups["sum"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sum))
                    {
                        result.Sum7d = sum;
                    }
                    break;
                }
            }

            // 明细
            foreach (var line in lines.Skip(1))
            {
                Match m;
                string channel = string.Empty;
                string shop = string.Empty;

                var mNew = RxLineWithChannel.Match(line);
                if (mNew.Success)
                {
                    m = mNew;
                    channel = mNew.Groups["channel"].Value.Trim();
                    shop = mNew.Groups["shop"].Value.Trim();
                }
                else
                {
                    m = RxLineLegacy.Match(line);
                    if (!m.Success)
                        continue;
                }

                if (!DateTime.TryParseExact(
                        m.Groups["date"].Value,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var dt))
                {
                    continue;
                }

                if (!int.TryParse(m.Groups["qty"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var qty))
                    continue;

                var rest = m.Groups["rest"].Value.Trim();
                if (string.IsNullOrEmpty(rest))
                    continue;

                var tokens = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3)
                    continue;

                var size = tokens[^2];
                var color = tokens[^1];
                var name = string.Join(" ", tokens.Take(tokens.Length - 2));

                result.Records.Add(new SaleRecord
                {
                    Date = dt,
                    Channel = channel,
                    Shop = shop,
                    Name = name,
                    Size = size,
                    Color = color,
                    Qty = qty
                });
            }

            return result;
        }
    }

    // Backward-compat wrapper for legacy references: StyleWatcherWin.Parser.Parse(...)
    public static class Parser
    {
        public static ParsedPayload Parse(string text) => PayloadParser.Parse(text);
    }
}
