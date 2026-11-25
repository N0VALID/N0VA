# N0VA

A Windows tool hub made by N0VALID. Version 2.2 opens with green Matrix rain and a centered logo reveal. Press any key to skip the intro. Use A/D or the left/right arrows to switch pages, type a number and press Enter to run a tool, or enter 0 to exit.

## N0VA Browser for Windows

The standalone [N0VA Browser](browser/README.md) adds native tabs, official uBlock Origin Lite, strict tracking prevention, a top-bar page-download panel, saved backgrounds/themes and idle-tab suspension. Version 0.4 refines rounded wallpaper overlays, adds direct favicon favorites and Google/Bing search choices, automatically updates Site files with Epic/Fortnite launcher detection, validates proxy routes before saving, and fixes web-focused Ctrl+T/Ctrl+W plus F11 fullscreen. Its `.exe` setup wizard creates a desktop shortcut and installs WebView2 if needed. Get the tested installer from [N0VA releases](https://github.com/N0VALID/N0VA/releases). This is an unsigned, unaudited preview; see its [security limits](browser/SECURITY.md). The existing hub and Seemless browser are unchanged.

## Page 1 - Tools and setup

| Option | What it does |
| --- | --- |
| 1 | Install Python with WinGet |
| 2 | Download EXE Compiler |
| 3 | Download and open Computer Info |
| 4 | Open the Desktop N0VA folder |
| 5 | Download Chris Titus WinUtil and open it in an administrator PowerShell window |
| 6 | Update installed apps |
| 7 | Open Disk Cleanup |
| 8 | Open Startup Apps settings |
| 9 | Download and open Command Explorer |
| 10 | Install Brave |
| 11 | Install Steam |
| 12 | Install all Python dependencies used by the N0VA tools |
| 13 | Install RustDesk |
| 21 | Open Seemless 0S MODE, a Windows-style terminal desktop |
| 0 | Close N0VA and CMD |

## Page 2 - Downloads and diagnostics

| Option | What it does |
| --- | --- |
| 14 | Install 7-Zip for archives |
| 15 | Install Notepad++ for text editing |
| 16 | Install Visual Studio Code |
| 17 | Install Git |
| 18 | Install VLC media player |
| 19 | Install Everything file search |
| 20 | Install Microsoft PowerToys |
| 22 | View live CPU and memory usage and the six largest processes by memory |
| 23 | See the most recent Application Error crashes from the past seven days |
| 24 | Check active adapters, gateway ping, DNS resolution and HTTPS access to example.com |

Options 10, 11, 13, and 14-20 use WinGet from Microsoft's App Installer. Accept installer or administrator prompts when Windows displays them. Downloads are stored in `Desktop\N0VA` (using your Windows Desktop location).

The Python setup detects an existing interpreter or installs Python if needed, enables pip, and installs PyInstaller, PyAutoGUI, Pillow, and opencv-python with their dependencies. This covers EXE Compiler, Coordinate Finder, screenshots, and Command Explorer's image-confidence matching; it does not install every package on PyPI. Python's standard library and Tkinter come with the normal Windows Python installation.

WinUtil opens in a separate administrator PowerShell window and leaves errors visible there. Download or elevation errors remain visible in N0VA.

## owner M3NU

Press **N four times** on either hub page. You can start during the intro. Esc returns to the previous page.

| Option | Owner tools |
| --- | --- |
| 7 | Run history, newest first |
| 8 | Display names, run counts, first and last seen, and latest build per sharing profile |
| 9 | Pair this Windows profile with your private N0VA owner key |
| 10 | Launches, profiles and last use by build |

After pairing, the panel shows total launches, distinct profiles and launches in the past 24 hours. Use A/D to page through records or R to refresh. The owner's key is supplied separately. The launcher checks it with the service and saves it with Windows current-user encryption. Keep the original private. Options 1 through 6 still open the local diagnostics and Windows tools.

### Share a launch

On either hub page, enter **25** and choose a display name to turn on sharing. Until then, the launcher sends no check-ins. Each check-in contains the chosen name, a random sharing profile, a launch ID and the N0VA build. The service timestamps accepted launches. Returning from a tool or retrying the same launch does not increase the count. Offline launches can be retried from option 25 during the same session. Names are self-reported, and one person can have multiple profiles.

History views cover 90 days. Use option 25 to stop future submissions. To erase this profile's shared records, choose **D** before turning off sharing.

## Seemless 0S MODE

Choose **21** on page 1 or type **seemless** on either page. The shortcut works immediately after the final letter. The launcher caches the desktop after its first download.

The desktop keeps common tasks inside its own windows:

- **Files:** browse folders, preview files, read and edit text, and create a text file or folder. Unsaved changes have Save, Discard, and Cancel choices.
- **Browser:** F3 opens WebView2 with title, loading and audio indicators, full-URL tab tooltips, middle-click close and Ctrl+L/T/W. Settings saves connection, blocking and session preferences. The default uses the Windows connection. The Mullvad preset needs an active Mullvad VPN connection. Add your own HTTP, HTTPS or SOCKS5 endpoints for fixed or random-per-start routing. With multiple saved endpoints random mode picks a different one from the previous start. Provider routing determines the public IP.
- **Privacy:** strict tracking prevention, password autosave and autofill disabled, and optional uBlock Origin Lite (on by default). The browser verifies the official SDK and blocker downloads. With blocking enabled, pages wait for extension initialization. New profiles default to temporary browsing data. Settings and downloaded files survive the session. If Windows still has a temporary profile open at exit, cleanup retries at the next launch.
- **Standalone:** in Browser Settings, select **Install on this PC** for a Start-menu shortcut or **Save installer** to create a Windows installer. Browser-only starts do not run N0VA's launch sharing. The Edge WebView2 Runtime is required; the SDK and blocker components are fetched on first use over the system connection.
- **Apps:** choose an app, confirm installation, and watch its progress inside the desktop. Windows may show its normal administrator prompt.
- **Settings:** view PC, storage, network, and running-app information; choose a saved desktop theme.

Use the arrows and Enter to select things. **S** opens Start, **Esc** goes home or cancels a dialog, and **F10** returns to N0VA. File windows and input boxes show the actions that are relevant there. **F1-F5** remain available for quick navigation. A running installation must finish before leaving the desktop.

The desktop is delivered as `SeemlessOS.ps1` in your Desktop N0VA folder and requires no Python packages. Native Windows applications run in their own windows; the terminal desktop does not replace Windows or embed other programs' interfaces.

## Run the latest N0VA

Open Command Prompt and paste:

```bat
curl.exe -fL "https://raw.githubusercontent.com/N0VALID/N0VA/main/N0VA.cmd?version=2.2" -o "%TEMP%\N0VA-v2.2.cmd" && "%TEMP%\N0VA-v2.2.cmd"
```

## Adding tools

Upload a new `.pyw` script, add a numbered action and matching download in `N0VA.cmd`, then add its label to the embedded PowerShell menu. Include any new third-party dependencies in the Python setup action.
