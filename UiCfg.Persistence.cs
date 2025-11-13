namespace StyleWatcherWin
{
    public partial class UiCfg
    {
        public static UiCfg? Load(string path)
        {
            if (!System.IO.File.Exists(path)) return default;
            return UiCfg.Load(path);
        }

        public void Save(string path)
        {
            this.Save(path);}
    }
}
