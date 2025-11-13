using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json.Serialization;


using System.Text.Json;
using System.Text;
using System;


namespace StyleWatcherWin
{
    internal static class ConfigCore
    {
        public static T Load<T>(string path)
        {
            var json = System.IO.File.ReadAllText(path);
            return JsonUtil.Deserialize<T>(json)!;
        }

        public static void Save<T>(string path, T obj)
        {
            obj.Save(path);}
    }
}
