using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
#pragma warning disable 0618
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;

namespace StyleWatcherWin
{
    public class InventoryTabPage : TabPage
    {
        public event Action<int, int, Dictionary<string, int>>? SummaryUpdated;

        #region 数据结构



        #endregion


        private readonly AppConfig _cfg;
        private IInventoryService? _inventoryService;
        private InvSnapshot _all = new();
        private string _styleName = "";

        // 主要图表
        private readonly PlotView _pvHeat = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        private readonly PlotView _pvColor = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        private readonly PlotView _pvSize = new() { Dock = DockStyle.Fill, BackColor = UI.Background };

        // 明细（总览右下）
        private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells };

        // 子 Tab（按仓库）
        private readonly TabControl _subTabs = new() { Dock = DockStyle.Fill };

        // 悬浮提示
        private readonly ToolTip _tip = new() { InitialDelay = 0, ReshowDelay = 0, AutoPopDelay = 8000, ShowAlways = true };

        // 当前点击筛选（总览热力图）
        private (string? color, string? size)? _activeCell = null;

        private Label _lblAvail = new();
        private Label _lblOnHand = new();

        public InventoryTabPage(AppConfig cfg)
        {
            _cfg = cfg;
            Text = "库存";

            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // 顶部工具条（合计/刷新）
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            Controls.Add(root);

            var tools = BuildTopTools();
            root.Controls.Add(tools, 0, 0);

            var four = BuildFourArea();
            root.Controls.Add(four, 0, 1);

            root.Controls.Add(_subTabs, 0, 2);
            // 分仓子 Tab 使用系统默认样式 + 自动宽度，确保仓名与库存数量可以完整显示
            _subTabs.DrawMode = TabDrawMode.Normal;
            _subTabs.SizeMode = TabSizeMode.Normal;
            _subTabs.Appearance = TabAppearance.Normal;
            _subTabs.Padding = new System.Drawing.Point(12, 4);
        }

        public void SetInventoryService(IInventoryService service)
        {
            _inventoryService = service ?? throw new ArgumentNullException(nameof(service));
        }


        
        private Control BuildTopTools()
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _lblAvail = new Label
            {
                AutoSize = true,
                Font = UI.Body,
                ForeColor = UI.Text
            };
            _lblOnHand = new Label
            {
                AutoSize = true,
                Font = UI.Body,
                ForeColor = UI.Text,
                Margin = new Padding(16, 0, 0, 0)
            };

            p.Controls.Add(_lblAvail, 0, 0);
            p.Controls.Add(_lblOnHand, 1, 0);

            var filler = new Panel { Dock = DockStyle.Fill };
            p.Controls.Add(filler, 2, 0);

            var btnReload = new Button { Text = "刷新" };
            UI.StyleSecondary(btnReload);
            btnReload.Margin = new Padding(8, 4, 0, 4);
            btnReload.Click += async (s, e) => await ReloadAsync(_styleName);
            p.Controls.Add(btnReload, 3, 0);

            return p;
        }


        private Control BuildFourArea()
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            PrepareGridColumns(_grid);

            grid.Controls.Add(_pvSize, 0, 0);
            grid.Controls.Add(_pvColor, 1, 0);
            grid.Controls.Add(_pvHeat, 0, 1);
            grid.Controls.Add(_grid, 1, 1);

            AttachHeatmapInteractions(_pvHeat, sel =>
            {
                _activeCell = sel;
                ApplyOverviewFilter();
            });

            return grid;
        }

        public async Task LoadAsync(string styleName)
        {
            _styleName = styleName ?? "";
            await ReloadAsync(_styleName);
        }

        public Task LoadInventoryAsync(string styleName) => LoadAsync(styleName); // 兼容 ResultForm 旧调用
        

        /// <summary>
        /// 清空当前库存视图（用于主查询没有解析出任何款式时，将库存页重置为“无数据”状态）。
        /// </summary>
        public void ResetToEmpty()
        {
            _styleName = string.Empty;
            _activeCell = null;
            var snap = new InvSnapshot();
            _all = snap;

            RenderAll(_all);
            _lblAvail.Text = "可用合计：0（未获取到库存数据）";
            _lblOnHand.Text = "现有合计：0（未获取到库存数据）";
        }



