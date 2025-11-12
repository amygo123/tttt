
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

public static class UiKit
{
    public static void ApplyGridDefaults(DataGridView g)
    {
        if (g == null) return;
        g.Dock = DockStyle.Fill;
        g.BackgroundColor = Color.White;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245,245,245);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        g.RowHeadersVisible = false;
        g.BorderStyle = BorderStyle.None;
        // Try enabling double-buffering to reduce flicker (reflection, as property is protected)
        var pi = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
        if (pi != null) pi.SetValue(g, true);
    }

    public static void ApplyTabsDefaults(TabControl t)
    {
        if (t == null) return;
        t.Dock = DockStyle.Fill;
    }

    public static void ApplyLabelDefaults(Label l)
    {
        if (l == null) return;
        l.AutoSize = true;
        l.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        l.ForeColor = Color.Black;
        l.BackColor = Color.Transparent;
    }

    public static void ApplyToolTipDefaults(ToolTip tip)
    {
        if (tip == null) return;
        tip.InitialDelay = 0;
        tip.AutoPopDelay = 8000;
        tip.ReshowDelay = 0;
        tip.ShowAlways = true;
    }

    // Reflection-based plot defaults so we don't need a compile-time reference
    public static void ApplyPlotDefaults(object plotViewLike)
    {
        if (plotViewLike == null) return;
        var t = plotViewLike.GetType();
        // Dock
        var dockProp = t.GetProperty("Dock");
        if (dockProp != null && dockProp.CanWrite)
            dockProp.SetValue(plotViewLike, DockStyle.Fill);
        // BackColor
        var backProp = t.GetProperty("BackColor");
        if (backProp != null && backProp.CanWrite)
            backProp.SetValue(plotViewLike, Color.White);
    }
}
