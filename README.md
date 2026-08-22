# AniDesk — Wallpaper Explorer

AniDesk is a beautiful, high-performance desktop wallpaper application built for Windows 11 using WPF and .NET 9. It allows you to seamlessly explore, download, and apply high-quality anime wallpapers directly from Moebooru-based image boards like `yande.re` and `konachan.net`.

## Features

- **Multi-Source Exploring:** Browse feeds from yande.re, konachan.net, or both combined!
- **High Performance:** Infinite scrolling with a virtualized 16:9 thumbnail grid that scales gracefully with your window size.
- **SFW Shield:** A built-in safety toggle that enforces Safe-For-Work content filtering, complete with visual UI indicators.
- **Background Image Decoding:** A custom asynchronous image loader prevents UI freezing and ensures smooth scrolling by loading thumbnails entirely on background threads.
- **Set Wallpaper & Lock Screen:** Apply high-resolution images to your desktop or lock screen across multiple monitors with a single click.
- **Favorites & Downloads:** Save your favorite images or download the original, full-resolution files locally.
- **Fluent Design:** A sleek, modern dark-themed interface built with `Wpf.Ui` that fits perfectly into Windows 11.

## Installation

1. Go to the [Releases](../../releases) page.
2. Download `AniDesk_Setup.exe`.
3. Run the installer and follow the prompts.

## Building from Source

To build AniDesk from source, you will need the [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```powershell
git clone https://github.com/Kwan-desu/AniDesk.git
cd AniDesk
dotnet build
dotnet run --project src/AniDesk.App/AniDesk.App.csproj
```

To create a single-file executable release:
```powershell
dotnet publish src/AniDesk.App/AniDesk.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

## Architecture

AniDesk employs a Clean Architecture design:
*   **`AniDesk.Core`**: Contains all business logic, API integrations, and Windows Interop logic. This library has no WPF dependencies.
*   **`AniDesk.App`**: The UI layer built on the MVVM (Model-View-ViewModel) pattern using `CommunityToolkit.Mvvm`.

## License

This project is open-source and available under the MIT License.
