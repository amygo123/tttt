using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StyleWatcherWin
{
    public class AppConfig
    {
        public string api_url { get; set; } = "http://47.111.189.27:8089/qrcode/saleVolumeParser";
        public string method { get; set; } = "POST";
        public string json_key { get; set; } = "code";
        public int timeout_seconds { get; set; } = 6;
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
        public static async System.Threading.Tasks.Task<string> QueryAsync(AppConfig cfg, string text)
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
                    request.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }
            }

            if (cfg.query != null && cfg.query.Count != 0)
            {
                var q = string.Join("&", cfg.query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
                url = cfg.api_url.Contains("?")
                    ? cfg.api_url + "&" + q
                    : cfg.api_url + "?" + q;

                request.RequestUri = new Uri(url);
            }

            if (method == "POST" || method == "PUT")
            {
                var body = cfg.body;
                if (string.IsNullOrWhiteSpace(body))
                {
                    body = text;
                }

                request.Content = new StringContent(body ?? string.Empty);
            }

            try
            {
                var resp = await http.SendAsync(request);
                resp.EnsureSuccessStatusCode();
                var raw = await resp.Content.ReadAsStringAsync();

                // 新版接口：优先从 JSON 中提取 msg 字段（如果存在）
                var trimmed = raw?.Trim();
                if (!string.IsNullOrEmpty(trimmed) && trimmed.StartsWith("{"))
                {
                    try
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                        if (doc.RootElement.TryGetProperty("msg", out var msgProp) &&
                            msgProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var msg = msgProp.GetString();
                            if (!string.IsNullOrEmpty(msg))
                                return msg;
                        }
                    }
                    catch
                    {
                        // 容错：如果解析失败，退回到原始文本
                    }
                }

                return raw;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "App/Config.cs");
                return "";
            }
        }
    }
}