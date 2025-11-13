using System;
using System.Collections.Generic;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace StyleWatcherWin
{
    /// <summary>
    /// Shared chart helpers built on top of OxyPlot.
    /// </summary>
    internal static class UiCharts
    {
        /// <summary>
        /// Builds a simple horizontal bar chart from (label, value) data.
        /// Optionally keeps only topN items by value (descending) if specified.
        /// </summary>
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

            model.Axes.Add(catAxis);
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                MinimumPadding = 0,
                AbsoluteMinimum = 0
            });

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
