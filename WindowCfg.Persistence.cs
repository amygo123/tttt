namespace StyleWatcherWin
{
    public partial class WindowCfg
    {
        public static WindowCfg? Load(string path)
        {
            if (!System.IO.File.Exists(path)) return default;
            return WindowCfg.Load(path);
        }

        public void Save(string path)
        {
            this.Save(path);}
    }
}
