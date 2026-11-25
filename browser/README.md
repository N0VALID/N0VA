# N0VA Browser

A native Windows x64 browser preview with a quiet, configurable interface. Built with C# WinForms and Microsoft's shared Evergreen WebView2 runtime. This is a separate app; the existing N0VA hub and Seemless OS browser continue to work.

## Install

Open [N0VA releases](https://github.com/N0VALID/N0VA/releases), find **N0VA Browser 0.3.0 preview**, and download `N0VA-Browser-Setup-0.3.0-x64.exe`. Run the setup wizard. The default desktop shortcut opens the installed `N0VA Browser.exe`; Start-menu and uninstall entries are also created. You can choose the install location and desktop shortcut in the wizard.

Supports supported Windows 11 x64 installations (including compatible x64 emulation). .NET 10 is bundled. If WebView2 is missing, setup invokes Microsoft's signed bootstrapper; that step needs an internet connection. No Python or PowerShell launch wrapper is needed. The app and installer are currently unsigned; SHA256SUMS.txt is supplied alongside the matching release. Do not disable Windows security features to bypass a warning.

## Features

- Centered rounded address bar, compact right-side tools, familiar back/forward/reload/home controls, website tabs with a + button, middle-click close, and keyboard shortcuts. Site files and Downloads open over the page without resizing it. Favorites, Anti-redirect and Connection have dropdown menus. Settings → Toolbar hides tools and changes their order; new installs show all tools (Site files appears while browsing). Hidden tools remain accessible through the main menu.
- Official **uBlock Origin Lite**, bundled with its upstream default network filter rules: uBlock filters, EasyList, EasyPrivacy, badware and malware lists. When enabled, blocking must initialize successfully before a website loads. Privacy settings can switch uBOL on or off immediately; strict tracking prevention remains on. This is **not full uBlock Origin** and does not promise equivalent cosmetic filtering or extension controls.
- WebView2 **Strict tracking prevention**, HTTPS navigation by default, certificate-error rejection, external-protocol blocking, host-object/web-message bridges disabled, password saving/autofill disabled, site permission requests denied, and SmartScreen reputation checking required.
- **Site files** appears only on websites and lists direct files, download-page links, and GitHub release links in the current document. It shows their source domain, deduplicates them and never prefetches files. Refresh after the site adds links. **Load release files** resolves public GitHub release assets, including the release linked by RustDesk’s homepage. This explicit action contacts the GitHub API through the current system/proxy route, without cookies or credentials; it is subject to rate limits. Windows EXE/MSI assets sort first. Choose a link to navigate to it; normal attachment responses invoke the save dialog, while viewable formats such as PDF can open a preview. Login-gated, iframe, blob, script-generated and API-only downloads are not exhaustively discoverable. Downloads started on the site are still managed by the browser when supported.
- Every download asks where to save. Active-content extensions get an additional warning, the separate **Downloads** panel shows progress, pause/resume/cancel controls and saved download history. Files can be revealed in Explorer. Clearing history keeps the files on disk. Files are never executed automatically. NTFS downloads receive an Internet-zone marker. No claim is made that a downloaded file is safe.
- Saved **Graphite / Midnight / Paper** themes, five accents, background Fill/Fit, home favorites, an optional clock, search-engine selection and a skippable eased wordmark reveal. Motion can be disabled. Small Credits and Personalize buttons sit at the corners of the home footer. Images are re-encoded as JPEG; no stock background requests or remote font downloads.
- N0VA home is a native landing view outside the website tab list and creates no web renderer until needed. Inactive, silent tabs are suspended after 1, 5 or 15 minutes when no downloads are running. This reduces activity, but does not unload every renderer or guarantee a particular RAM budget. Chromium websites still consume substantial memory.
- **Favorites:** use the star or Ctrl+D to save/remove the current website; manage favorites from the toolbar. Favorites and local download history survive temporary sessions. History retains filenames, local paths, source origins and status, not signed URL query parameters.
- **Home stats:** observed `ERR_BLOCKED_BY_CLIENT` network events, deduplicated by request ID, are counted for this session. The combined “Ads & trackers” display is not a count of unique ads, and WebView2 does not give a reliable separate attribution to ads versus trackers. No fabricated totals, saved bandwidth estimates or time-saved estimates are displayed. Some filtering does not produce these events and will not appear in this counter.
- **Credits** opens a native view headed “Made by N0VALID” with clickable project and guns.lol links.
- Temporary browsing profile by default. Settings and downloaded files persist. Saved browsing data is an explicit setting taking effect after restart. Old locked temporary data is retried at the next launch, not securely erased.

| Shortcut | Action |
| --- | --- |
| Ctrl+L | Focus address |
| Ctrl+T / Ctrl+W | Home to add a website / close website tab |
| Ctrl+Tab | Next tab |
| Ctrl+R / Escape | Reload / stop |
| Ctrl+J | Your downloads and history |
| Ctrl+D | Add/remove current favorite |

Settings live in `%LOCALAPPDATA%\N0VA\Browser`. Uninstall keeps this data and your downloads. Delete only that browser folder while the app is closed to reset it. Switching to temporary mode does **not** erase an older saved Profile folder.

## Anti-redirect and connection controls

**Anti-redirect** sits beside the download controls, saves its state and applies immediately. It allows explicit browser navigation, clicked links, normal HTTP redirect chains (up to 20 hops) and automatic same-origin navigation. It blocks unsolicited top-level cross-origin script/meta navigation. A file selected in Site files permits one additional cross-origin client-side handoff for 10 seconds so download gateways can complete; HTTP chains following it remain bounded. User-initiated HTTP(S) popup links open as tabs; unsolicited popups remain blocked. The Anti-redirect menu lets you inspect a blocked destination and open it explicitly. It is off by default; some script-driven cross-site sign-ins can still need it turned off. This control is not a guarantee against every deceptive link, same-origin redirect or iframe technique.

**Connection** offers a proxy toggle, [Mullvad SOCKS5](https://mullvad.net/en/help/socks5-proxy) (`10.64.0.1:1080`, requires the connected Mullvad VPN app), local [Tor](https://support.torproject.org/tor-browser/encountering-issues/proxy-errors/) on ports 9050/9150, and a custom HTTP/HTTPS/SOCKS5 server with an explicit port. No provider subscription, server, VPN tunnel, Tor client or country list is bundled. Tor routing in N0VA is not Tor Browser anonymity protection. You can open Windows VPN settings from this panel.

Changes apply after fully closing and reopening the browser; the panel shows the currently active route separately from saved preferences. To change the public IP, use another valid proxy endpoint or select another exit in your VPN provider’s app. Arbitrary IPs cannot be assigned to you. Authenticated proxies are not supported. A fixed proxy has no direct fallback and disables the runtime’s implicit loopback bypass. The IP-check button opens ipify through the active browser route only when clicked. It is not proof that every OS/runtime service uses that route. For device-wide coverage use a properly configured system VPN.

## Security status

An early, unaudited browser shell, **not "the most secure browser"** or a fork of Brave/LibreWolf. See [SECURITY.md](SECURITY.md) for threat boundaries and limitations. WebView2 updates through Microsoft; **N0VA and the pinned blocker/filter package require a new app release**. At startup N0VA checks public release metadata on GitHub through the current connection and offers a newer installer with **Yes, download** and **No** choices. No keeps the current version for this launch; the menu can check again manually. Settings → About can disable startup checks. Checks use no account/cookies, have an 8-second timeout and a 2 MB response limit, and accept only newer x64 installers from N0VALID/N0VA. Installers never execute automatically; run the saved setup to update the app and filters. Update checks expose the usual connection metadata to GitHub. No N0VA analytics/check-ins are included; WebView2/Microsoft services (including SmartScreen) have their own network behavior and policies.

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

The tests also cover server/script/meta redirects, blocker toggling, favorites/history, runtime block events, release-link detection, credits, animation completion, resizing and a local proxy with no direct fallback. A separate live RustDesk API check is available with `dotnet run --project browser/tests/PolicyTests.csproj -- --check-rustdesk`.

The [Windows build workflow](https://github.com/N0VALID/N0VA/actions/workflows/browser.yml) runs policy/persistence checks, a native WebView2 integration test, compiles the setup wizard, and verifies installation plus the desktop shortcut before publishing a preview. Its evidence artifact contains the native window screenshot, browser smoke result and proxy smoke result. Runtime GUI checks must run on Windows; Linux can compile the Windows target and run the policy tests. CI does not certify anti-fingerprinting, filter coverage, malicious-download detection, or every website.

To update the blocker, review a new official uBOL release, change the version and SHA-256 in `Blocker.cs` and `scripts/prepare.ps1`, then rerun all checks and publish a new installer. Never remove the digest check to accommodate a download mismatch.
