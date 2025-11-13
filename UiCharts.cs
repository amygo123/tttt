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
            var list = data.Where(a => !string.IsNullOrWhiteSpace(a.Key) && a.Qty != 0)
                           .OrderByDescending(a => a.Qty)
                           .ToList();
            if (topN.HasValue) list = list.Take(topN.Value).ToList();

            var model = new PlotModel { Title = title, PlotMargins = new OxyThickness(80,6,6,6) };
            var cat = new CategoryAxis{ Position=AxisPosition.Left, GapWidth=0.4, StartPosition=1, EndPosition=0 };
            foreach (var a in list) cat.Labels.Add(a.Key);
            model.Axes.Add(cat);
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding=0, AbsoluteMinimum=0 });

            var series = new BarSeries { LabelFormatString = "{0}", LabelPlacement = LabelPlacement.Inside, LabelMargin = 6 };
            foreach (var a in list) series.Items.Add(new BarItem { Value = a.Qty });
            model.Series.Add(series);
            return model;
        }
    }
}
