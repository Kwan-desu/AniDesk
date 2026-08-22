<div align="center">

<img src="https://raw.githubusercontent.com/Kwan-desu/AniDesk/main/src/AniDesk.App/icon.png" width="128" height="128" alt="AniDesk Logo" />

# AniDesk — Wallpaper Explorer

**A beautiful, high-performance desktop wallpaper application built for Windows 11.**

[![Release](https://img.shields.io/github/v/release/Kwan-desu/AniDesk?color=blue&style=for-the-badge)](https://github.com/Kwan-desu/AniDesk/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4?style=for-the-badge&logo=windows)](https://github.com/Kwan-desu/AniDesk/releases)
[![DotNet](https://img.shields.io/badge/.NET-9.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](https://opensource.org/licenses/MIT)

</div>

<br/>

AniDesk allows you to seamlessly explore, download, and apply high-quality anime wallpapers directly from Moebooru-based image boards like `yande.re` and `konachan.net`.

---

## ✨ Features

- 🌐 **Multi-Source Exploring**
  Browse feeds from yande.re, konachan.net (SFW), konachan.com (NSFW), or combine them into one infinite feed.
  
- ⚡ **Blazing Fast Performance**
  Infinite scrolling powered by a heavily optimized, virtualized 16:9 thumbnail grid that scales gracefully with your window size while keeping memory usage extremely low.
  
- 🛡️ **SFW Shield**
  A built-in safety toggle that strictly enforces Safe-For-Work content filtering, complete with visual UI indicators so you always know what you're browsing.
  
- 🖼️ **Background Image Decoding**
  A custom asynchronous image caching engine prevents UI freezing. Thumbnails load smoothly and entirely on background threads.
  
- 🖥️ **Instant Wallpaper & Lock Screen**
  Apply high-resolution images to your desktop or lock screen across multiple monitors with a single click natively through Windows APIs.
  
- 📥 **Favorites & Downloads**
  Save your favorite images for later or download the original, uncompressed, full-resolution files directly to your hard drive.
  
- 🎨 **Fluent Design**
  A sleek, modern dark-themed interface built with `Wpf.Ui` that looks and feels perfectly native to Windows 11.

---

## 🚀 Installation

1. Go to the [Releases](https://github.com/Kwan-desu/AniDesk/releases/latest) page.
2. Download the latest `AniDesk_Setup.exe`.
3. Run the installer and follow the on-screen prompts!

---

## 🛠️ Building from Source

To compile and build AniDesk yourself, you will need the [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) installed on your machine.

```powershell
# Clone the repository
git clone https://github.com/Kwan-desu/AniDesk.git
cd AniDesk

# Build and run the application
dotnet build
dotnet run --project src/AniDesk.App/AniDesk.App.csproj
```

**Creating a standalone executable:**
```powershell
dotnet publish src/AniDesk.App/AniDesk.App.csproj -c Release -r win-x64 -p:PublishSingleFile=true
```

---

## 🏗️ Architecture

AniDesk employs a **Clean Architecture** design to separate UI from business logic:
*   **`AniDesk.Core`**: Contains all business logic, API models, network services, caching algorithms, and Windows Interop (P/Invoke) logic. This library is fully decoupled and has absolutely zero WPF dependencies.
*   **`AniDesk.App`**: The presentation layer built on the MVVM (Model-View-ViewModel) pattern using `CommunityToolkit.Mvvm`.

---

## 📄 License

This project is open-source and distributed under the **MIT License**. Feel free to use, modify, and distribute it as you see fit.
