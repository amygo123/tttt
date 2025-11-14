using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace StyleWatcherWin
{
    internal static class UiCharts
    {
        public static PlotModel BuildBarModel(IEnumerable<(string Key, double Qty)> data, string title, int? topN = null)
        {
            var source = data ?? Enumerable.Empty<(string Key, double Qty)>();

            IEnumerable<(string Key, double Qty)> items = source
                .Where(d => !string.IsNullOrWhiteSpace(d.Key))
                .OrderByDescending(d => d.Qty);

            if (topN.HasValue && topN.Value > 0)
            {
                items = items.Take(topN.Value);
            }

            var list = items.ToList();

            var model = new PlotModel
            {
                Title = title,
                PlotMargins = new OxyThickness(80, 6, 6, 6)
            };

            // 统一柱状图主题
            model.Background = OxyColors.White;
            model.TextColor = OxyColor.FromRgb(47, 47, 47);
            model.TitleColor = model.TextColor;
            model.PlotAreaBorderThickness = new OxyThickness(0);

            var catAxis = new CategoryAxis
            {
                Position = AxisPosition.Left,
                GapWidth = 0.4,
                StartPosition = 1,
                EndPosition = 0
            };

            foreach (var item in list)
            {
                catAxis.Labels.Add(item.Key);
            }

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0,
                AbsoluteMinimum = 0,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.None
            };

            model.Axes.Add(catAxis);
            model.Axes.Add(valueAxis);

            var series = new BarSeries
            {
                LabelFormatString = "{0}",
                LabelPlacement = LabelPlacement.Inside,
                LabelMargin = 6
            };

            foreach (var item in list)
            {
                series.Items.Add(new BarItem { Value = item.Qty });
            }

            model.Series.Add(series);
            return model;
        }
    }
}
