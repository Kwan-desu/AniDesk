using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AniDesk.Core.Interop;

namespace AniDesk.Core.Services;

public sealed class PanicButtonService : IDisposable
{
    public const int PANIC_HOTKEY_ID = 0x9A01;

    private readonly IntPtr _hotkeySinkHwnd;
    private IntPtr _targetWindowHwnd;
    private string _safeWallpaperPath;
    private bool _isPanicked;
    private bool _isRegistered;
    private uint _currentModifiers;
    private uint _currentKey;
    private int _isTransitioning = 0;
    private readonly Dictionary<string, string> _previousWallpapers = new(StringComparer.OrdinalIgnoreCase);

    public bool IsPanicked => _isPanicked;
    public bool IsRegistered => _isRegistered;
    public string SafeWallpaperPath => _safeWallpaperPath;

    public event EventHandler<bool>? PanicStateChanged;

    public PanicButtonService(IntPtr hotkeySinkHwnd, IntPtr targetWindowHwnd = default, string? customSafePath = null)
    {
        _hotkeySinkHwnd = hotkeySinkHwnd;
        _targetWindowHwnd = targetWindowHwnd;
        _safeWallpaperPath = InitializeSafeWallpaper(customSafePath);
    }

    public void SetTargetWindow(IntPtr targetHwnd)
    {
        _targetWindowHwnd = targetHwnd;
    }

