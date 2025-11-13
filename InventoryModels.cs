using System;
using System.Collections.Generic;
using System.Linq;

namespace StyleWatcherWin
{
        internal sealed class InvRow
        {
            public string Name { get; set; } = "";
            public string Color { get; set; } = "";
            public string Size { get; set; } = "";
            public string Warehouse { get; set; } = "";
            public int Available { get; set; }
            public int OnHand { get; set; }
        }

        internal sealed class InvSnapshot
        {
            public List<InvRow> Rows { get; } = new();

            public int TotalAvailable => Rows.Sum(r => r.Available);
            public int TotalOnHand => Rows.Sum(r => r.OnHand);

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
                    .OrderByDescending(x => x.v)
                    .Select(x => x.s);

            public Dictionary<string, int> ByWarehouse() =>
                Rows.GroupBy(r => r.Warehouse)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Available));
        }

}
