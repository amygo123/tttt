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
        private readonly System.Collections.Generic.Dictionary<OxyPlot.Series.PieSlice, string> _warehouseSliceMap = new System.Collections.Generic.Dictionary<OxyPlot.Series.PieSlice, string>();

        private readonly AppConfig _cfg;
        private readonly IStyleAnalysisService _analysisService;
        private System.Threading.CancellationTokenSource? _analysisCts;


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
        private readonly Panel _kpiReturn30 = new();
        private readonly Panel _kpiReturnYear = new();
        private readonly Panel _kpiMinPrice = new();
        private readonly Panel _kpiBreakeven = new();
        private FlowLayoutPanel? _kpiMissingFlow;

        // Tabs
        private readonly TabControl _tabs = new();

        // Overview
        private readonly FlowLayoutPanel _trendSwitch = new();
        private int _trendWindow = 7;
        private readonly PlotView _plotTrend = new();
        private readonly PlotView _plotSize = new();
        private readonly PlotView _plotColor = new();
        private readonly PlotView _plotWarehouse = new();
        private readonly PlotView _plotChannel = new();

        // Status
        private readonly Label _status = new();

        // Detail
        private readonly DataGridView _grid = new();
        private readonly BindingSource _binding = new();
        private readonly TextBox _boxSearch = new();
        private readonly FlowLayoutPanel _channelSummary = new();
        private readonly FlowLayoutPanel _shopTop = new();
        private readonly PlotView _detailTrend = new();
        private readonly PlotView _detailChannelDonut = new();
        private readonly PlotView _detailShopBar = new();
        private readonly PlotView _detailSkuHeat = new();
        private readonly CheckBox _skuModeInventory = new();
        private readonly CheckBox _skuModeSales = new();
        private bool _skuShowInventory = false;
        private readonly System.Windows.Forms.Timer _searchDebounce = new System.Windows.Forms.Timer() { Interval = 200 };

        // Inventory page
        private InventoryTabPage? _invPage;

        // Caches
        private string _lastDisplayText = string.Empty;
        private List<Aggregations.SalesItem> _sales = new();
        private List<SaleRow> _gridMaster = new();
        private readonly DetailFilter _detailFilter = new();
        private List<string> _skuHeatSizes = new();
        private List<string> _skuHeatColors = new();
        private bool _skuHeatClickAttached = false;

        // cached inventory totals from event
        private int _invAvailTotal = 0;
        private int _invOnHandTotal = 0;
        private Dictionary<string,int> _invWarehouse = new Dictionary<string,int>();


        public ResultForm(AppConfig cfg, IStyleAnalysisService analysisService)
        {
            _cfg = cfg;
            _analysisService = analysisService ?? throw new ArgumentNullException(nameof(analysisService));
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

// KPI 顺序：缺码 KPI 放在最后
_kpi.Controls.Add(MakeKpi(_kpiSales7, "近7日销量", "—"));
_kpi.Controls.Add(MakeKpi(_kpiInv, "可用库存总量", "—"));
_kpi.Controls.Add(MakeKpi(_kpiDoc, "库存天数", "—"));
_kpi.Controls.Add(MakeKpi(_kpiGrade, "定级", "—"));
_kpi.Controls.Add(MakeKpi(_kpiReturn30, "30日退货率", "—"));
_kpi.Controls.Add(MakeKpi(_kpiReturnYear, "年度退货率", "—"));
_kpi.Controls.Add(MakeKpi(_kpiMinPrice, "最低价", "—"));
_kpi.Controls.Add(MakeKpi(_kpiBreakeven, "保本价", "—"));
_kpi.Controls.Add(MakeKpiMissingChips(_kpiMissing, "缺货尺码"));

content.Controls.Add(_kpi, 0, 0);

            _tabs.Dock = DockStyle.Fill;
            BuildTabs();
            UI.StyleTabs(_tabs);
            content.Controls.Add(_tabs,0,1);

            _searchDebounce.Tick += (s,e)=> { _searchDebounce.Stop(); ApplyFilter(_boxSearch.Text); };
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

                    _analysisCts?.Cancel();
                    _analysisCts = new System.Threading.CancellationTokenSource();
                    var token = _analysisCts.Token;

                    string result;
                    try
                    {
                        result = await _analysisService.GetParsedTextAsync(txt, token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    await ApplyRawTextAsync(txt, result);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError(ex, "UI/Forms/ResultForm.cs");
                    SetLoading(ex.Message);
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

// 销售明细
            var detail = new TabPage("销售明细") { BackColor = UI.Background };
            var panel = new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(12)};
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent,100));

            _boxSearch.Dock = DockStyle.Fill;
            _boxSearch.MinimumSize = new Size(0, 30);
            _boxSearch.Margin = new Padding(0, 4, 0, 4);
            _boxSearch.PlaceholderText = "搜索（日期/渠道/店铺/款式/尺码/颜色/数量）";
            UI.StyleInput(_boxSearch);
            _boxSearch.TextChanged += (s, e) => { _searchDebounce.Stop(); _searchDebounce.Start(); };
            panel.Controls.Add(_boxSearch, 0, 0);

            // 汇总行：左侧过滤 Chip，中间渠道汇总，右侧 Top 店铺
            // 汇总行：左侧渠道汇总，右侧 Top 店铺
            var summaryRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0),
                Padding = new Padding(0)
            };
            summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            _channelSummary.Dock = DockStyle.Fill;
            _channelSummary.FlowDirection = FlowDirection.LeftToRight;
            _channelSummary.WrapContents = true;
            _channelSummary.Padding = new Padding(0, 0, 0, 4);
            _channelSummary.AutoScroll = true;

            _shopTop.Dock = DockStyle.Fill;
            _shopTop.FlowDirection = FlowDirection.LeftToRight;
            _shopTop.WrapContents = true;
            _shopTop.Padding = new Padding(0, 0, 0, 4);
            _shopTop.AutoScroll = true;

            summaryRow.Controls.Add(_channelSummary, 0, 0);
            summaryRow.Controls.Add(_shopTop, 1, 0);

            panel.Controls.Add(summaryRow, 0, 1);

            var mainSplit = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 1,
                ColumnCount = 2,
            };
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            mainSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

            // 左侧表格
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            _grid.DataSource = _binding;
            UiGrid.Optimize(_grid);
            mainSplit.Controls.Add(_grid, 0, 0);

            // 右侧 Dashboard（趋势 + 渠道占比 + 店铺 Top5 + 热力图）
            var dashboard = BuildDetailDashboardLayout();
            mainSplit.Controls.Add(dashboard, 1, 0);

            panel.Controls.Add(mainSplit, 0, 2);
            detail.Controls.Add(panel);
            _tabs.TabPages.Add(detail);

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
                            RenderDetailDashboard();
                            RenderSkuHeatmap();
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

        // —— 与 TrayApp 对齐的接口 —— //
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
    SetKpiValue(_kpiReturn30, "—");
    SetKpiValue(_kpiReturnYear, "—");
    SetKpiValue(_kpiMinPrice, "—");
    SetKpiValue(_kpiBreakeven, "—");
    SetMissingSizes(Array.Empty<string>());
}

        public async System.Threading.Tasks.Task ApplyRawTextAsync(string selection, string parsed)
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
            SetMissingSizes(MissingSizes(_sales.Select(s=>s.Size), _invPage?.OfferedSizes() ?? System.Linq.Enumerable.Empty<string>(), _invPage?.CurrentZeroSizes() ?? System.Linq.Enumerable.Empty<string>()));

            RenderCharts(_sales);
            RenderSalesSummary(_sales);
            RenderDetailDashboard();
            RenderSkuHeatmap();

            _binding.DataSource = new BindingList<SaleRow>(_gridMaster);
            // 隐藏内部搜索列
            if (_grid.Columns.Contains("SearchText"))
                _grid.Columns["SearchText"].Visible = false;
            _grid.ClearSelection();
            if (_grid.Columns.Contains("款式")) _grid.Columns["款式"].DisplayIndex = 0;
            if (_grid.Columns.Contains("颜色")) _grid.Columns["颜色"].DisplayIndex = 1;
            if (_grid.Columns.Contains("尺码")) _grid.Columns["尺码"].DisplayIndex = 2;
            if (_grid.Columns.Contains("日期")) _grid.Columns["日期"].DisplayIndex = 3;
            if (_grid.Columns.Contains("数量")) _grid.Columns["数量"].DisplayIndex = 4;

            // 推断 styleName（仅基于解析结果，不再使用默认款兜底）
