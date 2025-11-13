namespace StyleWatcherWin
{
    public partial class InventoryCfg
    {
        public static InventoryCfg? Load(string path)
        {
            if (!System.IO.File.Exists(path)) return default;
            return InventoryCfg.Load(path);
        }

        public void Save(string path)
        {
            this.Save(path);}
    }
}
