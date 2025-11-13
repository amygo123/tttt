using System;
using System.Collections.Generic;
using System.Linq;

namespace StyleWatcherWin
{
    internal static class UiSearch
    {
        public static IEnumerable<T> FilterAllTokens<T>(IEnumerable<T> source, Func<T, string> toText, string query)
        {
            if (source == null) return Array.Empty<T>();
            query = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query)) return source;

            var parts = query.ToLowerInvariant()
                             .Split(new[]{' '}, StringSplitOptions.RemoveEmptyEntries)
                             .Distinct().ToArray();
            if (parts.Length == 0) return source;

            return source.Where(item =>
            {
                var text = (toText(item) ?? string.Empty).ToLowerInvariant();
                return parts.All(p => text.Contains(p));
            });
        }
    }
}
