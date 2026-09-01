# Neko Game Launcher

A Windows-first universal PC game launcher that combines installed libraries, game launching, playtime/session tracking, live PC performance monitoring, reversible Gaming Mode, free-game offers and deal discovery in one app.

This is an original launcher architecture inspired by the *category* of universal launchers such as Playnite; it does not copy Playnite source code or branding.

## Current features

### Game and launcher discovery

- **Steam** library manifest scanning across Steam library folders.
- **Epic Games Store** `.item`, `LauncherInstalled.dat`, and `.egstore` fallback discovery.
- **EA / Origin**, **Ubisoft Connect**, **GOG Galaxy**, **Battle.net**, **Rockstar Games**, and **Riot Games** through Windows install metadata.
- **Xbox / Microsoft Store** `XboxGames` folder discovery.
- **Wargaming.net** support for World of Tanks, World of Warships and World of Warplanes.
- **HoYoPlay** support for Zenless Zone Zero, Genshin Impact, Honkai: Star Rail and Honkai Impact 3rd.
- **Kuro Games** support for Wuthering Waves and Punishing: Gray Raven.
- **Neverness to Everness (NTE)** / Perfect World discovery.
- Additional known standalone/install discovery for games such as Infinity Nikki, Tower of Fantasy, Snowbreak, Once Human, Star Citizen, Escape from Tarkov, Warframe, FINAL FANTASY XIV, Guild Wars 2, Path of Exile and osu!.
- Dynamic launcher grouping: newly detected standalone publishers can appear in the Launchers page even without a hard-coded launcher client entry.

Discovery is deliberately layered. It uses launcher metadata where possible, Windows uninstall metadata for known titles, and common install-folder fallbacks for titles that do not expose a clean launcher API.

### Library and launching

- Search local games by game or launcher/publisher name.
- Launch Steam/Epic games through launcher URIs when available.
- Launch executable-backed games directly.
- Local library cache in `%LOCALAPPDATA%\NekoGameLauncher\library.json`.
- Modern dark gaming UI with a custom dark title bar, left navigation, gaming cards and responsive views.

## Gaming Hub

Neko Game Launcher now includes a local Gaming Hub rather than only acting as a shortcut list. See [docs/gaming-hub.md](docs/gaming-hub.md) for the design and safety notes.

### Playtime and sessions

- Tracks total playtime per game.
- Tracks session count and current session duration.
- Updates even if a detected game is launched from Steam, Epic, Wargaming, HoYoPlay or another original launcher instead of through Neko.
- Detects a running game by matching running executables to the detected game install directory.
- Stores playtime locally in `%LOCALAPPDATA%\NekoGameLauncher\playtime.json`.
- Shows a Most Played section in Gaming Hub.

### Live performance

- Live whole-system CPU usage.
- Live system RAM usage.
- Active detected game name.
- Per-game CPU usage while running.
- Per-game working-set RAM usage while running.
- Current game-session timer.

### Gaming Mode

Gaming Mode is intentionally reversible and conservative.

When enabled it can:

1. Remember the currently active Windows power plan.
2. Switch Windows to the High Performance power scheme.
3. Raise detected game processes from Normal/Below Normal to **Above Normal** priority.
4. Restore the original process priority and previous Windows power plan when Gaming Mode is disabled or Neko Game Launcher closes.

It **does not blindly terminate background processes**. That avoids breaking launchers, Discord, OBS, anti-cheat, audio software, VR runtimes or other apps a game may need.

Gaming Mode can be enabled manually or automatically whenever Neko detects that a game is running.

### Windows gaming quick tools

Gaming Hub can directly open:

- Windows Game Mode settings.
- Windows Graphics settings.
- Task Manager.

## Deals / free games

Two keyless public data providers are enabled by default:

- [GamerPower API](https://www.gamerpower.com/api-read) for active free games and giveaways.
- [CheapShark API](https://apidocs.cheapshark.com/) for PC game deals, price lookup and deal redirects.

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

GitHub Actions builds both **win-x64** and **win-arm64** artifacts.

## Microsoft Store / MSIX direction

The desktop app targets Windows 10 19041+. Microsoft Store submission can be added with MSIX packaging. Store identity, publisher ID, signing, icons and Partner Center metadata should remain separate from the core launcher so local development does not require Store credentials.

## Architecture

```text
src/NekoGameLauncher/
├─ Models/                 game, playtime, deal, launcher and settings models
├─ Services/               scanners, activity tracking, performance, Gaming Mode,
│                          deals, persistence and launching
├─ ViewModels/             WPF state and commands
├─ Infrastructure/         command helpers
├─ MainWindow.xaml         modern launcher + Gaming Hub UI
└─ App.xaml                dark gaming theme/resources
```

`IGameLibraryProvider` remains the extension point for additional launchers and game ecosystems.

## Privacy

Launcher/game discovery and playtime monitoring are local. The app reads local registry entries, known manifests/install folders and running process executable paths. It does **not** read launcher passwords, session tokens, browser cookies or account credentials.

Network requests are used only by Deals / Free Games and online price lookup features.

## Useful next upgrades

Good next steps for the Gaming Hub include:

- GPU utilisation / VRAM / temperature telemetry using vendor-neutral Windows GPU counters where available.
- FPS and frametime integration using an optional PresentMon-compatible helper.
- Per-game Gaming Mode profiles: power mode, priority, monitor, audio device and optional launch arguments.
- Temperature and thermal-throttling warnings.
- Disk free-space and game-drive health warnings.
- Network latency / packet-loss monitor for online games.
- Recently Played, favourites, categories and custom collections.
- Cover-art / metadata enrichment with a local artwork cache.
- Controller-friendly fullscreen / TV mode.
- Custom executable/folder import and emulator support.
- itch.io and Amazon Games importers.
- Optional free-game notifications.
- MSIX packaging and Microsoft Store publishing workflow.
- Optional cloud sync.

## License

No license has been selected yet. Add the project's preferred license before accepting external contributions.
