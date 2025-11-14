using System.Drawing;
using System.Windows.Forms;

namespace StyleWatcherWin
{
    internal static class UiGrid
    {
        public static void Optimize(DataGridView grid)
        {
            if (grid == null) return;

            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            if (grid.AutoSizeColumnsMode == DataGridViewAutoSizeColumnsMode.NotSet)
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;

            if (grid.AutoSizeRowsMode == DataGridViewAutoSizeRowsMode.None)
                grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            ApplyVisualStyle(grid);
            grid.DoubleBuffered(true);
        }

        public static void ApplyVisualStyle(DataGridView grid)
        {
            if (grid == null) return;

            grid.BorderStyle = BorderStyle.None;
            grid.BackgroundColor = UI.Background;
            grid.EnableHeadersVisualStyles = false;

            var header = grid.ColumnHeadersDefaultCellStyle;
            header.BackColor = UI.HeaderBack;
            header.ForeColor = UI.Text;
            header.Font = UI.Body;
            header.Alignment = DataGridViewContentAlignment.MiddleLeft;

            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.ColumnHeadersHeight = 28;

            var cell = grid.DefaultCellStyle;
            cell.BackColor = UI.Background;
            cell.ForeColor = UI.Text;
            cell.SelectionBackColor = Color.FromArgb(230, 243, 232);
            cell.SelectionForeColor = UI.Text;
            cell.Font = UI.Body;
            cell.WrapMode = DataGridViewTriState.False;

            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);

            grid.GridColor = Color.FromArgb(235, 238, 244);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.RowTemplate.Height = 26;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.ValueType == typeof(int) ||
                    col.ValueType == typeof(long) ||
                    col.ValueType == typeof(float) ||
                    col.ValueType == typeof(double) ||
                    col.ValueType == typeof(decimal))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                }
            }
        }
    }

    internal static class DataGridViewExtensions
    {
        public static void DoubleBuffered(this DataGridView dgv, bool setting)
        {
            if (dgv == null) return;

            var prop = typeof(DataGridView).GetProperty(
                "DoubleBuffered",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            prop?.SetValue(dgv, setting, null);
        }
    }
}
