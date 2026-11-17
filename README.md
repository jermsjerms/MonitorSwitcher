# Monitor Switcher

[![Made with Claude](https://img.shields.io/badge/Made%20with-Claude-D97757?style=flat-square&logo=claude&logoColor=white)](https://claude.ai)
![Platform: Windows 10 | 11](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D4?style=flat-square)
![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Portable: no install](https://img.shields.io/badge/Portable-no%20install-2ea44f?style=flat-square)

> **AI transparency:** this app was built with the help of [Claude](https://claude.ai), an AI assistant made by Anthropic. The repository owner has tested it on their own PC, but it has not been through a formal security review.

A small, portable Windows tool that temporarily changes your **primary monitor**, so apps, games and videos open on the screen you want. Use it to play on a living room TV, present on a projector, or work on a different display, much like Steam Big Picture mode does. When you close the app, your **original display layout is restored**.

> **Status:** v1.2. Please report problems in the Issues tab and include your log file (see [Troubleshooting](#troubleshooting)).

## Download

1. Go to the [**Releases**](../../releases) page and download `MonitorSwitcher.exe`.
2. Put it anywhere (a folder, a USB drive) and double-click it. No installer and no .NET needed.

Windows may show a "Windows protected your PC" warning because the app isn't code-signed. Click **More info → Run anyway**.

## How to use

1. Open `MonitorSwitcher.exe`. Your monitors appear in a list by brand and model (for example, `Dell U2723QE`). Turned-off monitors stay in the list, greyed out.
2. Select the monitor you want as primary and click **Switch to selected monitor**.
   - Click **Set selected as default** to remember a monitor for quick or automatic use (marked with a star).
   - To turn a single monitor off or on, select it and click **Turn selected monitor off/on**.
3. Confirm the **Keep settings?** prompt. If you do nothing, it reverts after 15 seconds so you can never get stuck on a blank screen.
4. Launch what you want to use (a game, a media player, a presentation). It opens on the new primary monitor.
5. When you're done, **close the app** (or use the tray icon → Exit). Your previous layout comes back.

### Options

| Option | What it does |
|---|---|
| Turn off other monitors while active | Detaches the other displays until the app closes |
| Apply default monitor automatically when app opens | Switches to your default monitor on launch |
| Ask me to confirm changes | Shows the 15-second Keep/Revert prompt |

### Toggle Monitor On/Off

- **Turn a monitor off:** Select a powered-on monitor and click "Turn selected monitor off"
- **Turn a monitor on:** Select a turned-off (greyed-out) monitor and click "Turn selected monitor on"
- Turned-off monitors stay visible in the list with their name and resolution

### Hotkey

**Ctrl + Alt + M** restores your previous setup at any time. If another app uses that shortcut, try **Ctrl + Alt + Shift + M**.

## Files it creates

- `config.json`: your settings, default monitor, and saved original layout
- `logs/`: one log file per day

## Troubleshooting

- **Stuck on the wrong layout?** Run `MonitorSwitcher.exe --restore` or press **Win + P** → Extend.
- **An app still opens on the wrong screen?** Some programs remember their last monitor setting.

## Known limitations

- Windows 10 and 11 only.
- You can't turn off your primary or last active monitor. Make another monitor primary first.
- Turning a monitor on may briefly flicker other monitors as Windows reconfigures.

## Build from source

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

1. Clone this repository
2. Double-click `Build.bat`
3. `MonitorSwitcher.exe` appears in the same folder

## How it works

The app uses Windows display APIs to change which monitor is primary and turn monitors on/off, saving your layout to restore when you close the app.

## License

MIT License