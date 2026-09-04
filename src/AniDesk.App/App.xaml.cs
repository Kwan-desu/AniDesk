using System.Net.Http;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui.Appearance;
using AniDesk.App.Services;
using AniDesk.App.ViewModels;
using AniDesk.App.Views;
using AniDesk.Core.Services;

namespace AniDesk.App;

public partial class App : Application
{
    private static HwndSource? _hotkeySink;
    private static PanicButtonService? _panicService;
    private static TrayIconManager? _trayManager;

    public static PanicButtonService? PanicService => _panicService;

    private static readonly IHost _host = Host
        .CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // Core Services
            services.AddSingleton<HttpClient>();
            services.AddSingleton<ILocalStorageService, LocalStorageService>();
            services.AddSingleton<IContentSafetyService, ContentSafetyService>();
            services.AddSingleton<IImageCacheService, ImageCacheService>();
            services.AddSingleton<IMoebooruService, MoebooruService>();
            services.AddSingleton<IWallpaperService, WallpaperService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<IDynamicWallpaperService, DynamicWallpaperService>();
            services.AddSingleton(sp => _panicService!);

            // ViewModels
            services.AddSingleton<ExploreViewModel>();
            services.AddSingleton<FavoritesViewModel>();
            services.AddSingleton<DownloadsViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<WallpaperDetailViewModel>();
            services.AddSingleton<MainViewModel>();

            // Views
            services.AddSingleton<MainWindow>();
        })
        .Build();

    public static IServiceProvider Services => _host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Global Exception Protection
        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk", "error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now}] Dispatcher Exception: {args.Exception}\n");
            }
            catch { }
            args.Handled = true; // Prevent abrupt crash
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk", "error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.AppendAllText(logPath, $"[{DateTime.Now}] AppDomain Exception: {args.ExceptionObject}\n");
            }
            catch { }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            args.SetObserved();
        };

        try
        {
            // 1. Create persistent HWND_MESSAGE sink for global hotkeys & tray messages
            var parameters = new System.Windows.Interop.HwndSourceParameters("AniDesk_MessageSink")
            {
                ParentWindow = (IntPtr)(-3) // HWND_MESSAGE (never destroyed on hide/minimize)
            };
            _hotkeySink = new System.Windows.Interop.HwndSource(parameters);

            await _host.StartAsync();

            // 2. Initialize PanicButtonService
            var storage = _host.Services.GetRequiredService<ILocalStorageService>();
            var savedSettings = storage.LoadSettings();
            _panicService = new PanicButtonService(_hotkeySink.Handle, customSafePath: savedSettings.PanicWallpaperPath);
            _panicService.Register(savedSettings.PanicModifiers, savedSettings.PanicKey);

            _hotkeySink.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (_panicService != null && _panicService.HandleWindowMessage(msg, wParam))
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                if (_trayManager != null && _trayManager.HandleWindowMessage(msg, wParam, lParam))
                {
                    handled = true;
                    return IntPtr.Zero;
                }
                return IntPtr.Zero;
            });

            // Apply dark Fluent theme
            ApplicationThemeManager.Apply(ApplicationTheme.Dark);

            bool isDaemon = e.Args.Any(a => string.Equals(a, "--daemon", StringComparison.OrdinalIgnoreCase) ||
                                            string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase));

            _panicService.IsEnabled = savedSettings.EnableEmergencyDesktop;

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
                        // Verbose startup tracing for troubleshooting UI not showing
                        try
                        {
                            string verboseLogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk");
                            Directory.CreateDirectory(verboseLogDir);
                            File.AppendAllText(Path.Combine(verboseLogDir, "startup_verbose.log"), $"[{DateTime.Now}] Obtained MainWindow instance\n");
                        }
                        catch { }

                        var helper = new System.Windows.Interop.WindowInteropHelper(mainWindow);
                        _panicService.SetTargetWindow(helper.EnsureHandle());

                        var dynamicService = _host.Services.GetRequiredService<IDynamicWallpaperService>();
                        dynamicService.Start();

                        _trayManager = new TrayIconManager(
                            mainWindow,
                            _panicService,
                            _hotkeySink.Handle,
                            _host.Services.GetService<IContentSafetyService>(),
                            dynamicService
                        );

                        try
                        {
                            string verboseLogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk");
                            File.AppendAllText(Path.Combine(verboseLogDir, "startup_verbose.log"), $"[{DateTime.Now}] TrayManager created, isDaemon={isDaemon}\n");
                        }
                        catch { }

                        if (!isDaemon)
                        {
                            try
                            {
                                try
                                {
                                    string verboseLogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk");
                                    File.AppendAllText(Path.Combine(verboseLogDir, "startup_verbose.log"), $"[{DateTime.Now}] About to call mainWindow.Show()\n");
                                }
                                catch { }

                                mainWindow.Show();

                                try
                                {
                                    string verboseLogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk");
                                    File.AppendAllText(Path.Combine(verboseLogDir, "startup_verbose.log"), $"[{DateTime.Now}] mainWindow.Show() called\n");
                                }
                                catch { }
                            }
                            catch (Exception ex)
                            {
                                try { File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk", "startup_verbose.log"), $"[{DateTime.Now}] Exception showing mainWindow: {ex}\n"); } catch { }
                                throw;
                            }
                        }
                        else
                        {
                            AppSuspensionManager.Hibernate();
                        }

            base.OnStartup(e);
        }
        catch (Exception ex)
        {
            try
            {
                string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniDesk", "startup_error.log");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath, ex.ToString());
            }
            catch { }
            MessageBox.Show($"AniDesk encountered an initialization error:\n\n{ex.Message}", "AniDesk Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _trayManager?.Dispose();
        _panicService?.Dispose();
        _hotkeySink?.Dispose();

        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