    public void SetCustomSafeWallpaper(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            _safeWallpaperPath = customPath;
        }
        else
        {
            _safeWallpaperPath = InitializeSafeWallpaper(null);
        }
    }

    private static string InitializeSafeWallpaper(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        // Try standard Windows default wallpapers first
        string[] winDefaults =
        [
            @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg",
            @"C:\Windows\Web\Wallpaper\Windows\img0.jpg",
            @"C:\Windows\Web\Wallpaper\Windows\img19.jpg"
        ];

        foreach (var path in winDefaults)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Fallback: Generate neutral clean wallpaper in LocalAppData
        string safeDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniDesk", "Safe"
        );
        Directory.CreateDirectory(safeDir);

        string generatedPath = Path.Combine(safeDir, "clean_default.bmp");
        if (!File.Exists(generatedPath))
        {
            try
            {
                // Generate a sleek, dark slate 1920x1080 24bpp BMP natively without WPF or System.Drawing
                int width = 1920;
                int height = 1080;
                int rowSize = (width * 3 + 3) & ~3; // Align to 4 bytes
                int imageSize = rowSize * height;
                int fileSize = 54 + imageSize;

                using var fs = new FileStream(generatedPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var bw = new BinaryWriter(fs);

                // BMP Header (14 bytes)
                bw.Write((byte)'B');
                bw.Write((byte)'M');
                bw.Write(fileSize);
                bw.Write((short)0); // Reserved
                bw.Write((short)0); // Reserved
                bw.Write(54); // Pixel data offset

                // DIB Header (BITMAPINFOHEADER - 40 bytes)
                bw.Write(40); // Header size
                bw.Write(width);
                bw.Write(height);
                bw.Write((short)1); // Color planes
                bw.Write((short)24); // Bits per pixel (24bpp)
                bw.Write(0); // Compression (BI_RGB)
                bw.Write(imageSize);
                bw.Write(2835); // Horizontal resolution (72 DPI in pixels/meter)
                bw.Write(2835); // Vertical resolution
                bw.Write(0); // Colors in color table
                bw.Write(0); // Important color count

                // Pixel data (BGR format, dark slate RGB 15, 23, 42 -> BGR 42, 23, 15)
                byte[] row = new byte[rowSize];
                for (int x = 0; x < width; x++)
                {
                    int offset = x * 3;
                    row[offset] = 42;     // Blue
                    row[offset + 1] = 23; // Green
                    row[offset + 2] = 15; // Red
                }

                for (int y = 0; y < height; y++)
                {
                    bw.Write(row);
                }
            }
            catch
            {
                // In case generation fails, return empty
            }
        }

        return generatedPath;
    }

    public bool Register(uint modifiers = NativeMethods.MOD_WIN | NativeMethods.MOD_SHIFT | NativeMethods.MOD_NOREPEAT, uint key = 0x48 /* 'H' */)
    {
        if (_hotkeySinkHwnd == IntPtr.Zero) return false;

        if (_isRegistered)
        {
            Unregister();
        }

        _isRegistered = NativeMethods.RegisterHotKey(_hotkeySinkHwnd, PANIC_HOTKEY_ID, modifiers, key);
        if (_isRegistered)
        {
            _currentModifiers = modifiers;
            _currentKey = key;
        }

        return _isRegistered;
    }

    public void Unregister()
    {
        if (_isRegistered && _hotkeySinkHwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hotkeySinkHwnd, PANIC_HOTKEY_ID);
            _isRegistered = false;
        }
    }

    public bool HandleWindowMessage(int msg, IntPtr wParam)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == PANIC_HOTKEY_ID)
        {
            ExecuteEmergencyToggle();
            return true;
        }
        return false;
    }

    public void ExecuteEmergencyToggle()
    {
        // Atomic compare-exchange: drop re-entrant hotkey triggers during active transition
        if (Interlocked.CompareExchange(ref _isTransitioning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            IDesktopWallpaper? desktopWallpaper = null;
            try
            {
                desktopWallpaper = DesktopWallpaperFactory.Create();

                if (!_isPanicked)
                {
                    // 1. Instantly hide window (<0.5ms)
                    if (_targetWindowHwnd != IntPtr.Zero)
                    {
                        NativeMethods.ShowWindowAsync(_targetWindowHwnd, NativeMethods.SW_HIDE);
                    }

                    // 2. Snapshot current active wallpaper(s)
                    _previousWallpapers.Clear();

                    bool comSucceeded = false;
                    if (desktopWallpaper != null)
                    {
                        try
                        {
                            uint count = desktopWallpaper.GetMonitorDevicePathCount();
                            if (count == 0)
                            {
                                string current = desktopWallpaper.GetWallpaper(null);
                                _previousWallpapers[string.Empty] = current;
                                if (File.Exists(_safeWallpaperPath))
                                {
                                    desktopWallpaper.SetWallpaper(null, _safeWallpaperPath);
                                }
                            }
                            else
                            {
                                for (uint i = 0; i < count; i++)
                                {
                                    string devicePath = desktopWallpaper.GetMonitorDevicePathAt(i);
                                    string current = desktopWallpaper.GetWallpaper(devicePath);
                                    _previousWallpapers[devicePath] = current;
                                    if (File.Exists(_safeWallpaperPath))
                                    {
                                        desktopWallpaper.SetWallpaper(devicePath, _safeWallpaperPath);
                                    }
                                }
                            }
                            comSucceeded = true;
                        }
                        catch { }
                    }

                    if (!comSucceeded)
                    {
                        ApplyFallbackSafeWallpaper();
                    }

                    _isPanicked = true;
                }
                else
                {
                    // REVERT / TOGGLE RESTORE
                    bool comRevertSucceeded = false;
                    if (desktopWallpaper != null && _previousWallpapers.Count > 0)
                    {
                        try
                        {
                            uint count = desktopWallpaper.GetMonitorDevicePathCount();
                            if (count == 0 && _previousWallpapers.TryGetValue(string.Empty, out var single) && File.Exists(single))
                            {
                                desktopWallpaper.SetWallpaper(null, single);
                            }
                            else
                            {
                                for (uint i = 0; i < count; i++)
                                {
                                    string devicePath = desktopWallpaper.GetMonitorDevicePathAt(i);
                                    if (_previousWallpapers.TryGetValue(devicePath, out var saved) && File.Exists(saved))
                                    {
                                        desktopWallpaper.SetWallpaper(devicePath, saved);
                                    }
                                }
                            }
                            comRevertSucceeded = true;
                        }
                        catch { }
                    }

                    if (!comRevertSucceeded)
                    {
                        RevertFallbackWallpaper();
                    }

                    // Restore window
                    if (_targetWindowHwnd != IntPtr.Zero)
                    {
                        NativeMethods.ShowWindowAsync(_targetWindowHwnd, NativeMethods.SW_RESTORE);
                        NativeMethods.SetForegroundWindow(_targetWindowHwnd);
                    }

                    _isPanicked = false;
                }
            }
            finally
            {
                if (desktopWallpaper != null)
                {
                    try { Marshal.ReleaseComObject(desktopWallpaper); } catch { }
                }
            }

            PanicStateChanged?.Invoke(this, _isPanicked);
        }
        finally
        {
            Interlocked.Exchange(ref _isTransitioning, 0);
        }
    }

    private string? _fallbackPreviousWallpaper;

    private void ApplyFallbackSafeWallpaper()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            _fallbackPreviousWallpaper = key?.GetValue("WallPaper") as string;
        }
        catch { }

        if (File.Exists(_safeWallpaperPath))
        {
            Win32Helper.SystemParametersInfo(
                Win32Helper.SPI_SETDESKWALLPAPER,
                0,
                _safeWallpaperPath,
                Win32Helper.SPIF_UPDATEINIFILE | Win32Helper.SPIF_SENDCHANGE
            );
        }
    }

    private void RevertFallbackWallpaper()
    {
        if (!string.IsNullOrWhiteSpace(_fallbackPreviousWallpaper) && File.Exists(_fallbackPreviousWallpaper))
        {
            Win32Helper.SystemParametersInfo(
                Win32Helper.SPI_SETDESKWALLPAPER,
                0,
                _fallbackPreviousWallpaper,
                Win32Helper.SPIF_UPDATEINIFILE | Win32Helper.SPIF_SENDCHANGE
            );
        }
    }

    public void Dispose()
    {
        Unregister();
    }
}
