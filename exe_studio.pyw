from __future__ import annotations

import importlib.util
import queue
import re
import subprocess
import sys
import threading
import tkinter as tk
from pathlib import Path
from tkinter import filedialog, messagebox, ttk


BG = "#F3F5F7"
PANEL = "#FFFFFF"
INK = "#1C2733"
MUTED = "#697586"
BLUE = "#2869E8"
BORDER = "#DCE2E8"


class ExeStudio:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        root.title("Exe Studio")
        root.geometry("700x650")
        root.minsize(620, 600)
        root.configure(bg=BG)

        self.script = tk.StringVar()
        self.name = tk.StringVar()
        self.icon = tk.StringVar()
        self.output = tk.StringVar()
        self.onefile = tk.BooleanVar(value=True)
        self.hide_console = tk.BooleanVar(value=False)
        self.status = tk.StringVar(value="Checking PyInstaller…")
        self.events: queue.Queue[tuple[str, str]] = queue.Queue()
        self.busy = False

        self._style()
        self._layout()
        self._update_status()
        root.after(120, self._poll)

    def _style(self) -> None:
        style = ttk.Style(self.root)
        try:
            style.theme_use("clam")
        except tk.TclError:
            pass
        style.configure("TFrame", background=BG)
        style.configure("Panel.TFrame", background=PANEL)
        style.configure("TLabel", background=BG, foreground=INK, font=("Segoe UI", 10))
        style.configure("Panel.TLabel", background=PANEL, foreground=INK, font=("Segoe UI", 10))
        style.configure("Title.TLabel", background=BG, foreground=INK, font=("Segoe UI Semibold", 24))
        style.configure("Hint.TLabel", background=PANEL, foreground=MUTED, font=("Segoe UI", 9))
        style.configure("Section.TLabel", background=PANEL, foreground=INK, font=("Segoe UI Semibold", 11))
        style.configure("TEntry", padding=(9, 8), fieldbackground="#FFFFFF", bordercolor=BORDER)
        style.map("TEntry", bordercolor=[("focus", BLUE)])
        style.configure("TCheckbutton", background=PANEL, foreground=INK, font=("Segoe UI", 10))
        style.configure("Primary.TButton", background=BLUE, foreground="white", padding=(16, 10),
                        font=("Segoe UI Semibold", 10), borderwidth=0)
        style.map("Primary.TButton", background=[("active", "#1F5CCC"), ("disabled", "#A9BEDF")])
        style.configure("Secondary.TButton", background="#EDF1F5", foreground=INK,
                        padding=(10, 7), borderwidth=0)
        style.map("Secondary.TButton", background=[("active", "#E1E7ED")])

    def _layout(self) -> None:
        page = ttk.Frame(self.root, padding=(26, 22, 26, 18))
        page.pack(fill="both", expand=True)
        ttk.Label(page, text="Exe Studio", style="Title.TLabel").pack(anchor="w")
        ttk.Label(page, text="Build a Windows executable from a Python script.").pack(anchor="w", pady=(3, 16))

        card = ttk.Frame(page, style="Panel.TFrame", padding=16)
        card.pack(fill="x")
        ttk.Label(card, text="Application details", style="Section.TLabel").pack(anchor="w")
        ttk.Label(card, text="Choose your script, executable name, icon, and destination.",
                  style="Hint.TLabel").pack(anchor="w", pady=(3, 13))
        self._field(card, "Python file", self.script, self._choose_script, "Browse…")
        self._field(card, "Executable name", self.name, None, hint="This becomes the .exe filename")
        self._field(card, "Application icon (.ico) · optional", self.icon, self._choose_icon, "Choose…")
        self._field(card, "Output folder", self.output, self._choose_output, "Choose…")

        options = ttk.Frame(page, style="Panel.TFrame", padding=(16, 13))
        options.pack(fill="x", pady=(12, 0))
        ttk.Label(options, text="Build options", style="Section.TLabel").pack(anchor="w", pady=(0, 8))
        row = ttk.Frame(options, style="Panel.TFrame")
        row.pack(fill="x")
        ttk.Checkbutton(row, text="Single-file executable", variable=self.onefile).pack(side="left")
        ttk.Checkbutton(row, text="Hide console (GUI apps)", variable=self.hide_console).pack(side="left", padx=(22, 0))

        bottom = ttk.Frame(page)
        bottom.pack(fill="x", pady=(12, 0))
        self.dot = tk.Label(bottom, text="●", bg=BG, fg="#B7791F", font=("Segoe UI", 9))
        self.dot.pack(side="left", padx=(0, 7))
        ttk.Label(bottom, textvariable=self.status).pack(side="left")
        self.install = ttk.Button(bottom, text="Install PyInstaller", style="Secondary.TButton", command=self._install)
        self.install.pack(side="right")
        self.build = ttk.Button(bottom, text="Build executable", style="Primary.TButton", command=self._build)
        self.build.pack(side="right", padx=(0, 8))
        self.progress = ttk.Progressbar(page, mode="indeterminate")
        self.progress.pack(fill="x", pady=(12, 10))

        log_panel = ttk.Frame(page, style="Panel.TFrame", padding=13)
        log_panel.pack(fill="both", expand=True)
        header = ttk.Frame(log_panel, style="Panel.TFrame")
        header.pack(fill="x", pady=(0, 7))
        ttk.Label(header, text="Build output", style="Section.TLabel").pack(side="left")
        ttk.Button(header, text="Clear", style="Secondary.TButton", command=self._clear).pack(side="right")
        self.log = tk.Text(log_panel, height=8, wrap="word", state="disabled", relief="flat",
                           bg="#F7F9FB", fg="#405064", font=("Consolas", 9), padx=9, pady=8)
        self.log.pack(fill="both", expand=True)
        self.log.tag_configure("error", foreground="#B42318")
        self.log.tag_configure("success", foreground="#16845B")

    def _field(self, parent: ttk.Frame, label: str, var: tk.StringVar, browse, button: str | None = None,
               hint: str | None = None) -> None:
        box = ttk.Frame(parent, style="Panel.TFrame")
        box.pack(fill="x", pady=(0, 10))
        ttk.Label(box, text=label, style="Hint.TLabel").pack(anchor="w", pady=(0, 4))
        line = ttk.Frame(box, style="Panel.TFrame")
        line.pack(fill="x")
        ttk.Entry(line, textvariable=var).pack(side="left", fill="x", expand=True)
        if browse:
            ttk.Button(line, text=button or "Browse…", style="Secondary.TButton", command=browse).pack(side="left", padx=(8, 0))
        if hint:
            ttk.Label(box, text=hint, style="Hint.TLabel").pack(anchor="w", pady=(4, 0))

    def _choose_script(self) -> None:
        value = filedialog.askopenfilename(title="Choose Python script", filetypes=[("Python scripts", "*.py *.pyw")])
        if value:
            path = Path(value)
            self.script.set(str(path))
            self.name.set(path.stem)
            if not self.output.get():
                self.output.set(str(path.parent / "dist"))

    def _choose_icon(self) -> None:
        value = filedialog.askopenfilename(title="Choose icon", filetypes=[("Icon files", "*.ico")])
        if value:
            self.icon.set(value)

    def _choose_output(self) -> None:
        value = filedialog.askdirectory(title="Choose output folder", mustexist=False)
        if value:
            self.output.set(value)

    def _update_status(self) -> None:
        available = importlib.util.find_spec("PyInstaller") is not None
        self.install.configure(state="disabled" if self.busy or available else "normal")
        if not self.busy:
            self.status.set("PyInstaller is ready" if available else "PyInstaller is not installed")
            self.dot.configure(fg="#16845B" if available else "#B7791F")

    def _set_busy(self, busy: bool, message: str) -> None:
        self.busy = busy
        self.status.set(message)
        self.dot.configure(fg=BLUE if busy else "#16845B")
        self.build.configure(state="disabled" if busy else "normal")
        if busy:
            self.install.configure(state="disabled")
            self.progress.start(10)
        else:
            self.progress.stop()
            self._update_status()

    def _write(self, text: str, tag: str | None = None) -> None:
        self.log.configure(state="normal")
        self.log.insert("end", text.rstrip() + "\n", tag or ())
        self.log.see("end")
        self.log.configure(state="disabled")

    def _clear(self) -> None:
        self.log.configure(state="normal")
        self.log.delete("1.0", "end")
        self.log.configure(state="disabled")

    def _install(self) -> None:
        if self.busy:
            return
        self._set_busy(True, "Installing PyInstaller…")
        threading.Thread(target=self._run, args=([sys.executable, "-m", "pip", "install", "pyinstaller"], "install", None), daemon=True).start()

    def _build(self) -> None:
        script = Path(self.script.get().strip().strip('"')).expanduser()
        name = self.name.get().strip()
        icon_text = self.icon.get().strip().strip('"')
        output = Path(self.output.get().strip().strip('"')).expanduser()
        if not self.script.get().strip() or not script.is_file() or script.suffix.lower() not in {".py", ".pyw"}:
            messagebox.showerror("Choose a Python file", "Select an existing .py or .pyw file to build.", parent=self.root)
            return
        if not name or re.search(r'[<>:"/\\|?*]', name) or name.endswith((".", " ")):
            messagebox.showerror("Check executable name", "Enter a valid Windows file name.", parent=self.root)
            return
        if not self.output.get().strip():
            messagebox.showerror("Choose an output folder", "Select where the executable should be saved.", parent=self.root)
            return
        icon = Path(icon_text).expanduser() if icon_text else None
        if icon and (not icon.is_file() or icon.suffix.lower() != ".ico"):
            messagebox.showerror("Check icon", "Choose an existing .ico file, or leave the icon field blank.", parent=self.root)
            return
        if importlib.util.find_spec("PyInstaller") is None:
            messagebox.showerror("PyInstaller required", "Select Install PyInstaller, then build again.", parent=self.root)
            return
        try:
            output.mkdir(parents=True, exist_ok=True)
        except OSError as exc:
            messagebox.showerror("Output folder error", str(exc), parent=self.root)
            return
        command = [sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean", "--name", name,
                   "--distpath", str(output), "--workpath", str(output / ".exe-studio-build"),
                   "--specpath", str(output / ".exe-studio-spec")]
        if self.onefile.get():
            command.append("--onefile")
        if self.hide_console.get() or script.suffix.lower() == ".pyw":
            command.append("--windowed")
        if icon:
            command.extend(["--icon", str(icon)])
        command.append(str(script))
        self._set_busy(True, "Building executable…")
        self._write("Starting build")
        self._write("Command: " + subprocess.list2cmdline(command))
        threading.Thread(target=self._run, args=(command, "build", (output, name, script.parent)), daemon=True).start()

    def _run(self, command: list[str], kind: str, result_info) -> None:
        try:
            flags = getattr(subprocess, "CREATE_NO_WINDOW", 0)
            cwd = str(result_info[2]) if kind == "build" else None
            process = subprocess.Popen(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                       text=True, encoding="utf-8", errors="replace", cwd=cwd,
                                       creationflags=flags, bufsize=1)
            assert process.stdout is not None
            for line in process.stdout:
                self.events.put(("log", line.rstrip()))
            code = process.wait()
            if code == 0 and kind == "build":
                output, name, _ = result_info
                self.events.put(("success", str(output / (name + ".exe"))))
            elif code == 0:
                self.events.put(("installed", "PyInstaller installed successfully."))
            else:
                self.events.put(("failure", f"{kind.title()} failed with exit code {code}."))
        except Exception as exc:
            self.events.put(("failure", f"Could not start {kind}: {exc}"))

    def _poll(self) -> None:
        try:
            while True:
                kind, value = self.events.get_nowait()
                if kind == "log":
                    self._write(value)
                elif kind == "success":
                    self._set_busy(False, "Build complete")
                    self.dot.configure(fg="#16845B")
                    self._write("Created: " + value, "success")
                    messagebox.showinfo("Build complete", "Your executable is ready:\n\n" + value, parent=self.root)
                elif kind == "installed":
                    self._set_busy(False, value)
                    self._write(value, "success")
                elif kind == "failure":
                    self._set_busy(False, "Build failed — see output below")
                    self.dot.configure(fg="#B42318")
                    self._write(value, "error")
                    messagebox.showerror("Build failed", value + "\n\nReview the build output for details.", parent=self.root)
        except queue.Empty:
            pass
        self.root.after(120, self._poll)


def main() -> None:
    root = tk.Tk()
    ExeStudio(root)
    root.mainloop()


if __name__ == "__main__":
    main()


