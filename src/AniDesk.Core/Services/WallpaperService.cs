using System.Runtime.InteropServices;
using AniDesk.Core.Interop;
using AniDesk.Core.Models;

namespace AniDesk.Core.Services;

public interface IWallpaperService
{
    List<DisplayMonitorInfo> GetConnectedMonitors();
    Task<bool> SetWallpaperAsync(string imageUrlOrPath, int monitorIndex = -1, WallpaperFit fit = WallpaperFit.Fill);
    Task<bool> SetLockScreenAsync(string imageUrlOrPath);
}

public class WallpaperService : IWallpaperService
{
    private readonly HttpClient _httpClient;
    private readonly IImageCacheService? _cacheService;
    private readonly string _wallpaperFolder;

    public WallpaperService(IImageCacheService? cacheService = null, HttpClient? httpClient = null)
    {
        _cacheService = cacheService;
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AniDesk/1.0");
        _wallpaperFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AniDesk",
            "ActiveWallpapers"
        );
        Directory.CreateDirectory(_wallpaperFolder);
    }

    public List<DisplayMonitorInfo> GetConnectedMonitors()
    {
        var list = new List<DisplayMonitorInfo>();

        IDesktopWallpaper? desktopWallpaper = null;
        try
        {
            desktopWallpaper = DesktopWallpaperFactory.Create();
            if (desktopWallpaper != null)
            {
                uint count = desktopWallpaper.GetMonitorDevicePathCount();

            for (uint i = 0; i < count; i++)
            {
                string devicePath = desktopWallpaper.GetMonitorDevicePathAt(i);
                desktopWallpaper.GetMonitorRECT(devicePath, out var rect);

                list.Add(new DisplayMonitorInfo
                {
                    Index = (int)i,
                    DeviceId = devicePath,
                    DeviceName = $"Display {i + 1}",
                    Width = rect.Width > 0 ? rect.Width : 1920,
                    Height = rect.Height > 0 ? rect.Height : 1080,
                    IsPrimary = (i == 0)
                });
            }
        }
        }
        catch
        {
            // Fallback via EnumDisplayMonitors Win32 API
            int idx = 0;
            Win32Helper.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr d) =>
            {
                var mi = new Win32Helper.MONITORINFOEX { cbSize = Marshal.SizeOf(typeof(Win32Helper.MONITORINFOEX)) };
                if (Win32Helper.GetMonitorInfo(hMon, ref mi))
                {
                    list.Add(new DisplayMonitorInfo
                    {
                        Index = idx++,
                        DeviceId = mi.szDevice,
                        DeviceName = $"Display {idx}",
                        Width = mi.rcMonitor.Width,
                        Height = mi.rcMonitor.Height,
                        IsPrimary = (mi.dwFlags & Win32Helper.MONITORINFOF_PRIMARY) != 0
                    });
                }
                return true;
            }, IntPtr.Zero);
        }
        finally
        {
            if (desktopWallpaper != null)
            {
                Marshal.ReleaseComObject(desktopWallpaper);
            }
        }

        if (list.Count == 0)
        {
            list.Add(new DisplayMonitorInfo
            {
                Index = 0,
                DeviceName = "Display 1 (Primary)",
                Width = 1920,
                Height = 1080,
                IsPrimary = true
            });
        }

        return list;
    }

    public async Task<bool> SetWallpaperAsync(string imageUrlOrPath, int monitorIndex = -1, WallpaperFit fit = WallpaperFit.Fill)
    {
        try
        {
            string localFilePath = await EnsureLocalFileAsync(imageUrlOrPath);
            if (!File.Exists(localFilePath)) return false;

            // 1. Try Windows 8/10/11 IDesktopWallpaper COM API
            IDesktopWallpaper? desktopWallpaper = null;
            try
            {
                desktopWallpaper = DesktopWallpaperFactory.Create();
                if (desktopWallpaper == null) throw new InvalidOperationException("COM wallpaper service unavailable");
                desktopWallpaper.SetPosition((DesktopWallpaperPosition)fit);

                if (monitorIndex < 0)
                {
                    // Apply to all monitors
                    uint count = desktopWallpaper.GetMonitorDevicePathCount();
                    if (count == 0)
                    {
                        desktopWallpaper.SetWallpaper(null, localFilePath);
                    }
                    else
                    {
                        for (uint i = 0; i < count; i++)
                        {
                            string devicePath = desktopWallpaper.GetMonitorDevicePathAt(i);
                            desktopWallpaper.SetWallpaper(devicePath, localFilePath);
                        }
                    }
                }
                else
                {
                    // Apply to specific monitor
                    uint count = desktopWallpaper.GetMonitorDevicePathCount();
                    if (monitorIndex < count)
                    {
                        string devicePath = desktopWallpaper.GetMonitorDevicePathAt((uint)monitorIndex);
                        desktopWallpaper.SetWallpaper(devicePath, localFilePath);
                    }
                    else
                    {
                        desktopWallpaper.SetWallpaper(null, localFilePath);
                    }
                }

                return true;
            }
            catch
            {
                // Fallback to legacy SystemParametersInfo API
                return Win32Helper.SystemParametersInfo(
                    Win32Helper.SPI_SETDESKWALLPAPER,
                    0,
                    localFilePath,
                    Win32Helper.SPIF_UPDATEINIFILE | Win32Helper.SPIF_SENDCHANGE
                );
            }
            finally
            {
                if (desktopWallpaper != null)
                {
                    Marshal.ReleaseComObject(desktopWallpaper);
                }
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SetLockScreenAsync(string imageUrlOrPath)
    {
        try
        {
            string localFilePath = await EnsureLocalFileAsync(imageUrlOrPath);
            if (!File.Exists(localFilePath)) return false;

            string lockScreenFile = Path.Combine(_wallpaperFolder, "lockscreen.jpg");
            File.Copy(localFilePath, lockScreenFile, true);

            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\PersonalizationCSP", true);
                if (key != null)
                {
                    key.SetValue("LockScreenImagePath", lockScreenFile);
                    key.SetValue("LockScreenImageUrl", lockScreenFile);
                    key.SetValue("LockScreenImageStatus", 1);
                }
            }
            catch { }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> EnsureLocalFileAsync(string pathOrUrl)
    {
        if (File.Exists(pathOrUrl))
        {
            return pathOrUrl;
        }

        if (_cacheService != null)
        {
            string cached = await _cacheService.GetCachedImagePathAsync(pathOrUrl);
            if (File.Exists(cached))
            {
                return cached;
            }
        }

        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            string ext = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";
            string targetFile = Path.Combine(_wallpaperFolder, $"current_wallpaper_{Guid.NewGuid():N}{ext}");
            string tempFile = $"{targetFile}.download";

            try
            {
                foreach (var old in Directory.GetFiles(_wallpaperFolder))
                {
                    try { File.Delete(old); } catch { }
                }
            }
            catch { }

            using var response = await _httpClient.GetAsync(pathOrUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            try
            {
                await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 81920, useAsync: true))
                {
                    await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                    await fileStream.FlushAsync().ConfigureAwait(false);
                }

                if (File.Exists(targetFile))
                {
                    try { File.Delete(targetFile); } catch { }
                }
                File.Move(tempFile, targetFile);
                return targetFile;
            }
            catch
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                throw;
            }
        }

        return pathOrUrl;
    }
}
