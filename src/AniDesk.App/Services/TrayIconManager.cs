using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AniDesk.Core.Interop;
using AniDesk.Core.Services;

namespace AniDesk.App.Services;

public sealed class TrayIconManager : IDisposable
{
    private const int TRAY_ICON_ID = 0x1001;

    private readonly Window _mainWindow;
    private readonly PanicButtonService _panicService;
    private readonly IContentSafetyService? _safetyService;
    private readonly IDynamicWallpaperService? _dynamicWallpaperService;
    private readonly IntPtr _sinkHwnd;
    private NativeMethods.NOTIFYICONDATA _nid;
    private bool _isAdded;
    private ContextMenu? _contextMenu;
    private IntPtr _hIcon = IntPtr.Zero;

    public TrayIconManager(Window mainWindow, PanicButtonService panicService, IntPtr sinkHwnd, IContentSafetyService? safetyService = null, IDynamicWallpaperService? dynamicWallpaperService = null)
    {
        _mainWindow = mainWindow;
        _panicService = panicService;
        _sinkHwnd = sinkHwnd;
        _safetyService = safetyService;
        _dynamicWallpaperService = dynamicWallpaperService;

        InitializeTrayIcon();
        CreateContextMenu();

        // Intercept close button on main window to minimize to tray instead
        _mainWindow.Closing += OnMainWindowClosing;
    }

    private void InitializeTrayIcon()
    {
        try
        {
            // Extract icon from the current executable
            string? exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                NativeMethods.ExtractIconEx(exePath, 0, out _, out _hIcon, 1);
            }
        }
        catch { }

        _nid = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf(typeof(NativeMethods.NOTIFYICONDATA)),
            hWnd = _sinkHwnd,
            uID = TRAY_ICON_ID,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "AniDesk — Wallpaper Explorer"
        };

        _isAdded = NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
    }

    private void CreateContextMenu()
    {
        _contextMenu = new ContextMenu
        {
            Placement = PlacementMode.MousePoint
        };

        var openItem = new MenuItem { Header = "Open AniDesk", FontWeight = FontWeights.SemiBold };
        openItem.Click += (s, e) => AppSuspensionManager.RestoreForegroundMode(_mainWindow);

        var nextWallpaperItem = new MenuItem { Header = "Next Wallpaper (Shuffle)" };
        nextWallpaperItem.Click += async (s, e) =>
        {
            if (_dynamicWallpaperService != null)
            {
                await _dynamicWallpaperService.TriggerNextAsync();
            }
        };

        var panicItem = new MenuItem { Header = "Emergency Panic (Toggle)" };
        panicItem.Click += (s, e) => _panicService.ExecuteEmergencyToggle();

        var sfwItem = new MenuItem { Header = "SFW Shield Active", IsCheckable = true };
        if (_safetyService != null)
        {
            sfwItem.IsChecked = _safetyService.IsSfwShieldActive;
            sfwItem.Click += (s, e) =>
            {
                _safetyService.IsSfwShieldActive = !_safetyService.IsSfwShieldActive;
                sfwItem.IsChecked = _safetyService.IsSfwShieldActive;
            };
        }

        var exitItem = new MenuItem { Header = "Exit AniDesk" };
        exitItem.Click += (s, e) =>
        {
            _mainWindow.Closing -= OnMainWindowClosing;
            Dispose();
            Application.Current.Shutdown();
        };

        _contextMenu.Items.Add(openItem);
        _contextMenu.Items.Add(nextWallpaperItem);
        _contextMenu.Items.Add(panicItem);
        if (_safetyService != null)
        {
            _contextMenu.Items.Add(sfwItem);
        }
        _contextMenu.Items.Add(new Separator());
        _contextMenu.Items.Add(exitItem);
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        AppSuspensionManager.EnterBackgroundMode(_mainWindow);
    }

    public bool HandleWindowMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == NativeMethods.WM_TRAYICON && wParam.ToInt32() == TRAY_ICON_ID)
        {
            int eventType = lParam.ToInt32() & 0xFFFF;

            if (eventType == NativeMethods.WM_LBUTTONUP || eventType == NativeMethods.WM_LBUTTONDBLCLK)
            {
                AppSuspensionManager.RestoreForegroundMode(_mainWindow);
                return true;
            }
            else if (eventType == NativeMethods.WM_RBUTTONUP)
            {
                if (_contextMenu != null)
                {
                    _mainWindow.Dispatcher.InvokeAsync(() =>
                    {
                        _contextMenu.IsOpen = true;
                    });
                }
                return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        _mainWindow.Closing -= OnMainWindowClosing;

        if (_isAdded && _sinkHwnd != IntPtr.Zero)
        {
            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);
            _isAdded = false;
        }

        if (_hIcon != IntPtr.Zero)
        {
            try { NativeMethods.DestroyIcon(_hIcon); } catch { }
            _hIcon = IntPtr.Zero;
        }
    }
}
