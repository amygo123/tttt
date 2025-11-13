using System.Windows.Forms;

namespace StyleWatcherWin
{
    internal static class UiGrid
    {
        public static void AutoSize(DataGridView grid)
        {
            if (grid == null) return;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        }

        public static void Optimize(DataGridView grid)
        {
            if (grid == null) return;
            grid.DoubleBuffered(true);
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }
    }

    internal static class DataGridViewExtensions
    {
        public static void DoubleBuffered(this DataGridView dgv, bool setting)
        {
            var prop = typeof(DataGridView).GetProperty("DoubleBuffered",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            prop?.SetValue(dgv, setting, null);
        }
    }
}
