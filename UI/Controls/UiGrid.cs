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

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

            grid.DoubleBuffered(true);
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
