# Neko Game Launcher

A Windows-first universal PC game launcher that combines installed libraries, launcher detection, game launching, free-game offers, deal discovery, and online game lookup in one app.

This is an original launcher architecture inspired by the *category* of universal launchers such as Playnite; it does not copy Playnite source code or branding.

## Current features

- Detect and import **Steam** games from Steam library manifests.
- Detect and import **Epic Games Store** games from Epic launcher manifests.
- Detect games registered by **EA / Origin**, **Ubisoft Connect**, **GOG Galaxy**, **Battle.net**, **Rockstar Games**, and **Riot Games** through Windows uninstall metadata.
- Detect common **Xbox / Microsoft Store** installs stored under `XboxGames` folders.
- Detect whether supported launcher clients are installed and show game counts per launcher.
- Search the local library by game or launcher name.
- Launch Steam/Epic games using launcher URIs when available and executable-backed games directly.
- Cache the scanned library in `%LOCALAPPDATA%\NekoGameLauncher\library.json`.
- Show active free-game offers through **GamerPower**.
- Show PC game discounts and search games/prices through **CheapShark**.
- Add arbitrary custom deal endpoints from Settings without rebuilding the app.
- Dark Windows desktop UI with Library, Deals & Free Games, Launchers, and Settings sections.
- GitHub Actions builds for **Windows x64** and **Windows ARM64**.

## Deals / free games

Two keyless public data providers are enabled by default:

- [GamerPower API](https://www.gamerpower.com/api-read) for active free games and giveaways. GamerPower requests attribution, which the app displays in its footer.
- [CheapShark API](https://apidocs.cheapshark.com/) for PC game deals, price lookup, and deal redirects.

You can disable either provider in Settings and add your own endpoints. See [docs/deals-endpoint.md](docs/deals-endpoint.md) for the generic JSON contract.

## Build on Windows

Requirements:

- Windows 10 19041+ or Windows 11
- .NET 8 SDK
- Visual Studio 2022 is optional

```powershell
git clone https://github.com/NekoSuneVR/NekoGameLauncher.git
cd NekoGameLauncher
dotnet restore
dotnet build NekoGameLauncher.sln -c Release
dotnet run --project src/NekoGameLauncher/NekoGameLauncher.csproj
```

Publish:

```powershell
dotnet publish src/NekoGameLauncher/NekoGameLauncher.csproj -c Release -r win-x64 --self-contained false -o publish/win-x64
```

## Microsoft Store / MSIX direction

The desktop app is structured as a normal Windows app and targets Windows 10 19041+. A Microsoft Store submission can be added on top using MSIX packaging. Store identity, publisher ID, signing, icons, and Partner Center metadata should stay outside the core launcher logic so local development does not require Store credentials.

## Architecture

```text
src/NekoGameLauncher/
├─ Models/                 game, deal, launcher and settings models
├─ Services/               launcher scanners, deals, persistence and launching
├─ ViewModels/             WPF view model / commands
├─ Infrastructure/         command helpers
├─ MainWindow.xaml         launcher UI
└─ App.xaml
```

`IGameLibraryProvider` is the extension point for more launchers. Additional providers can be added for itch.io, Amazon Games, standalone emulators, custom folders, console Remote Play shortcuts, or other stores without rewriting the library UI.

## Privacy

Launcher detection is local. The app reads local registry entries and known launcher manifest/install folders. It does **not** read launcher passwords, session tokens, browser cookies, or account credentials. Network requests are only used for the Deals / Free Games and online lookup features.

## Roadmap

- Better Microsoft Store/Xbox package identity detection and launch activation.
- itch.io and Amazon Games importers.
- Cover-art / metadata enrichment and local artwork cache.
- Favorites, categories, playtime, recently played and custom games.
- Controller-friendly fullscreen / TV mode.
- Import custom executable folders and emulators.
- Optional notifications when a new free game appears.
- MSIX packaging and Microsoft Store publishing workflow.
- Cloud sync as an opt-in feature.

## License

No license has been selected yet. Add the project's preferred license before accepting external contributions.
