namespace StyleWatcherWin
{
    /// <summary>
    /// 行缓存：销售明细行 + 预计算的搜索文本，避免每次过滤时通过反射拼接字符串。
    /// </summary>
    internal sealed class SaleRow
    {
        public string 日期   { get; set; } = string.Empty;
        public string 渠道   { get; set; } = string.Empty;
        public string 店铺   { get; set; } = string.Empty;
        public string 款式   { get; set; } = string.Empty;
        public string 颜色   { get; set; } = string.Empty;
        public string 尺码   { get; set; } = string.Empty;
        public int    数量   { get; set; }

        /// <summary>
        /// 用于本地搜索的预计算字段，包含日期/渠道/店铺/款式/尺码/颜色/数量等信息。
        /// </summary>
        public string SearchText { get; set; } = string.Empty;
    }
}
