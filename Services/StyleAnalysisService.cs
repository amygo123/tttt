using System;
using System.Threading;
using System.Threading.Tasks;

namespace StyleWatcherWin
{
    /// <summary>
    /// 封装从原始选中文本到“已清洗文本”的完整调用链：
    /// 1. 调用 ApiHelper.QueryAsync 访问后端（如果配置了 api_url）；
    /// 2. 使用 Formatter.Prettify 规范化文本；
    /// 3. 统一处理各种错误消息。
    ///
    /// 该服务被 TrayApp 和 ResultForm 共享，避免在多个 UI 入口重复写调用逻辑。
    /// </summary>
    public interface IStyleAnalysisService
    {
        /// <summary>
        /// 根据选中文本调用后端并返回已清洗的结果文本。
        /// </summary>
        Task<string> GetParsedTextAsync(string selection, CancellationToken cancellationToken = default);
    }

    public sealed class StyleAnalysisService : IStyleAnalysisService
    {
        private readonly AppConfig _config;

        public StyleAnalysisService(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<string> GetParsedTextAsync(string selection, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(selection))
                throw new ArgumentException("待查询内容不能为空", nameof(selection));

            cancellationToken.ThrowIfCancellationRequested();

            string raw;
            if (string.IsNullOrWhiteSpace(_config.api_url))
            {
                // 未配置接口时，退化为直接对原始文本做本地解析
                raw = selection;
            }
            else
            {
                raw = await ApiHelper.QueryAsync(_config, selection, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (raw != null && raw.StartsWith("请求失败：", StringComparison.Ordinal))
                throw new InvalidOperationException(raw);

            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException("接口未返回任何内容");

            var result = Formatter.Prettify(raw);
            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidOperationException("未解析到任何结果");

            return result;
        }
    }
}