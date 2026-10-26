# Codex Token Widget

[简体中文](README.md) | [English](README.en.md)

A lightweight Windows desktop widget for Codex usage. Track 5-hour and weekly limits, today's tokens, and a 7-day usage chart without opening a browser.

Built with C# / WPF / .NET 8 and distributed as a standalone Windows x64 executable. This project is community-maintained and is not affiliated with OpenAI.

## Preview

![Light and dark themes with illustrative usage data](docs/images/widget-themes-showcase.png)

![Settings and edge indicator concept preview](docs/images/settings-edge-showcase.png)

These images were recreated and enhanced with AI from actual screenshots. Usage totals, durations, and streaks are demonstration data, not the author's actual usage. Typography, spacing, and some controls have been polished for presentation. Refer to the application for its actual appearance. The preview images show the Chinese interface; the application also supports English.

## Features

- 5-hour and weekly usage limits with reset times.
- Today's token count and a chart for the last 7 days.
- Lifetime tokens, peak daily tokens, longest turn, and usage streaks, when provided by the data source.
- Floating desktop widget, edge docking and collapse, and a system tray menu.
- Configurable automatic refresh, light / dark / system themes, and individual card visibility.
- Chinese, English, and automatic system language selection, including localized numbers and durations.

## Language settings

Open **Settings → Language → Display language** and choose:

| Option | Behavior |
| --- | --- |
| Follow system (default) | Uses the Windows user interface language. English systems use English; Chinese and other languages fall back to Simplified Chinese. |
| 中文 | Always uses Simplified Chinese. |
| English | Always uses English. |

Click **Save** to apply immediately, without restarting. The preference is stored locally. Existing settings without a language field default to **Follow system**. Language and theme preferences are independent; the theme can also follow Windows.

Chinese numbers use 万 / 亿; English numbers use K / M / B. Switching languages changes formatting without changing the underlying usage data.

## Requirements and installation

- Windows 10 / 11, x64.
- Codex installed and signed in. The widget searches for `codex.exe` under `%LOCALAPPDATA%\OpenAI\Codex\bin`, then falls back to `codex` on PATH.
- A Codex app-server version supporting `account/rateLimits/read` and `account/usage/read`. Availability depends on the installed version.

Download the archive from [Releases](https://github.com/GMMG55/codex-token-widget/releases), extract it to a permanent folder, and run `CodexTokenWidget.exe`. The self-contained release does not require a separate .NET installation.

Drag the widget to move it, click refresh to reload usage, and open settings to customize the display.

## Data sources and local files

Limits and historical statistics come from a local `codex app-server --stdio` process. Today's count prioritizes `token_usage_record` entries in `%USERPROFILE%\.codex\sessions`, grouped by local date and deduplicated by response ID. This local estimate can differ from backend statistics, which may be delayed.

If the app-server query fails, the widget may retain cached limits or display only the local daily estimate. Unavailable history is marked as pending rather than treated as confirmed zero usage.

The widget does not scrape websites or browser data and contains no independent statistics upload code. Codex app-server uses your existing sign-in and may contact its backend. Settings and error logs are stored in `%LOCALAPPDATA%\CodexTokenWidget`. Remove personal information before sharing logs.

At startup, the application attempts to configure a per-user Windows startup entry according to its settings and creates a desktop shortcut. You can manage the entry in **Windows Settings → Apps → Startup**. **Show when Codex starts** hides the window while no matching Codex process is detected and checks periodically before showing it. Process matching is broad, so behavior depends on the processes running on your machine.

## Build from source

Install the .NET 8 SDK on Windows, then run:

```powershell
git clone https://github.com/GMMG55/codex-token-widget.git
cd codex-token-widget
dotnet build -c Release
dotnet run --project tests/LocalizationChecks -c Release
```

Publish a standalone executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
```

Output: `publish\CodexTokenWidget.exe`. Build outputs, generated protocol files, and local data are excluded from the source repository.

## Contributing and license

Issues and pull requests are welcome. Include your Windows version, Codex version, and reproduction steps. Do not upload credentials or complete session logs.

Licensed under the [MIT License](LICENSE).
