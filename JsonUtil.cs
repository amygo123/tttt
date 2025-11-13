using System.Text.Json;

namespace StyleWatcherWin
{
    internal static class JsonUtil
    {
        private static readonly JsonSerializerOptions _opts = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        public static string Serialize<T>(T obj) => JsonSerializer.Serialize(obj, _opts);
        public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, _opts)!;
    }
}
