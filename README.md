# N0VA

A Windows tool hub made by N0VALID. Version 1.5 includes RGB Matrix rain, a centered RGB menu, and a full-screen Windows Terminal launch. Press `0` to close CMD.

## Menu

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
| 0 | Close N0VA and CMD |

Options 10 and 11 use WinGet from Microsoft's App Installer. Accept installer or administrator prompts when Windows displays them. Downloads are stored in `Desktop\N0VA` (using your Windows Desktop location).

The Python setup detects an existing interpreter or installs Python if needed, enables pip, and installs PyInstaller, PyAutoGUI, Pillow, and opencv-python with their dependencies. This covers EXE Compiler, Coordinate Finder, screenshots, and Command Explorer's image-confidence matching; it does not install every package on PyPI. Python's standard library and Tkinter come with the normal Windows Python installation.

WinUtil opens in a separate administrator PowerShell window and leaves errors visible there. Download or elevation errors remain visible in N0VA.

## Run the latest N0VA

Open Command Prompt and paste:

```bat
curl.exe -fL "https://raw.githubusercontent.com/N0VALID/N0VA/main/N0VA.cmd?version=1.5" -o "%TEMP%\N0VA-v1.5.cmd" && "%TEMP%\N0VA-v1.5.cmd"
```

## Adding tools

Upload a new `.pyw` script, add a numbered action and matching download in `N0VA.cmd`, then add its label to the embedded PowerShell menu. Include any new third-party dependencies in the Python setup action.
