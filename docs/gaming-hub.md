# Gaming Hub

The Gaming Hub turns Neko Game Launcher from a library aggregator into a lightweight local game-session and PC-performance dashboard.

## Current monitoring

The app polls every five seconds while open and correlates running executable paths against the install directories of detected games. This means playtime can be counted even when the game was started outside Neko Game Launcher.

Tracked locally:

- Total playtime per game.
- Session count.
- Current session duration.
- Last played time.
- Per-game CPU utilisation while running.
- Per-game working-set memory while running.
- Whole-system CPU utilisation.
- Whole-system physical-memory utilisation.

Playtime is persisted to `%LOCALAPPDATA%\NekoGameLauncher\playtime.json`.

## Gaming Mode safety model

Gaming Mode is designed to be reversible rather than acting like an aggressive "optimizer".

When enabled:

1. Neko reads and remembers the current Windows power scheme.
2. It asks Windows to use the High Performance scheme.
3. Active detected game processes can be raised to Above Normal priority.
4. Original process priorities are remembered.

When disabled or the application closes, Neko restores the saved process priorities and the previous Windows power scheme.

Neko does not automatically kill services or arbitrary background programs. Doing that can break anti-cheat, game launchers, OBS, Discord, VR runtimes, audio software and accessibility tools.

## Discovery families

The initial expanded discovery set includes:

- Wargaming.net: World of Tanks, World of Warships, World of Warplanes.
- HoYoPlay: Zenless Zone Zero, Genshin Impact, Honkai: Star Rail, Honkai Impact 3rd.
- Kuro Games: Wuthering Waves, Punishing: Gray Raven.
- Perfect World / NTE: Neverness to Everness and Tower of Fantasy.
- Other known installs: Infinity Nikki, Snowbreak, Once Human, Star Citizen, Escape from Tarkov, Warframe, FINAL FANTASY XIV, Guild Wars 2, Path of Exile and osu!.

Detection uses Windows uninstall metadata first where possible and then known common install folders as a fallback. The architecture is intentionally data-driven so more titles can be added without changing the main UI.

## Good next telemetry upgrades

The next performance features should remain optional and lightweight:

- Windows GPU Engine utilisation and dedicated/shared GPU memory.
- NVIDIA/AMD/Intel temperature data when a safe local source is available.
- PresentMon-compatible FPS and frametime capture as an optional helper rather than bundling a heavy overlay into the launcher.
- Thermal throttling alerts.
- Game-drive free-space and health warnings.
- Per-game network latency and packet-loss checks.
- Session history graphs and daily/weekly playtime summaries.

## Per-game profiles direction

A future profile can store optional settings such as:

- Whether Gaming Mode should activate.
- Preferred process priority.
- Custom launch arguments.
- Preferred monitor/display mode.
- Preferred audio output.
- Optional pre-launch and post-exit scripts, disabled by default and explicitly configured by the user.

Profiles should always restore changed system state after the game exits.
