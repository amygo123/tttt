using System;

namespace StyleWatcherWin
{
    /// <summary>
    /// 销售明细过滤条件：渠道 / 店铺 / 颜色 / 尺码 / 关键字。
    /// </summary>
    internal sealed class DetailFilter
    {
        public string? Channel { get; set; }
        public string? Shop   { get; set; }
        public string? Color  { get; set; }
        public string? Size   { get; set; }
        public string? Text   { get; set; }
    }
}
