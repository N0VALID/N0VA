# N0VA Tool Hub

A simple, numbered Windows installer menu for small Python desktop tools.

## Available tools

| Menu option | Action | File |
| --- | --- | --- |
| 1 | Install Python 3.14 via WinGet | Official Python package |
| 2 | Download EXE Studio (Python-to-EXE GUI) | `exe_compiler.pyw` |
| 3 | Download Computer Info (local-only GUI) | `cmptr_info.pyw` |
| 4 | Open N0VA downloads folder | `%USERPROFILE%\Downloads\N0VA` |
| 0 | Exit | |

## Get started

1. On Windows, open **Command Prompt**.
2. Download the installer menu:

   ```bat
   curl.exe -fL "https://raw.githubusercontent.com/N0VALID/N0VA/main/N0VA.cmd" -o "N0VA.cmd"
   ```

3. Open the downloaded `N0VA.cmd` in Notepad and review it, then double-click it to start the menu.
4. Type a menu number and press Enter.

Options 2 and 3 **download** Python scripts to your Downloads\N0VA folder. They are not automatically executed. Open them with Python after reviewing the scripts.

## Requirements

- Windows with `curl.exe` for downloading the menu and scripts
- WinGet to use the Python installation option
- Python with Tkinter to run the `.pyw` desktop apps
- EXE Studio offers an optional button to install PyInstaller using pip

## Privacy

Computer Info displays basic OS, processor, memory, and disk information **locally**. It does not contact external servers or copy/save information without a button click. Its old Discord-upload functionality was intentionally removed.

## Updating

Edit `N0VA.cmd` to add new numbered tools. To download a file from this repository, use the raw file URL:

`https://raw.githubusercontent.com/N0VALID/N0VA/main/FILENAME.pyw`

Do not put passwords, API tokens, or Discord webhook URLs into publicly accessible files. Treat any previously committed secret as exposed and rotate/revoke it at the provider; deleting it from the latest version does not erase git history.
