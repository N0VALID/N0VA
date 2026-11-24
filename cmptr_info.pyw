"""N0VA Computer Info — local-only Windows system overview.

No network requests, automatic clipboard access, hidden collection,
or background uploads. Copying/saving requires a button click.
"""
import ctypes
import os
import platform
import shutil
import sys
import tkinter as tk
from ctypes import wintypes
from datetime import datetime
from pathlib import Path
from tkinter import filedialog, messagebox, ttk


class MEMORYSTATUSEX(ctypes.Structure):
    _fields_ = [
        ("dwLength", wintypes.DWORD),
        ("dwMemoryLoad", wintypes.DWORD),
        ("ullTotalPhys", ctypes.c_ulonglong),
        ("ullAvailPhys", ctypes.c_ulonglong),
        ("ullTotalPageFile", ctypes.c_ulonglong),
        ("ullAvailPageFile", ctypes.c_ulonglong),
        ("ullTotalVirtual", ctypes.c_ulonglong),
        ("ullAvailVirtual", ctypes.c_ulonglong),
        ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
    ]


def installed_ram():
    try:
        memory = MEMORYSTATUSEX()
        memory.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
        func = ctypes.windll.kernel32.GlobalMemoryStatusEx
        func.argtypes = [ctypes.POINTER(MEMORYSTATUSEX)]
        func.restype = wintypes.BOOL
        if func(ctypes.byref(memory)):
            return f"{memory.ullTotalPhys / 1024**3:.1f} GB"
    except (OSError, AttributeError):
        pass
    return "Unavailable"


def system_name():
    name = platform.system() + " " + platform.release()
    try:
        build = sys.getwindowsversion().build
        if name.startswith("Windows 10") and build >= 22000:
            name = name.replace("Windows 10", "Windows 11", 1)
        name += f" (build {build})"
    except AttributeError:
        pass
    return name


def gather():
    rows = [
        ("Checked", datetime.now().astimezone().strftime("%Y-%m-%d %H:%M:%S %Z")),
        ("Operating system", system_name()),
        ("Architecture", platform.machine() or "Unavailable"),
        ("Processor", platform.processor() or "Unavailable"),
        ("Python version", platform.python_version()),
        ("Installed memory", installed_ram()),
    ]
    try:
        drive = Path.home().anchor or os.environ.get("SystemDrive", "C:") + "\\"
        usage = shutil.disk_usage(drive)
        rows.append(("Free disk space", f"{usage.free / 1024**3:.1f} GB on {drive}"))
    except OSError:
        rows.append(("Free disk space", "Unavailable"))
    return rows


class ComputerInfo:
    def __init__(self, root):
        self.root = root
        root.title("N0VA - Computer Info")
        root.geometry("720x510")
        root.minsize(520, 350)
        root.configure(bg="#101827")

        top = tk.Frame(root, bg="#101827")
        top.pack(fill="x", padx=20, pady=(20, 10))
        tk.Label(top, text="N0VA / Computer Info",
                 font=("Segoe UI", 21, "bold"), bg="#101827",
                 fg="#e8f0ff").pack(anchor="w")
        tk.Label(top, text="Local system details only. Nothing is uploaded.",
                 font=("Segoe UI", 10), bg="#101827",
                 fg="#91abc9").pack(anchor="w", pady=(4, 0))

        frame = tk.Frame(root, bg="#101827")
        frame.pack(fill="both", expand=True, padx=20, pady=10)
        self.view = ttk.Treeview(frame, columns=("value",), show="tree headings")
        self.view.heading("#0", text="PROPERTY")
        self.view.heading("value", text="VALUE")
        self.view.column("#0", width=165, minwidth=125)
        self.view.column("value", width=470, minwidth=240)
        scroll = ttk.Scrollbar(frame, orient="vertical", command=self.view.yview)
        self.view.configure(yscrollcommand=scroll.set)
        self.view.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")

        buttons = tk.Frame(root, bg="#101827")
        buttons.pack(fill="x", padx=20, pady=(4, 18))
        for label, callback in [
            ("Refresh", self.refresh),
            ("Copy details", self.copy),
            ("Save as text...", self.save),
        ]:
            tk.Button(buttons, text=label, command=callback,
                      bg="#263c5b", fg="white", activebackground="#365780",
                      activeforeground="white", relief="flat", padx=14,
                      pady=8, font=("Segoe UI", 10)).pack(side="left", padx=(0, 10))
        self.rows = []
        self.refresh()

    def refresh(self):
        self.rows = gather()
        for item in self.view.get_children():
            self.view.delete(item)
        for key, value in self.rows:
            self.view.insert("", "end", text=key, values=(value,))

    def report(self):
        return "N0VA - Computer Info\n\n" + "\n".join(
            f"{key}: {value}" for key, value in self.rows
        ) + "\n"

    def copy(self):
        self.root.clipboard_clear()
        self.root.clipboard_append(self.report())
        self.root.update()
        messagebox.showinfo("Copied", "System summary copied to the clipboard.",
                            parent=self.root)

    def save(self):
        path = filedialog.asksaveasfilename(
            parent=self.root, title="Save computer summary",
            defaultextension=".txt", initialfile="computer_info.txt",
            filetypes=[("Text files", "*.txt")],
        )
        if path:
            try:
                Path(path).write_text(self.report(), encoding="utf-8")
            except OSError as exc:
                messagebox.showerror("Save failed", str(exc), parent=self.root)


if __name__ == "__main__":
    root = tk.Tk()
    ComputerInfo(root)
    root.mainloop()
