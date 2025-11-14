using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StyleWatcherWin
{
    internal static class Program
    {
        // 用于模拟快捷键，唤醒已存在实例的主窗口
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_MENU    = 0x12; // Alt
        private const byte VK_CONTROL = 0x11; // Ctrl
        private const byte VK_SHIFT   = 0x10; // Shift

        [STAThread]
        static void Main()
        {
            // 单实例互斥量
            using (var mutex = new Mutex(true, "Global\\StyleWatcherWin_SingleInstance", out bool createdNew))
            {
                if (!createdNew)
                {
                    // 已有实例在运行：模拟一次配置好的快捷键，唤醒原实例的窗口，然后退出
                    try
                    {
                        var cfg = AppConfig.Load();
                        var hot = cfg?.hotkey ?? "Alt+S";

                        ParseHotkeyForSimulation(hot, out bool useCtrl, out bool useShift, out bool useAlt, out byte keyVk);

                        // 按下修饰键
                        if (useAlt)   keybd_event(VK_MENU,    0, 0, 0);
                        if (useCtrl)  keybd_event(VK_CONTROL, 0, 0, 0);
                        if (useShift) keybd_event(VK_SHIFT,   0, 0, 0);

                        // 按下并抬起主体键
                        keybd_event(keyVk, 0, 0, 0);
                        keybd_event(keyVk, 0, KEYEVENTF_KEYUP, 0);

                        // 释放修饰键
                        if (useShift) keybd_event(VK_SHIFT,   0, KEYEVENTF_KEYUP, 0);
                        if (useCtrl)  keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
                        if (useAlt)   keybd_event(VK_MENU,    0, KEYEVENTF_KEYUP, 0);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogError(ex, "App/Program.cs");
                        // 即便失败，也不要启动第二个实例
                    }

                    return;
                }

                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
        }

        /// <summary>
        /// 解析配置中的热键字符串，供第二个实例模拟按键使用。
        /// 格式如："Alt+S"、"Ctrl+Shift+K" 等。
        /// </summary>
        private static void ParseHotkeyForSimulation(string s, out bool useCtrl, out bool useShift, out bool useAlt, out byte keyVk)
        {
            useCtrl = useShift = useAlt = false;
            keyVk = (byte)Keys.S;

            if (string.IsNullOrWhiteSpace(s))
            {
                useAlt = true;
                keyVk = (byte)Keys.S;
                return;
            }

            var parts = s.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                var t = p.Trim().ToUpperInvariant();
                if (t == "CTRL" || t == "CONTROL")
                {
                    useCtrl = true;
                }
                else if (t == "SHIFT")
                {
                    useShift = true;
                }
                else if (t == "ALT")
                {
                    useAlt = true;
                }
                else
                {
                    if (Enum.TryParse<Keys>(t, true, out var key))
                    {
                        keyVk = (byte)key;
                    }
                }
            }

            // 如果没有任何修饰键，默认给 Alt
            if (!useCtrl && !useShift && !useAlt)
            {
                useAlt = true;
            }
        }
    }
}