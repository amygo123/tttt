using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
#pragma warning disable 0618
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace StyleWatcherWin
{
    // 确保这些辅助类是公开的，供 ResultForm 使用
    public class InvRow
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public string Size { get; set; } = "";
        public string Warehouse { get; set; } = "";
        public int Available { get; set; }
        public int OnHand { get; set; }
    }

    public class InvSnapshot
    {
        public List<InvRow> Rows { get; } = new();
        public int TotalAvailable => Rows.Sum(x => x.Available);
        public int TotalOnHand => Rows.Sum(x => x.OnHand);
        public Dictionary<string, int> ByWarehouse() =>
            Rows.GroupBy(x => x.Warehouse).ToDictionary(g => g.Key, g => g.Sum(x => x.Available));
    }

    public class InventoryTabPage : TabPage
    {
        public event Action<int, int, Dictionary<string, int>>? SummaryUpdated;

        private static readonly HttpClient _http = new();
        private readonly AppConfig _cfg;
        private InvSnapshot _all = new();
        private string _styleName = "";

        // 主要图表
        private readonly PlotView _pvHeat = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        private readonly PlotView _pvColor = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        private readonly PlotView _pvSize = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells };
        private readonly TabControl _subTabs = new() { Dock = DockStyle.Fill };
        private readonly ToolTip _tip = new() { InitialDelay = 0, ReshowDelay = 0, AutoPopDelay = 8000, ShowAlways = true };

        private (string? color, string? size)? _activeCell = null;
        private Label _lblAvail = new();
        private Label _lblOnHand = new();

        public InventoryTabPage(AppConfig cfg)
        {
            _cfg = cfg;
            _http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _cfg.timeout_seconds));
            Text = "库存";

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            Controls.Add(root);

            root.Controls.Add(BuildTopTools(), 0, 0);
            root.Controls.Add(BuildFourArea(), 0, 1);
            root.Controls.Add(_subTabs, 0, 2);

            _subTabs.DrawMode = TabDrawMode.Normal;
            _subTabs.SizeMode = TabSizeMode.Normal;
            _subTabs.Padding = new System.Drawing.Point(12, 4);
        }

        private Control BuildTopTools()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _lblAvail = new Label { AutoSize = true, Font = UI.Body, ForeColor = UI.Text };
            _lblOnHand = new Label { AutoSize = true, Font = UI.Body, ForeColor = UI.Text, Margin = new Padding(16, 0, 0, 0) };
            var btnReload = new Button { Text = "刷新" };
            UI.StyleSecondary(btnReload);
            btnReload.Click += async (s, e) => await ReloadAsync(_styleName);

            p.Controls.Add(_lblAvail, 0, 0); p.Controls.Add(_lblOnHand, 1, 0); p.Controls.Add(new Panel(), 2, 0); p.Controls.Add(btnReload, 3, 0);
            return p;
        }

        private Control BuildFourArea()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            PrepareGridColumns(_grid);
            grid.Controls.Add(_pvSize, 0, 0); grid.Controls.Add(_pvColor, 1, 0); grid.Controls.Add(_pvHeat, 0, 1); grid.Controls.Add(_grid, 1, 1);
            AttachHeatmapInteractions(_pvHeat, sel => { _activeCell = sel; ApplyOverviewFilter(); });
            return grid;
        }

        public async Task LoadAsync(string styleName) { _styleName = styleName ?? ""; await ReloadAsync(_styleName); }
        public Task LoadInventoryAsync(string styleName) => LoadAsync(styleName);

        public void ResetToEmpty()
        {
            _styleName = string.Empty; _activeCell = null; _all = new InvSnapshot();
            RenderAll(_all);
            _lblAvail.Text = "可用合计：0（未获取到库存数据）";
            _lblOnHand.Text = "现有合计：0（未获取到库存数据）";
        }

        private async Task ReloadAsync(string styleName)
        {
            _activeCell = null;
            _all = await FetchInventoryAsync(styleName);
            RenderAll(_all);
            if (_all.Rows.Count == 0)
            {
                _lblAvail.Text = "可用合计：0（未获取到库存数据）";
                _lblOnHand.Text = "现有合计：0（未获取到库存数据）";
            }
        }

        private void RenderAll(InvSnapshot snap)
        {
            _lblAvail.Text = $"可用合计：{snap.TotalAvailable:N0}";
            _lblOnHand.Text = $"现有合计：{snap.TotalOnHand:N0}";
            RenderHeatmap(snap, _pvHeat, "颜色×尺码 可用数热力图");
            RenderBarsByColor(snap, _pvColor, "颜色可用（降序）");
            RenderBarsBySize(snap, _pvSize, "尺码可用（降序）");
            RenderWarehouseTabs(snap);
            try { SummaryUpdated?.Invoke(snap.TotalAvailable, snap.TotalOnHand, snap.ByWarehouse()); } catch { }
            BindGrid(_grid, snap.Rows);
        }

        private async Task<InvSnapshot> FetchInventoryAsync(string styleName)
        {
            var s = new InvSnapshot();
            if (string.IsNullOrWhiteSpace(styleName)) return s;
            try
            {
                var baseUrl = (_cfg?.inventory?.url_base ?? "");
                var url = baseUrl.Contains("style_name=") ? baseUrl + Uri.EscapeDataString(styleName) : baseUrl.TrimEnd('/') + "?style_name=" + Uri.EscapeDataString(styleName);
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                var resp = await _http.SendAsync(req);
                var raw = await resp.Content.ReadAsStringAsync();
                List<string>? lines = null;
                try { lines = JsonSerializer.Deserialize<List<string>>(raw); } catch { }
                if (lines == null) lines = raw.Replace("\r\n", "\n").Split('\n').ToList();
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var seg = line.Replace('，', ',').Split(',');
                    if (seg.Length < 6) continue;
                    s.Rows.Add(new InvRow { Name = seg[0].Trim(), Color = seg[1].Trim(), Size = seg[2].Trim(), Warehouse = seg[3].Trim(), Available = int.TryParse(seg[4].Trim(), out var a) ? a : 0, OnHand = int.TryParse(seg[5].Trim(), out var h) ? h : 0 });
                }
            }
            catch (Exception ex) { AppLogger.LogError(ex, "UI/Forms/InventoryTabPage.cs"); }
            return s;
        }

        private void RenderBarsByColor(InvSnapshot snap, PlotView pv, string title) { pv.Model = UiCharts.BuildBarModel(snap.Rows.GroupBy(r => r.Color).Select(g => (Key: g.Key, Qty: (double)g.Sum(x => x.Available))), title, topN: 10); BindPanZoom(pv); }
        private void RenderBarsBySize(InvSnapshot snap, PlotView pv, string title) { pv.Model = UiCharts.BuildBarModel(snap.Rows.GroupBy(r => r.Size).Select(g => (Key: g.Key, Qty: (double)g.Sum(x => x.Available))), title, topN: 10); BindPanZoom(pv); }
        private void RenderHeatmap(InvSnapshot snap, PlotView pv, string title) { HeatmapRenderer.BuildHeatmap(snap, pv, title); BindPanZoom(pv); }

        private void RenderWarehouseTabs(InvSnapshot snap)
        {
            _subTabs.SuspendLayout(); _subTabs.TabPages.Clear();
            foreach (var g in snap.Rows.GroupBy(r => r.Warehouse).OrderByDescending(x => x.Sum(y => y.Available)))
            {
                var page = new TabPage($"{g.Key}（{g.Sum(x => x.Available)}）");
                var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 }; root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var search = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "搜索本仓", MinimumSize = new Size(0, 28) }; UI.StyleInput(search);
                var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 }; panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                var pv = new PlotView { Dock = DockStyle.Fill, BackColor = UI.Background }; var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells }; PrepareGridColumns(grid); UiGrid.Optimize(grid);
                panel.Controls.Add(pv, 0, 0); panel.Controls.Add(grid, 1, 0); root.Controls.Add(search, 0, 0); root.Controls.Add(panel, 0, 1); page.Controls.Add(root);
                var subSnap = new InvSnapshot(); foreach (var r in g) subSnap.Rows.Add(r); HeatmapRenderer.BuildHeatmap(subSnap, pv, $"{g.Key}"); BindGrid(grid, subSnap.Rows);
                (string? color, string? size)? sel = null;
                AttachHeatmapInteractions(pv, newSel => { sel = newSel; ApplyWarehouseFilter(grid, subSnap, search.Text, sel); });
                search.TextChanged += (s, e) => ApplyWarehouseFilter(grid, subSnap, search.Text, sel);
                _subTabs.TabPages.Add(page);
            }
            _subTabs.ResumeLayout();
        }

        private void BindGrid(DataGridView grid, IEnumerable<InvRow> rows) { PrepareGridColumns(grid); UiGrid.Optimize(grid); grid.DataSource = new BindingList<InvRow>(rows.ToList()); }
        private void PrepareGridColumns(DataGridView grid) { grid.AutoGenerateColumns = false; grid.Columns.Clear(); grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "品名" }); grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Color", HeaderText = "颜色" }); grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Size", HeaderText = "尺码" }); grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Warehouse", HeaderText = "仓库" }); grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Available", HeaderText = "可用" }); grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OnHand", HeaderText = "现有" }); }

        private void ApplyOverviewFilter() { IEnumerable<InvRow> q = _all.Rows; if (_activeCell is { } ac) { if (!string.IsNullOrEmpty(ac.color)) q = q.Where(r => r.Color == ac.color); if (!string.IsNullOrEmpty(ac.size)) q = q.Where(r => r.Size == ac.size); } BindGrid(_grid, q); }
        private void ApplyWarehouseFilter(DataGridView grid, InvSnapshot snap, string key, (string? color, string? size)? sel) { IEnumerable<InvRow> q = snap.Rows; var k = (key ?? string.Empty).Trim(); if (!string.IsNullOrWhiteSpace(k)) { q = UiSearch.FilterAllTokens(q, r => r.Color + " " + r.Size, k); } if (sel is { } ac) { if (!string.IsNullOrEmpty(ac.color)) q = q.Where(r => r.Color == ac.color); if (!string.IsNullOrEmpty(ac.size)) q = q.Where(r => r.Size == ac.size); } BindGrid(grid, q); }

        private void BindPanZoom(PlotView pv) { try { var ctl = pv.Controller ?? new PlotController(); ctl.BindMouseWheel(PlotCommands.ZoomWheel); ctl.BindMouseDown(OxyMouseButton.Right, PlotCommands.PanAt); pv.Controller = ctl; } catch (Exception ex) { AppLogger.LogError(ex, "UI/Forms/InventoryTabPage.cs"); } }

        private void AttachHeatmapInteractions(PlotView pv, Action<(string? color, string? size)?> onSelectionChanged)
        {
            try { var ctl = pv.Controller ?? new PlotController(); ctl.UnbindMouseDown(OxyMouseButton.Left); ctl.BindMouseDown(OxyMouseButton.Middle, PlotCommands.PanAt); ctl.BindMouseDown(OxyMouseButton.Right, PlotCommands.PanAt); ctl.BindMouseWheel(PlotCommands.ZoomWheel); pv.Controller = ctl; } catch { }
            pv.MouseMove += (s, e) => {
                var model = pv.Model; if (model == null) return; var hm = model.Series.OfType<HeatMapSeries>().FirstOrDefault(); var ctx = pv.Tag as HeatmapContext; if (hm == null || ctx == null) return;
                var sp = new ScreenPoint(e.Location.X, e.Location.Y); var hit = hm.GetNearestPoint(sp, false);
                if (hit != null) { int xi = (int)Math.Round(hit.DataPoint.X), yi = (int)Math.Round(hit.DataPoint.Y); if (xi >= 0 && xi < ctx.Sizes.Count && yi >= 0 && yi < ctx.Colors.Count) { _tip.Show($"颜色：{ctx.Colors[yi]}  尺码：{ctx.Sizes[xi]}  库存：{ctx.Data[xi, yi]:0}", pv, e.Location.X + 12, e.Location.Y + 12); return; } } _tip.Hide(pv);
            };
            pv.MouseLeave += (s, e) => _tip.Hide(pv);
            pv.MouseDown += (s, e) => {
                if (e.Button != MouseButtons.Left) return; var model = pv.Model; if (model == null) return; var hm = model.Series.OfType<HeatMapSeries>().FirstOrDefault(); var ctx = pv.Tag as HeatmapContext;
                if (hm != null && ctx != null) { var hit = hm.GetNearestPoint(new ScreenPoint(e.Location.X, e.Location.Y), false); if (hit != null) { int xi = (int)Math.Round(hit.DataPoint.X), yi = (int)Math.Round(hit.DataPoint.Y); if (xi >= 0 && xi < ctx.Sizes.Count && yi >= 0 && yi < ctx.Colors.Count) { onSelectionChanged((ctx.Colors[yi], ctx.Sizes[xi])); return; } } } onSelectionChanged(null);
            };
        }

        public IEnumerable<string> CurrentZeroSizes() => _all.Rows.GroupBy(r => r.Size).Select(g => new { s = g.Key, v = g.Sum(x => x.Available) }).Where(x => !string.IsNullOrWhiteSpace(x.s) && x.v == 0).Select(x => x.s).ToList();
        public IEnumerable<string> OfferedSizes() => _all.Rows.Select(r => r.Size).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        public IReadOnlyList<InvRow> AllRows => _all.Rows;
    }
}
#pragma warning restore 0618