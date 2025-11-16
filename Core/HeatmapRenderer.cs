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
        public List<string> Colors { get; set; } = new();
        public List<string> Sizes  { get; set; } = new();
        public double[,] Data      { get; set; } = new double[0, 0];
    }

    internal static class HeatmapRenderer
    {
        private static double Percentile(IReadOnlyList<double> sorted, double p)
        {
            if (sorted == null || sorted.Count == 0) return 0;
            if (p <= 0) return sorted[0];
            if (p >= 1) return sorted[sorted.Count - 1];

            var idx = (sorted.Count - 1) * p;
            var lo = (int)Math.Floor(idx);
            var hi = (int)Math.Ceiling(idx);
            if (lo == hi) return sorted[lo];

            var frac = idx - lo;
            return sorted[lo] * (1 - frac) + sorted[hi] * frac;
        }

        /// <summary>
        /// 构建库存热力图：X = 尺码，Y = 颜色
        /// </summary>
        public static HeatmapContext BuildHeatmap(InvSnapshot snap, PlotView pv, string title)
        {
            if (pv == null) throw new ArgumentNullException(nameof(pv));
            snap ??= new InvSnapshot();

            var colors = snap.ColorsNonZero().ToList();
            var sizes  = snap.SizesNonZero().ToList();

            // 无数据：只给一个空模型，保持风格一致
            if (colors.Count == 0 || sizes.Count == 0)
            {
                var emptyModel = new PlotModel { Title = title };
                emptyModel.Background = OxyColors.White;
                emptyModel.TextColor   = OxyColor.FromRgb(47, 47, 47);
                emptyModel.TitleColor  = emptyModel.TextColor;
                emptyModel.PlotMargins = new OxyThickness(60, 6, 24, 40);
                emptyModel.PlotAreaBorderThickness = new OxyThickness(0);

                pv.Model = emptyModel;

                var emptyCtx = new HeatmapContext
                {
                    Colors = colors,
                    Sizes  = sizes,
                    Data   = new double[0, 0]
                };
                pv.Tag = emptyCtx;
                return emptyCtx;
            }

            // 映射：color → 行索引 (Y)，size → 列索引 (X)
            var colorIndex = colors
                .Select((c, i) => (Color: c, Index: i))
                .ToDictionary(x => x.Color, x => x.Index);

            var sizeIndex = sizes
                .Select((s, i) => (Size: s, Index: i))
                .ToDictionary(x => x.Size, x => x.Index);

            // Data[y, x] = available
            var data = new double[colors.Count, sizes.Count];

            foreach (var g in snap.Rows.GroupBy(r => new { r.Color, r.Size }))
            {
                int cy;
                int sx;
                if (!colorIndex.TryGetValue(g.Key.Color, out cy)) continue;
                if (!sizeIndex.TryGetValue(g.Key.Size,  out sx)) continue;

                data[cy, sx] = g.Sum(x => x.Available);
            }

            // 统计非零值，用于色带上下限
            var values = new List<double>();
            foreach (var v in data)
            {
                if (v > 0)
                {
                    values.Add(v);
                }
            }

            values.Sort();
            var minPos = values.Count > 0 ? values[0] : 1.0;
            var p95    = values.Count > 0 ? Percentile(values, 0.95) : 1.0;
            if (p95 <= 0) p95 = minPos;

            var model = new PlotModel { Title = title };
            model.Background          = OxyColors.White;
            model.TextColor           = OxyColor.FromRgb(47, 47, 47);
            model.TitleColor          = model.TextColor;
            model.PlotMargins         = new OxyThickness(60, 6, 24, 40);
            model.PlotAreaBorderThickness = new OxyThickness(0);

            // 颜色轴：浅绿 → 绿 → 黄 → 橙 → 红
            var palette = OxyPalette.Interpolate(
                256,
                OxyColor.FromRgb(229, 245, 224),
                OxyColor.FromRgb(161, 217, 155),
                OxyColor.FromRgb(255, 224, 102),
                OxyColor.FromRgb(253, 174, 97),
                OxyColor.FromRgb(244, 109, 67),
                OxyColor.FromRgb(215, 48, 39)
            );

            var caxis = new LinearColorAxis
            {
                Position  = AxisPosition.Right,
                Palette   = palette,
                Minimum   = minPos,
                Maximum   = p95,
                LowColor  = OxyColor.FromRgb(242, 242, 242),
                HighColor = OxyColor.FromRgb(153, 0, 0)
            };
            model.Axes.Add(caxis);

            // X 轴：尺码
            var xAxis = new LinearAxis
            {
                Position       = AxisPosition.Bottom,
                Minimum        = -0.5,
                Maximum        = Math.Max(sizes.Count - 0.5, 0.5),
                MajorStep      = 1,
                MinorStep      = 1,
                IsZoomEnabled  = true,
                IsPanEnabled   = true,
                LabelFormatter = d =>
                {
                    var idx = (int)Math.Round(d);
                    return idx >= 0 && idx < sizes.Count ? sizes[idx] : string.Empty;
                }
            };

            // Y 轴：颜色
            var yAxis = new LinearAxis
            {
                Position       = AxisPosition.Left,
                Minimum        = -0.5,
                Maximum        = Math.Max(colors.Count - 0.5, 0.5),
                MajorStep      = 1,
                MinorStep      = 1,
                IsZoomEnabled  = true,
                IsPanEnabled   = true,
                LabelFormatter = d =>
                {
                    var idx = (int)Math.Round(d);
                    return idx >= 0 && idx < colors.Count ? colors[idx] : string.Empty;
                }
            };

            model.Axes.Add(xAxis);
            model.Axes.Add(yAxis);

            var hm = new HeatMapSeries
            {
                X0           = -0.5,
                X1           = sizes.Count  - 0.5,
                Y0           = -0.5,
                Y1           = colors.Count - 0.5,
                Interpolate  = false,
                RenderMethod = HeatMapRenderMethod.Rectangles,
                Data         = data
            };

            model.Series.Add(hm);
            pv.Model = model;

            var ctx = new HeatmapContext
            {
                Colors = colors,
                Sizes  = sizes,
                Data   = data
            };
            pv.Tag = ctx;
            return ctx;
        }
    }
}
