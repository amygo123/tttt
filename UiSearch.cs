using System;
using System.Collections.Generic;
using System.Linq;

namespace StyleWatcherWin
{
    /// <summary>
    /// Shared search helpers for WinForms grids and collections.
    /// Non-breaking: currently unused; can be wired in gradually.
    /// </summary>
    internal static class UiSearch
    {
        /// <summary>
        /// Filters a sequence by splitting the query into tokens (space-separated)
        /// and requiring that each token appears in the text representation of the item.
        /// Returns the original sequence if the query is null/empty/whitespace.
        /// </summary>
        public static IEnumerable<T> FilterAllTokens<T>(IEnumerable<T> source, Func<T, string> toText, string query)
        {
            if (source == null) return Array.Empty<T>();
            if (toText == null) throw new ArgumentNullException(nameof(toText));

            query = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query))
                return source;

            var tokens = query
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLowerInvariant())
                .Distinct()
                .ToArray();

            if (tokens.Length == 0)
                return source;

            return source.Where(item =>
            {
                var text = (toText(item) ?? string.Empty).ToLowerInvariant();
                foreach (var token in tokens)
                {
                    if (!text.Contains(token))
                        return false;
                }
                return true;
            });
        }
    }
}
