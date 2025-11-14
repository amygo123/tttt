using System;
using System.Windows.Forms;

namespace StyleWatcherWin
{
    internal static class ControlExtensions
    {
        /// <summary>
        /// 在 WinForms 中安全地在 UI 线程上执行指定操作。
        /// 如果当前已经在 UI 线程，则直接执行；否则通过 Invoke 回到 UI 线程。
        /// </summary>
        public static void SafeInvoke(this Control control, Action action)
        {
            if (control == null || control.IsDisposed) return;

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // 控件已释放，忽略
                }
            }
            else
            {
                action();
            }
        }
    }
}
