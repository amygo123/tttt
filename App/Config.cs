using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StyleWatcherWin
{

    public class ApiResult<T>
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public T? Data { get; set; }

        public static ApiResult<T> Ok(T data) => new ApiResult<T>
        {
            Success = true,
            Data = data
        };

        public static ApiResult<T> Fail(string? errorMessage) => new ApiResult<T>
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }

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

        private static string BuildFriendlyErrorMessage(Exception ex, string url, int timeoutSeconds, string scene)
        {
            // HTTP 请求类异常
            if (ex is HttpRequestException)
            {
                var host = string.Empty;
                try
                {
                    host = new Uri(url).Host;
                }
                catch
                {
                    host = url;
                }
                return $"网络 / 连接错误（场景：{scene}），请检查网络或服务器地址：{host}";
            }

            // 超时（通常是 TaskCanceledException / OperationCanceledException）
            if (ex is System.Threading.Tasks.TaskCanceledException || ex is OperationCanceledException)
            {
                if (timeoutSeconds <= 0) timeoutSeconds = 1;
                return $"请求超时（场景：{scene}），当前超时时间为 {timeoutSeconds} 秒，可在配置中进行调整。";
            }

            // 其他异常：保留原始消息，方便排查
            return $"调用接口出现异常（场景：{scene}）：{ex.Message}";
        }


        public static async System.Threading.Tasks.Task<ApiResult<string>> QueryAsync(AppConfig cfg, string text)
        {
            if (cfg == null) return ApiResult<string>.Fail("配置为空");
            if (string.IsNullOrWhiteSpace(cfg.api_url)) return ApiResult<string>.Fail("未配置 api_url");

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
                var content = await resp.Content.ReadAsStringAsync();
                return ApiResult<string>.Ok(content);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "App/Config.cs");
                var friendly = BuildFriendlyErrorMessage(ex, url, cfg?.timeout_seconds ?? 6, "款式价格接口");
                return ApiResult<string>.Fail(friendly);
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
                AppLogger.LogError(ex, "App/Config.cs");
                return "[] // 请求失败：" + ex.Message;
            }
        }

        public static async System.Threading.Tasks.Task<ApiResult<string>> QueryStyleInfoAsync(AppConfig cfg, string styleName)
        {
            if (cfg == null || cfg.inventory == null)
                return ApiResult<string>.Fail("未配置库存接口");

            var baseUrl = cfg.inventory.price_url_base;
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(styleName))
                return ApiResult<string>.Fail("未配置价格接口或款号为空");

            var url = baseUrl + Uri.EscapeDataString(styleName);

            using var http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, cfg.timeout_seconds))
            };

            try
            {
                var resp = await http.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var content = await resp.Content.ReadAsStringAsync();
                return ApiResult<string>.Ok(content);
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "App/Config.cs");
                var friendly = BuildFriendlyErrorMessage(ex, url, cfg?.timeout_seconds ?? 6, "款式价格接口");
                return ApiResult<string>.Fail(friendly);
            }
        }
    }
}