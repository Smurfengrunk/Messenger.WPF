# Messenger for Windows

An unofficial Facebook Messenger desktop client for Windows, built with C# and WPF. Wraps the Messenger web interface in a native window with taskbar badge support, tray icon, and persistent sessions — everything the official app used to provide before Meta discontinued it.

![Windows 11](https://img.shields.io/badge/Windows-11-0078D4?logo=windows)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)
![Version](https://img.shields.io/badge/Version-2.0.2-blue)

---

## Features

- Unread message badge on both taskbar button and tray icon
- Minimize to tray on close (optional)
- Persistent login session across restarts
- Autostart at Windows login (optional)
- Single-instance enforcement
- Clean Messenger UI — Facebook navigation chrome removed via CSS injection
- DOM-based unread count detection (resilient to Meta deploys)

---

## Requirements

- Windows 10 or later (x64)
- No additional runtime required — the installer is self-contained

---

## Installation

If you have .NET8 or newer installed download `Messenger_Setup_2.0.2.exe` from [Releases](../../releases), otherwise download the `Messenger_Setup_2.0.2 with .NET8.exe` and run it.

**First launch note:** After logging in and entering your E2E encryption PIN, the app may appear to stall on verification. Should it stall please close the app (from the tray menu) and relaunch — the second launch will prompt for the PIN once more, and from then on you go straight to your chats. This is a one-time sequence required for Facebook to establish the encrypted session on a new device.

---

## Building from source

### Prerequisites

- Visual Studio 2022 or later
- .NET 10 SDK
- Windows 11 SDK (10.0.22621 or later)

### Steps

```powershell
git clone https://github.com/yourusername/Messenger.WPF.git
cd Messenger.WPF
dotnet build Messenger/Messenger.csproj -c Debug
```

To produce a self-contained release build, run the publish script from the solution root:

```powershell
.\publish.ps1
```

Output lands in `Messenger/publish/win-x64` and is ready for Inno Setup packaging.

---

## How it works

### WebView2 wrapper

The app hosts Facebook Messenger (`facebook.com/messages`) in a `WebView2` control embedded via `WindowsFormsHost`. A persistent user data folder is stored at `%LOCALAPPDATA%\MessengerWrapper\WebView2`, so login sessions and cached data survive restarts.

### Unread count detection

JavaScript is injected at document creation and polls the DOM every 3 seconds, scanning for language-specific accessibility strings that Facebook injects for screen readers:

```javascript
const UNREAD_MARKERS = [
    'Oläst meddelande:',    // Swedish
    'Unread message:',       // English
    'Ungelesene Nachricht:', // German
    'Message non lu:',       // French
    'Bericht niet gelezen:', // Dutch
    'Mensaje no leído:',     // Spanish
    'Messaggio non letto:',  // Italian
];

const unread = Array.from(document.querySelectorAll('[role="row"]'))
    .filter(el => UNREAD_MARKERS.some(m => el.textContent?.includes(m)))
    .length;
```

This approach targets semantic DOM structure rather than CSS classes, making it resilient to Meta's frequent frontend deploys. Contributors are welcome to add markers for additional languages — see `JS_UNREAD_SCRIPT` in `MainWindow.xaml.cs`.

### Taskbar badge and tray icon

`ITaskbarList3.SetOverlayIcon` via COM Interop updates the taskbar button overlay. The tray icon is dynamically redrawn using GDI+ to show the unread count. Both are implemented in pure C# without any native C++ module — a non-trivial constraint given Windows shell API limitations.

---

## The road to version 2

This is the second attempt at a Messenger desktop wrapper. The path here was longer than expected:

**Electron/Node.js** — first attempt. Abandoned when taskbar badge support proved impossible to implement reliably without native modules that conflicted with Electron's sandbox.

**WinUI 3** — second attempt, recommended by GitHub Copilot. Hit a hard wall: WinUI 3's MSIX sandbox isolation blocks the COM Interop access required for `ITaskbarList3`. No workaround exists within the WinUI 3 model.

**WPF (this version)** — the right tool for the job. Direct Win32/COM access, mature Windows shell integration, and a straightforward WebView2 embedding story. The badge implementation without a C++ helper module required solving several non-obvious assembly reference conflicts between WebView2's `netcoreapp3.0`, `net5.0`, and `net8.0` so the build targets on .NET 10.

---

## Contributing

Pull requests are welcome. For significant changes, open an issue first to discuss what you'd like to change.

---

## License

[MIT](LICENSE)

---

## Disclaimer

This project is not affiliated with, endorsed by, or connected to Meta Platforms, Inc. Facebook and Messenger are trademarks of Meta Platforms, Inc. This app accesses the Messenger web interface in the same way a browser would.
