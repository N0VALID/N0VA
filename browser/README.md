# N0VA Browser

A native Windows x64 browser preview with a quiet, configurable interface. Built with C# WinForms and Microsoft's shared Evergreen WebView2 runtime. This is a separate app; the existing N0VA hub and Seemless OS browser continue to work.

## Install

Open [N0VA releases](https://github.com/N0VALID/N0VA/releases), find **N0VA Browser 0.1.0 preview**, and download `N0VA-Browser-Setup-0.1.0-x64.exe`. Run the setup wizard. The default desktop shortcut opens the installed `N0VA Browser.exe`; Start-menu and uninstall entries are also created. You can choose the install location and desktop shortcut in the wizard.

Supports supported Windows 11 x64 installations (including compatible x64 emulation). .NET 10 is bundled. If WebView2 is missing, setup invokes Microsoft's signed bootstrapper; that step needs an internet connection. No Python or PowerShell launch wrapper is needed. The app and installer are currently unsigned; SHA256SUMS.txt is supplied alongside the matching release. Do not disable Windows security features to bypass a warning.

## Features

- Real tabbed browsing, DuckDuckGo search, back/forward/reload, middle-click close, and keyboard shortcuts.
- Official **uBlock Origin Lite**, bundled with its upstream default network filter rules: uBlock filters, EasyList, EasyPrivacy, badware and malware lists. Blocking must initialize successfully before a website loads. This is **not full uBlock Origin** and does not promise equivalent cosmetic filtering or extension controls.
- WebView2 **Strict tracking prevention**, HTTPS navigation by default, certificate-error rejection, external-protocol blocking, host-object/web-message bridges disabled, password saving/autofill disabled, site permission requests denied, and SmartScreen reputation checking required.
- **Downloads** at the top lists direct file links and `download` links in the current document. It shows their source domain, deduplicates them and never prefetches files. Refresh after the site adds links. Choose a link to navigate to it; normal attachment responses invoke the save dialog, while viewable formats such as PDF can open a preview. Login-gated, iframe, blob, script-generated and API-only downloads are not exhaustively discoverable. Downloads started on the site are still managed by the browser when supported.
- Every download asks where to save. Active-content extensions get an additional warning, transfers show progress/cancel controls, and completed files can be revealed in Explorer. Files are never executed automatically. NTFS downloads receive an Internet-zone marker. No claim is made that a downloaded file is safe.
- Saved **Graphite / Paper** themes, **Sage / Clay / Blue** accents and an optional local background. Images are re-encoded as JPEG; no stock background requests or remote font downloads.
- Native new tabs create no web renderer until needed. Inactive, silent tabs are suspended after 1, 5 or 15 minutes when no downloads are running. This reduces activity, but does not unload every renderer or guarantee a particular RAM budget. Chromium websites still consume substantial memory.
- Temporary browsing profile by default. Settings and downloaded files persist. Saved browsing data is an explicit setting taking effect after restart. Old locked temporary data is retried at the next launch, not securely erased.

| Shortcut | Action |
| --- | --- |
| Ctrl+L | Focus address |
| Ctrl+T / Ctrl+W | New / close tab |
| Ctrl+Tab | Next tab |
| Ctrl+R / Escape | Reload / stop |
| Ctrl+J | Page files and transfers |

Settings live in `%LOCALAPPDATA%\N0VA\Browser`. Uninstall keeps this data and your downloads. Delete only that browser folder while the app is closed to reset it. Switching to temporary mode does **not** erase an older saved Profile folder.

## Security status

An early, unaudited browser shell, **not "the most secure browser"** or a fork of Brave/LibreWolf. See [SECURITY.md](SECURITY.md) for threat boundaries and limitations. WebView2 updates through Microsoft; **N0VA and the pinned blocker/filter package require a new app release**. There is no automatic app updater or filter downloader. Keep both updated. No N0VA analytics/check-ins are included; WebView2/Microsoft services (including SmartScreen) have their own network behavior and policies.

## Build and verify

On Windows, install .NET 10 SDK and Inno Setup 6, then run from the repository root:

```powershell
./browser/scripts/prepare.ps1
dotnet run --project browser/tests/PolicyTests.csproj -c Release
dotnet publish browser/src/NovaBrowser.csproj -c Release -r win-x64 --self-contained true -p:RestoreLockedMode=true -o browser/out
& './browser/out/N0VA Browser.exe' --smoke-test
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" browser/installer/Nova.iss
```

`prepare.ps1` verifies the exact official uBOL archive by SHA-256 and the Microsoft bootstrapper by Authenticode signature. The blocker is verified again on every app start before extraction. NuGet dependencies are locked. Installer output is under `browser/artifacts/`.

The [Windows build workflow](https://github.com/N0VALID/N0VA/actions/workflows/browser.yml) runs policy/persistence checks, a native WebView2 integration test, compiles the setup wizard, and verifies installation plus the desktop shortcut before publishing a preview. Its evidence artifact contains the native window screenshot and smoke result. Runtime GUI checks must run on Windows; Linux can compile the Windows target and run the policy tests. CI does not certify anti-fingerprinting, filter coverage, malicious-download detection, or every website.

To update the blocker, review a new official uBOL release, change the version and SHA-256 in `Blocker.cs` and `scripts/prepare.ps1`, then rerun all checks and publish a new installer. Never remove the digest check to accommodate a download mismatch.