private async Task ReloadAsync(string styleName)
{
    _activeCell = null; // 清筛选

    var snap = await _inventoryService!.GetSnapshotAsync(styleName);

    _all = snap;
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
            RenderBarsByColor(snap, _pvColor, "颜色可用（降序，滚轮/右键查看更多）");
            RenderBarsBySize(snap, _pvSize, "尺码可用（降序，滚轮/右键查看更多）");
            RenderWarehouseTabs(snap);

            try { SummaryUpdated?.Invoke(snap.TotalAvailable, snap.TotalOnHand, snap.ByWarehouse()); } catch { }
            BindGrid(_grid, snap.Rows);
        }

        #endregion

        #region 绘图与缩放（柱状图降序 + 默认 Top10）
        private void RenderBarsByColor(InvSnapshot snap, PlotView pv, string title)
        {
            var data = snap.Rows
                .GroupBy(r => r.Color)
                .Select(g => (Key: g.Key, Qty: (double)g.Sum(x => x.Available)));

            pv.Model = UiCharts.BuildBarModel(data, title, topN: 10);
            BindPanZoom(pv);
        }

        private void RenderBarsBySize(InvSnapshot snap, PlotView pv, string title)
        {
            var data = snap.Rows
                .GroupBy(r => r.Size)
                .Select(g => (Key: g.Key, Qty: (double)g.Sum(x => x.Available)));

            pv.Model = UiCharts.BuildBarModel(data, title, topN: 10);
            BindPanZoom(pv);
        }

        private void ApplyTopNZoom(CategoryAxis cat, int total, int n)
        {
            if (total <= 0) return;
            var maxIndex = Math.Min(n - 1, total - 1);
            cat.Minimum = -0.5;
            cat.Maximum = maxIndex + 0.5;
        }
        #endregion

        #region 热力图（使用分位截断与更直观的配色）







                private void RenderHeatmap(InvSnapshot snap, PlotView pv, string title)
        {
            HeatmapRenderer.BuildHeatmap(snap, pv, title);
            BindPanZoom(pv);
        }

