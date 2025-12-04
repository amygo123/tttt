using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace StyleWatcherWin
{
    /// <summary>
    /// 封装结果报表（导出 Excel）的构建逻辑。
    /// 当前只负责填充三个工作表到给定的 XLWorkbook：
    /// 1) 销售明细
    /// 2) 趋势
    /// 3) 口径说明
    /// </summary>
    internal static class ResultExporter
    {
        public static void FillWorkbook(
            XLWorkbook wb,
            IReadOnlyList<object> salesDetails,
            IReadOnlyList<InvRow> inventoryRows,
            IReadOnlyList<Dictionary<string, object?>> vipRows)
        {
            if (wb == null) throw new ArgumentNullException(nameof(wb));
            salesDetails ??= Array.Empty<object>();
            inventoryRows ??= Array.Empty<InvRow>();
            vipRows ??= Array.Empty<Dictionary<string, object?>>();

            // 1) 销售明细
            var ws1 = wb.AddWorksheet("销售明细");
            ws1.Cell(1, 1).Value = "日期";
            ws1.Cell(1, 2).Value = "款式";
            ws1.Cell(1, 3).Value = "颜色";
            ws1.Cell(1, 4).Value = "尺码";
            ws1.Cell(1, 5).Value = "数量";

            int r = 2;
            foreach (var it in salesDetails)
            {
                if (it == null) continue;
                var t = it.GetType();
                ws1.Cell(r, 1).Value = t.GetProperty("日期")?.GetValue(it)?.ToString();
                ws1.Cell(r, 2).Value = t.GetProperty("款式")?.GetValue(it)?.ToString();
                ws1.Cell(r, 3).Value = t.GetProperty("颜色")?.GetValue(it)?.ToString();
                ws1.Cell(r, 4).Value = t.GetProperty("尺码")?.GetValue(it)?.ToString();
                ws1.Cell(r, 5).Value = t.GetProperty("数量")?.GetValue(it)?.ToString();
                r++;
            }
            ws1.Columns().AdjustToContents();

            // 2) 库存明细
            var ws2 = wb.AddWorksheet("库存明细");
            ws2.Cell(1, 1).Value = "品名";
            ws2.Cell(1, 2).Value = "颜色";
            ws2.Cell(1, 3).Value = "尺码";
            ws2.Cell(1, 4).Value = "仓库";
            ws2.Cell(1, 5).Value = "可用";
            ws2.Cell(1, 6).Value = "现有";

            int r2 = 2;
            foreach (var row in inventoryRows)
            {
                if (row == null) continue;
                ws2.Cell(r2, 1).Value = row.Name;
                ws2.Cell(r2, 2).Value = row.Color;
                ws2.Cell(r2, 3).Value = row.Size;
                ws2.Cell(r2, 4).Value = row.Warehouse;
                ws2.Cell(r2, 5).Value = row.Available;
                ws2.Cell(r2, 6).Value = row.OnHand;
                r2++;
            }
            ws2.Columns().AdjustToContents();

            // 3) 唯品库存明细
            var ws3 = wb.AddWorksheet("唯品库存明细");
            ws3.Cell(1, 1).Value = "款式名";
            ws3.Cell(1, 2).Value = "白胚可用数";
            ws3.Cell(1, 3).Value = "进货仓库存";
            ws3.Cell(1, 4).Value = "成品占用数";
            ws3.Cell(1, 5).Value = "可用数汇总";

            int r3 = 2;
            foreach (var row in vipRows)
            {
                if (row == null) continue;

                row.TryGetValue("product_original_code", out var style);
                var white = GetVipNumber(row, "白胚可用数", "白坯可用数");
                var stock = GetVipNumber(row, "进货仓库存");
                var used = GetVipNumber(row, "成品占用数");
                var sum = white + stock + used;

                ws3.Cell(r3, 1).Value = style?.ToString();
                ws3.Cell(r3, 2).Value = white;
                ws3.Cell(r3, 3).Value = stock;
                ws3.Cell(r3, 4).Value = used;
                ws3.Cell(r3, 5).Value = sum;
                r3++;
            }
            ws3.Columns().AdjustToContents();
        }

        private static double GetVipNumber(Dictionary<string, object?> row, params string[] keys)
        {
            if (row == null || keys == null || keys.Length == 0) return 0d;

            foreach (var key in keys)
            {
                if (!row.TryGetValue(key, out var val) || val == null)
                    continue;

                switch (val)
                {
                    case int i: return i;
                    case long l: return l;
                    case double d: return d;
                    case float f: return f;
                    case string s when double.TryParse(s, out var dv): return dv;
                }
            }

            return 0d;
        }
    }
}
