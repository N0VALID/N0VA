# N0VA

A Windows tool hub made by N0VALID. Version 1.9 includes RGB Matrix rain, a centered RGB menu, and a full-screen Windows Terminal launch. Press `A` or `D` to move between pages instantly. Type an option number and press Enter to select it; `0` then Enter closes CMD. Page navigation uses the same running menu process and cached frames to avoid a blank screen between pages. It stops at the first and last pages, and returning from a tool keeps the current page.

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

## Page 2 - Useful downloads

| Option | What it does |
| --- | --- |
| 14 | Install 7-Zip for archives |
| 15 | Install Notepad++ for text editing |
| 16 | Install Visual Studio Code |
| 17 | Install Git |
| 18 | Install VLC media player |
| 19 | Install Everything file search |
| 20 | Install Microsoft PowerToys |

Options 10, 11, 13, and 14-20 use WinGet from Microsoft's App Installer. Accept installer or administrator prompts when Windows displays them. Downloads are stored in `Desktop\N0VA` (using your Windows Desktop location).

The Python setup detects an existing interpreter or installs Python if needed, enables pip, and installs PyInstaller, PyAutoGUI, Pillow, and opencv-python with their dependencies. This covers EXE Compiler, Coordinate Finder, screenshots, and Command Explorer's image-confidence matching; it does not install every package on PyPI. Python's standard library and Tkinter come with the normal Windows Python installation.

WinUtil opens in a separate administrator PowerShell window and leaves errors visible there. Download or elevation errors remain visible in N0VA.

## Seemless 0S MODE

Choose **21** on page 1 to download and open the terminal desktop. It runs inside the current terminal and returns to N0VA when closed. It does not replace your Windows desktop or change your Windows shell.

The desktop includes a Start menu, file explorer, web reader, app installer, and control panel. App downloads use WinGet, with visible progress and installation results. The explorer navigates folders and opens selected files through their Windows associations. The web reader shows readable page text and links; use its external browser action for sign-ins, videos, or pages that require JavaScript.

| Key | Desktop action |
| --- | --- |
| S | Toggle the Start menu |
| F1 / F2 / F3 | Desktop / File Explorer / Web Reader |
| F4 / F5 | App Center / Control Panel |
| Arrows / Enter | Select and open |
| Esc | Cancel a dialog or return to the desktop |
| F10 | Return to N0VA |

In File Explorer, `N` opens the N0VA downloads folder and `L` enters a folder path. In Web Reader, `L` enters a URL or search, `Tab` switches to links, `B` goes back, and `O` opens the page in your normal browser. App Center supports `/` search. Input fields support `Ctrl+A` to replace their contents.

The mode is delivered as `SeemlessOS.ps1` in your Desktop N0VA folder. Windows PowerShell 5.1 and Windows Terminal are supported; no Python packages are required for the desktop.

## Run the latest N0VA

Open Command Prompt and paste:

```bat
curl.exe -fL "https://raw.githubusercontent.com/N0VALID/N0VA/main/N0VA.cmd?version=1.9" -o "%TEMP%\N0VA-v1.9.cmd" && "%TEMP%\N0VA-v1.9.cmd"
```

## Adding tools

Upload a new `.pyw` script, add a numbered action and matching download in `N0VA.cmd`, then add its label to the embedded PowerShell menu. Include any new third-party dependencies in the Python setup action.
