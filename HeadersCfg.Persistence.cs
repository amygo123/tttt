namespace StyleWatcherWin
{
    public partial class HeadersCfg
    {
        public static HeadersCfg? Load(string path)
        {
            if (!System.IO.File.Exists(path)) return default;
            return HeadersCfg.Load(path);
        }

        public void Save(string path)
        {
            this.Save(path);}
    }
}
