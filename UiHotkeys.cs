using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StyleWatcherWin
{
    internal static class UiHotkeys
    {
        // Common modifier flags (Mirror of Win32 MOD_*)
        public const int MOD_ALT = 0x0001;
        public const int MOD_CONTROL = 0x0002;
        public const int MOD_SHIFT = 0x0004;
        public const int MOD_WIN = 0x0008;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, Keys vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static bool TryRegister(IntPtr handle, int id, int modifiers, Keys key)
            => RegisterHotKey(handle, id, modifiers, key);

        public static bool TryUnregister(IntPtr handle, int id)
            => UnregisterHotKey(handle, id);
    }
}
