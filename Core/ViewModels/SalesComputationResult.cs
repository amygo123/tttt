using System;
using System.Collections.Generic;

namespace StyleWatcherWin
{
    /// <summary>
    /// 销售页的“计算结果”模型：
    /// 从原始 displayText 解析得到的销售明细行、聚合后的 SalesItem 列表等。
    /// 目前作为 ResultForm 后续重构的骨架类型，便于将计算逻辑与 UI 渲染解耦。
    /// </summary>
    internal sealed class SalesComputationResult
    {
        /// <summary>
        /// 用于计算的原始展示文本（经过 Prettify 之后的 displayText）。
        /// </summary>
        public string DisplayText { get; init; } = string.Empty;

        /// <summary>
        /// 主表格的数据源（SaleRow 列表）。
        /// </summary>
        public List<SaleRow> GridRows { get; init; } = new();

        /// <summary>
        /// 供趋势 / 渠道 / 店铺等图表使用的明细汇总。
        /// </summary>
        public List<Aggregations.SalesItem> SalesItems { get; init; } = new();
    }
}
