using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace StyleWatcherWin
{
    internal sealed class HeatmapContext
    {
        public List<string> Colors { get; } = new();
        public List<string> Sizes  { get; } = new();
        public double[,] Data      { get; set; } = new double[0, 0];
    }

    internal static class HeatmapRenderer
    {
        private static double Percentile(List<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            if (p <= 0) return sorted.First();
            if (p >= 1) return sorted.Last();
            var idx = (sorted.Count - 1) * p;
            var lo = (int)Math.Floor(idx);
            var hi = (int)Math.Ceiling(idx);
            if (lo == hi) return sorted[lo];
            var frac = idx - lo;
            return sorted[lo] * (1 - frac) + sorted[hi] * frac;
        }

        public static HeatmapContext BuildHeatmap(InvSnapshot snap, PlotView pv, string title)
        {
            var colors = snap.ColorsNonZero().ToList();
            var sizes = snap.SizesNonZero().ToList();

            var ci = colors.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
            var si = sizes.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);

            var data = new double[colors.Count, sizes.Count];
            foreach (var g in snap.Rows.GroupBy(r => new { r.Color, r.Size }))
            {
                if (!ci.ContainsKey(g.Key.Color) || !si.ContainsKey(g.Key.Size)) continue;
                data[ci[g.Key.Color], si[g.Key.Size]] = g.Sum(x => x.Available);
            }

            var model = new PlotModel { Title = title };

            // 统计分布
            var vals = new List<double>();
            foreach (var v in data) if (v > 0) vals.Add(v);
            vals.Sort();
            var minPos = vals.Count > 0 ? vals.First() : 1.0;
            var p95 = vals.Count > 0 ? Percentile(vals, 0.95) : 1.0;
            if (p95 <= 0) p95 = minPos;

            // 自定义更直观的配色：浅 -> 绿 -> 橙 -> 红
            var palette = OxyPalette.Interpolate(256, 
                OxyColor.FromRgb(229, 245, 224), // very light
                OxyColor.FromRgb(161, 217, 155), // green
                OxyColor.FromRgb(255, 224, 102), // yellow-ish
                OxyColor.FromRgb(253, 174, 97),  // orange
                OxyColor.FromRgb(244, 109, 67),  // orange-red
                OxyColor.FromRgb(215, 48, 39)    // red
            );

            var caxis = new LinearColorAxis
            {
                Position = AxisPosition.Right,
                Palette = palette,
                Minimum = minPos,                           // 低于最小正值的（包括 0）走 LowColor
                Maximum = p95,                              // 高于 P95 的走 HighColor
                LowColor = OxyColor.FromRgb(242, 242, 242), // 0 值显示很浅灰
                HighColor = OxyColor.FromRgb(153, 0, 0)     // 极高值深红
            };
            model.Axes.Add(caxis);

            // 类目映射轴
            var axX = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = -0.5, Maximum = Math.Max(colors.Count - 0.5, 0.5),
                MajorStep = 1, MinorStep = 1,
                IsZoomEnabled = true, IsPanEnabled = true,
                LabelFormatter = d =>
                {
                    var k = (int)Math.Round(d);
                    return (k >= 0 && k < colors.Count) ? colors[k] : "";
                }
            };

            var axY = new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = -0.5, Maximum = Math.Max(sizes.Count - 0.5, 0.5),
                MajorStep = 1, MinorStep = 1,
                IsZoomEnabled = true, IsPanEnabled = true,
                LabelFormatter = d =>
                {
                    var k = (int)Math.Round(d);
                    return (k >= 0 && k < sizes.Count) ? sizes[k] : "";
                }
            };

            model.Axes.Add(axX);
            model.Axes.Add(axY);

            var hm = new HeatMapSeries
            {
                X0 = -0.5,
                X1 = colors.Count - 0.5,
                Y0 = -0.5,
                Y1 = sizes.Count - 0.5,
                Interpolate = false,
                RenderMethod = HeatMapRenderMethod.Rectangles,
                Data = data
            };

            model.Series.Add(hm);
            pv.Model = model;

            var ctx = new HeatmapContext { Colors = colors, Sizes = sizes, Data = data };
            pv.Tag = ctx;
            return ctx;
        }
    }
}
