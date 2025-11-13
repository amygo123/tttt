using System.Windows.Forms;

namespace StyleWatcherWin
{
    /// <summary>
    /// Shared DataGridView helpers to keep UI configuration consistent.
    /// </summary>
    internal static class UiGrid
    {
        /// <summary>
        /// Applies a set of sensible defaults for read-only analytics grids.
        /// </summary>
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
        /// <summary>
        /// Enables double buffering on DataGridView to reduce flicker.
        /// Uses reflection to set the protected DoubleBuffered property.
        /// </summary>
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
