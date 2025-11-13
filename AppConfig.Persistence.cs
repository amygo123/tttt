namespace StyleWatcherWin
{
    public partial class AppConfig
    {
        public static AppConfig? Load(string path)
        {
            if (!System.IO.File.Exists(path)) return default;
            return AppConfig.Load(path);
        }

        public void Save(string path)
        {
            this.Save(path);}
    }
}
