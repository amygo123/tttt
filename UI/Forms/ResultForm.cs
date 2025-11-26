using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClosedXML.Excel;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.Annotations;
using OxyPlot.WindowsForms;
using System.Text.Json;

namespace StyleWatcherWin
{
    static class UI
    {
        public static readonly Font Title    = new("Microsoft YaHei UI", 12, FontStyle.Bold);
        public static readonly Font Body     = new("Microsoft YaHei UI", 10);
        public static readonly Font Subtitle = new("Microsoft YaHei UI", 9,  FontStyle.Regular);
        public static readonly Font KpiValue = new("Microsoft YaHei UI", 14, FontStyle.Bold);

        public static readonly Color Background = Color.White;
        public static readonly Color HeaderBack = Color.FromArgb(245, 247, 250);
        public static readonly Color CardBack   = Color.FromArgb(250, 250, 250);
        public static readonly Color CardBorder = Color.FromArgb(230, 232, 236);
        public static readonly Color Text       = Color.FromArgb(47, 47, 47);
        public static readonly Color MutedText  = Color.FromArgb(130, 136, 148);

        public static readonly Color ChipBack   = Color.FromArgb(235, 238, 244);
        public static readonly Color ChipBorder = Color.FromArgb(210, 214, 222);

        public static readonly Color Red        = Color.FromArgb(215, 58, 73);
        public static readonly Color Yellow     = Color.FromArgb(216, 160, 18);
        public static readonly Color Green      = Color.FromArgb(26, 127, 55);

        public static void StylePrimary(Button b)
        {
            b.AutoSize = true;
            b.Padding = new Padding(16, 6, 16, 6);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Green;
            b.ForeColor = Color.White;
            b.Font = Body;
        }

        public static void StyleSecondary(Button b)
        {
            b.AutoSize = true;
            b.Padding = new Padding(12, 6, 12, 6);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = CardBorder;
            b.BackColor = CardBack;
            b.ForeColor = Text;
            b.Font = Body;
        }

        public static void StyleInput(TextBox t)
        {
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = UI.Background;
            t.ForeColor = Text;
            t.Font = Body;
            t.Margin = new Padding(0, 2, 0, 2);
        }

        public static void StyleTabs(TabControl tabs)
        {
            if (tabs == null) return;

            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(96, 32);
            tabs.Padding = new Point(16, 4);
            tabs.Appearance = TabAppearance.Normal;
            tabs.Alignment = TabAlignment.Top;
            tabs.Multiline = false;
            tabs.BackColor = HeaderBack;

            tabs.DrawItem -= Tabs_DrawItem;
            tabs.DrawItem += Tabs_DrawItem;
        }

        private static void Tabs_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tabs || e.Index < 0 || e.Index >= tabs.TabPages.Count)
                return;

            var page = tabs.TabPages[e.Index];
            var bounds = e.Bounds;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            using (var bg = new SolidBrush(selected ? Background : HeaderBack))
            {
                e.Graphics.FillRectangle(bg, bounds);
            }

            // 底部分割线
            using (var sepPen = new Pen(CardBorder))
            {
                e.Graphics.DrawLine(sepPen, bounds.Left, bounds.Bottom - 1, bounds.Right, bounds.Bottom - 1);
            }

