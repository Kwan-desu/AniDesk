using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AniDesk.Core.Services;

namespace AniDesk.App.ViewModels;

public partial class DownloadsViewModel : ObservableObject
{
    private readonly IDownloadService _downloadService;
    private readonly ILocalStorageService _storageService;

    public ObservableCollection<DownloadItem> Downloads => _downloadService.Downloads;

    public DownloadsViewModel(IDownloadService downloadService, ILocalStorageService storageService)
    {
        _downloadService = downloadService;
        _storageService = storageService;
    }

    [RelayCommand]
    private void OpenDownloadFolder()
    {
        string folder = _storageService.GetDownloadDirectory();
        if (Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void OpenFile(DownloadItem? item)
    {
        if (item != null && File.Exists(item.TargetFilePath))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.TargetFilePath,
                UseShellExecute = true
            });
        }
    }
}
