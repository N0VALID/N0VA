# N0VA

A Windows tool hub made by N0VALID. Version 2.1 opens with green Matrix rain and a centered logo reveal. Press any key to skip the intro. Use A/D or the left/right arrows to switch pages, type a number and press Enter to run a tool, or enter 0 to exit.

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

Press **N four times in a row** on either hub page. An amber intro opens the panel. Enter 1, 2, or 3 for live performance, crash history, or a connection check. Enter 4, 5, or 6 for Event Viewer, Services, or Task Manager. Esc returns to the hub page you started on. The panel displays the Windows account's current elevation and uses that account's permissions.

## Seemless 0S MODE

Choose **21** on page 1 or type **seemless** on either page. The shortcut works immediately after the final letter. The launcher caches the desktop after its first download.

The desktop keeps common tasks inside its own windows:

- **Files:** browse folders, preview files, read and edit text, and create a text file or folder. Unsaved changes have Save, Discard, and Cancel choices.
- **Browser:** F3 opens a separate WebView2 window with tabs, search, back, forward and reload. Close the window to return to the desktop. First use downloads the pinned Microsoft SDK components and requires the Microsoft Edge WebView2 Runtime. To use your own proxy, open Settings > Browser proxy. Clear the value to restore the system default.
- **Apps:** choose an app, confirm installation, and watch its progress inside the desktop. Windows may show its normal administrator prompt.
- **Settings:** view PC, storage, network, and running-app information; choose a saved desktop theme.

Use the arrows and Enter to select things. **S** opens Start, **Esc** goes home or cancels a dialog, and **F10** returns to N0VA. File windows and input boxes show the actions that are relevant there. **F1-F5** remain available for quick navigation. A running installation must finish before leaving the desktop.

The desktop is delivered as `SeemlessOS.ps1` in your Desktop N0VA folder and requires no Python packages. Native Windows applications run in their own windows; the terminal desktop does not replace Windows or embed other programs' interfaces.

## Run the latest N0VA

Open Command Prompt and paste:

```bat
curl.exe -fL "https://raw.githubusercontent.com/N0VALID/N0VA/main/N0VA.cmd?version=2.1" -o "%TEMP%\N0VA-v2.1.cmd" && "%TEMP%\N0VA-v2.1.cmd"
```

## Adding tools

Upload a new `.pyw` script, add a numbered action and matching download in `N0VA.cmd`, then add its label to the embedded PowerShell menu. Include any new third-party dependencies in the Python setup action.
