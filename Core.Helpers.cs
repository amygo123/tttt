using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StyleWatcherWin
{
    // ===== Merged from: Aggregations.cs =====
    public static class Aggregations
        {
            // —— 数据模型 —— //
            public struct SalesItem
            {
                public DateTime Date;
                public string Size;
                public string Color;
                public int Qty;
            }

            // —— 构建按日聚合序列 —— //
            public static List<(DateTime day, int qty)> BuildDateSeries(IEnumerable<SalesItem> sales, int windowDays)
            {
                var list = sales.ToList();
                if (list.Count == 0) return new List<(DateTime, int)>();

                var minDay = list.Min(x => x.Date.Date);
                var maxDay = list.Max(x => x.Date.Date);

                // 若指定窗口，则只取最近 windowDays 天
                if (windowDays > 0)
                {
                    var from = maxDay.AddDays(1 - windowDays);
                    if (from > minDay) minDay = from;
                }

                var dict = list
                    .GroupBy(x => x.Date.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(z => z.Qty));

                var result = new List<(DateTime, int)>();
                for (var day = minDay; day <= maxDay; day = day.AddDays(1))
                {
                    dict.TryGetValue(day, out var qty);
                    result.Add((day, qty));
                }
                return result;
            }

            // —— 数字格式化（K/M） —— //
            public static string FormatNumber(double v)
            {
                if (Math.Abs(v) >= 1_000_000) return (v / 1_000_000d).ToString("0.##") + "M";
                if (Math.Abs(v) >= 1_000) return (v / 1_000d).ToString("0.##") + "K";
                return v.ToString("0");
            }
        }

    // ===== Merged from: Config.cs =====
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
                catch
                {
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
                        var value = kv.Value.ToString().Trim('"');
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            request.Headers.TryAddWithoutValidation(kv.Key, value);
                        }
                    }
                }

                if (method == "GET")
                {
                    var key = string.IsNullOrWhiteSpace(cfg.json_key) ? "code" : cfg.json_key;
                    var connector = url.Contains("?") ? "&" : "?";
                    request.RequestUri = new Uri(url + connector + Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(text ?? string.Empty));
                }
                else
                {
                    var key = string.IsNullOrWhiteSpace(cfg.json_key) ? "code" : cfg.json_key;
                    var body = new Dictionary<string, string>
                    {
                        [key] = text ?? string.Empty
                    };
                    var json = JsonSerializer.Serialize(body);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                }

                try
                {
                    var resp = await http.SendAsync(request);
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return "请求失败：" + ex.Message;
                }
            }

            public static async System.Threading.Tasks.Task<string> QueryInventoryAsync(AppConfig cfg, string styleName)
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
                    var resp = await http.GetAsync(url);
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    return "[] // 请求失败：" + ex.Message;
                }
            }

            public static async System.Threading.Tasks.Task<string> QueryStyleInfoAsync(AppConfig cfg, string styleName)
            {
                if (cfg == null || cfg.inventory == null)
                    return "";

                var baseUrl = cfg.inventory.price_url_base;
                if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(styleName))
                    return "";

                var url = baseUrl + Uri.EscapeDataString(styleName);

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
                catch
                {
                    return "";
                }
            }
        }

}