var styleName = parsed.Records
    .Select(r => r.Name)
    .Where(n => !string.IsNullOrWhiteSpace(n))
    .GroupBy(n => n)
    .OrderByDescending(g => g.Count())
    .FirstOrDefault()
    ?.Key;

if (!string.IsNullOrWhiteSpace(styleName))
            {
                try { _ = _invPage?.LoadInventoryAsync(styleName); } catch {}
                try { _ = LoadPriceAsync(styleName); } catch {}
                try { _ = LoadReturnRateAsync(styleName); } catch {}
            }
            else
            {
                // 没有解析出任何款式名称：销售为空或数据格式异常
                // 将库存页和价格 KPI 一并重置为“无数据”状态，避免残留上一笔查询的结果。
                try { _invPage?.ResetToEmpty(); } catch {}
                try { _ = LoadPriceAsync(string.Empty); } catch {}
                try { _ = LoadReturnRateAsync(string.Empty); } catch {}

                // 同时给出明确提示，说明当前查询没有销售明细。
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
            System.Drawing.Color? daysColor = null;
            if (avg > 0)
            {
                var d = Math.Round(totalAvail / avg, 1);
                daysText = d.ToString("0.0");

                var red = _cfg.inventoryAlert?.docRed ?? 3;
                var yellow = _cfg.inventoryAlert?.docYellow ?? 7;
                daysColor = d < red ? Color.FromArgb(215, 58, 73) : (d < yellow ? Color.FromArgb(216, 160, 18) : Color.FromArgb(26, 127, 55));
            }
            SetKpiValue(_kpiDoc, daysText, daysColor);

            // 概览页：分仓占比
            RenderWarehousePieOverview(_invWarehouse);
        }

        
        private void RenderWarehousePieOverview(Dictionary<string, int> warehouseAgg)
        {
            _warehouseSliceMap.Clear();

            if (warehouseAgg == null || warehouseAgg.Count == 0)
            {
                var emptyModel = new PlotModel { Title = "分仓库存占比（无数据）" };
                ApplyPlotTheme(emptyModel);
                _plotWarehouse.Model = emptyModel;
                return;
            }

            var list = warehouseAgg
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value > 0)
                .OrderByDescending(kv => kv.Value)
                .ToList();

            var total = list.Sum(x => (double)x.Value);
            if (total <= 0)
            {
                var emptyModel = new PlotModel { Title = "分仓库存占比（无数据）" };
                ApplyPlotTheme(emptyModel);
                _plotWarehouse.Model = emptyModel;
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

            // 保留占比 >=3% 的仓，若不足 3 个，则取前 3 个，其余合并为“其他”
            var keep = list.Where(kv => kv.Value / total >= 0.03).ToList();
            if (keep.Count < 3)
            {
                keep = list.Take(3).ToList();
            }

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
                else
                {
                    other += kv.Value;
                }
            }

            if (other > 0)
            {
                var sliceOther = new PieSlice("其他", other);
                pie.Slices.Add(sliceOther);
                _warehouseSliceMap[sliceOther] = "其他";
            }

            model.Series.Clear();
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

            var xAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "MM-dd",
                IntervalType = DateTimeIntervalType.Days,
                MinorIntervalType = DateTimeIntervalType.Days,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.None,
                IsZoomEnabled = false,
                IsPanEnabled = false
            };

            var yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                MinimumPadding = 0,
                AbsoluteMinimum = 0,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.None,
                IsZoomEnabled = false,
                IsPanEnabled = false
            };

            modelTrend.Axes.Clear();
            modelTrend.Axes.Add(xAxis);
            modelTrend.Axes.Add(yAxis);

            var line = new LineSeries
            {
                Title = "销量",
                MarkerType = MarkerType.Circle,
                MarkerSize = 3,
                CanTrackerInterpolatePoints = false,
                TrackerFormatString = "日期: {2:yyyy-MM-dd}\n销量: {4:0}"
            };

            foreach (var (day, qty) in series)
            {
                var x = DateTimeAxis.ToDouble(day);
                var y = qty;
                line.Points.Add(new DataPoint(x, y));

                var label = new TextAnnotation
                {
                    Text = qty.ToString("0"),
                    TextPosition = new DataPoint(x, y),
                    TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                    TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                    Stroke = OxyColors.Transparent,
                    FontSize = 9,
                    TextColor = modelTrend.TextColor
                };
                modelTrend.Annotations.Add(label);
            }

            modelTrend.Series.Clear();
            modelTrend.Series.Add(line);
            _plotTrend.Model = modelTrend;

            // 2) 尺码销量（降序，Top10 视角）
            var sizeAggRaw = cleaned
                .GroupBy(x => x.Size)
                .Select(g => (Key: g.Key, Qty: (double)g.Sum(z => z.Qty)));
            _plotSize.Model = UiCharts.BuildBarModel(sizeAggRaw, "尺码销量 (Top10)", topN: 10);

            // 3) 颜色销量（降序，Top10 视角）
            var colorAggRaw = cleaned
                .GroupBy(x => x.Color)
                .Select(g => (Key: g.Key, Qty: (double)g.Sum(z => z.Qty)));
            _plotColor.Model = UiCharts.BuildBarModel(colorAggRaw, "颜色销量 (Top10)", topN: 10);

            // 4) 渠道销量（近7日，各渠道对比）
            var cutoff = DateTime.Today.AddDays(-6);
            var channelAggRaw = cleaned
                .Where(x => x.Date >= cutoff)
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Channel) ? "其他渠道" : x.Channel)
                .Select(g => (Key: g.Key, Qty: (double)g.Sum(z => z.Qty)));
            _plotChannel.Model = UiCharts.BuildBarModel(channelAggRaw, "近7日各渠道销量");
        }



        
