using System;
using System.Threading;
using System.Threading.Tasks;

namespace StyleWatcherWin
{
    /// <summary>
    /// 封装从原始选中文本到结构化 ParsedPayload 的完整调用链：
    /// 1. 调用 ApiHelper.QueryAsync 访问后端（如果配置了 api_url）
    /// 2. 使用 Formatter.Prettify 规范化文本
    /// 3. 使用 Parser.Parse 解析为业务模型
    /// 未来 ResultForm / TrayApp 可以逐步改为通过该服务调用，降低 UI 与解析/接口之间的耦合。
    /// </summary>
    public interface IStyleAnalysisService
    {
        Task<ParsedPayload> AnalyzeAsync(string selection, CancellationToken cancellationToken = default);
    }

    /// <inheritdoc />
    public sealed class StyleAnalysisService : IStyleAnalysisService
    {
        private readonly AppConfig _config;

        public StyleAnalysisService(AppConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<ParsedPayload> AnalyzeAsync(string selection, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(selection))
                throw new ArgumentException("待分析文本不能为空", nameof(selection));

            // 如果没有配置 api_url，则直接对原文做本地解析
            string raw;
            if (string.IsNullOrWhiteSpace(_config.api_url))
            {
                raw = selection;
            }
            else
            {
                // 统一调用现有 ApiHelper，保持与当前 UI 逻辑一致的行为
                raw = await ApiHelper.QueryAsync(_config, selection).ConfigureAwait(false);

                // 与 ResultForm 中现有逻辑保持一致：前缀为“请求失败：”时视为错误消息
                if (raw != null && raw.StartsWith("请求失败：", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(raw);
                }

                if (string.IsNullOrWhiteSpace(raw))
                {
                    throw new InvalidOperationException("接口未返回任何内容");
                }
            }

            // 文本美化（去除多余换行等）
            var prettified = Formatter.Prettify(raw);
            if (string.IsNullOrWhiteSpace(prettified))
            {
                throw new InvalidOperationException("未解析到任何结果");
            }

            // 核心解析：文本 -> ParsedPayload（SaleRecord 等结构化数据）
            var payload = Parser.Parse(prettified);
            return payload ?? throw new InvalidOperationException("解析结果为空");
        }
    }
}
