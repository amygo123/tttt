using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StyleWatcherWin
{
    /// <summary>
    /// 提供统一的库存查询与解析服务：
    /// - 基于 AppConfig.inventory 配置调用库存接口（通过 ApiHelper.QueryInventoryAsync）；
    /// - 兼容 JSON 数组字符串或纯文本行；
    /// - 解析为 InvSnapshot，供 UI 直接使用。
    /// </summary>
    public interface IInventoryService
    {
        Task<InvSnapshot> GetSnapshotAsync(string styleName, CancellationToken cancellationToken = default);
    }

    public sealed class InventoryService : IInventoryService
    {
        private readonly AppConfig _config;

        public InventoryService(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<InvSnapshot> GetSnapshotAsync(string styleName, CancellationToken cancellationToken = default)
        {
            var snap = new InvSnapshot();
            if (string.IsNullOrWhiteSpace(styleName))
                return snap;

            // 调用统一的库存接口封装
            var raw = await ApiHelper.QueryInventoryAsync(_config, styleName, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(raw))
                return snap;

            // 兼容：JSON 数组字符串 或 纯文本行
            List<string>? lines = null;
            try
            {
                lines = JsonSerializer.Deserialize<List<string>>(raw);
            }
            catch
            {
                // ignore
            }

            if (lines == null)
            {
                raw = raw.Replace("\r\n", "\n");
                lines = raw.Split('\n').ToList();
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var seg = line.Replace('，', ',').Split(',');
                if (seg.Length < 6) continue;

                snap.Rows.Add(new InvRow
                {
                    Name = seg[0].Trim(),
                    Color = seg[1].Trim(),
                    Size = seg[2].Trim(),
                    Warehouse = seg[3].Trim(),
                    Available = int.TryParse(seg[4].Trim(), out var a) ? a : 0,
                    OnHand = int.TryParse(seg[5].Trim(), out var h) ? h : 0
                });
            }

            return snap;
        }
    }
}