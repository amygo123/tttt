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

        public static HeatmapContext BuildHeatmap(InvSnapshot snap, PlotView pv, string title)
        {
            if (snap == null) snap = new InvSnapshot();
            if (pv == null) throw new ArgumentNullException(nameof(pv));

            var colors = snap.ColorsNonZero().ToList();
            var sizes  = snap.SizesNonZero().ToList();

            if (colors.Count == 0 || sizes.Count == 0)
            {
                var emptyModel = new PlotModel { Title = title };
                emptyModel.Background = OxyColors.White;
                emptyModel.TextColor = OxyColor.FromRgb(47, 47, 47);
                emptyModel.TitleColor = emptyModel.TextColor;
                pv.Model = emptyModel;

                var emptyCtx = new HeatmapContext { Colors = colors, Sizes = sizes, Data = new double[0, 0] };
                pv.Tag = emptyCtx;
                return emptyCtx;
            }

            var ci = colors.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
            var si = sizes.Select((s, i) => (s, i)).ToDictionary(x => x.s, x => x.i);

            // OxyPlot HeatMapSeries.Data is indexed as [x, y] where the first dimension is X (horizontal)
            // and the second dimension is Y (vertical). We want:
            //   X -> 尺码(Size), Y -> 颜色(Color)
            // so we store data[sizeIndex, colorIndex].
            var data = new double[sizes.Count, colors.Count];
            foreach (var g in snap.Rows.GroupBy(r => new { r.Color, r.Size }))
            {
                if (!ci.TryGetValue(g.Key.Color, out var cx)) continue;
                if (!si.TryGetValue(g.Key.Size, out var sx)) continue;
                data[sx, cx] = g.Sum(x => x.Available);
            }

            var vals = new List<double>();
            foreach (var v in data)
            {
                if (v > 0) vals.Add(v);
            }
            vals.Sort();
            var minPos = vals.Count > 0 ? vals[0] : 1.0;
            var p95 = vals.Count > 0 ? Percentile(vals, 0.95) : 1.0;
            if (p95 <= 0) p95 = minPos;

            var model = new PlotModel { Title = title };
            model.Background = OxyColors.White;
            model.TextColor = OxyColor.FromRgb(47, 47, 47);
            model.TitleColor = model.TextColor;
            model.PlotMargins = new OxyThickness(110, 40, 80, 80);
            model.PlotAreaBorderThickness = new OxyThickness(0);

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
                Position = AxisPosition.Right,
                Palette = palette,
                Minimum = minPos,
                Maximum = p95,
                LowColor = OxyColor.FromRgb(242, 242, 242),
                HighColor = OxyColor.FromRgb(153, 0, 0)
            };
            model.Axes.Add(caxis);

            var axX = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = -0.5,
                Maximum = sizes.Count - 0.5,
                MinimumPadding = 0,
                MaximumPadding = 0,
                MajorStep = 1,
                MinorStep = 1,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                LabelFormatter = d =>
                {
                    var k = (int)Math.Round(d);
                    return (k >= 0 && k < sizes.Count) ? sizes[k] : string.Empty;
                }
            };

            var axY = new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = -0.5,
                Maximum = colors.Count - 0.5,
                MinimumPadding = 0,
                MaximumPadding = 0,
                MajorStep = 1,
                MinorStep = 1,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                LabelFormatter = d =>
                {
                    var k = (int)Math.Round(d);
                    return (k >= 0 && k < colors.Count) ? colors[k] : string.Empty;
                }
            };

            model.Axes.Add(axX);
            model.Axes.Add(axY);

            var hm = new HeatMapSeries
            {
                X0 = -0.5,
                X1 = sizes.Count - 0.5,
                Y0 = -0.5,
                Y1 = colors.Count - 0.5,
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
