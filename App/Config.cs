using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace StyleWatcherWin
{
    public class AppConfig
    {
        public string api_url { get; set; } = "http://47.111.189.27:8089/qrcode/saleVolumeParserNew";
        public string method { get; set; } = "POST";
        public string json_key { get; set; } = "code";
        public int timeout_seconds { get; set; } = 6;
        // 唯品库存专用超时（秒），未配置则回退到 timeout_seconds
        public int vip_timeout_seconds { get; set; } = 20;
        public string hotkey { get; set; } = "Alt+S";

        public WindowCfg window { get; set; } = new WindowCfg();
        public InventoryCfg inventory { get; set; } = new InventoryCfg();
        public UiCfg ui { get; set; } = new UiCfg();
        public InventoryAlertCfg inventoryAlert { get; set; } = new InventoryAlertCfg();
        public HeadersCfg headers { get; set; } = new HeadersCfg();

        public class WindowCfg
        {
            public int width { get; set; } = 1600;
            public int height { get; set; } = 900;
            public int fontSize { get; set; } = 13;
            public bool alwaysOnTop { get; set; } = true;
        }

        public class HeadersCfg
        {
            [JsonExtensionData]
            public Dictionary<string, JsonElement> ExtraHeaders { get; set; } = new Dictionary<string, JsonElement>();
        }

        public class InventoryCfg
        {
            // 库存查询基础地址，按当前实现要求包含 style_name 参数
            public string url_base { get; set; } = "http://192.168.40.97:8000/inventory?style_name=";

            // 退货率查询基础地址（可选），按当前实现要求包含 style_name 参数
            public string return_rate_url_base { get; set; } = "http://192.168.40.97:8004/inventory?style_name=";

            // 款式信息 / 价格查询基础地址（可选）
            public string price_url_base { get; set; } = "";
        }

        public class UiCfg
        {
            // 趋势窗口配置（保留多窗口支持，不含 MA7 逻辑）
            public int[] trendWindows { get; set; } = new[] { 7, 14, 30 };
        }

        public class InventoryAlertCfg
        {
            public double docRed { get; set; } = 3;
            public double docYellow { get; set; } = 7;
            public int minSalesWindowDays { get; set; } = 7;
        }

        public static string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        public static AppConfig Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                {
                    var def = new AppConfig();
                    var jsonNew = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(ConfigPath, jsonNew, Encoding.UTF8);
                    return def;
                }

                var txt = File.ReadAllText(ConfigPath, Encoding.UTF8);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                var cfg = JsonSerializer.Deserialize<AppConfig>(txt, options);
                return cfg ?? new AppConfig();
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "App/Config.cs");
                return new AppConfig();
            }
        }

        public static void Save(AppConfig cfg)
        {
            var json = JsonSerializer.Serialize(cfg ?? new AppConfig(), new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(ConfigPath, json, Encoding.UTF8);
        }
    }

    public static class ApiHelper
{
    public static async System.Threading.Tasks.Task<string> QueryAsync(AppConfig cfg, string text, CancellationToken cancellationToken = default)
    {
        if (cfg == null) return "请求失败：配置为空";
        if (string.IsNullOrWhiteSpace(cfg.api_url)) return "请求失败：未配置 api_url";

        var method = string.IsNullOrWhiteSpace(cfg.method) ? "POST" : cfg.method.ToUpperInvariant();

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, cfg.timeout_seconds))
        };

        var url = cfg.api_url;
        var request = new HttpRequestMessage(new HttpMethod(method), url);

        if (cfg.headers?.ExtraHeaders != null)
        {
            foreach (var kv in cfg.headers.ExtraHeaders)
            {
                var value = kv.Value.ToString().Trim('"');
                if (!string.IsNullOrWhiteSpace(value))
                {
                    request.Headers.TryAddWithoutValidation(kv.Key, value);
                }
            }
        }

        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            // GET 请求不需要内容
        }
        else
        {
            request.Content = new StringContent(text ?? string.Empty, Encoding.UTF8, "text/plain");
        }

        try
        {
            var resp = await http.SendAsync(request, cancellationToken);
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadAsStringAsync();

            // 新版接口：优先从 JSON 中提取 msg 字段（如果存在）
            var trimmed = raw?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && trimmed.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.TryGetProperty("msg", out var msgProp) &&
                        msgProp.ValueKind == JsonValueKind.String)
                    {
                        var msg = msgProp.GetString();
                        if (!string.IsNullOrWhiteSpace(msg))
                        {
                            return msg;
                        }
                    }
                }
                catch
                {
                    // 容错：如果解析失败，退回到原始文本
                }
            }

            return raw ?? string.Empty;
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "App/Config.cs");
            var friendly = BuildFriendlyErrorMessage(ex, url, cfg.timeout_seconds);
            return "请求失败：" + friendly;
        }
    }

    private static string BuildFriendlyErrorMessage(Exception ex, string url, int timeoutSeconds)
    {
        if (ex is HttpRequestException)
        {
            string host;
            try
            {
                host = new Uri(url).Host;
            }
            catch
            {
                host = url;
            }

            return $"无法请求接口 {host}：{ex.Message}";
        }

        if (ex is TaskCanceledException)
        {
            return $"请求超时（超过 {timeoutSeconds} 秒），请稍后重试。";
        }

        return ex.Message;
    }

    public static async System.Threading.Tasks.Task<string> QueryInventoryAsync(AppConfig cfg, string styleName, CancellationToken cancellationToken = default)
    {
        if (cfg == null || cfg.inventory == null)
            return "[] // 请求失败：未配置库存接口";

        var baseUrl = cfg.inventory.url_base;
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(styleName))
            return "[] // 请求失败：库存接口地址或款号为空";

        string url;
        if (baseUrl.Contains("style_name="))
        {
            url = baseUrl + Uri.EscapeDataString(styleName);
        }
        else
        {
            var connector = baseUrl.Contains("?") ? "&" : "?";
            url = baseUrl + connector + "style_name=" + Uri.EscapeDataString(styleName);
        }

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, cfg.timeout_seconds))
        };

        try
        {
            var resp = await http.GetAsync(url, cancellationToken);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "App/Config.cs");
            return "[] // 请求失败：" + ex.Message;
        }
    }

    public static async System.Threading.Tasks.Task<string> QueryStyleInfoAsync(AppConfig cfg, string styleName)
    {
        if (cfg == null || cfg.inventory == null || cfg.inventory.style_info == null)
            return "";

        var styleInfo = cfg.inventory.style_info;
        if (string.IsNullOrWhiteSpace(styleInfo.url_base) || string.IsNullOrWhiteSpace(styleName))
            return "";

        string url;
        if (styleInfo.url_base.Contains("style_name="))
        {
            url = styleInfo.url_base + Uri.EscapeDataString(styleName);
        }
        else
        {
            var connector = styleInfo.url_base.Contains("?") ? "&" : "?";
            url = styleInfo.url_base + connector + "style_name=" + Uri.EscapeDataString(styleName);
        }

        using var http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(1, cfg.timeout_seconds))
        };

        try
        {
            var resp = await http.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "App/Config.cs");
            return "";
        }
    }
}

}