            if (selected)
            {
                using (var accent = new Pen(Green, 2))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawLine(accent, bounds.Left + 8, bounds.Bottom - 2, bounds.Right - 8, bounds.Bottom - 2);
                }
            }

            var text = page.Text;
            using (var brush = new SolidBrush(selected ? Text : MutedText))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                e.Graphics.DrawString(text, Body, brush, bounds, sf);
            }
        }

    }

    public class ResultForm : Form
    {
        // 映射饼图切片 -> 仓库名（OxyPlot 2.1.0 的 PieSlice 无 Tag 属性）
        private readonly Dictionary<OxyPlot.Series.PieSlice, string> _warehouseSliceMap = new Dictionary<OxyPlot.Series.PieSlice, string>();

        private readonly AppConfig _cfg;

        // Header
        private readonly TextBox _input = new();
        private readonly Button _btnQuery = new();
        private readonly Button _btnExport = new();

        // KPI
        private readonly FlowLayoutPanel _kpi = new();
        private readonly Panel _kpiSales7 = new();
        private readonly Panel _kpiInv = new();
        private readonly Panel _kpiDoc = new();
        private readonly Panel _kpiMissing = new();
        private readonly Panel _kpiGrade = new();
        private readonly Panel _kpiMinPrice = new();
        private readonly Panel _kpiBreakeven = new();
        private FlowLayoutPanel? _kpiMissingFlow;

        // Tabs
        private readonly TabControl _tabs = new();

        // Overview Tab Charts
        private readonly FlowLayoutPanel _trendSwitch = new();
        private int _trendWindow = 7;
        private readonly PlotView _plotTrend = new();
        private readonly PlotView _plotSize = new();
        private readonly PlotView _plotColor = new();
        private readonly PlotView _plotWarehouse = new();
        private readonly PlotView _plotChannel = new();

        // Dashboard Charts (Detail Tab - New)
        private readonly PlotView _dashTrend = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        private readonly PlotView _dashChannel = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        private readonly PlotView _dashStore = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        private readonly PlotView _dashHeat = new() { Dock = DockStyle.Fill, BackColor = UI.Background };
        // Dashboard Interaction State
        private (string? Color, string? Size)? _dashActiveFilter = null;

        // Status
        private readonly Label _status = new();

        // Detail
        private readonly DataGridView _grid = new();
        private readonly BindingSource _binding = new();
        private readonly TextBox _boxSearch = new();
        private readonly FlowLayoutPanel _filterChips = new(); // Used in left panel now
        private readonly System.Windows.Forms.Timer _searchDebounce = new System.Windows.Forms.Timer() { Interval = 200 };

        // Inventory page
        private InventoryTabPage? _invPage;

        // Caches
        private string _lastDisplayText = string.Empty;
        private List<Aggregations.SalesItem> _sales = new();
        private List<SaleRow> _gridMaster = new();

        // cached inventory totals from event
        private int _invAvailTotal = 0;
        private int _invOnHandTotal = 0;
        private Dictionary<string,int> _invWarehouse = new Dictionary<string,int>();

        public ResultForm(AppConfig cfg)
        {
            _cfg = cfg;
            _vipHttp.Timeout = TimeSpan.FromSeconds(Math.Max(1, _cfg.timeout_seconds));

            Text = "StyleWatcher";
            Font = new Font("Microsoft YaHei UI", _cfg.window.fontSize);
            Width = Math.Max(1600, _cfg.window.width);
            Height = Math.Max(900, _cfg.window.height);
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = false;
            BackColor = UI.Background;
            KeyPreview = true;
            KeyDown += (s,e)=>{ if(e.KeyCode==Keys.Escape) Hide(); };

            var root = new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var header = BuildHeader();
            root.Controls.Add(header,0,0);

            var content = new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(content,0,1);

            _kpi.Dock = DockStyle.Fill;
            _kpi.FlowDirection = FlowDirection.LeftToRight;
            _kpi.WrapContents = true;
            _kpi.Padding = new Padding(16, 8, 16, 8);
            _kpi.BackColor = UI.Background;

            // KPI 顺序
            _kpi.Controls.Add(MakeKpi(_kpiSales7, "近7日销量", "—"));
            _kpi.Controls.Add(MakeKpi(_kpiInv, "可用库存总量", "—"));
            _kpi.Controls.Add(MakeKpi(_kpiDoc, "库存天数", "—"));
            _kpi.Controls.Add(MakeKpi(_kpiGrade, "定级", "—"));
            _kpi.Controls.Add(MakeKpi(_kpiMinPrice, "最低价", "—"));
            _kpi.Controls.Add(MakeKpi(_kpiBreakeven, "保本价", "—"));
            _kpi.Controls.Add(MakeKpiMissingChips(_kpiMissing, "缺货尺码"));

            content.Controls.Add(_kpi, 0, 0);

            _tabs.Dock = DockStyle.Fill;
            BuildTabs();
            UI.StyleTabs(_tabs);
            content.Controls.Add(_tabs,0,1);

            _searchDebounce.Tick += (s,e)=> { _searchDebounce.Stop(); ApplyDetailFilter(); };
        }

        private Control BuildHeader()
        {
            var head = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(16, 10, 16, 8),
                BackColor = UI.HeaderBack
            };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            _input.MinimumSize = new Size(420, 32);
            _input.Height = 32;
            _input.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            UI.StyleInput(_input);

            _btnQuery.Text = "重新查询";
            UI.StylePrimary(_btnQuery);
            _btnQuery.Click += async (s, e) =>
            {
                _btnQuery.Enabled = false;
                try
                {
                    var txt = _input.Text ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(txt))
                    {
                        SetLoading("请输入要查询的内容");
                        return;
                    }

                    SetLoading("查询中...");
                    string raw = await ApiHelper.QueryAsync(_cfg, txt);

                    if (raw != null && raw.StartsWith("请求失败：", StringComparison.Ordinal))
                    {
                        SetLoading(raw);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        SetLoading("接口未返回任何内容");
                        return;
                    }

                    string result = Formatter.Prettify(raw);
                    if (string.IsNullOrWhiteSpace(result))
                    {
                        SetLoading("未解析到任何结果");
                        return;
                    }

                    await ApplyRawTextAsync(txt, result);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex, "UI/Forms/ResultForm.cs");
                    SetLoading($"错误：{ex.Message}");
                }
                finally
                {
                    _btnQuery.Enabled = true;
                }
            };

            _btnExport.Text = "导出Excel";
            UI.StyleSecondary(_btnExport);
            _btnExport.Click += (s, e) => ExportExcel();

            head.Controls.Add(_input, 0, 0);
            head.Controls.Add(_btnQuery, 1, 0);
            head.Controls.Add(_btnExport, 2, 0);
            return head;
        }

        private Control MakeKpi(Panel host, string title, string value)
        {
            host.Width = 260;
            host.Height = 110;
            host.Padding = new Padding(12);
            host.BackColor = UI.CardBack;
            host.Margin = new Padding(8, 4, 8, 4);
            host.BorderStyle = BorderStyle.None;

            host.Paint += (s, e) =>
            {
                var rect = host.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using var pen = new Pen(UI.CardBorder);
                e.Graphics.DrawRectangle(pen, rect);
            };

            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var t = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Height = 22,
                Font = UI.Subtitle,
                ForeColor = UI.MutedText,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var v = new Label
            {
                Text = value,
                Dock = DockStyle.Fill,
                Font = UI.KpiValue,
                ForeColor = UI.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(0, 4, 0, 0),
                AutoEllipsis = true
            };
            v.Name = "ValueLabel";

            inner.Controls.Add(t, 0, 0);
            inner.Controls.Add(v, 0, 1);
            host.Controls.Clear();
            host.Controls.Add(inner);
            return host;
        }

        private Control MakeKpiMissingChips(Panel host, string title)
        {
            host.Width = 260;
            host.Height = 110;
            host.Padding = new Padding(12);
            host.BackColor = UI.CardBack;
            host.Margin = new Padding(8, 4, 8, 4);
            host.BorderStyle = BorderStyle.None;

            host.Paint += (s, e) =>
            {
                var rect = host.ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                using var pen = new Pen(UI.CardBorder);
                e.Graphics.DrawRectangle(pen, rect);
            };

            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var t = new Label
            {
                Text = title,
                Dock = DockStyle.Fill,
                Height = 22,
                Font = UI.Subtitle,
                ForeColor = UI.MutedText,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(0),
                Margin = new Padding(0),
                BackColor = UI.CardBack
            };
            _kpiMissingFlow = flow;

            inner.Controls.Add(t, 0, 0);
            inner.Controls.Add(flow, 0, 1);
            host.Controls.Clear();
            host.Controls.Add(inner);
            return host;
        }

        private void SetMissingSizes(IEnumerable<string> sizes)
        {
            if (_kpiMissingFlow == null) return;
            _kpiMissingFlow.SuspendLayout();
            _kpiMissingFlow.Controls.Clear();

            foreach (var s in sizes)
            {
                var chip = new Label
                {
                    AutoSize = true,
                    Text = s,
                    Font = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular),
                    BackColor = UI.ChipBack,
                    ForeColor = UI.Text,
                    Padding = new Padding(6, 2, 6, 2),
                    Margin = new Padding(4, 2, 0, 2),
                    BorderStyle = BorderStyle.FixedSingle
                };
                _kpiMissingFlow.Controls.Add(chip);
            }

            if (_kpiMissingFlow.Controls.Count == 0)
            {
                var none = new Label
                {
                    AutoSize = true,
                    Text = "无",
                    Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular),
                    ForeColor = Color.FromArgb(47,47,47)
                };
                _kpiMissingFlow.Controls.Add(none);
            }

            _kpiMissingFlow.ResumeLayout();
        }

        private void BuildTabs()
        {
            // 概览
            BuildOverviewTab();

            // 销售明细 (Dashboard Layout: Left List / Right Charts)
            var detailTab = new TabPage("销售明细") { BackColor = UI.Background };
            
            // 使用 SplitContainer 左右分割
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 8,
                IsSplitterFixed = false,
                BackColor = UI.HeaderBack // 分割线颜色
            };
            split.Panel1.BackColor = UI.Background;
            split.Panel2.BackColor = UI.Background;

            // --- 左侧：列表区 (35%) ---
            var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); // 搜索框
            leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));     // 筛选 Chips
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 表格

            _boxSearch.Dock = DockStyle.Fill;
            _boxSearch.MinimumSize = new Size(0, 30);
            _boxSearch.Margin = new Padding(0, 4, 0, 4);
            _boxSearch.PlaceholderText = "搜索（日期/渠道/店铺/颜色/尺码）";
            UI.StyleInput(_boxSearch);
            _boxSearch.TextChanged += (s, e) => { _searchDebounce.Stop(); _searchDebounce.Start(); };
            
            _filterChips.Dock = DockStyle.Fill;
            _filterChips.AutoSize = true;
            _filterChips.FlowDirection = FlowDirection.LeftToRight;
            _filterChips.WrapContents = true;
            _filterChips.Padding = new Padding(0, 0, 0, 8);

            _grid.Dock = DockStyle.Fill; _grid.ReadOnly = true; _grid.AllowUserToAddRows = false; _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false; _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            _grid.DataSource = _binding;
            UiGrid.Optimize(_grid);

            leftLayout.Controls.Add(_boxSearch, 0, 0);
            leftLayout.Controls.Add(_filterChips, 0, 1);
            leftLayout.Controls.Add(_grid, 0, 2);

            split.Panel1.Controls.Add(leftLayout);

            // --- 右侧：数据洞察看板 (65%) ---
            var dashLayout = new TableLayoutPanel 
            { 
                Dock = DockStyle.Fill, 
                RowCount = 3, 
                ColumnCount = 2, 
                Padding = new Padding(12) 
            };
            // Row 1: 趋势图 (30%)
            dashLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            // Row 2: 渠道 + 店铺 (30%)
            dashLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            // Row 3: SKU 热力图 (40%)
            dashLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            
            dashLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); // 饼图窄一点
            dashLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));

            // 1. 趋势图 (跨两列)
            dashLayout.Controls.Add(_dashTrend, 0, 0);
            dashLayout.SetColumnSpan(_dashTrend, 2);

            // 2. 中间层 (左饼右柱)
            dashLayout.Controls.Add(_dashChannel, 0, 1);
            dashLayout.Controls.Add(_dashStore, 1, 1);

            // 3. 底部层 (SKU热力图 跨两列)
            dashLayout.Controls.Add(_dashHeat, 0, 2);
            dashLayout.SetColumnSpan(_dashHeat, 2);
            
            // 绑定热力图点击事件
            BindHeatmapDashboardInteraction(_dashHeat);

            split.Panel2.Controls.Add(dashLayout);
            
            // 设置默认分割比例 (35% : 65%)
            // 注意：SplitContainer 需要 Size 确定后 SplitterDistance 才准确，这里先设个大概
            split.SplitterDistance = 400; 

            detailTab.Controls.Add(split);
            _tabs.TabPages.Add(detailTab);

            // 库存页
            _invPage = new InventoryTabPage(_cfg);
            _invPage.SummaryUpdated += OnInventorySummary;
            _tabs.TabPages.Add(_invPage);
        
            BuildVipUI();
        }

        // 概览
        private void BuildOverviewTab()
        {
            var overview = new TabPage("概览") { BackColor = UI.Background };

            var container = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            container.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 顶部工具：趋势窗口选择
            var tools = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(12, 8, 12, 0)
            };
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var trendLabel = new Label
            {
                Text = "销量趋势窗口：",
                Dock = DockStyle.Fill,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = UI.Body,
                ForeColor = UI.MutedText,
                Margin = new Padding(0, 4, 8, 0)
            };

            _trendSwitch.FlowDirection = FlowDirection.LeftToRight;
            _trendSwitch.WrapContents = false;
            _trendSwitch.AutoSize = true;
            _trendSwitch.Dock = DockStyle.Fill;
            _trendSwitch.Padding = new Padding(0);
            _trendSwitch.Margin = new Padding(0, 2, 0, 0);

            var wins = (_cfg.ui?.trendWindows ?? new int[] { 7, 14, 30 })
                        .Where(x => x > 0 && x <= 90)
                        .Distinct()
                        .OrderBy(x => x)
                        .ToArray();

            if (wins.Length == 0)
            {
                wins = new[] { 7, 14, 30 };
            }

            if (!wins.Contains(_trendWindow))
            {
                _trendWindow = wins.FirstOrDefault();
            }

            _trendSwitch.Controls.Clear();
            foreach (var w in wins)
            {
                var rb = new RadioButton
                {
                    Text = $"{w} 日",
                    AutoSize = true,
                    Tag = w,
                    Margin = new Padding(0, 2, 16, 0),
                    Font = UI.Body
                };
                if (w == _trendWindow) rb.Checked = true;

                rb.CheckedChanged += (s, e) =>
                {
                    if (s is RadioButton rbCtrl && rbCtrl.Checked && rbCtrl.Tag is int w2)
                    {
                        _trendWindow = w2;
                        if (_sales != null && _sales.Count > 0)
                        {
                            RenderCharts(_sales);
                        }
                    }
                };

                _trendSwitch.Controls.Add(rb);
            }

            tools.Controls.Add(trendLabel, 0, 0);
            tools.Controls.Add(_trendSwitch, 1, 0);
            container.Controls.Add(tools, 0, 0);

            // 主图网格
            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 2,
                Padding = new Padding(12, 8, 12, 12)
            };
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _plotTrend.Dock = DockStyle.Fill;
            _plotWarehouse.Dock = DockStyle.Fill;
            _plotSize.Dock = DockStyle.Fill;
            _plotColor.Dock = DockStyle.Fill;
            _plotChannel.Dock = DockStyle.Fill;

            grid.Controls.Add(_plotTrend, 0, 0);
            grid.Controls.Add(_plotWarehouse, 1, 0);
            grid.Controls.Add(_plotSize, 0, 1);
            grid.Controls.Add(_plotColor, 1, 1);
            grid.Controls.Add(_plotChannel, 0, 2);
            grid.SetColumnSpan(_plotChannel, 2);

            container.Controls.Add(grid, 0, 1);
            overview.Controls.Add(container);
            _tabs.TabPages.Add(overview);
        }

        private static Label? ValueLabelOf(Panel p)
        {
            var table = p.Controls.OfType<TableLayoutPanel>().FirstOrDefault();
            if (table != null)
            {
                var labels = table.Controls.OfType<Label>().ToList();
                if (labels.Count > 0) return labels.Last();
            }
            return null;
        }

        private void SetKpiValue(Panel p,string value, Color? color = null)
        {
            var lbl = ValueLabelOf(p);
            if (lbl == null) return;
            lbl.Text = value ?? "—";
            if (color.HasValue) lbl.ForeColor = color.Value;
            else lbl.ForeColor = Color.FromArgb(47,47,47);
        }

        public void FocusInput(){ try{ if(WindowState==FormWindowState.Minimized) WindowState=FormWindowState.Normal; _input.Focus(); _input.SelectAll(); }catch{} }
        public void ShowNoActivateAtCursor(){ try{ StartPosition=FormStartPosition.Manual; var pt=Cursor.Position; Location=new Point(Math.Max(0,pt.X-Width/2),Math.Max(0,pt.Y-Height/2)); Show(); }catch{ Show(); } }
        public void ShowAndFocusCentered(){ ShowAndFocusCentered(false); }
        public void ShowAndFocusCentered(bool alwaysOnTop){ TopMost=false; StartPosition=FormStartPosition.CenterScreen; Show(); Activate(); FocusInput(); }
        public void SetLoading(string message)
        {
            _status.Text = message ?? string.Empty;

            SetKpiValue(_kpiSales7, "—");
            SetKpiValue(_kpiInv, "—");
            SetKpiValue(_kpiDoc, "—");
            SetKpiValue(_kpiGrade, "—");
            SetKpiValue(_kpiMinPrice, "—");
            SetKpiValue(_kpiBreakeven, "—");
            SetMissingSizes(Array.Empty<string>());
        }

        public async Task ApplyRawTextAsync(string selection, string parsed)
        {
            _input.Text = selection ?? string.Empty;
            await LoadTextAsync(parsed ?? string.Empty);
        }

        public void ApplyRawText(string text)
        {
            _input.Text = text ?? string.Empty;
        }

        public async Task LoadTextAsync(string raw)=>await ReloadAsync(raw);
        private async Task ReloadAsync()=>await ReloadAsync(_input.Text);

        private async Task ReloadAsync(string displayText)
        {
            await Task.Yield();
            if (string.IsNullOrWhiteSpace(displayText))
                displayText = _lastDisplayText;

            var parsed = Parser.Parse(displayText ?? string.Empty);

            var newGrid = parsed.Records
                .OrderBy(r => r.Name)
                .ThenBy(r => r.Color)
                .ThenBy(r => r.Size)
                .ThenByDescending(r => r.Date)
                .Select(r =>
                {
                    var date   = r.Date.ToString("yyyy-MM-dd");
                    var chan   = r.Channel ?? string.Empty;
                    var shop   = r.Shop ?? string.Empty;
                    var name   = r.Name ?? string.Empty;
                    var color  = r.Color ?? string.Empty;
                    var size   = r.Size ?? string.Empty;
                    var qtyStr = r.Qty.ToString();
                    return new SaleRow
                    {
                        日期       = date,
                        渠道       = chan,
                        店铺       = shop,
                        款式       = name,
                        颜色       = color,
                        尺码       = size,
                        数量       = r.Qty,
                        SearchText = string.Join(" ", date, chan, shop, name, size, color, qtyStr)
                    };
                })
                .ToList();

            var newSales = parsed.Records.Select(r => new Aggregations.SalesItem
            {
                Date    = r.Date,
                Channel = r.Channel ?? string.Empty,
                Shop    = r.Shop ?? string.Empty,
                Size    = r.Size ?? string.Empty,
                Color   = r.Color ?? string.Empty,
                Qty     = r.Qty
            }).ToList();

            _lastDisplayText = displayText ?? string.Empty;
            _sales = newSales;
            _gridMaster = newGrid;

            // KPI: 近 N 天销量（固定用近 7 天）
            var sales7 = _sales.Where(x=>x.Date>=DateTime.Today.AddDays(-6)).Sum(x=>x.Qty);
            SetKpiValue(_kpiSales7, sales7.ToString());

            // 缺失尺码 chips（按销售基线）
            SetMissingSizes(MissingSizes(_sales.Select(s=>s.Size), _invPage?.OfferedSizes() ?? Enumerable.Empty<string>(), _invPage?.CurrentZeroSizes() ?? Enumerable.Empty<string>()));

            RenderCharts(_sales);
            // RenderSalesSummary(_sales); // Moved to Dashboard Panel

            _binding.DataSource = new BindingList<SaleRow>(_gridMaster);
            if (_grid.Columns.Contains("SearchText"))
                _grid.Columns["SearchText"].Visible = false;
            _grid.ClearSelection();
            if (_grid.Columns.Contains("款式")) _grid.Columns["款式"].DisplayIndex = 0;
            if (_grid.Columns.Contains("颜色")) _grid.Columns["颜色"].DisplayIndex = 1;
            if (_grid.Columns.Contains("尺码")) _grid.Columns["尺码"].DisplayIndex = 2;
            if (_grid.Columns.Contains("日期")) _grid.Columns["日期"].DisplayIndex = 3;
            if (_grid.Columns.Contains("数量")) _grid.Columns["数量"].DisplayIndex = 4;

            // 触发推断
            var styleName = parsed.Records
                .Select(r => r.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .GroupBy(n => n)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()
                ?.Key;

            if (!string.IsNullOrWhiteSpace(styleName))
            {
                try { 
                    await _invPage?.LoadInventoryAsync(styleName); 
                    // 库存加载完成后，渲染右侧的详细 Dashboard (结合销售 + 库存)
                    RenderDetailDashboard();
                } catch {}
                try { _ = LoadPriceAsync(styleName); } catch {}
            }
            else
            {
                try { _invPage?.ResetToEmpty(); } catch {}
                try { _ = LoadPriceAsync(string.Empty); } catch {}
                // 清空 Dashboard
                RenderDetailDashboard(); 
                SetLoading("未解析到任何销售明细记录，库存与价格信息已清空。");
            }
        }

        private void OnInventorySummary(int totalAvail, int totalOnHand, Dictionary<string,int> warehouseAgg)
        {
            _invAvailTotal = totalAvail;
            _invOnHandTotal = totalOnHand;
            _invWarehouse = warehouseAgg ?? new Dictionary<string,int>();

            SetKpiValue(_kpiInv, totalAvail.ToString());

            // 库存天数：用最近 N 天平均销量（来自配置 inventoryAlert.minSalesWindowDays）
            var baseDays = Math.Max(1, _cfg.inventoryAlert?.minSalesWindowDays ?? 7);
            var lastN = _sales.Where(x=> x.Date >= DateTime.Today.AddDays(-(baseDays-1))).Sum(x=>x.Qty);
            var avg = lastN / (double)baseDays;

            string daysText = "—";
            Color? daysColor = null;
            if (avg > 0)
            {
                var d = Math.Round(totalAvail / avg, 1);
                daysText = d.ToString("0.0");

                var red = _cfg.inventoryAlert?.docRed ?? 3;
                var yellow = _cfg.inventoryAlert?.docYellow ?? 7;
                daysColor = d < red ? UI.Red : (d < yellow ? UI.Yellow : UI.Green);
            }
            SetKpiValue(_kpiDoc, daysText, daysColor);

            // 概览页：分仓占比
            RenderWarehousePieOverview(_invWarehouse);
            
            // 每次库存更新后，也尝试更新 Dashboard (如果已经在显示)
            if (_sales != null && _sales.Count > 0)
                RenderDetailDashboard();
        }

        private void RenderWarehousePieOverview(Dictionary<string, int> warehouseAgg)
        {
            _warehouseSliceMap.Clear();
            if (warehouseAgg == null || warehouseAgg.Count == 0)
            {
                _plotWarehouse.Model = new PlotModel { Title = "分仓库存占比（无数据）" };
                ApplyPlotTheme(_plotWarehouse.Model);
                return;
            }

            var list = warehouseAgg
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
                .OrderByDescending(kv => kv.Value)
                .ToList();

            var total = list.Sum(x => (double)x.Value);
            if (total <= 0)
            {
                _plotWarehouse.Model = new PlotModel { Title = "分仓库存占比（无数据）" };
                ApplyPlotTheme(_plotWarehouse.Model);
                return;
            }

            var model = new PlotModel { Title = "分仓库存占比" };
            ApplyPlotTheme(model);

            var pie = new PieSeries
            {
                AngleSpan = 360,
                StartAngle = 0,
                StrokeThickness = 0.5,
                InsideLabelPosition = 0.7,
                InsideLabelFormat = "{1:0.#}%",
                OutsideLabelFormat = string.Empty,
                TickHorizontalLength = 4,
                TickRadialLength = 4
            };

            var keep = list.Where(kv => kv.Value / total >= 0.03).ToList();
            if (keep.Count < 3) keep = list.Take(3).ToList();

            var keepSet = new HashSet<string>(keep.Select(k => k.Key));
            double other = 0;

            foreach (var kv in list)
            {
                if (keepSet.Contains(kv.Key))
                {
                    var slice = new PieSlice(kv.Key, kv.Value);
                    pie.Slices.Add(slice);
                    _warehouseSliceMap[slice] = kv.Key;
                }
                else other += kv.Value;
            }

            if (other > 0)
            {
                var sliceOther = new PieSlice("其他", other);
                pie.Slices.Add(sliceOther);
                _warehouseSliceMap[sliceOther] = "其他";
            }

            model.Series.Add(pie);
            _plotWarehouse.Model = model;
        }

        private static IEnumerable<string> MissingSizes(
            IEnumerable<string> _sizesFromSales,
            IEnumerable<string> sizesOfferedFromInv,
            IEnumerable<string> sizesZeroFromInv)
        {
            if (sizesOfferedFromInv == null || sizesZeroFromInv == null)
                yield break;

            var offered = new HashSet<string>(sizesOfferedFromInv.Where(s=>!string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);
            var zeros   = new HashSet<string>(sizesZeroFromInv.Where(s=>!string.IsNullOrWhiteSpace(s)), StringComparer.OrdinalIgnoreCase);

            foreach (var s in offered)
                if (zeros.Contains(s))
                    yield return s;
        }
        
        private static List<Aggregations.SalesItem> CleanSalesForVisuals(IEnumerable<Aggregations.SalesItem> src)
        {
            var list = src.Where(s => !string.IsNullOrWhiteSpace(s.Color) && !string.IsNullOrWhiteSpace(s.Size)).ToList();
            var byColor = list.GroupBy(x=>x.Color).ToDictionary(g=>g.Key, g=>g.Sum(z=>z.Qty));
            var bySize  = list.GroupBy(x=>x.Size ).ToDictionary(g=>g.Key, g=>g.Sum(z=>z.Qty));
            list = list.Where(x => byColor.GetValueOrDefault(x.Color,0) != 0 && bySize.GetValueOrDefault(x.Size,0) != 0).ToList();
            return list;
        }

        private static void ApplyPlotTheme(PlotModel model)
        {
            if (model == null) return;
            model.Background = OxyColors.White;
            model.TextColor = OxyColor.FromRgb(47, 47, 47);
            model.TitleColor = model.TextColor;
            model.PlotAreaBorderThickness = new OxyThickness(0);
        }

        private void RenderCharts(List<Aggregations.SalesItem> salesItems)
        {
            var cleaned = CleanSalesForVisuals(salesItems);

            // 1) 趋势
            var series = Aggregations.BuildDateSeries(cleaned, _trendWindow);
            var modelTrend = new PlotModel
            {
                Title = $"近{_trendWindow}日销量趋势",
                PlotMargins = new OxyThickness(40, 8, 12, 32)
            };
            ApplyPlotTheme(modelTrend);
            // ... (Axis setup same as before)
            var xAxis = new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "MM-dd", IsZoomEnabled = false, IsPanEnabled = false };
            var yAxis = new LinearAxis { Position = AxisPosition.Left, MinimumPadding = 0, AbsoluteMinimum = 0, IsZoomEnabled = false, IsPanEnabled = false };
            modelTrend.Axes.Add(xAxis); modelTrend.Axes.Add(yAxis);

            var line = new LineSeries { Title = "销量", MarkerType = MarkerType.Circle, MarkerSize = 3, Color = OxyColors.SteelBlue };
            foreach (var (day, qty) in series)
            {
                var x = DateTimeAxis.ToDouble(day);
                line.Points.Add(new DataPoint(x, qty));
                var label = new TextAnnotation { Text = qty.ToString("0"), TextPosition = new DataPoint(x, qty), TextVerticalAlignment = VerticalAlignment.Bottom, TextHorizontalAlignment = HorizontalAlignment.Center, Stroke = OxyColors.Transparent, FontSize = 9, TextColor = modelTrend.TextColor };
                modelTrend.Annotations.Add(label);
            }
            modelTrend.Series.Add(line);
            _plotTrend.Model = modelTrend;

            // 2) 尺码 & 颜色 & 渠道 (Overview Tab)
            var sizeAggRaw = cleaned.GroupBy(x => x.Size).Select(g => (Key: g.Key, Qty: (double)g.Sum(z => z.Qty)));
            _plotSize.Model = UiCharts.BuildBarModel(sizeAggRaw, "尺码销量 (Top10)", topN: 10);

            var colorAggRaw = cleaned.GroupBy(x => x.Color).Select(g => (Key: g.Key, Qty: (double)g.Sum(z => z.Qty)));
            _plotColor.Model = UiCharts.BuildBarModel(colorAggRaw, "颜色销量 (Top10)", topN: 10);

            var cutoff = DateTime.Today.AddDays(-6);
            var channelAggRaw = cleaned.Where(x => x.Date >= cutoff)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Channel) ? "其他渠道" : x.Channel)
                .Select(g => (Key: g.Key, Qty: (double)g.Sum(z => z.Qty)));
            _plotChannel.Model = UiCharts.BuildBarModel(channelAggRaw, "近7日各渠道销量");
        }

        // =================================================================================
        // NEW: Dashboard Rendering Logic (Right side of Detail Tab)
        // =================================================================================
        private void RenderDetailDashboard()
        {
            if (_sales == null || _sales.Count == 0) return;

            var cutoff7 = DateTime.Today.AddDays(-6);
            var recentSales = _sales.Where(s => s.Date >= cutoff7).ToList();

            // 1. Dashboard Trend (Top) - Full Width Line Chart
            var series = Aggregations.BuildDateSeries(recentSales, 7);
            var modelTrend = new PlotModel { Title = "全渠道销量趋势 (近7天)", PlotMargins = new OxyThickness(30, 0, 10, 20) };
            ApplyPlotTheme(modelTrend);
            modelTrend.Axes.Add(new DateTimeAxis { Position = AxisPosition.Bottom, StringFormat = "MM-dd", IsZoomEnabled = false, IsPanEnabled = false, MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = OxyColors.WhiteSmoke });
            modelTrend.Axes.Add(new LinearAxis { Position = AxisPosition.Left, MinimumPadding = 0, AbsoluteMinimum = 0, IsZoomEnabled = false, IsPanEnabled = false, MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = OxyColors.WhiteSmoke });
            
            var line = new LineSeries { StrokeThickness = 3, Color = OxyColor.Parse("#409EFF"), MarkerType = MarkerType.Circle, MarkerSize = 4, MarkerFill = OxyColors.White, MarkerStroke = OxyColor.Parse("#409EFF") };
            foreach(var (d, q) in series) line.Points.Add(new DataPoint(DateTimeAxis.ToDouble(d), q));
            modelTrend.Series.Add(line);
            _dashTrend.Model = modelTrend;

            // 2. Dashboard Middle: Channel Pie & Store Bar
            // A. Channel Pie
            var chanData = recentSales.GroupBy(x => string.IsNullOrWhiteSpace(x.Channel) ? "其他" : x.Channel)
                .Select(g => new { Name = g.Key, Qty = g.Sum(x => x.Qty) }).OrderByDescending(x => x.Qty).ToList();
            var modelChan = new PlotModel { Title = "渠道占比" };
            ApplyPlotTheme(modelChan);
            var pie = new PieSeries { InnerDiameter = 0.5, StrokeThickness = 1.0, InsideLabelPosition = 0.6, AngleSpan = 360, StartAngle = 0 };
            foreach(var c in chanData) pie.Slices.Add(new PieSlice(c.Name, c.Qty) { IsExploded = false });
            modelChan.Series.Add(pie);
            _dashChannel.Model = modelChan;

            // B. Store Top 5
            var storeData = recentSales.GroupBy(x => string.IsNullOrWhiteSpace(x.Shop) ? "未知店铺" : x.Shop)
                .Select(g => (Key: g.Key, Qty: (double)g.Sum(x => x.Qty) )).ToList();
            _dashStore.Model = UiCharts.BuildBarModel(storeData, "店铺贡献 Top 5", topN: 5);

            // 3. Dashboard Bottom: SKU Heatmap with Turnover Logic (The Core Feature)
            RenderTurnoverHeatmap();
        }

        private void RenderTurnoverHeatmap()
        {
            var model = new PlotModel { Title = "SKU 销售热力与缺货预警" };
            ApplyPlotTheme(model);
            
            // Data Prep
            var sales7 = _sales.Where(s => s.Date >= DateTime.Today.AddDays(-6)).ToList();
            if (sales7.Count == 0) return;

            // Aggregate Sales 7 Days
            var salesMap = sales7.GroupBy(x => new { C = x.Color, S = x.Size })
                                 .ToDictionary(g => $"{g.Key.C}_{g.Key.S}", g => g.Sum(x => x.Qty));

            // Aggregate Inventory (Current)
            var invRows = _invPage?.AllRows ?? new List<InvRow>();
            var invMap = invRows.GroupBy(x => new { C = x.Color, S = x.Size })
                                .ToDictionary(g => $"{g.Key.C}_{g.Key.S}", g => g.Sum(x => x.Available));

            // Distinct Axes
            var allColors = sales7.Select(x => x.Color).Union(invRows.Select(x => x.Color))
                                  .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();
            var allSizes = sales7.Select(x => x.Size).Union(invRows.Select(x => x.Size))
                                 .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
            
            // Sort sizes logically (S, M, L...)
            var sizeOrder = new List<string> { "XS", "S", "M", "L", "XL", "2XL", "3XL", "4XL", "5XL", "F", "均码" };
            var sortedSizes = allSizes.OrderBy(s => {
                var idx = sizeOrder.IndexOf(s.ToUpper());
                return idx == -1 ? 99 : idx;
            }).ThenBy(s => s).ToList();

            // Axes
            var xa = new CategoryAxis { Position = AxisPosition.Bottom, Key = "SizeAxis" };
            var ya = new CategoryAxis { Position = AxisPosition.Left, Key = "ColorAxis" };
            for(int i=0; i<sortedSizes.Count; i++) xa.Labels.Add(sortedSizes[i]);
            for(int i=0; i<allColors.Count; i++) ya.Labels.Add(allColors[i]);
            model.Axes.Add(xa);
            model.Axes.Add(ya);

            // HeatMap Series (For Sales Volume - Green Scale)
            var hm = new HeatMapSeries {
                X0 = 0, X1 = sortedSizes.Count - 1,
                Y0 = 0, Y1 = allColors.Count - 1,
                Interpolate = false,
                RenderMethod = HeatMapRenderMethod.Rectangles,
                LabelFontSize = 10,
                LabelFormatString = "0"
            };
            
            // Custom Color Axis (Green Gradient)
            model.Axes.Add(new LinearColorAxis { 
                Position = AxisPosition.Right, 
                Palette = OxyPalettes.Linear(OxyColors.WhiteSmoke, OxyColor.Parse("#67C23A")), // Light to Green
                Key = "ColorScale"
            });

            var data = new double[sortedSizes.Count, allColors.Count];

            // Fill Data & Detect Shortages
            for (int x = 0; x < sortedSizes.Count; x++)
            {
                for (int y = 0; y < allColors.Count; y++)
                {
                    var key = $"{allColors[y]}_{sortedSizes[x]}";
                    var sQty = salesMap.GetValueOrDefault(key, 0);
                    var iQty = invMap.GetValueOrDefault(key, 0);
                    
                    data[x, y] = sQty; // Heatmap shows Sales Volume

                    // Calculate Turnover & Shortage
                    // ADS (Avg Daily Sales)
                    double ads = sQty / 7.0;
                    double days = ads > 0 ? iQty / ads : 999;

                    // Shortage Condition: 
                    // 1. Out of stock AND has sales
                    // 2. Turnover days < 7 AND has sales
                    bool isShortage = (iQty == 0 && sQty > 0) || (days < 7 && sQty > 0);

                    if (isShortage)
                    {
                        // Draw Red Border using Annotation
                        var rect = new RectangleAnnotation
                        {
                            MinimumX = x - 0.45, MaximumX = x + 0.45,
                            MinimumY = y - 0.45, MaximumY = y + 0.45,
                            Stroke = OxyColors.Red,
                            StrokeThickness = 2,
                            Fill = OxyColors.Transparent,
                            ToolTip = $"缺货预警!\n销量: {sQty}\n库存: {iQty}\n可销: {days:0.0}天"
                        };
                        model.Annotations.Add(rect);
                    }
                }
            }
            
            hm.Data = data;
            model.Series.Add(hm);
            
            // Store context for interaction
            _dashHeat.Tag = new { Colors = allColors, Sizes = sortedSizes }; 
            _dashHeat.Model = model;
        }

        private void BindHeatmapDashboardInteraction(PlotView pv)
        {
            var controller = new PlotController();
            controller.BindMouseDown(OxyMouseButton.Left, new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) =>
            {
                var model = view.Model;
                if (model == null) return;
                var hm = model.Series.OfType<HeatMapSeries>().FirstOrDefault();
                var ctx = view.Tag as dynamic; // Colors, Sizes lists
                if (hm == null || ctx == null) return;

                var sp = args.Position;
                var result = hm.GetNearestPoint(sp, false);
                
                if (result != null)
                {
                    int x = (int)Math.Round(result.DataPoint.X);
                    int y = (int)Math.Round(result.DataPoint.Y);
                    
                    var sizes = (List<string>)ctx.Sizes;
                    var colors = (List<string>)ctx.Colors;

                    if (x >= 0 && x < sizes.Count && y >= 0 && y < colors.Count)
                    {
                        var s = sizes[x];
                        var c = colors[y];
                        
                        // Toggle Filter
                        if (_dashActiveFilter != null && _dashActiveFilter.Value.Color == c && _dashActiveFilter.Value.Size == s)
                            _dashActiveFilter = null; // Deselect
                        else
                            _dashActiveFilter = (c, s);

                        ApplyDetailFilter();
                    }
                }
                else
                {
                    _dashActiveFilter = null;
                    ApplyDetailFilter();
                }
            }));
            
            pv.Controller = controller;
        }

        private void ApplyDetailFilter()
        {
            var q = _boxSearch.Text?.Trim() ?? string.Empty;
            
            // Update Chip Display
            _filterChips.Controls.Clear();
            if (_dashActiveFilter != null)
            {
                var chip = new Label { 
                    Text = $"{_dashActiveFilter.Value.Color} / {_dashActiveFilter.Value.Size} (点击热力图取消)", 
                    AutoSize = true, BackColor = UI.Red, ForeColor = Color.White, Padding = new Padding(4) 
                };
                chip.Click += (s,e) => { _dashActiveFilter = null; ApplyDetailFilter(); };
                _filterChips.Controls.Add(chip);
            }

            // Filter Grid
            var list = _gridMaster.AsEnumerable();
            
            // 1. Search Box
            if (!string.IsNullOrWhiteSpace(q))
            {
                list = UiSearch.FilterAllTokens(list, x => x.SearchText, q);
            }

            // 2. Heatmap Filter
            if (_dashActiveFilter != null)
            {
                list = list.Where(x => x.颜色 == _dashActiveFilter.Value.Color && x.尺码 == _dashActiveFilter.Value.Size);
            }

            _binding.DataSource = new BindingList<SaleRow>(list.ToList());
            _grid.ClearSelection();
        }

        private void ExportExcel()
        {
            var saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
            Directory.CreateDirectory(saveDir);
            var path = Path.Combine(saveDir, $"StyleWatcher_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");

            using var wb = new XLWorkbook();
            IReadOnlyList<InvRow> invRows = Array.Empty<InvRow>();
            if (_invPage != null) invRows = _invPage.AllRows;
            ResultExporter.FillWorkbook(wb, _gridMaster, invRows, _vipAll);
            wb.SaveAs(path);
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
        }

        private async Task LoadPriceAsync(string styleName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(styleName))
                {
                    SetKpiValue(_kpiGrade, "—");
                    SetKpiValue(_kpiMinPrice, "—");
                    SetKpiValue(_kpiBreakeven, "—");
                    return;
                }

                using (var http = new HttpClient())
                {
                    http.Timeout = TimeSpan.FromSeconds(5);
                    var url = "http://192.168.40.97:8002/lookup?name=" + Uri.EscapeDataString(styleName);
                    var resp = await http.GetAsync(url);
                    resp.EnsureSuccessStatusCode();
                    var json = await resp.Content.ReadAsStringAsync();

                    using (var doc = JsonDocument.Parse(json))
                    {
                        var arr = doc.RootElement;
                        if (arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                        {
                            var first = arr[0];
                            string grade = null, minp = null, brk = null;
                            JsonElement tmp;
                            if (first.TryGetProperty("grade", out tmp)) grade = tmp.ToString();
                            if (first.TryGetProperty("min_price_one", out tmp)) minp = tmp.ToString();
                            if (first.TryGetProperty("breakeven_one", out tmp)) brk = tmp.ToString();
                            SetKpiValue(_kpiGrade, grade ?? "—");
                            SetKpiValue(_kpiMinPrice, minp ?? "—");
                            SetKpiValue(_kpiBreakeven, brk ?? "—");
                        }
                        else
                        {
                            SetKpiValue(_kpiGrade, "—"); SetKpiValue(_kpiMinPrice, "—"); SetKpiValue(_kpiBreakeven, "—");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UI/Forms/ResultForm.cs");
                SetKpiValue(_kpiGrade, "—"); SetKpiValue(_kpiMinPrice, "—"); SetKpiValue(_kpiBreakeven, "—");
            }
        }

        // === VIP INTEGRATION BEGIN ===
        // Vip inventory page (virtualized)
        private TabPage? _vipInvTab;
        private readonly DataGridView _vipGrid = new()
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, VirtualMode = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
        };
        private readonly TextBox _vipSearchBox = new();
        private readonly System.Windows.Forms.Timer _vipSearchDebounce = new() { Interval = 200 };
        private readonly Label _vipStatus = new() { AutoSize = true, Margin = new Padding(8, 8, 0, 0), ForeColor = UI.MutedText };
        private readonly List<Dictionary<string, object?>> _vipAll = new();
        private List<Dictionary<string, object?>> _vipView = new();
        private readonly List<string> _vipColumns = new();
        private string? _vipSortColumn;
        private bool _vipSortAscending = true;
        private bool _vipLoaded;
        private bool _vipLoading;
        private static readonly HttpClient _vipHttp = new();

        private void BuildVipUI()
        {
            _vipInvTab = new TabPage("唯品库存");
            var vipLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            vipLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            vipLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            
            var vipTop = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
            vipTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            vipTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            vipTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            
            _vipSearchBox.Dock = DockStyle.Fill;
            _vipSearchBox.MinimumSize = new Size(0, 30);
            _vipSearchBox.Margin = new Padding(0, 4, 0, 4);
            _vipSearchBox.PlaceholderText = "搜索（款式名/颜色/尺码等，空格分隔多条件）";
            UI.StyleInput(_vipSearchBox);
            _vipSearchBox.TextChanged += (s, e) => { _vipSearchDebounce.Stop(); _vipSearchDebounce.Start(); };
            vipTop.Controls.Add(_vipSearchBox, 0, 0);
            
            _vipStatus.Text = string.Empty;
            vipTop.Controls.Add(_vipStatus, 1, 0);
            
            var vipRefresh = new Button { Text = "刷新" };
            UI.StyleSecondary(vipRefresh);
            vipRefresh.Margin = new Padding(8, 4, 0, 4);
            vipRefresh.Click += async (s, e) => await ForceReloadVipInventoryAsync();
            vipTop.Controls.Add(vipRefresh, 2, 0);
            
            vipLayout.Controls.Add(vipTop, 0, 0);
            vipLayout.Controls.Add(_vipGrid, 0, 1);
            UiGrid.OptimizeVirtual(_vipGrid);
            _vipInvTab.Controls.Add(vipLayout);
            _tabs.TabPages.Add(_vipInvTab);
            
            _vipGrid.CellValueNeeded += VipGrid_CellValueNeeded;
            _vipGrid.ColumnHeaderMouseClick += VipGrid_ColumnHeaderMouseClick;
            _vipSearchDebounce.Tick += (s, e) => { _vipSearchDebounce.Stop(); ApplyVipFilter(_vipSearchBox.Text); };
            _tabs.SelectedIndexChanged += async (s, e) => { if (_tabs.SelectedTab == _vipInvTab) await EnsureVipInventoryLoadedAsync(); };
        }

        private async Task EnsureVipInventoryLoadedAsync()
        {
            if (_vipLoaded || _vipLoading) return;
            await ForceReloadVipInventoryAsync();
        }

        private async Task ForceReloadVipInventoryAsync()
        {
            if (_vipLoading) return;
            _vipLoading = true; _vipStatus.Text = "唯品库存加载中...";

            try
            {
                var rows = await FetchVipInventoryAsync();
                _vipAll.Clear();
                if (rows != null) _vipAll.AddRange(rows);
                _vipView = _vipAll.ToList();
                BuildVipColumnsAndBind();
                _vipLoaded = true;
                _vipStatus.Text = _vipView.Count == 0 ? "未获取到唯品库存数据" : $"共 {_vipView.Count} 条记录";
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UI/Forms/ResultForm.cs");
                var msg = GetVipErrorMessage(ex);
                _vipAll.Clear();
                _vipView = new List<Dictionary<string, object?>> { new() { ["错误"] = msg } };
                BuildVipColumnsAndBind();
                _vipLoaded = false;
                _vipStatus.Text = msg;
            }
            finally { _vipLoading = false; }
        }

        private static string GetVipErrorMessage(Exception ex)
        {
            if (ex is HttpRequestException) return "唯品库存接口网络 / 连接错误，请检查网络或服务器地址";
            if (ex is TaskCanceledException || ex is OperationCanceledException) return "唯品库存接口请求超时，请稍后重试";
            if (ex is JsonException) return "唯品库存接口返回数据格式异常，接口可能已变更";
            return "唯品库存加载失败：" + ex.Message;
        }

        private async Task<List<Dictionary<string, object?>>> FetchVipInventoryAsync()
        {
            var url = "http://192.168.40.97:8001/inventory";
            using var resp = await _vipHttp.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            return await Task.Run(() =>
            {
                var list = new List<Dictionary<string, object?>>();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        var dict = new Dictionary<string, object?>();
                        foreach (var prop in elem.EnumerateObject())
                        {
                            object? val = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString(),
                                JsonValueKind.Number when prop.Value.TryGetInt64(out var iv) => iv,
                                JsonValueKind.Number when prop.Value.TryGetDouble(out var dv) => dv,
                                JsonValueKind.True or JsonValueKind.False => prop.Value.GetBoolean(),
                                JsonValueKind.Null => null,
                                _ => prop.Value.ToString()
                            };
                            dict[prop.Name] = val;
                        }
                        list.Add(dict);
                    }
                }
                return list;
            });
        }

        private void BuildVipColumnsAndBind()
        {
            _vipGrid.SuspendLayout();
            try
            {
                _vipGrid.Columns.Clear(); _vipColumns.Clear();
                if (_vipView == null || _vipView.Count == 0) { _vipGrid.RowCount = 0; return; }
                _vipColumns.Add("product_original_code"); _vipColumns.Add("白胚可用数");
                _vipColumns.Add("进货仓库存"); _vipColumns.Add("成品占用数"); _vipColumns.Add("__sum");

                foreach (var col in _vipColumns)
                {
                    var header = col switch { "product_original_code" => "款式名", "白胚可用数" => "白胚可用数", "进货仓库存" => "进货仓库存", "成品占用数" => "成品占用数", "__sum" => "可用数汇总", _ => col };
                    _vipGrid.Columns.Add(col, header);
                }
                _vipGrid.RowCount = _vipView.Count;
            }
            finally { _vipGrid.ResumeLayout(); }
        }

        private void VipGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
        {
            if (_vipView == null || e.RowIndex < 0 || e.RowIndex >= _vipView.Count || e.ColumnIndex < 0 || e.ColumnIndex >= _vipColumns.Count) return;
            var row = _vipView[e.RowIndex];
            var key = _vipColumns[e.ColumnIndex];
            if (key == "__sum") { e.Value = GetVipNumber(row, "白胚可用数", "白坯可用数") + GetVipNumber(row, "进货仓库存") + GetVipNumber(row, "成品占用数"); return; }
            if (key == "白胚可用数") { if (!row.TryGetValue("白胚可用数", out var val) || val == null) row.TryGetValue("白坯可用数", out val); e.Value = val ?? 0; return; }
            if (row.TryGetValue(key, out var v)) e.Value = v;
        }

        private void VipGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (_vipView == null || _vipView.Count <= 1 || e.ColumnIndex < 0 || e.ColumnIndex >= _vipColumns.Count) return;
            var col = _vipColumns[e.ColumnIndex];
            if (_vipSortColumn == col) _vipSortAscending = !_vipSortAscending; else { _vipSortColumn = col; _vipSortAscending = true; }
            _vipView.Sort((a, b) =>
            {
                var ka = GetVipSortKey(a, col); var kb = GetVipSortKey(b, col);
                if (ka == null && kb == null) return 0; if (ka == null) return -1; if (kb == null) return 1;
                return ka.CompareTo(kb);
            });
            if (!_vipSortAscending) _vipView.Reverse();
            _vipGrid.Invalidate();
        }

        private IComparable? GetVipSortKey(Dictionary<string, object?>? row, string col)
        {
            if (row == null) return null;
            if (col == "__sum") return GetVipNumber(row, "白胚可用数", "白坯可用数") + GetVipNumber(row, "进货仓库存") + GetVipNumber(row, "成品占用数");
            if (col == "白胚可用数") return GetVipNumber(row, "白胚可用数", "白坯可用数");
            if (!row.TryGetValue(col, out var val) || val == null) return null;
            if (val is int i) return i; if (val is double d) return d;
            return val.ToString();
        }

        private double GetVipNumber(Dictionary<string, object?> row, params string[] keys)
        {
            foreach (var key in keys) { if (row.TryGetValue(key, out var val) && val != null) { if (val is int i) return i; if (val is double d) return d; if (double.TryParse(val.ToString(), out var dv)) return dv; } }
            return 0d;
        }

        private void ApplyVipFilter(string? keyword)
        {
            if (_vipAll == null || _vipAll.Count == 0) _vipView = new List<Dictionary<string, object?>>();
            else if (string.IsNullOrWhiteSpace(keyword)) _vipView = _vipAll.ToList();
            else
            {
                var parts = keyword.Split(new[] { ' ', ',', '+' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.ToLowerInvariant()).ToArray();
                _vipView = _vipAll.Where(row => { var text = string.Join(" ", row.Values.Select(v => v?.ToString() ?? "")).ToLowerInvariant(); return parts.All(p => text.Contains(p)); }).ToList();
            }
            BuildVipColumnsAndBind(); _vipGrid.Invalidate();
        }

        private sealed class SaleRow
        {
            public string 日期 { get; set; } = string.Empty;
            public string 渠道 { get; set; } = string.Empty;
            public string 店铺 { get; set; } = string.Empty;
            public string 款式 { get; set; } = string.Empty;
            public string 颜色 { get; set; } = string.Empty;
            public string 尺码 { get; set; } = string.Empty;
            public int 数量 { get; set; }
            public string SearchText { get; set; } = string.Empty;
        }
    }
}