#endregion

        // 供外部调用：切换到指定仓库子页
        public void ActivateWarehouse(string warehouse)
        {
            if (string.IsNullOrWhiteSpace(warehouse)) return;
            foreach (TabPage tp in _subTabs.TabPages)
            {
                var name = tp.Text.Split('（')[0]; // "仓库名（xxx）"
                if (string.Equals(name, warehouse, StringComparison.OrdinalIgnoreCase))
                {
                    _subTabs.SelectedTab = tp;
                    return;
                }
            }
        }

        private void RenderWarehouseTabs(InvSnapshot snap)
        {
            _subTabs.SuspendLayout();
            _subTabs.TabPages.Clear();

            foreach (var g in snap.Rows.GroupBy(r => r.Warehouse).OrderByDescending(x => x.Sum(y => y.Available)))
            {
                var page = new TabPage($"{g.Key}（{g.Sum(x => x.Available)}）");

                // 布局：上=搜索框，下=左右联动（热力图+明细）
                var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

                var search = new TextBox
                {
                    Dock = DockStyle.Fill,
                    PlaceholderText = "搜索本仓（颜色/尺码）",
                    MinimumSize = new Size(0, 28),
                    Margin = new Padding(0, 4, 0, 4)
                };
                UI.StyleInput(search);

                root.Controls.Add(search, 0, 0);

                var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

                var pv = new PlotView { Dock = DockStyle.Fill, BackColor = UI.Background };
                var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells };
                PrepareGridColumns(grid);
                UiGrid.Optimize(grid);

                panel.Controls.Add(pv, 0, 0);
                panel.Controls.Add(grid, 1, 0);

                root.Controls.Add(panel, 0, 1);
                page.Controls.Add(root);

                var subSnap = new InvSnapshot();
                foreach (var r in g) subSnap.Rows.Add(r);
                HeatmapRenderer.BuildHeatmap(subSnap, pv, $"{g.Key} 颜色×尺码");

                // 初始填充该仓明细
                BindGrid(grid, subSnap.Rows);

                // per-tab 筛选状态
                (string? color, string? size)? sel = null;

                // 联动：点击该仓热力图筛选右侧明细
                AttachHeatmapInteractions(pv, newSel =>
                {
                    sel = newSel;
                    ApplyWarehouseFilter(grid, subSnap, search.Text, sel);
                });

                // 搜索：防抖 220ms
                var t = new System.Windows.Forms.Timer { Interval = 220 };
                search.TextChanged += (s2, e2) => { t.Stop(); t.Start(); };
                t.Tick += (s3, e3) => { t.Stop(); ApplyWarehouseFilter(grid, subSnap, search.Text, sel); };

                _subTabs.TabPages.Add(page);
            }
            _subTabs.ResumeLayout();
        }

        private void BindGrid(DataGridView grid, IEnumerable<InvRow> rows)
        {
            PrepareGridColumns(grid);
            UiGrid.Optimize(grid);
            grid.DataSource = new BindingList<InvRow>(rows.ToList());
        }

        private void PrepareGridColumns(DataGridView grid)
        {
            grid.AutoGenerateColumns = false;
            grid.Columns.Clear();
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "品名" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Color", HeaderText = "颜色" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Size", HeaderText = "尺码" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Warehouse", HeaderText = "仓库" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Available", HeaderText = "可用" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "OnHand", HeaderText = "现有" });
        }

        private void ApplyOverviewFilter()
        {
            IEnumerable<InvRow> q = _all.Rows;
            if (_activeCell is { } ac)
            {
                if (!string.IsNullOrEmpty(ac.color)) q = q.Where(r => r.Color == ac.color);
                if (!string.IsNullOrEmpty(ac.size)) q = q.Where(r => r.Size == ac.size);
            }
            BindGrid(_grid, q);
        }

        private void ApplyWarehouseFilter(DataGridView grid, InvSnapshot snap, string key, (string? color, string? size)? sel)
        {
            IEnumerable<InvRow> q = snap.Rows;
            var k = (key ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(k))
            {
                string ToText(InvRow r)
                {
                    var color = r.Color ?? string.Empty;
                    var size = r.Size ?? string.Empty;
                    return string.Concat(color, " ", size);
                }

                q = UiSearch.FilterAllTokens(q, ToText, k);
            }

            if (sel is { } ac)
            {
                if (!string.IsNullOrEmpty(ac.color)) q = q.Where(r => r.Color == ac.color);
                if (!string.IsNullOrEmpty(ac.size)) q = q.Where(r => r.Size == ac.size);
            }

            BindGrid(grid, q);
        }

                #region 交互：悬浮与联动 + 右键平移/滚轮缩放
        private void BindPanZoom(PlotView pv)
        {
            try
            {
                var ctl = pv.Controller ?? new PlotController();
                // 允许滚轮缩放
                ctl.BindMouseWheel(PlotCommands.ZoomWheel);
                // 右键拖拽平移
                ctl.BindMouseDown(OxyMouseButton.Right, PlotCommands.PanAt);
                pv.Controller = ctl;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UI/Forms/InventoryTabPage.cs");
            }
        }

        private void AttachHeatmapInteractions(PlotView pv, Action<(string? color, string? size)?> onSelectionChanged)
        {
            // 取消默认左键行为（包括默认 Tracker）
            try
            {
                var ctl = pv.Controller ?? new PlotController();
                ctl.UnbindMouseDown(OxyMouseButton.Left);
                // 保留中键/右键平移 + 滚轮缩放
                ctl.BindMouseDown(OxyMouseButton.Middle, PlotCommands.PanAt);
                ctl.BindMouseDown(OxyMouseButton.Right, PlotCommands.PanAt);
                ctl.BindMouseWheel(PlotCommands.ZoomWheel);
                pv.Controller = ctl;
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UI/Forms/InventoryTabPage.cs");
                // ignore
            }

            pv.MouseMove += (s, e) =>
            {
                var model = pv.Model;
                if (model == null) return;

                var hm = model.Series.OfType<HeatMapSeries>().FirstOrDefault();
                var ctx = pv.Tag as HeatmapContext;
                if (hm == null || ctx == null || ctx.Colors.Count == 0 || ctx.Sizes.Count == 0) return;

                try
                {
                    var sp = new ScreenPoint(e.Location.X, e.Location.Y);
                    var hit = hm.GetNearestPoint(sp, false);
                    if (hit == null)
                    {
                        _tip.Hide(pv);
                        return;
                    }

                    var xi = (int)Math.Round(hit.DataPoint.X);
                    var yi = (int)Math.Round(hit.DataPoint.Y);

                    // Data is stored as [sizeIndex, colorIndex], axes are:
                    //   X -> 尺码(Size), Y -> 颜色(Color)
                    if (xi >= 0 && xi < ctx.Sizes.Count && yi >= 0 && yi < ctx.Colors.Count)
                    {
                        var size = ctx.Sizes[xi];
                        var color = ctx.Colors[yi];
                        var val = ctx.Data[xi, yi];
                        _tip.Show($"颜色：{color}  尺码：{size}  库存：{val:0}", pv, e.Location.X + 12, e.Location.Y + 12);
                    }
                    else
                    {
                        _tip.Hide(pv);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex, "UI/Heatmap/MouseMove");
                    _tip.Hide(pv);
                }
            };

            pv.MouseLeave += (s, e) => _tip.Hide(pv);

            pv.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Left) return;

                var model = pv.Model;
                if (model == null) return;

                var hm = model.Series.OfType<HeatMapSeries>().FirstOrDefault();
                var ctx = pv.Tag as HeatmapContext;
                if (hm == null || ctx == null || ctx.Colors.Count == 0 || ctx.Sizes.Count == 0) return;

                try
                {
                    var sp = new ScreenPoint(e.Location.X, e.Location.Y);
                    var hit = hm.GetNearestPoint(sp, false);
                    if (hit != null)
                    {
                        var xi = (int)Math.Round(hit.DataPoint.X);
                        var yi = (int)Math.Round(hit.DataPoint.Y);
                        // X: size index, Y: color index
                        if (xi >= 0 && xi < ctx.Sizes.Count && yi >= 0 && yi < ctx.Colors.Count)
                        {
                            var size = ctx.Sizes[xi];
                            var color = ctx.Colors[yi];
                            onSelectionChanged((color, size));
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex, "UI/Heatmap/MouseDown");
                }

                // 点击空白取消
                onSelectionChanged(null);
            };
        }
        #endregion
        public System.Collections.Generic.IEnumerable<string> CurrentZeroSizes()
        {
            return _all.Rows
                .GroupBy(r => r.Size)
                .Select(g => new { s = g.Key, v = g.Sum(x => x.Available) })
                .Where(x => !string.IsNullOrWhiteSpace(x.s) && x.v == 0)
                .Select(x => x.s)
                .ToList();
        }

        public System.Collections.Generic.IEnumerable<string> OfferedSizes()
        {
            return _all.Rows
                .Select(r => r.Size)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }


        internal System.Collections.Generic.IReadOnlyList<InvRow> AllRows => _all.Rows;
    }
}
#pragma warning restore 0618