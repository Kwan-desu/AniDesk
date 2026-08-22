using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wpf.Ui.Appearance;
using AniDesk.App.ViewModels;
using AniDesk.App.Views;
using AniDesk.Core.Services;

namespace AniDesk.App;

public partial class App : Application
{
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
            args.Handled = true; // Prevent crash
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            // Prevent unhandled background crash
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            args.SetObserved(); // Prevent task crash
        };

        await _host.StartAsync();

        // Apply dark Fluent theme
        ApplicationThemeManager.Apply(ApplicationTheme.Dark);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