private TableLayoutPanel BuildDetailDashboardLayout()
{
    var dashboard = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        RowCount = 3,
        ColumnCount = 1,
        Margin = new Padding(0)
    };
    dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
    dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
    dashboard.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

    // Top: 近7日销量趋势
    _detailTrend.Dock = DockStyle.Fill;
    dashboard.Controls.Add(_detailTrend, 0, 0);

    // Middle: 渠道占比 + 店铺 Top5
    var attributionRow = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        RowCount = 1,
        ColumnCount = 2,
        Margin = new Padding(0, 4, 0, 4)
    };
    attributionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
    attributionRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

    _detailChannelDonut.Dock = DockStyle.Fill;
    _detailShopBar.Dock = DockStyle.Fill;
    attributionRow.Controls.Add(_detailChannelDonut, 0, 0);
    attributionRow.Controls.Add(_detailShopBar, 1, 0);
    dashboard.Controls.Add(attributionRow, 0, 1);

    // Bottom: SKU 热力图 + 模式切换
    var skuPanel = new TableLayoutPanel
    {
        Dock = DockStyle.Fill,
        RowCount = 2,
        ColumnCount = 1,
        Margin = new Padding(0, 4, 0, 0)
    };
    skuPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
    skuPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

    var toolbar = new FlowLayoutPanel
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.RightToLeft,
        WrapContents = false,
        Margin = new Padding(0),
        Padding = new Padding(0, 4, 0, 4)
    };

    _skuModeInventory.AutoSize = true;
    _skuModeInventory.Text = "显示库存";
    _skuModeInventory.Checked = false;
    _skuModeInventory.CheckedChanged += (s, e) =>
    {
        if (_skuModeInventory.Checked)
        {
            _skuShowInventory = true;
            if (_sales != null && _sales.Count > 0)
            {
                RenderSkuHeatmap();
            }
        }
    };

    _skuModeSales.AutoSize = true;
    _skuModeSales.Text = "显示销量";
    _skuModeSales.Checked = true;
    _skuModeSales.CheckedChanged += (s, e) =>
    {
        if (_skuModeSales.Checked)
        {
            _skuShowInventory = false;
            if (_sales != null && _sales.Count > 0)
            {
                RenderSkuHeatmap();
            }
        }
    };

    var skuLabel = new Label
    {
        Text = "SKU 热力图",
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock = DockStyle.Left,
        ForeColor = UI.MutedText,
        Padding = new Padding(4, 8, 8, 0)
    };

    toolbar.Controls.Add(_skuModeInventory);
    toolbar.Controls.Add(_skuModeSales);
    toolbar.Controls.Add(skuLabel);

    skuPanel.Controls.Add(toolbar, 0, 0);

    if (!_skuHeatClickAttached)
    {
        _detailSkuHeat.MouseDown += DetailSkuHeat_MouseDown;
        _skuHeatClickAttached = true;
    }
    _detailSkuHeat.Dock = DockStyle.Fill;
    skuPanel.Controls.Add(_detailSkuHeat, 0, 1);

    dashboard.Controls.Add(skuPanel, 0, 2);

    return dashboard;
}

