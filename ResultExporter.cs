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
            IReadOnlyList<object> gridMaster,
            IReadOnlyList<Aggregations.SalesItem> sales,
            int trendWindow,
            AppConfig cfg)
        {
            if (wb == null) throw new ArgumentNullException(nameof(wb));
            gridMaster ??= Array.Empty<object>();
            sales ??= Array.Empty<Aggregations.SalesItem>();

            // 1) 销售明细
            var ws1 = wb.AddWorksheet("销售明细");
            ws1.Cell(1, 1).Value = "日期";
            ws1.Cell(1, 2).Value = "款式";
            ws1.Cell(1, 3).Value = "颜色";
            ws1.Cell(1, 4).Value = "尺码";
            ws1.Cell(1, 5).Value = "数量";

            int r = 2;
            foreach (var it in gridMaster)
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

            // 2) 趋势
            var ws2 = wb.AddWorksheet("趋势");
            ws2.Cell(1, 1).Value = "日期";
            ws2.Cell(1, 2).Value = "数量";

            var series = Aggregations.BuildDateSeries(sales, trendWindow);
            int rr = 2;
            for (int i = 0; i < series.Count; i++)
            {
                ws2.Cell(rr, 1).Value = series[i].day.ToString("yyyy-MM-dd");
                ws2.Cell(rr, 2).Value = series[i].qty;
                rr++;
            }
            ws2.Columns().AdjustToContents();

            // 3) 口径说明
            var ws3 = wb.AddWorksheet("口径说明");
            ws3.Cell(1, 1).Value = "趋势窗口（天）";
            ws3.Cell(1, 2).Value = trendWindow;

            var alert = cfg?.inventoryAlert;
            var red = alert?.docRed ?? 3;
            var yellow = alert?.docYellow ?? 7;
            var minSalesWindow = alert?.minSalesWindowDays ?? 7;

            ws3.Cell(2, 1).Value = "库存天数阈值";
            ws3.Cell(2, 2).Value = $"红<{red}，黄<{yellow}";
            ws3.Cell(3, 1).Value = "销量基线天数";
            ws3.Cell(3, 2).Value = minSalesWindow;
            ws3.Columns().AdjustToContents();
        }
    }
}
