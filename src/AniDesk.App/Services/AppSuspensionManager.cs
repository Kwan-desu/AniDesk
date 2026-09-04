using System;
using System.Diagnostics;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using AniDesk.App.Converters;
using AniDesk.Core.Interop;

namespace AniDesk.App.Services;

public static class AppSuspensionManager
{
    private static volatile bool _isHibernating;

    public static bool IsHibernating => _isHibernating;

    public static void EnterBackgroundMode(Window mainWindow)
    {
        mainWindow.Hide();
        mainWindow.ShowInTaskbar = false;
        Hibernate();
    }

    public static void Hibernate()
    {
        _isHibernating = true;
        AsyncImageLoader.PurgeMemoryCache();

        _ = Task.Run(async () =>
        {
            await Task.Delay(200);
            if (!_isHibernating) return;

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            try
            {
                IntPtr hProcess = Process.GetCurrentProcess().Handle;
                NativeMethods.SetProcessWorkingSetSize(hProcess, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        });
    }

    public static void RestoreForegroundMode(Window mainWindow)
    {
        _isHibernating = false;

        mainWindow.ShowInTaskbar = true;
        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
        NativeMethods.SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle);
    }
}
