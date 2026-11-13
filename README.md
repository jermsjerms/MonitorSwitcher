# Monitor Switcher

[![Made with Claude](https://img.shields.io/badge/Made%20with-Claude-D97757?style=flat-square&logo=claude&logoColor=white)](https://claude.ai)
![Platform: Windows 10 | 11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=flat-square)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Portable: no install](https://img.shields.io/badge/Portable-no%20install-2ea44f?style=flat-square)

<!--
Optional live badges. These only display once the repository is PUBLIC.
Check that jermsjerms/MonitorSwitcher matches your GitHub username/repository name, then delete the
first and last lines of this block (the comment markers) to turn them on:

[![Latest release](https://img.shields.io/github/v/release/jermsjerms/MonitorSwitcher?style=flat-square)](../../releases)
[![Build](https://img.shields.io/github/actions/workflow/status/jermsjerms/MonitorSwitcher/build.yml?style=flat-square&label=build)](../../actions)
[![Downloads](https://img.shields.io/github/downloads/jermsjerms/MonitorSwitcher/total?style=flat-square)](../../releases)
-->

> **AI transparency:** this app was built with the help of [Claude](https://claude.ai), an AI assistant made by Anthropic. The repository owner has tested it on their own PC, but it has not been through a formal security review. You are welcome to read the source in the `src` folder before running it.

A small, portable Windows tool that temporarily changes your **primary monitor**, so apps, games and videos open on the screen you want. Use it to play on a living room TV, present on a projector, or work on a different display, much like Steam Big Picture mode does. When you close the app, your **original display layout is restored**.

> **Status:** early release (v1.1). Please report problems in the Issues tab and include your log file (see [Troubleshooting](#troubleshooting)).

## Download

1. Go to the [**Releases**](../../releases) page and download `MonitorSwitcher.exe`.
2. Put it anywhere (a folder, a USB drive) and double-click it. No installer and no .NET needed.

Windows may show a "Windows protected your PC" warning because the app isn't code-signed. Click **More info → Run anyway**.

## How to use

1. Open `MonitorSwitcher.exe`. Your monitors appear in a list by brand and model (for example, `Dell U2723QE`). The window sizes itself so every column is visible.
2. Select the monitor you want as primary and click **Switch to selected monitor**.
   - Click **Set selected as default** to remember a monitor for quick or automatic use (marked with a star).
3. Confirm the **Keep settings?** prompt. If you do nothing, it reverts after 15 seconds so you can never get stuck on a blank screen.
4. Launch what you want to use (a game, a media player, a presentation). It opens on the new primary monitor.
5. When you're done (after the game, movie or meeting), **close the app** (or use the tray icon → Exit). Your previous layout comes back.

### Options

| Option | What it does |
|---|---|
| Turn off other monitors while active | Detaches the other displays until the app closes, handy when you only want the TV on |
| Apply default monitor automatically when app opens | Switches to your default monitor on launch |
| Ask me to confirm changes | Shows the 15-second Keep/Revert prompt |

### Hotkey

**Ctrl + Alt + M** restores your previous setup at any time, even when the window is hidden in the system tray. If another app already uses that shortcut, the app switches to **Ctrl + Alt + Shift + M** and shows which one is active.

### Command-line options

| Command | What it does |
|---|---|
| `MonitorSwitcher.exe --auto` | Opens and immediately switches to your default monitor |
| `MonitorSwitcher.exe --restore` | Restores your saved layout and exits (a good "panic button") |

Tip: make a shortcut to `MonitorSwitcher.exe --auto`, for example one-click "TV mode", then launch your game or media app afterward.

## Files it creates

Next to the exe (or in `%AppData%\MonitorSwitcher` if that folder isn't writable):

- `config.json`: your settings, default monitor, and the saved original layout
- `logs/`: one log file per day

If the app crashes or your PC loses power while switched, it offers to restore your original layout the next time it starts.

## Troubleshooting

- **Stuck on the wrong layout?** Run `MonitorSwitcher.exe --restore`. Or press **Win + P** and choose **Extend**, then set your main display in Windows Settings → System → Display.
- **Something failed?** Open the `logs` folder next to the exe and attach the newest `.log` file to a new Issue.
- **An app or game still opens on the wrong screen?** Some programs remember their last monitor or have their own display setting. Change it once in the program's own settings.

## Known limitations

- Windows 10 and 11 only.
- Monitor names come from the monitor itself. Some TVs and older displays report only a generic name, in which case the hardware code is shown instead (for example, `Generic PnP Monitor [DELA0B1]`).
- Identical monitors can share the same hardware ID. The app tells them apart with `#2`, `#3`, but their order could change if you move cables.
- If you switch from one monitor to another without restoring in between, closing the app returns you to the very first layout.

## Build from source

You only need this if you want to build it yourself. Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or `winget install Microsoft.DotNet.SDK.8`).

1. Download or clone this repository.
2. Double-click `Build.bat`.
3. `MonitorSwitcher.exe` appears in the same folder.

GitHub also builds the exe automatically: see the **Actions** tab, or tag a version (for example `v1.0.0`) to publish a Release.

## How it works

The app uses the Windows display API (`ChangeDisplaySettingsEx`) to make one monitor primary, repositioning the others around it. Before changing anything, it saves your full layout (position, resolution, refresh rate, rotation, and which monitor is primary) to `config.json`, and puts it back when you close the app, press the hotkey, or run `--restore`.
