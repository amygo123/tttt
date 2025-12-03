using System;
using System.Collections.Generic;
using System.Linq;

namespace StyleWatcherWin
{
        public sealed class InvRow
        {
            public string Name { get; set; } = "";
            public string Color { get; set; } = "";
            public string Size { get; set; } = "";
            public string Warehouse { get; set; } = "";
            public int Available { get; set; }
            public int OnHand { get; set; }
        }

        public sealed class InvSnapshot
        {
            public List<InvRow> Rows { get; } = new();

            public int TotalAvailable => Rows.Sum(r => r.Available);
            public int TotalOnHand => Rows.Sum(r => r.OnHand);


            private static int SizeSortKey(string size)
            {
                if (string.IsNullOrWhiteSpace(size)) return int.MaxValue;

                var s = size.Trim().ToUpperInvariant();

                // Common apparel sizes in ascending order
                string[] order =
                {
                    "XXS", "XS",
                    "S",
                    "M", "M-CP",
                    "L",
                    "XL",
                    "2XL", "XXL",
                    "3XL", "4XL", "5XL", "6XL", "7XL", "8XL"
                };

                for (int i = 0; i < order.Length; i++)
                {
                    if (s == order[i]) return i;
                }

                // Numeric sizes like 35, 36, 37...
                if (int.TryParse(s, out var n))
                {
                    // Shift numeric range after textual ones
                    return 1000 + n;
                }

                // Fallback: keep unknowns at the end but stable by name
                return 500_000 + s.GetHashCode();
            }
            public IEnumerable<string> ColorsNonZero() =>
                Rows.GroupBy(r => r.Color)
                    .Select(g => new { c = g.Key, v = g.Sum(x => x.Available) })
                    .Where(x => !string.IsNullOrWhiteSpace(x.c) && x.v != 0)
                    .OrderByDescending(x => x.v)
                    .Select(x => x.c);

            public IEnumerable<string> SizesNonZero() =>
                Rows.GroupBy(r => r.Size)
                    .Select(g => new { s = g.Key, v = g.Sum(x => x.Available) })
                    .Where(x => !string.IsNullOrWhiteSpace(x.s) && x.v != 0)
                    .OrderBy(x => SizeSortKey(x.s))
                    .ThenByDescending(x => x.v)
                    .Select(x => x.s);

            public Dictionary<string, int> ByWarehouse() =>
                Rows.GroupBy(r => r.Warehouse)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Available));
        }

}
