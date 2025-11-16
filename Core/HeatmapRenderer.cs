
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
            if (pv == null) throw new ArgumentNullException(nameof(pv));
            snap ??= new InvSnapshot();

            var colors = snap.ColorsNonZero().ToList();
            var sizes  = snap.SizesNonZero().ToList();

            if (colors.Count == 0 || sizes.Count == 0)
            {
                var emptyModel = new PlotModel { Title = title };
                emptyModel.Background = OxyColors.White;
                emptyModel.TextColor   = OxyColor.FromRgb(47, 47, 47);
                emptyModel.TitleColor  = emptyModel.TextColor;
                emptyModel.PlotMargins = new OxyThickness(60, 6, 24, 40);

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

            var colorIndex = colors
                .Select((c, i) => (c, i))
                .ToDictionary(x => x.c, x => x.i);

            var sizeIndex = sizes
                .Select((s, i) => (s, i))
                .ToDictionary(x => x.s, x => x.i);

            var data = new double[colors.Count, sizes.Count];

            foreach (var g in snap.Rows.GroupBy(r => new { r.Color, r.Size }))
            {
                if (!colorIndex.TryGetValue(g.Key.Color, out var cy)) continue;
                if (!sizeIndex.TryGetValue(g.Key.Size,  out var sx)) continue;
                data[cy, sx] = g.Sum(x => x.Available);
            }

            var values = new List<double>();
            foreach (var v in data):
                pass