private void RenderDetailDashboard()
{
    try
    {
        if (_sales == null || _sales.Count == 0)
        {
            _detailTrend.Model = null;
            _detailChannelDonut.Model = null;
            _detailShopBar.Model = null;
            return;
        }

        var cleaned = CleanSalesForVisuals(FilterSalesForDetail(_sales));

        // 1) 趋势层：近 N 日销量趋势
        var series = Aggregations.BuildDateSeries(cleaned, _trendWindow);
        var trendModel = new PlotModel
        {
            Title = $"近{_trendWindow}日销量趋势",
            PlotMargins = new OxyThickness(40, 8, 12, 32)
        };
        ApplyPlotTheme(trendModel);

        var xAxis = new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            StringFormat = "MM-dd",
            IntervalType = DateTimeIntervalType.Days,
            MinorIntervalType = DateTimeIntervalType.Days,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.None,
            IsZoomEnabled = false,
            IsPanEnabled = false
        };

        var yAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            MinimumPadding = 0,
            AbsoluteMinimum = 0,
            MajorGridlineStyle = LineStyle.Solid,
            MinorGridlineStyle = LineStyle.None,
            IsZoomEnabled = false,
            IsPanEnabled = false
        };

        trendModel.Axes.Add(xAxis);
        trendModel.Axes.Add(yAxis);

        var line = new LineSeries
        {
            Title = "销量",
            MarkerType = MarkerType.Circle,
            MarkerSize = 3,
            CanTrackerInterpolatePoints = false,
            TrackerFormatString = "日期: {2:yyyy-MM-dd}\n销量: {4:0}"
        };

        foreach (var (day, qty) in series)
        {
            var x = DateTimeAxis.ToDouble(day);
            var y = qty;
            line.Points.Add(new DataPoint(x, y));

            var label = new TextAnnotation
            {
                Text = qty.ToString("0"),
                TextPosition = new DataPoint(x, y),
                TextVerticalAlignment = OxyPlot.VerticalAlignment.Bottom,
                TextHorizontalAlignment = OxyPlot.HorizontalAlignment.Center,
                Stroke = OxyColors.Transparent,
                FontSize = 9,
                TextColor = trendModel.TextColor
            };
            trendModel.Annotations.Add(label);
        }

        trendModel.Series.Clear();
        trendModel.Series.Add(line);
        _detailTrend.Model = trendModel;

        // 2) 渠道占比圆环 + 店铺 Top5
        var cutoff = DateTime.Today.AddDays(-6);
        var recent = cleaned.Where(x => x.Date >= cutoff).ToList();
        if (recent.Count == 0)
        {
            _detailChannelDonut.Model = null;
            _detailShopBar.Model = null;
            return;
        }

        var channelAgg = recent
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Channel) ? "其他渠道" : x.Channel)
            .Select(g => new { Channel = g.Key, Qty = g.Sum(z => z.Qty) })
            .OrderByDescending(x => x.Qty)
            .ToList();

        var channelModel = new PlotModel { Title = "近7日渠道占比" };
        ApplyPlotTheme(channelModel);

        var pie = new PieSeries
        {
            InnerDiameter = 0.5,
            StrokeThickness = 0.25,
            StartAngle = 0,
            AngleSpan = 360,
            InsideLabelPosition = 0.75,
            FontSize = 10
        };

        foreach (var c in channelAgg)
        {
            if (c.Qty <= 0) continue;
            pie.Slices.Add(new PieSlice(c.Channel, c.Qty));
        }

        channelModel.Series.Clear();
        channelModel.Series.Add(pie);
        _detailChannelDonut.Model = channelModel;

        var shopAgg = recent
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Shop) ? "未命名店铺" : x.Shop)
            .Select(g => new { Shop = g.Key, Qty = g.Sum(z => z.Qty) })
            .OrderByDescending(x => x.Qty)
            .Take(5)
            .ToList();

        var shopData = shopAgg.Select(x => (Key: x.Shop, Qty: (double)x.Qty));
        _detailShopBar.Model = UiCharts.BuildBarModel(shopData, "近7日店铺销量 Top5", topN: 5);
    }
    catch (Exception ex)
    {
        AppLogger.LogError(ex, "UI/Forms/ResultForm.RenderDetailDashboard");
    }
}

