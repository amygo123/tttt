using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;


using System.Text.Json;
using System.Text;
using System;


namespace StyleWatcherWin
{
    public static static class ApiClient
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
