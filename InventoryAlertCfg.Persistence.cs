namespace StyleWatcherWin
{
    public partial class InventoryAlertCfg
    {
        public static InventoryAlertCfg? Load(string path)
        {
            if (!System.IO.File.Exists(path)) return default;
            return InventoryAlertCfg.Load(path);
        }

        public void Save(string path)
        {
            this.Save(path);}
    }
}