private void RenderSkuHeatmap()
{
    try
    {
        if (_sales == null || _sales.Count == 0)
        {
            _detailSkuHeat.Model = null;
            return;
        }

        // 仅使用最近 7 日数据构建热力图
        var cutoff = DateTime.Today.AddDays(-6);
        var recent = _sales
            .Where(x => x.Date >= cutoff)
            .Where(x => !string.IsNullOrWhiteSpace(x.Color) && !string.IsNullOrWhiteSpace(x.Size))
            .ToList();

        if (recent.Count == 0)
        {
            _detailSkuHeat.Model = null;
            return;
        }

        // 销量聚合
        var skuSales = recent
            .GroupBy(x => new
            {
                Color = x.Color?.Trim() ?? string.Empty,
                Size = x.Size?.Trim() ?? string.Empty
            })
            .ToDictionary(g => g.Key, g => g.Sum(z => z.Qty));

        // 库存聚合（仅在库存模式下才会访问库存页）
        var skuInventory = new Dictionary<(string Color, string Size), int>();
        if (_skuShowInventory && _invPage != null)
        {
            try
            {
                var rows = _invPage.AllRows;
                if (rows != null)
                {
                    foreach (var r in rows)
                    {
                        var color = (r.Color ?? string.Empty).Trim();
                        var size = (r.Size ?? string.Empty).Trim();
                        var key = (color, size);
                        if (skuInventory.TryGetValue(key, out var old))
                        {
                            skuInventory[key] = old + r.Available;
                        }
                        else
                        {
                            skuInventory[key] = r.Available;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UI/Forms/ResultForm.RenderSkuHeatmap.Inventory");
            }
        }

        var allColors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allSizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var k in skuSales.Keys)
        {
            allColors.Add(k.Color);
            allSizes.Add(k.Size);
        }

        foreach (var kv in skuInventory.Keys)
        {
            allColors.Add(kv.Color);
            allSizes.Add(kv.Size);
        }

        if (allColors.Count == 0 || allSizes.Count == 0)
        {
            _detailSkuHeat.Model = null;
            return;
        }

        string[] preferredSizeOrder = { "XS", "S", "M", "L", "XL", "2XL", "3XL", "4XL", "5XL" };

        int SizeRank(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return preferredSizeOrder.Length + 1;
            var u = s.Trim().ToUpperInvariant();
            var idx = Array.IndexOf(preferredSizeOrder, u);
            return idx >= 0 ? idx : preferredSizeOrder.Length;
        }

        var sizes = allSizes
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .OrderBy(SizeRank)
            .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var colors = allColors
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (sizes.Count == 0 || colors.Count == 0)
        {
            _detailSkuHeat.Model = null;
            return;
        }
        _skuHeatSizes = sizes;
        _skuHeatColors = colors;


        int w = sizes.Count;
        int h = colors.Count;

        var data = new double[w, h];
        double maxValue = 0;

        for (int xi = 0; xi < w; xi++)
        {
            for (int yi = 0; yi < h; yi++)
            {
                var color = colors[yi];
                var size = sizes[xi];

                skuSales.TryGetValue(new { Color = color, Size = size }, out var sQty);
                skuInventory.TryGetValue((color, size), out var iQty);

                double value = _skuShowInventory ? iQty : sQty;
                if (value < 0) value = 0;

                data[xi, yi] = value;
                if (value > maxValue) maxValue = value;
            }
        }

        if (maxValue <= 0)
        {
            _detailSkuHeat.Model = null;
            return;
        }

        var model = new PlotModel
        {
            Title = _skuShowInventory ? "SKU 库存热力图（颜色 x 尺码）" : "SKU 销量热力图（颜色 x 尺码）",
            PlotMargins = new OxyThickness(60, 8, 8, 40)
        };
        ApplyPlotTheme(model);

        var xAxis = new CategoryAxis
        {
            Position = AxisPosition.Bottom
        };
        foreach (var s in sizes)
        {
            xAxis.Labels.Add(s);
        }

        var yAxis = new CategoryAxis
        {
            Position = AxisPosition.Left
        };
        foreach (var c in colors)
        {
            yAxis.Labels.Add(c);
        }

        model.Axes.Add(xAxis);
        model.Axes.Add(yAxis);

        var colorAxis = new LinearColorAxis
        {
            Position = AxisPosition.Right,
            Minimum = 0,
            Maximum = maxValue,
            Palette = OxyPalettes.Jet(256)
        };
        model.Axes.Add(colorAxis);

        var heat = new HeatMapSeries
        {
            X0 = 0,
            X1 = w,
            Y0 = 0,
            Y1 = h,
            Interpolate = false,
            RenderMethod = HeatMapRenderMethod.Rectangles,
            Data = data
        };

        model.Series.Add(heat);
        _detailSkuHeat.Model = model;
    }
    catch (Exception ex)
    {
        AppLogger.LogError(ex, "UI/Forms/ResultForm.RenderSkuHeatmap");
        _detailSkuHeat.Model = null;
    }
}

    private void DetailSkuHeat_MouseDown(object? sender, MouseEventArgs e)
    {
        try
        {
            if (_skuHeatSizes == null || _skuHeatSizes.Count == 0) return;
            if (_skuHeatColors == null || _skuHeatColors.Count == 0) return;
            if (sender is not PlotView pv) return;

            var model = pv.Model;
            if (model == null) return;

            var heat = model.Series.OfType<HeatMapSeries>().FirstOrDefault();
            if (heat == null) return;

            var xAxis = model.Axes.OfType<CategoryAxis>()
                .FirstOrDefault(a => a.Position == AxisPosition.Bottom);
            var yAxis = model.Axes.OfType<CategoryAxis>()
                .FirstOrDefault(a => a.Position == AxisPosition.Left);

            if (xAxis == null || yAxis == null) return;

            var sp = new ScreenPoint(e.X, e.Y);
            var xValue = xAxis.InverseTransform(sp.X);
            var yValue = yAxis.InverseTransform(sp.Y);

            int xi = (int)Math.Floor(xValue);
            int yi = (int)Math.Floor(yValue);


            if (xi < 0 || yi < 0 ||
                xi >= _skuHeatSizes.Count ||
                yi >= _skuHeatColors.Count)
            {
                return;
            }

            var size = _skuHeatSizes[xi];
            var color = _skuHeatColors[yi];

            bool sameAsCurrent =
                string.Equals(_detailFilter.Size, size, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_detailFilter.Color, color, StringComparison.OrdinalIgnoreCase);

            if (sameAsCurrent)
            {
                _detailFilter.Size = null;
                _detailFilter.Color = null;
            }
            else
            {
                _detailFilter.Size = size;
                _detailFilter.Color = color;
            }

            ApplyDetailFilter();
        }
        catch (Exception ex)
        {
            AppLogger.LogError(ex, "UI/Forms/ResultForm.DetailSkuHeat_MouseDown");
        }
    }

private void RenderSalesSummary(List<Aggregations.SalesItem> sales)
        {
            _channelSummary.SuspendLayout();
            _shopTop.SuspendLayout();
            try
            {
                _channelSummary.Controls.Clear();
                _shopTop.Controls.Clear();

                if (sales == null || sales.Count == 0)
                {
                    return;
                }

                var cutoff = DateTime.Today.AddDays(-6);
                var recent = sales.Where(x => x.Date >= cutoff).ToList();
                if (recent.Count == 0)
                {
                    return;
                }

                // 渠道汇总
                var channelAgg = recent
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Channel) ? "其他渠道" : x.Channel)
                    .Select(g => new { Channel = g.Key, Qty = g.Sum(z => z.Qty) })
                    .OrderByDescending(x => x.Qty)
                    .ToList();

                foreach (var c in channelAgg)
                {
                    var channel = c.Channel;
                    var lbl = new Label
                    {
                        AutoSize = true,
                        Text = $"{channel} {c.Qty}",
                        Font = UI.Subtitle,
                        BackColor = UI.ChipBack,
                        ForeColor = UI.Text,
                        Padding = new Padding(6, 2, 6, 2),
                        Margin = new Padding(4, 2, 0, 2),
                        Cursor = Cursors.Hand
                    };
                    lbl.Click += (s, e) =>
                    {
                        // 点击渠道 chip：切换 DetailFilter.Channel，并应用统一过滤逻辑。
                        if (string.Equals(_detailFilter.Channel, channel, StringComparison.OrdinalIgnoreCase))
                        {
                            _detailFilter.Channel = null;
                        }
                        else
                        {
                            _detailFilter.Channel = channel;
                            // 切换渠道时，当前店铺过滤可能已经不适配，清掉。
                            _detailFilter.Shop = null;
                        }

                        ApplyDetailFilter();
                    };
                    _channelSummary.Controls.Add(lbl);
                }

                // 店铺 TopN
                var shopAgg = recent
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Shop) ? "未命名店铺" : x.Shop)
                    .Select(g => new { Shop = g.Key, Qty = g.Sum(z => z.Qty) })
                    .OrderByDescending(x => x.Qty)
                    .Take(10)
                    .ToList();

                int rank = 1;
                foreach (var s in shopAgg)
                {
                    var shop = s.Shop;
                    var lbl = new Label
                    {
                        AutoSize = true,
                        Text = $"{rank}. {shop} {s.Qty}",
                        Font = UI.Subtitle,
                        BackColor = UI.ChipBack,
                        ForeColor = UI.Text,
                        Padding = new Padding(6, 2, 6, 2),
                        Margin = new Padding(4, 2, 0, 2),
                        Cursor = Cursors.Hand
                    };
                    lbl.Click += (sender, e) =>
                    {
                        // 点击店铺 chip：切换 DetailFilter.Shop，并应用统一过滤逻辑。
                        if (string.Equals(_detailFilter.Shop, shop, StringComparison.OrdinalIgnoreCase))
                        {
                            _detailFilter.Shop = null;
                        }
                        else
                        {
                            _detailFilter.Shop = shop;
                        }

                        ApplyDetailFilter();
                    };
                    _shopTop.Controls.Add(lbl);
                    rank++;
                }
                }
            finally
            {
                _channelSummary.ResumeLayout();
                _shopTop.ResumeLayout();
            }
        }

        private void ApplyFilter(string q)
        {
            _detailFilter.Text = (q ?? string.Empty).Trim();
            ApplyDetailFilter();
        }

        /// <summary>
        /// 根据当前 DetailFilter（渠道/店铺/颜色/尺码/搜索文本）刷新明细表。
        /// </summary>
        private void ApplyDetailFilter()
        {
            var text = (_detailFilter.Text ?? string.Empty).Trim();

            IEnumerable<SaleRow> rows = _gridMaster;

            if (!string.IsNullOrEmpty(_detailFilter.Channel))
            {
                rows = rows.Where(r => r.渠道 == _detailFilter.Channel);
            }
            if (!string.IsNullOrEmpty(_detailFilter.Shop))
            {
                rows = rows.Where(r => r.店铺 == _detailFilter.Shop);
            }
            if (!string.IsNullOrEmpty(_detailFilter.Color))
            {
                rows = rows.Where(r => r.颜色 == _detailFilter.Color);
            }
            if (!string.IsNullOrEmpty(_detailFilter.Size))
            {
                rows = rows.Where(r => r.尺码 == _detailFilter.Size);
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _binding.DataSource = new BindingList<SaleRow>(rows.ToList());
                _grid.ClearSelection();
                return;
            }

            string ToText(SaleRow x) => x?.SearchText ?? string.Empty;

            var filtered = UiSearch
                .FilterAllTokens(rows, ToText, text)
                .ToList();

            _binding.DataSource = new BindingList<SaleRow>(filtered);
            _grid.ClearSelection();
        }

        /// <summary>
        /// 将 _sales 按当前 DetailFilter 过滤，用于右侧 dashboard（趋势 / 渠道 / 店铺）。
        /// 不参与文本搜索，只按渠道 / 店铺 / 颜色 / 尺码过滤。
        /// </summary>
        private IEnumerable<Aggregations.SalesItem> FilterSalesForDetail(IEnumerable<Aggregations.SalesItem> source)
        {
            if (source == null)
            {
                return Enumerable.Empty<Aggregations.SalesItem>();
            }

            var query = source;

            if (!string.IsNullOrEmpty(_detailFilter.Channel))
            {
                query = query.Where(x => string.Equals(x.Channel, _detailFilter.Channel, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(_detailFilter.Shop))
            {
                query = query.Where(x => string.Equals(x.Shop, _detailFilter.Shop, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(_detailFilter.Color))
            {
                query = query.Where(x => string.Equals(x.Color, _detailFilter.Color, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(_detailFilter.Size))
            {
                query = query.Where(x => string.Equals(x.Size, _detailFilter.Size, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }




        private void ExportExcel()
        {
            var saveDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exports");
            Directory.CreateDirectory(saveDir);
            var path = Path.Combine(saveDir, $"StyleWatcher_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");

            using var wb = new XLWorkbook();

            // 报表内容交给 ResultExporter 构建：
            // 1) 销售明细（当前主表 _gridMaster）
            // 2) 库存明细（库存页 InventoryTabPage 内部的 InvRow 列表）
            // 3) 唯品库存明细（_vipAll 中的原始唯品库存数据）
            IReadOnlyList<InvRow> invRows = Array.Empty<InvRow>();
            if (_invPage != null)
            {
                invRows = _invPage.AllRows;
            }

            ResultExporter.FillWorkbook(
                wb,
                _gridMaster,
                invRows,
                _vipAll
            );

            wb.SaveAs(path);
            try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { }
        }
    

        

        

        

        
    


        private async System.Threading.Tasks.Task LoadReturnRateAsync(string styleName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(styleName))
                {
                    SetKpiValue(_kpiReturn30, "—");
                    SetKpiValue(_kpiReturnYear, "—");
                    return;
                }

                // 优先从配置读取退货率接口基础地址，如未设置则使用默认值
                var baseUrl = _cfg?.inventory?.return_rate_url_base;
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    baseUrl = "http://192.168.40.97:8004/inventory?style_name=";
                }

                var url = baseUrl.Contains("style_name=")
                    ? baseUrl + Uri.EscapeDataString(styleName)
                    : baseUrl.TrimEnd('/') + "?style_name=" + Uri.EscapeDataString(styleName);

                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = System.TimeSpan.FromSeconds(5);
                    var resp = await http.GetAsync(url);
                    resp.EnsureSuccessStatusCode();
                    var raw = await resp.Content.ReadAsStringAsync();

                    // 兼容：JSON 数组字符串 或 纯文本行
                    System.Collections.Generic.List<string>? lines = null;
                    try { lines = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(raw); } catch { }
                    if (lines == null)
                    {
                        lines = raw.Replace("\r\n", "\n").Split('\n').ToList();
                    }

                    double? rate30 = null;
                    double? rateYear = null;

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var seg = line.Replace('，', ',').Split(',');
                        if (seg.Length < 3) continue;

                        if (double.TryParse(seg[1].Trim(), out var r30))
                            rate30 = r30;
                        if (double.TryParse(seg[2].Trim(), out var rYear))
                            rateYear = rYear;
                        break; // 只取第一行
                    }

                    var text30 = rate30.HasValue ? (rate30.Value * 100).ToString("0.0") + "%" : "—";
                    var textYear = rateYear.HasValue ? (rateYear.Value * 100).ToString("0.0") + "%" : "—";

                    SetKpiValue(_kpiReturn30, text30);
                    SetKpiValue(_kpiReturnYear, textYear);
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UI/Forms/ResultForm.cs");
                SetKpiValue(_kpiReturn30, "—");
                SetKpiValue(_kpiReturnYear, "—");
            }
        }

        private async System.Threading.Tasks.Task LoadPriceAsync(string styleName)
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

                using (var http = new System.Net.Http.HttpClient())
                {
                    http.Timeout = System.TimeSpan.FromSeconds(5);
                    var url = "http://192.168.40.97:8002/lookup?name=" + System.Uri.EscapeDataString(styleName);
                    var resp = await http.GetAsync(url);
                    resp.EnsureSuccessStatusCode();
                    var json = await resp.Content.ReadAsStringAsync();

                    using (var doc = System.Text.Json.JsonDocument.Parse(json))
                    {
                        var arr = doc.RootElement;
                        if (arr.ValueKind == System.Text.Json.JsonValueKind.Array && arr.GetArrayLength() > 0)
                        {
                            var first = arr[0];

                            string? grade = null;
                            string? minp = null;
                            string? brk = null;

                            System.Text.Json.JsonElement tmp;
                            if (first.TryGetProperty("grade", out tmp) && tmp.ValueKind == System.Text.Json.JsonValueKind.String)
                                grade = tmp.GetString();
                            if (first.TryGetProperty("min_price_one", out tmp) && tmp.ValueKind == System.Text.Json.JsonValueKind.String)
                                minp = tmp.GetString();
                            if (first.TryGetProperty("breakeven_one", out tmp) && tmp.ValueKind == System.Text.Json.JsonValueKind.String)
                                brk = tmp.GetString();

                            SetKpiValue(_kpiGrade, grade ?? "—");
                            SetKpiValue(_kpiMinPrice, minp ?? "—");
                            SetKpiValue(_kpiBreakeven, brk ?? "—");
                        }
                        else
                        {
                            SetKpiValue(_kpiGrade, "—");
                            SetKpiValue(_kpiMinPrice, "—");
                            SetKpiValue(_kpiBreakeven, "—");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError(ex, "UI/Forms/ResultForm.cs");
                SetKpiValue(_kpiGrade, "—");
                SetKpiValue(_kpiMinPrice, "—");
                SetKpiValue(_kpiBreakeven, "—");
            }
        }


        


// === VIP INTEGRATION BEGIN ===
// ===== 提取自 ResultForm.cs：唯品库存 页面相关代码（不多不少，按调用逻辑排序） =====
// 说明：本文件聚合了字段声明、UI 构建与事件绑定、数据加载/解析/绑定、虚拟表格回显、排序与搜索过滤等。
// 原始位置见各段注释（以“源：ResultForm.cs [Lxx-Lyy]”标注）。

// ---------- 一、字段声明 ----------
// 源：ResultForm.cs [L40-L70]
// 作用：唯品库存页的控件、状态与缓存。
/* begin: fields */
// Vip inventory page (virtualized)
private TabPage? _vipInvTab;
private readonly DataGridView _vipGrid = new()
{
    Dock = DockStyle.Fill,
    ReadOnly = true,
    AllowUserToAddRows = false,
    AllowUserToDeleteRows = false,
    RowHeadersVisible = false,
    VirtualMode = true,
    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
};
private readonly TextBox _vipSearchBox = new();
private readonly System.Windows.Forms.Timer _vipSearchDebounce = new() { Interval = 200 };
private readonly Label _vipStatus = new()
{
    AutoSize = true,
    Margin = new Padding(8, 8, 0, 0),
    ForeColor = UI.MutedText
};
private readonly List<Dictionary<string, object?>> _vipAll = new();
private List<Dictionary<string, object?>> _vipView = new();
private readonly List<string> _vipColumns = new();
private string? _vipSortColumn;
private bool _vipSortAscending = true;
private bool _vipLoaded;
private bool _vipLoading;
private static readonly HttpClient _vipHttp = new();
/* end: fields */

// ---------- 二、UI 构建与事件绑定（在 BuildTabs 中） ----------
// 源：ResultForm.cs [L384-L451] + [附加 L1-L56]
// 作用：创建“唯品库存”Tab 页；绑定 CellValueNeeded/ColumnHeaderMouseClick；
//      搜索框防抖 -> ApplyVipFilter；Tab 切换 -> EnsureVipInventoryLoadedAsync；刷新按钮 -> ForceReloadVipInventoryAsync。
/* begin: BuildTabs wiring */ // wrapped into BuildVipUI()
        private void BuildVipUI()
        {
            _vipInvTab = new TabPage("唯品库存");
            var vipLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            vipLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            vipLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            
            var vipTop = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3
            };
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
            
            _tabs.SelectedIndexChanged += async (s, e) =>
            {
                if (_tabs.SelectedTab == _vipInvTab) await EnsureVipInventoryLoadedAsync();
            };
        }
/* end: BuildTabs wiring */

// ---------- 三、加载/刷新 & 数据获取与解析 ----------
// 源：ResultForm.cs [L65-L104] + [L106-L83]
/* begin: load & fetch */
private async Task EnsureVipInventoryLoadedAsync()
{
    if (_vipLoaded || _vipLoading) return;
    await ForceReloadVipInventoryAsync();
}

private async Task ForceReloadVipInventoryAsync()
{
    if (_vipLoading) return;

    _vipLoading = true;
    _vipStatus.Text = "唯品库存加载中...";

    try
    {
        var rows = await FetchVipInventoryAsync();
        _vipAll.Clear();
        if (rows != null) _vipAll.AddRange(rows);
        _vipView = _vipAll.ToList();

        BuildVipColumnsAndBind();
        _vipLoaded = true;

        if (_vipView.Count == 0)
        {
            _vipStatus.Text = "未获取到唯品库存数据";
        }
        else
        {
            _vipStatus.Text = $"共 {_vipView.Count} 条记录";
        }
    }
    catch (Exception ex)
    {
        AppLogger.LogError(ex, "UI/Forms/ResultForm.cs");
        var msg = GetVipErrorMessage(ex);
        _vipAll.Clear();
        _vipView = new List<Dictionary<string, object?>>
        {
            new() { ["错误"] = msg }
        };
        BuildVipColumnsAndBind();
        _vipLoaded = false;
        _vipStatus.Text = msg;
    }
    finally
    {
        _vipLoading = false;
    }
}


        private static string GetVipErrorMessage(Exception ex)
        {
            if (ex is HttpRequestException)
            {
                return "唯品库存接口网络 / 连接错误，请检查网络或服务器地址";
            }

            if (ex is System.Threading.Tasks.TaskCanceledException || ex is OperationCanceledException)
            {
                return "唯品库存接口请求超时，请稍后重试";
            }

            if (ex is JsonException)
            {
                return "唯品库存接口返回数据格式异常，接口可能已变更";
            }

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
/* end: load & fetch */

// ---------- 四、列定义与绑定（虚拟模式） ----------
// 源：ResultForm.cs [L85-L106] + [L35-L76]
/* begin: columns & bind */
/// <summary>
/// 固定列顺序：款式名 / 白胚可用数 / 进货仓库存 / 成品占用数 / 可用数汇总
/// </summary>
private void BuildVipColumnsAndBind()
{
    _vipGrid.SuspendLayout();
    try
    {
        _vipGrid.Columns.Clear();
        _vipColumns.Clear();

        if (_vipView == null || _vipView.Count == 0)
        {
            _vipGrid.RowCount = 0;
            return;
        }

        _vipColumns.Add("product_original_code");
        _vipColumns.Add("白胚可用数");
        _vipColumns.Add("进货仓库存");
        _vipColumns.Add("成品占用数");
        _vipColumns.Add("__sum");

        foreach (var col in _vipColumns)
        {
            var header = col switch
            {
                "product_original_code" => "款式名",
                "白胚可用数" => "白胚可用数",
                "进货仓库存" => "进货仓库存",
                "成品占用数" => "成品占用数",
                "__sum" => "可用数汇总",
                _ => col
            };

            _vipGrid.Columns.Add(col, header);
        }

        _vipGrid.RowCount = _vipView.Count;
    }
    finally
    {
        _vipGrid.ResumeLayout();
    }
}
/* end: columns & bind */

// ---------- 五、虚拟表格回显（CellValueNeeded） ----------
// 源：ResultForm.cs [L78-L103] + [L1-L19]
/* begin: virtual grid value */
private void VipGrid_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
{
    if (_vipView == null) return;
    if (e.RowIndex < 0 || e.RowIndex >= _vipView.Count) return;
    if (e.ColumnIndex < 0 || e.ColumnIndex >= _vipColumns.Count) return;

    var row = _vipView[e.RowIndex];
    if (row == null) return;

    var key = _vipColumns[e.ColumnIndex];

    if (key == "__sum")
    {
        var sum =
            GetVipNumber(row, "白胚可用数", "白坯可用数") +
            GetVipNumber(row, "进货仓库存") +
            GetVipNumber(row, "成品占用数");
        e.Value = sum;
        return;
    }

    if (key == "成品占用数")
    {
        e.Value = GetVipNumber(row, "成品占用数");
        return;
    }

    if (key == "白胚可用数")
    {
        if (!row.TryGetValue("白胚可用数", out var val) || val == null)
            row.TryGetValue("白坯可用数", out val);
        e.Value = val ?? 0;
        return;
    }

    if (key == "product_original_code")
    {
        row.TryGetValue("product_original_code", out var val);
        e.Value = val;
        return;
    }

    if (row.TryGetValue(key, out var v))
    {
        e.Value = v;
    }
}
/* end: virtual grid value */

// ---------- 六、列头点击排序 ----------
// 源：ResultForm.cs [L22-L54] + [L56-L89]
/* begin: sorting */
private void VipGrid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
{
    if (_vipView == null || _vipView.Count <= 1) return;
    if (e.ColumnIndex < 0 || e.ColumnIndex >= _vipColumns.Count) return;

    var col = _vipColumns[e.ColumnIndex];

    if (_vipSortColumn == col) _vipSortAscending = !_vipSortAscending;
    else { _vipSortColumn = col; _vipSortAscending = true; }

    _vipView.Sort((a, b) =>
    {
        var ka = GetVipSortKey(a, col);
        var kb = GetVipSortKey(b, col);

        if (ka == null && kb == null) return 0;
        if (ka == null) return -1;
        if (kb == null) return 1;
        return ka.CompareTo(kb);
    });

    if (!_vipSortAscending) _vipView.Reverse();

    _vipGrid.Invalidate();
}

private IComparable? GetVipSortKey(Dictionary<string, object?>? row, string col)
{
    if (row == null) return null;

    if (col == "__sum")
    {
        return
            GetVipNumber(row, "白胚可用数", "白坯可用数") +
            GetVipNumber(row, "进货仓库存") +
            GetVipNumber(row, "成品占用数");
    }

    if (col == "白胚可用数") return GetVipNumber(row, "白胚可用数", "白坯可用数");
    if (col == "进货仓库存") return GetVipNumber(row, "进货仓库存");
    if (col == "成品占用数") return GetVipNumber(row, "成品占用数");

    if (!row.TryGetValue(col, out var val) || val == null) return null;

    switch (val)
    {
        case int i: return i;
        case long l: return l;
        case double d: return d;
    }

    if (double.TryParse(val.ToString(), out var dv)) return dv;

    return val.ToString();
}

private double GetVipNumber(Dictionary<string, object?> row, params string[] keys)
{
    foreach (var key in keys)
    {
        if (!row.TryGetValue(key, out var val) || val == null)
            continue;

        switch (val)
        {
            case int i: return i;
            case long l: return l;
            case double d: return d;
            case float f: return f;
            case string s when double.TryParse(s, out var dv): return dv;
        }
    }

    return 0d;
}
/* end: sorting */

// ---------- 七、本地过滤（搜索框） ----------
// 源：ResultForm.cs [L52-L101]
/* begin: filter */
/// <summary>
/// 多关键词 AND 搜索（空格/逗号/加号等拆分），在整行文本上匹配。
/// 不重新请求接口，仅针对本地缓存数据过滤。
/// </summary>
private void ApplyVipFilter(string? keyword)
{
    if (_vipAll == null || _vipAll.Count == 0)
    {
        _vipView = new List<Dictionary<string, object?>>();
    }
    else
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            _vipView = _vipAll.ToList();
        }
        else
        {
            var parts = keyword
                .Split(new[] { ' ', '　', ',', '，', '+', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Select(p => p.ToLowerInvariant())
                .ToArray();

            if (parts.Length == 0)
            {
                _vipView = _vipAll.ToList();
            }
            else
            {
                _vipView = _vipAll
                    .Where(row =>
                    {
                        if (row == null) return false;

                        var text = string.Join(" ", row.Values
                            .Select(v => v?.ToString() ?? string.Empty))
                            .ToLowerInvariant();

                        return parts.All(p => text.Contains(p));
                    })
                    .ToList();
            }
        }
    }

    BuildVipColumnsAndBind();
    _vipGrid.Invalidate();
}
/* end: filter */

        private sealed class DetailFilter
        {
            public string? Channel { get; set; }
            public string? Shop { get; set; }
            public string? Color { get; set; }
            public string? Size { get; set; }
            public string? Text { get; set; }
        }


        // 行缓存：销售明细行 + 预计算的搜索文本，避免每次过滤时通过反射拼接字符串。
        private sealed class SaleRow
        {
            public string 日期   { get; set; } = string.Empty;
            public string 渠道   { get; set; } = string.Empty;
            public string 店铺   { get; set; } = string.Empty;
            public string 款式   { get; set; } = string.Empty;
            public string 颜色   { get; set; } = string.Empty;
            public string 尺码   { get; set; } = string.Empty;
            public int    数量   { get; set; }

            /// <summary>
            /// 用于本地搜索的预计算字段，包含日期/渠道/店铺/款式/尺码/颜色/数量等信息。
            /// </summary>
            public string SearchText { get; set; } = string.Empty;
        }

// ===== 结束 =====
// === VIP INTEGRATION END ===
}

}