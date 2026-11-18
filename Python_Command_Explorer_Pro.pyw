"""Python Automation Reference

A standalone, searchable Python automation command browser and mini editor.
Includes Tab-based autocomplete and automatic missing-import insertion.
Only Python's standard library is needed to launch this reference app.
Libraries shown in examples are required only when you run your own scripts.
"""

from __future__ import annotations

import ast
import difflib
import io
import re
import subprocess
import sys
import tkinter as tk
import tokenize
from dataclasses import dataclass
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

# Coordinate Finder runs in a separate window. Nothing is added to the editor.
COORDINATE_FINDER_SOURCE = r'''"""Coordinate Finder Pro – capture mouse X/Y and RGB on Windows.

Built from the user's coordinate-finder concept. Launch by double-clicking
this .pyw file once PyAutoGUI is installed: py -m pip install pyautogui
"""

import ctypes
import sys
import tkinter as tk
from tkinter import messagebox

try:
    import pyautogui
except ImportError:
    hidden = tk.Tk()
    hidden.withdraw()
    messagebox.showerror(
        "Missing dependency",
        "Coordinate Finder needs PyAutoGUI.\n\nInstall it once with:\npy -m pip install pyautogui",
        parent=hidden,
    )
    hidden.destroy()
    raise SystemExit(1)

BG = "#0d121c"
CARD = "#192433"
CARD_DARK = "#131c29"
BORDER = "#344458"
FOREGROUND = "#eff6ff"
MUTED = "#a9b8cb"
BLUE = "#73b7ff"
MINT = "#94eed2"
FONT = "Segoe UI"


class CoordinateFinder(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Coordinate Finder Pro")
        self.geometry("590x470")
        self.minsize(550, 460)
        self.configure(bg=BG)
        self.last_capture = None
        self.space_held = False
        self.is_pinned = False
        self.x_var = tk.StringVar(value="—")
        self.y_var = tk.StringVar(value="—")
        self.rgb_var = tk.StringVar(value="—")
        self.live_var = tk.StringVar(value="LIVE  •  Move your cursor")
        self.status_var = tk.StringVar(value="Move your mouse to a location, then press Space.")
        self._make_ui()
        # Windows reads Space globally without the 'keyboard' package or admin rights.
        # Other systems can use the Capture button or press Space with this window focused.
        if sys.platform != "win32":
            self.bind_all("<space>", lambda event: self.capture())
        self.after(90, self._update_cursor)

    def _button(self, parent, text, command, *, featured=False):
        return tk.Button(
            parent, text=text, command=command, cursor="hand2", relief="flat", bd=0,
            font=(FONT, 10, "bold"), padx=13, pady=10,
            bg=BLUE if featured else CARD, fg=BG if featured else FOREGROUND,
            activebackground="#a0d1ff" if featured else "#293b51",
            activeforeground=BG if featured else FOREGROUND,
        )

    def _make_ui(self):
        top = tk.Frame(self, bg=BG, padx=23, pady=18)
        top.pack(fill="x")
        title_row = tk.Frame(top, bg=BG)
        title_row.pack(fill="x")
        tk.Label(title_row, text="⌖", fg=BLUE, bg=BG, font=(FONT, 24, "bold")).pack(side="left")
        titles = tk.Frame(title_row, bg=BG)
        titles.pack(side="left", padx=(12, 0))
        tk.Label(titles, text="COORDINATE FINDER", fg=FOREGROUND, bg=BG,
                 font=(FONT, 16, "bold")).pack(anchor="w")
        tk.Label(titles, text="Pixel-perfect X, Y and RGB sampling", fg=MUTED,
                 bg=BG, font=(FONT, 9)).pack(anchor="w", pady=(3, 0))
        self.pin_btn = self._button(title_row, "Pin on top", self.toggle_pin)
        self.pin_btn.pack(side="right")

        hint = tk.Frame(self, bg=CARD_DARK, padx=15, pady=11,
                        highlightbackground=BORDER, highlightthickness=1)
        hint.pack(fill="x", padx=23, pady=(0, 16))
        tk.Label(hint, text="SPACE", bg="#264461", fg=BLUE,
                 font=("Consolas", 11, "bold"), padx=9, pady=5).pack(side="left")
        tk.Label(hint, text="Capture anywhere on Windows", fg=FOREGROUND, bg=CARD_DARK,
                 font=(FONT, 10)).pack(side="left", padx=(12, 0))

        live = tk.Frame(self, bg=BG, padx=23)
        live.pack(fill="x", pady=(0, 10))
        tk.Label(live, textvariable=self.live_var, fg=MINT, bg=BG,
                 font=("Consolas", 10)).pack(anchor="w")

        values = tk.Frame(self, bg=BG)
        values.pack(fill="x", padx=23)
        for i, (label, variable) in enumerate((
            ("X POSITION", self.x_var), ("Y POSITION", self.y_var), ("RGB COLOR", self.rgb_var)
        )):
            card = tk.Frame(values, bg=CARD, padx=13, pady=14,
                            highlightbackground=BORDER, highlightthickness=1)
            card.grid(row=0, column=i, sticky="nsew", padx=(0, 8) if i < 2 else (0, 0))
            values.grid_columnconfigure(i, weight=1, uniform="values")
            tk.Label(card, text=label, fg=MUTED, bg=CARD,
                     font=(FONT, 9, "bold")).pack(anchor="w")
            tk.Label(card, textvariable=variable, fg=FOREGROUND, bg=CARD,
                     font=("Consolas", 17, "bold")).pack(anchor="w", pady=(12, 0))

        swatch_row = tk.Frame(self, bg=BG, padx=23, pady=14)
        swatch_row.pack(fill="x")
        self.color_swatch = tk.Label(swatch_row, bg="#253144", width=6, height=2,
                                     highlightthickness=1, highlightbackground=BORDER)
        self.color_swatch.pack(side="left")
        self.hex_var = tk.StringVar(value="HEX  —")
        tk.Label(swatch_row, textvariable=self.hex_var, bg=BG, fg=MUTED,
                 font=("Consolas", 11)).pack(side="left", padx=(12, 0))
        self._button(swatch_row, "Capture now", self.capture, featured=True).pack(side="right")

        tk.Frame(self, bg=BORDER, height=1).pack(fill="x", padx=23, pady=(0, 14))
        actions = tk.Frame(self, bg=BG)
        actions.pack(fill="x", padx=23)
        self._button(actions, "Copy X, Y", self.copy_coordinates).pack(side="left")
        self._button(actions, "Copy RGB", self.copy_rgb).pack(side="left", padx=7)
        self._button(actions, "Copy click() code", self.copy_click).pack(side="left")

        status = tk.Frame(self, bg=BG, padx=23, pady=17)
        status.pack(fill="x")
        tk.Label(status, textvariable=self.status_var, bg=BG, fg=MUTED,
                 font=(FONT, 9), anchor="w").pack(fill="x")
        tk.Label(status, text="Made for Python + PyAutoGUI · No global keyboard package needed",
                 bg=BG, fg="#71869f", font=(FONT, 8)).pack(anchor="w", pady=(8, 0))

    def _update_cursor(self):
        try:
            cursor = pyautogui.position()
            self.live_var.set(f"LIVE  •  X {cursor.x:>5}   Y {cursor.y:>5}")
            if sys.platform == "win32":
                # Bit 15 indicates the key is currently down. Trigger once per press.
                down = bool(ctypes.windll.user32.GetAsyncKeyState(0x20) & 0x8000)
                if down and not self.space_held:
                    self.capture()
                self.space_held = down
        except Exception as exc:
            self.status_var.set(f"Cursor read failed: {exc}")
        finally:
            self.after(90, self._update_cursor)

    def capture(self):
        try:
            point = pyautogui.position()
            x, y = int(point.x), int(point.y)
            red, green, blue = pyautogui.pixel(x, y)
        except Exception as exc:
            self.status_var.set(f"Capture failed: {exc}")
            return
        self.last_capture = (x, y, (red, green, blue))
        self.x_var.set(str(x))
        self.y_var.set(str(y))
        self.rgb_var.set(f"{red}, {green}, {blue}")
        hex_code = f"#{red:02X}{green:02X}{blue:02X}"
        self.hex_var.set(f"HEX  {hex_code}")
        self.color_swatch.configure(bg=hex_code)
        self.status_var.set(f"Captured ({x}, {y})  •  Click a copy button to reuse it.")

    def _copy(self, text, message):
        self.clipboard_clear()
        self.clipboard_append(text)
        self.update_idletasks()
        self.status_var.set(message)

    def _captured(self):
        if self.last_capture is None:
            self.status_var.set("Capture a point first using Space or Capture now.")
            return None
        return self.last_capture

    def copy_coordinates(self):
        value = self._captured()
        if value:
            self._copy(f"{value[0]}, {value[1]}", "Copied X, Y to clipboard.")

    def copy_rgb(self):
        value = self._captured()
        if value:
            self._copy(", ".join(map(str, value[2])), "Copied RGB color to clipboard.")

    def copy_click(self):
        value = self._captured()
        if value:
            self._copy(f"pyautogui.click({value[0]}, {value[1]})", "Copied PyAutoGUI click code.")

    def toggle_pin(self):
        self.is_pinned = not self.is_pinned
        self.attributes("-topmost", self.is_pinned)
        self.pin_btn.configure(text="Unpin window" if self.is_pinned else "Pin on top")


if __name__ == "__main__":
    CoordinateFinder().mainloop()
'''


@dataclass(frozen=True)
class Command:
    name: str
    category: str
    description: str
    snippet: str
    imports: tuple[str, ...] = ()
    packages: tuple[str, ...] = ()
    note: str = ""


def cmd(name, category, description, snippet, imports=(), packages=(), note=""):
    return Command(name, category, description, snippet.strip(), tuple(imports), tuple(packages), note)


P = ("import pyautogui",)
PIP = ("pyautogui",)
COMMANDS = [
    cmd("Pause for 1 second", "Time & Delays", "Stops your script for a given number of seconds.",
        "sleep(1)", ("from time import sleep",)),
    cmd("Pause for 0.2 seconds", "Time & Delays", "Sleep accepts decimals for short delays.",
        "sleep(0.2)", ("from time import sleep",)),
    cmd("Pause with time.sleep", "Time & Delays", "Alternative to importing sleep directly.",
        "time.sleep(2)", ("import time",)),
    cmd("Elapsed time (monotonic)", "Time & Delays", "Time how long an operation takes without relying on the system clock.",
        "start = time.monotonic()\nsleep(1)\nprint(f\"Elapsed: {time.monotonic() - start:.2f}s\")", ("import time", "from time import sleep")),
    cmd("Time right now", "Time & Delays", "Get the current Unix timestamp.",
        "timestamp = time.time()\nprint(timestamp)", ("import time",)),
    cmd("Wait until a time", "Time & Delays", "Keep checking the clock until a deadline has passed.",
        "deadline = time.monotonic() + 3\nwhile time.monotonic() < deadline:\n    sleep(0.1)", ("import time", "from time import sleep")),
    cmd("Click mouse", "Mouse", "Left-click the current cursor position.",
        "pyautogui.click()", P, PIP),
    cmd("Click specific coordinates", "Mouse", "Click an exact X/Y position on screen.",
        "pyautogui.click(x=500, y=300)", P, PIP),
    cmd("Double click", "Mouse", "Double-click the cursor position.",
        "pyautogui.doubleClick()", P, PIP),
    cmd("Right click", "Mouse", "Right-click the cursor position.",
        "pyautogui.rightClick()", P, PIP),
    cmd("Middle click", "Mouse", "Press the middle mouse button.",
        "pyautogui.middleClick()", P, PIP),
    cmd("Click repeatedly", "Mouse", "Click a fixed number of times with a pause between clicks.",
        "pyautogui.click(clicks=5, interval=0.2)", P, PIP),
    cmd("Move cursor", "Mouse", "Move the mouse to X/Y over a number of seconds.",
        "pyautogui.moveTo(600, 400, duration=0.5)", P, PIP),
    cmd("Move cursor relative", "Mouse", "Move relative to the cursor's current position.",
        "pyautogui.moveRel(100, -50, duration=0.3)", P, PIP),
    cmd("Drag cursor to position", "Mouse", "Hold left mouse button while moving to a target.",
        "pyautogui.dragTo(800, 450, duration=1, button='left')", P, PIP),
    cmd("Drag cursor relative", "Mouse", "Drag by an offset from the cursor's current position.",
        "pyautogui.dragRel(200, 0, duration=0.6, button='left')", P, PIP),
    cmd("Mouse button down", "Mouse", "Press and hold a mouse button. Release with mouseUp.",
        "pyautogui.mouseDown(button='left')", P, PIP),
    cmd("Mouse button up", "Mouse", "Release a held mouse button.",
        "pyautogui.mouseUp(button='left')", P, PIP),
    cmd("Scroll up or down", "Mouse", "Scroll by a specified number of steps; negative scrolls down.",
        "pyautogui.scroll(-5)", P, PIP),
    cmd("Horizontal scroll", "Mouse", "Scroll horizontally on systems/apps that support it.",
        "pyautogui.hscroll(5)", P, PIP),
    cmd("Get mouse position", "Mouse", "Read the current cursor coordinates.",
        "x, y = pyautogui.position()\nprint(x, y)", P, PIP),
    cmd("Show mouse position live", "Mouse", "Print cursor X/Y until Ctrl+C is pressed in the terminal.",
        "try:\n    while True:\n        print(pyautogui.position(), end='\\r', flush=True)\n        sleep(0.1)\nexcept KeyboardInterrupt:\n    print()", P + ("from time import sleep",), PIP),
    cmd("Type text", "Keyboard", "Type characters into the currently focused field.",
        "pyautogui.write('Hello world!', interval=0.05)", P, PIP),
    cmd("Press a key", "Keyboard", "Press and release a single key.",
        "pyautogui.press('enter')", P, PIP),
    cmd("Press repeatedly", "Keyboard", "Press a key multiple times with optional interval.",
        "pyautogui.press('tab', presses=3, interval=0.1)", P, PIP),
    cmd("Hotkey / shortcut", "Keyboard", "Press multiple keys together, in order, then release them.",
        "pyautogui.hotkey('ctrl', 's')", P, PIP),
    cmd("Hold a key down", "Keyboard", "Hold a key using a context manager for automatic release.",
        "with pyautogui.hold('shift'):\n    pyautogui.press('a')", P, PIP),
    cmd("Key down", "Keyboard", "Press and hold a key until keyUp is called.",
        "pyautogui.keyDown('shift')", P, PIP),
    cmd("Key up", "Keyboard", "Release a key held down earlier.",
        "pyautogui.keyUp('shift')", P, PIP),
    cmd("Select all", "Keyboard", "Send the Ctrl+A shortcut to the focused app.",
        "pyautogui.hotkey('ctrl', 'a')", P, PIP),
    cmd("Copy selection", "Keyboard", "Send the Ctrl+C shortcut.",
        "pyautogui.hotkey('ctrl', 'c')", P, PIP),
    cmd("Paste clipboard", "Keyboard", "Send the Ctrl+V shortcut.",
        "pyautogui.hotkey('ctrl', 'v')", P, PIP),
    cmd("List supported key names", "Keyboard", "Inspect valid PyAutoGUI key strings.",
        "print(pyautogui.KEYBOARD_KEYS)", P, PIP),
    cmd("Get screen size", "Screen & Images", "Return the primary screen width and height.",
        "width, height = pyautogui.size()\nprint(width, height)", P, PIP),
    cmd("Check coordinates", "Screen & Images", "See whether an X/Y pair falls on screen.",
        "print(pyautogui.onScreen(500, 300))", P, PIP),
    cmd("Take screenshot", "Screen & Images", "Capture your screen and write to an image file.",
        "pyautogui.screenshot().save('screenshot.png')", P, PIP),
    cmd("Screenshot a region", "Screen & Images", "Capture left, top, width, height.",
        "image = pyautogui.screenshot(region=(100, 100, 400, 300))\nimage.save('region.png')", P, PIP),
    cmd("Find image on screen", "Screen & Images", "Find a small reference image visible on the screen.",
        "box = pyautogui.locateOnScreen('button.png')\nprint(box)", P, PIP,
        "PyAutoGUI may raise ImageNotFoundException if the image isn't found."),
    cmd("Find center of image", "Screen & Images", "Find the center point of a matching reference image.",
        "point = pyautogui.locateCenterOnScreen('button.png')\nprint(point)", P, PIP,
        "PyAutoGUI may raise ImageNotFoundException if there is no match."),
    cmd("Find image and click", "Screen & Images", "Look for an image and click its center, safely handling missing matches.",
        "try:\n    point = pyautogui.locateCenterOnScreen('button.png')\n    if point is not None:\n        pyautogui.click(point)\nexcept pyautogui.ImageNotFoundException:\n    print('Image not found')", P, PIP),
    cmd("Find with confidence", "Screen & Images", "Use a similarity threshold instead of an exact pixel match.",
        "point = pyautogui.locateCenterOnScreen('button.png', confidence=0.9)\nprint(point)", P, ("pyautogui", "opencv-python"),
        "The confidence option additionally requires opencv-python."),
    cmd("Read pixel color", "Screen & Images", "Get RGB color at a screen coordinate.",
        "rgb = pyautogui.pixel(300, 200)\nprint(rgb)", P, PIP),
    cmd("Match pixel color", "Screen & Images", "Check whether an RGB color matches (with tolerance).",
        "matches = pyautogui.pixelMatchesColor(300, 200, (255, 0, 0), tolerance=10)\nprint(matches)", P, PIP),
    cmd("Mouse fail-safe", "PyAutoGUI Settings", "Turn on emergency stop by moving mouse to a screen corner.",
        "pyautogui.FAILSAFE = True", P, PIP,
        "Strongly recommended. Moving the pointer to a fail-safe corner can interrupt automation."),
    cmd("Default pause after calls", "PyAutoGUI Settings", "Add a small delay after PyAutoGUI operations.",
        "pyautogui.PAUSE = 0.2", P, PIP),
    cmd("Show alert", "Dialogs", "Display a message box that waits for OK.",
        "pyautogui.alert('Finished!', title='Automation')", P, PIP),
    cmd("Ask for confirmation", "Dialogs", "Display OK/Cancel and return the clicked button label.",
        "result = pyautogui.confirm('Continue?')\nprint(result)", P, PIP),
    cmd("Ask for text", "Dialogs", "Display a text input dialog.",
        "name = pyautogui.prompt('What is your name?')\nprint(name)", P, PIP),
    cmd("Random integer", "Random & Helpers", "Generate a random whole number in an inclusive range.",
        "number = random.randint(1, 10)\nprint(number)", ("import random",)),
    cmd("Random decimal", "Random & Helpers", "Generate a random floating-point value in a range.",
        "delay = random.uniform(0.3, 0.8)\nprint(delay)", ("import random",)),
    cmd("Randomized pause", "Random & Helpers", "Sleep for a randomly selected amount of time.",
        "sleep(random.uniform(0.3, 0.8))", ("import random", "from time import sleep")),
    cmd("Counted loop", "Random & Helpers", "Repeat an action a fixed number of times.",
        "for i in range(5):\n    print('Iteration', i + 1)"),
    cmd("While loop", "Random & Helpers", "Repeat while a condition remains true.",
        "counter = 0\nwhile counter < 5:\n    print(counter)\n    counter += 1"),
    cmd("Try / except", "Random & Helpers", "Catch and handle a specific error.",
        "try:\n    result = int('not a number')\nexcept ValueError as error:\n    print('Invalid number:', error)"),
    cmd("Define a function", "Random & Helpers", "Group reusable actions into a function.",
        "def greet(name):\n    print(f'Hello, {name}!')\n\ngreet('Alex')"),
    cmd("Read clipboard text", "Clipboard", "Read text from your system clipboard.",
        "text = pyperclip.paste()\nprint(text)", ("import pyperclip",), ("pyperclip",)),
    cmd("Copy to clipboard", "Clipboard", "Put text into your system clipboard.",
        "pyperclip.copy('Hello from Python')", ("import pyperclip",), ("pyperclip",)),
    cmd("Read a text file", "Files & System", "Load a UTF-8 text file using pathlib.",
        "content = Path('notes.txt').read_text(encoding='utf-8')\nprint(content)", ("from pathlib import Path",)),
    cmd("Write a text file", "Files & System", "Write text to a UTF-8 file.",
        "Path('notes.txt').write_text('Hello world!', encoding='utf-8')", ("from pathlib import Path",)),
    cmd("Create a folder", "Files & System", "Create a directory if it doesn't exist.",
        "Path('output').mkdir(parents=True, exist_ok=True)", ("from pathlib import Path",)),
    cmd("List files in a folder", "Files & System", "Print file names from your working directory.",
        "for item in Path('.').iterdir():\n    print(item.name)", ("from pathlib import Path",)),
    cmd("Get current directory", "Files & System", "Print the directory your script is running from.",
        "print(Path.cwd())", ("from pathlib import Path",)),
    cmd("Launch an application", "Files & System", "Start an application with arguments using subprocess.",
        "subprocess.Popen(['notepad.exe'])", ("import subprocess",), (),
        "This example opens Notepad on Windows."),
    cmd("Check platform", "Files & System", "Identify which operating system is running.",
        "print(platform.system())", ("import platform",)),
]



# Editor autocomplete and import detection are implemented entirely with Python's
# standard library. You can browse and write without installing PyAutoGUI.
@dataclass(frozen=True)
class Completion:
    label: str
    text: str
    description: str
    imports: tuple[str, ...] = ()


def completion(label, text, description, *imports):
    return Completion(label, text, description, tuple(imports))


MODULE_METHODS = {
    "pyautogui": [
        completion("click()", "click()", "Click the current mouse position", "import pyautogui"),
        completion("click(x=500, y=300)", "click(x=500, y=300)", "Click a specific X/Y position", "import pyautogui"),
        completion("doubleClick()", "doubleClick()", "Double click", "import pyautogui"),
        completion("rightClick()", "rightClick()", "Right click", "import pyautogui"),
        completion("middleClick()", "middleClick()", "Middle mouse click", "import pyautogui"),
        completion("moveTo(500, 300)", "moveTo(500, 300)", "Move mouse to X/Y", "import pyautogui"),
        completion("moveRel(100, 0)", "moveRel(100, 0)", "Move mouse by a relative offset", "import pyautogui"),
        completion("dragTo(500, 300, duration=0.5)", "dragTo(500, 300, duration=0.5)", "Drag mouse to X/Y", "import pyautogui"),
        completion("dragRel(100, 0, duration=0.5)", "dragRel(100, 0, duration=0.5)", "Drag relative to cursor", "import pyautogui"),
        completion("mouseDown()", "mouseDown()", "Hold mouse button", "import pyautogui"),
        completion("mouseUp()", "mouseUp()", "Release mouse button", "import pyautogui"),
        completion("scroll(-3)", "scroll(-3)", "Scroll down; positive scrolls up", "import pyautogui"),
        completion("hscroll(3)", "hscroll(3)", "Horizontal scroll", "import pyautogui"),
        completion("position()", "position()", "Get cursor X/Y", "import pyautogui"),
        completion("size()", "size()", "Get screen dimensions", "import pyautogui"),
        completion("onScreen(500, 300)", "onScreen(500, 300)", "Check a point is on screen", "import pyautogui"),
        completion("write('hello')", "write('hello')", "Type text into the focused window", "import pyautogui"),
        completion("press('enter')", "press('enter')", "Tap a keyboard key", "import pyautogui"),
        completion("hotkey('ctrl', 's')", "hotkey('ctrl', 's')", "Press a keyboard shortcut", "import pyautogui"),
        completion("keyDown('shift')", "keyDown('shift')", "Hold a key", "import pyautogui"),
        completion("keyUp('shift')", "keyUp('shift')", "Release a key", "import pyautogui"),
        completion("hold('shift')", "hold('shift')", "Context manager for holding a key", "import pyautogui"),
        completion("screenshot()", "screenshot()", "Capture screen image", "import pyautogui"),
        completion("locateOnScreen('button.png')", "locateOnScreen('button.png')", "Find image on screen", "import pyautogui"),
        completion("locateCenterOnScreen('button.png')", "locateCenterOnScreen('button.png')", "Find image center on screen", "import pyautogui"),
        completion("pixel(100, 100)", "pixel(100, 100)", "Read screen pixel RGB", "import pyautogui"),
        completion("pixelMatchesColor(100, 100, (255, 0, 0))", "pixelMatchesColor(100, 100, (255, 0, 0))", "Compare a pixel's color", "import pyautogui"),
        completion("alert('Finished')", "alert('Finished')", "Display an alert window", "import pyautogui"),
        completion("confirm('Continue?')", "confirm('Continue?')", "Display confirm/cancel dialog", "import pyautogui"),
        completion("prompt('Enter text')", "prompt('Enter text')", "Prompt user for text", "import pyautogui"),
        completion("FAILSAFE", "FAILSAFE", "PyAutoGUI emergency-stop setting", "import pyautogui"),
        completion("PAUSE", "PAUSE", "Delay between PyAutoGUI commands", "import pyautogui"),
    ],
    "time": [
        completion("sleep(1)", "sleep(1)", "Pause for 1 second", "import time"),
        completion("time()", "time()", "Current Unix time", "import time"),
        completion("monotonic()", "monotonic()", "Monotonic clock", "import time"),
        completion("perf_counter()", "perf_counter()", "High-resolution timer", "import time"),
    ],
    "random": [
        completion("randint(1, 10)", "randint(1, 10)", "Random integer", "import random"),
        completion("uniform(0.2, 0.8)", "uniform(0.2, 0.8)", "Random decimal", "import random"),
        completion("choice([1, 2, 3])", "choice([1, 2, 3])", "Choose a random item", "import random"),
    ],
    "pyperclip": [
        completion("copy('text')", "copy('text')", "Copy text to clipboard", "import pyperclip"),
        completion("paste()", "paste()", "Read clipboard", "import pyperclip"),
    ],
    "subprocess": [
        completion("Popen(['notepad.exe'])", "Popen(['notepad.exe'])", "Start another program", "import subprocess"),
        completion("run(['python', '--version'])", "run(['python', '--version'])", "Run a program and wait", "import subprocess"),
    ],
    "platform": [completion("system()", "system()", "Name of operating system", "import platform")],
    "Path": [
        completion("cwd()", "cwd()", "Current working directory", "from pathlib import Path"),
        completion("home()", "home()", "User's home folder", "from pathlib import Path"),
        completion("exists()", "exists()", "Check a file/folder exists", "from pathlib import Path"),
        completion("mkdir(parents=True, exist_ok=True)", "mkdir(parents=True, exist_ok=True)", "Create folder", "from pathlib import Path"),
        completion("read_text(encoding='utf-8')", "read_text(encoding='utf-8')", "Read a text file", "from pathlib import Path"),
    ],
}

BARE_COMPLETIONS = [
    completion("pyautogui", "pyautogui", "Mouse, keyboard, and screen automation"),
    completion("pyautogui.click()", "pyautogui.click()", "Click with the mouse", "import pyautogui"),
    completion("pyautogui.moveTo(500, 300)", "pyautogui.moveTo(500, 300)", "Move mouse to X/Y", "import pyautogui"),
    completion("pyautogui.press('enter')", "pyautogui.press('enter')", "Press a key", "import pyautogui"),
    completion("pyautogui.hotkey('ctrl', 's')", "pyautogui.hotkey('ctrl', 's')", "Keyboard shortcut", "import pyautogui"),
    completion("pyautogui.screenshot()", "pyautogui.screenshot()", "Capture screen", "import pyautogui"),
    completion("sleep(1)", "sleep(1)", "Pause 1 second", "from time import sleep"),
    completion("time.sleep(1)", "time.sleep(1)", "Pause 1 second using time", "import time"),
    completion("time", "time", "Time functions"),
    completion("random", "random", "Random numbers"),
    completion("pyperclip", "pyperclip", "Copy and paste clipboard"),
    completion("subprocess", "subprocess", "Launch other programs"),
    completion("platform", "platform", "Operating system information"),
    completion("Path('file.txt')", "Path('file.txt')", "Work with a path", "from pathlib import Path"),
    completion("print()", "print()", "Display a value"),
    completion("range(5)", "range(5)", "Make a counted sequence"),
]

AUTO_IMPORTS = {
    "pyautogui": "import pyautogui",
    "time": "import time",
    "random": "import random",
    "pyperclip": "import pyperclip",
    "subprocess": "import subprocess",
    "platform": "import platform",
    "Path": "from pathlib import Path",
}


def completion_context(line: str):
    """Returns (start offset from end, module or None, partial text)."""
    # Suggestions only in code, not after a comment or within a quoted literal.
    # Lightweight line scanner handles ordinary strings while typing incomplete code.
    in_quote = None
    triple = False
    escaped = False
    i = 0
    while i < len(line):
        ch = line[i]
        if escaped:
            escaped = False
            i += 1
            continue
        if in_quote:
            if ch == "\\":
                escaped = True
            elif triple and line[i:i+3] == in_quote * 3:
                in_quote = None
                triple = False
                i += 3
                continue
            elif not triple and ch == in_quote:
                in_quote = None
        elif ch == "#":
            return None
        elif ch in ("'", '"'):
            in_quote = ch
            triple = line[i:i+3] == ch * 3
            if triple:
                i += 3
                continue
        i += 1
    if in_quote:
        return None
    member = re.search(r"\b([A-Za-z_]\w*)\.([A-Za-z_]\w*)?$", line)
    if member and member.group(1) in MODULE_METHODS:
        return (len(member.group(2) or ""), member.group(1), member.group(2) or "")
    word = re.search(r"\b([A-Za-z_]\w*)$", line)
    if word and len(word.group(1)) >= 2:
        # Avoid completing within an import line or after an attribute of an unknown object.
        if re.match(r"\s*(?:from\s+|import\s+)", line) or line[:word.start()].endswith("."):
            return None
        return (len(word.group(1)), None, word.group(1))
    return None


def ranked_suggestions(module, partial):
    options = MODULE_METHODS[module] if module else BARE_COMPLETIONS
    if not partial:
        return options[:8]
    prefix = partial.casefold()
    starts = [opt for opt in options if opt.label.casefold().startswith(prefix)]
    # Include likely fixes for misspelled names without aggressively guessing.
    if not starts:
        names = [item.label.split("(")[0].casefold() for item in options]
        nearest = difflib.get_close_matches(prefix, names, n=5, cutoff=0.58)
        starts = [item for name in nearest for item in options if item.label.split("(")[0].casefold() == name]
    return starts[:8]


def needed_imports(source: str):
    """Detect the APIs used in an unfinished Python editor buffer.

    tokenize makes it safe to ignore commands written in comments or strings,
    and allows us to detect calls while parentheses are still incomplete.
    """
    uses = set()
    try:
        stream = tokenize.generate_tokens(io.StringIO(source).readline)
        toks = [t for t in stream if t.type not in (tokenize.COMMENT, tokenize.STRING,
                  tokenize.ENCODING, tokenize.NL, tokenize.NEWLINE,
                  tokenize.INDENT, tokenize.DEDENT, tokenize.ENDMARKER)]
    except tokenize.TokenError:
        # Code can be unfinished; use tokens accumulated up to the error.
        toks = []
        stream = tokenize.generate_tokens(io.StringIO(source).readline)
        try:
            while True:
                tok = next(stream)
                if tok.type not in (tokenize.COMMENT, tokenize.STRING,
                    tokenize.ENCODING, tokenize.NL, tokenize.NEWLINE,
                    tokenize.INDENT, tokenize.DEDENT, tokenize.ENDMARKER):
                    toks.append(tok)
        except (tokenize.TokenError, StopIteration):
            pass
    for i, tok in enumerate(toks):
        if tok.type != tokenize.NAME:
            continue
        prev = toks[i-1] if i else None
        follow = toks[i+1] if i+1 < len(toks) else None
        # Never add imports because they are merely written in import statements.
        this_line = source.splitlines()[tok.start[0]-1] if tok.start[0] <= len(source.splitlines()) else ""
        if re.match(r"\s*(import|from)\b", this_line):
            continue
        if prev and prev.string == ".":
            continue
        if tok.string in AUTO_IMPORTS and follow and follow.string == ".":
            uses.add(AUTO_IMPORTS[tok.string])
        elif tok.string == "Path" and follow and follow.string == "(":
            uses.add(AUTO_IMPORTS["Path"])
        elif tok.string == "sleep" and follow and follow.string == "(":
            if not (prev and prev.string in ("def", ".")):
                uses.add("from time import sleep")
    existing = set()
    for line in source.splitlines():
        if not re.match(r"\s*(?:from|import)\s+", line):
            continue
        try:
            parsed = ast.parse(line.lstrip())
        except SyntaxError:
            continue
        for node in parsed.body:
            if isinstance(node, ast.Import):
                for alias in node.names:
                    if alias.asname is None:
                        existing.add(f"import {alias.name}")
            elif isinstance(node, ast.ImportFrom):
                if node.module:
                    for alias in node.names:
                        if alias.asname is None:
                            existing.add(f"from {node.module} import {alias.name}")
    # Preserved stable order makes saved scripts less noisy.
    return [value for value in ("import pyautogui", "import time", "from time import sleep", "import random",
                                "import pyperclip", "import subprocess", "import platform",
                                "from pathlib import Path") if value in uses and value not in existing]


def import_insertion_line(source: str):
    """Insert imports after shebang, leading comments, docstring and __future__ imports."""
    lines = source.splitlines(keepends=True)
    i = 0
    while i < len(lines) and (not lines[i].strip() or lines[i].lstrip().startswith("#")):
        i += 1
    try:
        tree = ast.parse(source)
        if tree.body and isinstance(tree.body[0], ast.Expr) and isinstance(tree.body[0].value, ast.Constant) and isinstance(tree.body[0].value.value, str):
            i = max(i, tree.body[0].end_lineno)
    except SyntaxError:
        pass
    while i < len(lines) and re.match(r"\s*from\s+__future__\s+import\b", lines[i]):
        i += 1
    return i


BG = "#0d1117"
SURFACE = "#161c27"
SURFACE_2 = "#1c2533"
BORDER = "#2c3849"
TEXT = "#e6edf7"
MUTED = "#9cacc1"
ACCENT = "#5ca9ff"
ACCENT_2 = "#234264"
GREEN = "#9be0bc"
MONO = ("Consolas", 10)


class ReferenceApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Python Command Explorer")
        self.geometry("1220x790")
        self.minsize(990, 650)
        self.configure(bg=BG)
        self.active_command: Command | None = None
        self.visible_commands: list[Command] = []
        self.file_path: Path | None = None
        self.suggested_filename = "automation_script.py"
        self.search_var = tk.StringVar()
        self.category_var = tk.StringVar(value="All categories")
        self.status_var = tk.StringVar(value="Select a command to see its imports and example.")
        self.count_var = tk.StringVar()
        self._editor_change_job = None
        self._import_job = None
        self._completions = []
        self._completion_ctx = None
        self._popup = None
        self._popup_list = None
        self._popup_description = None
        self._style()
        self._layout()
        self.search_var.trace_add("write", lambda *_: self._filter_commands())
        self._filter_commands()
        self.bind("<Control-s>", lambda _: self.save_script())
        self.bind("<Control-o>", lambda _: self.open_script())
        self.bind("<Control-f>", self.focus_search)
        self.bind("<Control-space>", self.force_autocomplete)
        self.bind("<Escape>", lambda _: self.search_var.set("") if self.focus_get() == self.search_entry else None)
        self.after_idle(self.search_entry.focus_set)
        self.protocol("WM_DELETE_WINDOW", self.close_window)

    def _style(self):
        style = ttk.Style(self)
        style.theme_use("clam")
        style.configure("Dark.TCombobox", fieldbackground=SURFACE_2, background=SURFACE_2,
                        foreground=TEXT, arrowcolor=TEXT, bordercolor=BORDER, padding=6)
        style.map("Dark.TCombobox", fieldbackground=[("readonly", SURFACE_2)],
                  selectbackground=[("readonly", SURFACE_2)], selectforeground=[("readonly", TEXT)])
        self.option_add("*TCombobox*Listbox.background", SURFACE_2)
        self.option_add("*TCombobox*Listbox.foreground", TEXT)

    def _label(self, parent, text, size=10, fg=TEXT, bold=False):
        font = ("Segoe UI", size, "bold" if bold else "normal")
        return tk.Label(parent, text=text, fg=fg, bg=parent.cget("bg"), font=font,
                        anchor="w", justify="left")

    def _button(self, parent, text, handler, primary=False):
        button = tk.Button(parent, text=text, command=handler, cursor="hand2", relief="flat",
                           bg=ACCENT if primary else SURFACE_2,
                           fg="#071626" if primary else TEXT,
                           activebackground="#86c1ff" if primary else ACCENT_2,
                           activeforeground="#071626" if primary else TEXT,
                           font=("Segoe UI", 9, "bold"), padx=12, pady=7,
                           bd=0, highlightthickness=0)
        return button

    def _layout(self):
        header = tk.Frame(self, bg=BG, padx=22, pady=15)
        header.pack(fill="x")
        brand = tk.Frame(header, bg=BG)
        brand.pack(side="left", fill="x", expand=True)
        self._label(brand, "PYTHON COMMAND EXPLORER", 17, bold=True).pack(anchor="w")
        self._label(brand, "Explore commands  /  Autocomplete  /  Build complete scripts",
                    10, MUTED).pack(anchor="w", pady=(5, 0))
        template_area = tk.Frame(header, bg=BG)
        template_area.pack(side="right", padx=(12, 0))
        self._label(template_area, "TOOLS", 8, GREEN,
                    bold=True).pack(anchor="e", pady=(0, 5))
        self._button(template_area, "Coordinate Finder", self.open_coordinate_finder,
                     primary=True).pack(anchor="e")

        content = tk.PanedWindow(self, orient="horizontal", bg=BG, sashwidth=7,
                                 sashrelief="flat", borderwidth=0, showhandle=False)
        content.pack(fill="both", expand=True, padx=16, pady=(0, 8))

        left = tk.Frame(content, bg=SURFACE, padx=14, pady=14)
        content.add(left, minsize=260, width=360)
        self._label(left, "SEARCH COMMANDS", 12, bold=True).pack(anchor="w", pady=(0, 12))
        self.search_entry = tk.Entry(left, textvariable=self.search_var, bg=SURFACE_2, fg=TEXT,
                                     insertbackground=TEXT, relief="flat", font=("Segoe UI", 11),
                                     highlightbackground=BORDER, highlightcolor=ACCENT,
                                     highlightthickness=1)
        self.search_entry.pack(fill="x", ipady=10)
        self._label(left, "Try: sleep, click, screenshot, hotkey, clipboard...", 9, MUTED).pack(
            anchor="w", pady=(7, 0))
        self.category_menu = ttk.Combobox(left, textvariable=self.category_var, state="readonly",
                                          style="Dark.TCombobox",
                                          values=["All categories"] + sorted({item.category for item in COMMANDS}))
        self.category_menu.pack(fill="x", pady=(12, 12))
        self.category_menu.bind("<<ComboboxSelected>>", lambda _: self._filter_commands())
        self._label(left, "CLICK A COMMAND TO SEE ITS IMPORT", 9, MUTED, bold=True).pack(anchor="w", pady=(2, 8))
        list_frame = tk.Frame(left, bg=SURFACE)
        list_frame.pack(fill="both", expand=True)
        scroll = tk.Scrollbar(list_frame, bg=SURFACE_2, troughcolor=SURFACE,
                              activebackground=ACCENT_2)
        scroll.pack(side="right", fill="y")
        self.command_list = tk.Listbox(list_frame, bg=SURFACE, fg=TEXT, selectbackground=ACCENT_2,
                                       selectforeground="#ffffff", activestyle="none", relief="flat",
                                       bd=0, highlightthickness=0, font=("Segoe UI", 10),
                                       yscrollcommand=scroll.set, exportselection=False)
        self.command_list.pack(side="left", fill="both", expand=True)
        scroll.configure(command=self.command_list.yview)
        self.command_list.bind("<<ListboxSelect>>", self._select_command)
        tk.Label(left, textvariable=self.count_var, bg=SURFACE, fg=MUTED,
                 font=("Segoe UI", 9)).pack(anchor="w", pady=(10, 0))

        right = tk.PanedWindow(content, orient="vertical", bg=BG, sashwidth=7,
                               sashrelief="flat", borderwidth=0, showhandle=False)
        content.add(right, minsize=490)

        detail = tk.Frame(right, bg=SURFACE, padx=18, pady=14)
        right.add(detail, minsize=230, height=350)
        detail_header = tk.Frame(detail, bg=SURFACE)
        detail_header.pack(fill="x")
        self.title_var = tk.StringVar(value="Choose a command from the left")
        tk.Label(detail_header, textvariable=self.title_var, bg=SURFACE, fg=TEXT,
                 font=("Segoe UI", 14, "bold"), anchor="w").pack(anchor="w", fill="x")
        self.desc_var = tk.StringVar(value="Imports are hidden until you select an item.")
        tk.Label(detail_header, textvariable=self.desc_var, bg=SURFACE, fg=MUTED,
                 font=("Segoe UI", 10), wraplength=800, justify="left", anchor="w").pack(
                 anchor="w", fill="x", pady=(5, 10))

        requirements = tk.Frame(detail, bg=SURFACE_2, padx=12, pady=9,
                                highlightbackground=BORDER, highlightthickness=1)
        requirements.pack(fill="x", pady=(0, 12))
        req_head = tk.Frame(requirements, bg=SURFACE_2)
        req_head.pack(fill="x")
        self._label(req_head, "REQUIRED IMPORTS  •  SHOWN AFTER CLICK", 9, MUTED, bold=True).pack(side="left")
        self.copy_imports_btn = self._button(req_head, "Copy imports", self.copy_imports)
        self.copy_imports_btn.pack(side="right")
        self.requirements_var = tk.StringVar(value="Select a command in the list to reveal its import.")
        tk.Label(requirements, textvariable=self.requirements_var, bg=SURFACE_2,
                 fg=GREEN, font=MONO, anchor="w", justify="left", wraplength=700).pack(
                 anchor="w", fill="x", pady=(7, 0))
        self.install_var = tk.StringVar(value="")
        tk.Label(requirements, textvariable=self.install_var, bg=SURFACE_2,
                 fg=MUTED, font=MONO, anchor="w", justify="left", wraplength=700).pack(
                 anchor="w", fill="x", pady=(4, 0))

        self._label(detail, "EXAMPLE", 9, MUTED, bold=True).pack(anchor="w", pady=(0, 6))
        example_frame = tk.Frame(detail, bg=SURFACE_2, highlightbackground=BORDER,
                                 highlightthickness=1)
        example_frame.pack(fill="both", expand=True)
        self.example = tk.Text(example_frame, bg=SURFACE_2, fg=TEXT, insertbackground=TEXT,
                               font=MONO, relief="flat", bd=0, padx=12, pady=10,
                               wrap="none", height=5, state="disabled")
        ex_yscroll = tk.Scrollbar(example_frame, command=self.example.yview)
        ex_yscroll.pack(side="right", fill="y")
        self.example.configure(yscrollcommand=ex_yscroll.set)
        self.example.pack(fill="both", expand=True)

        action_bar = tk.Frame(detail, bg=SURFACE)
        action_bar.pack(fill="x", pady=(10, 0))
        self.copy_snippet_btn = self._button(action_bar, "Copy example", self.copy_snippet)
        self.copy_snippet_btn.pack(side="left")
        self.insert_btn = self._button(action_bar, "Insert into editor + add imports", self.insert_snippet, primary=True)
        self.insert_btn.pack(side="left", padx=(9, 0))
        self.note_var = tk.StringVar(value="")
        tk.Label(detail, textvariable=self.note_var, bg=SURFACE, fg=MUTED,
                 font=("Segoe UI", 9), justify="left", anchor="w", wraplength=790).pack(
                 fill="x", pady=(9, 0))

        editor_container = tk.Frame(right, bg=SURFACE, padx=18, pady=13)
        right.add(editor_container, minsize=165)
        toolbar = tk.Frame(editor_container, bg=SURFACE)
        toolbar.pack(fill="x", pady=(0, 10))
        self._label(toolbar, "SCRIPT EDITOR", 12, bold=True).pack(side="left")
        tk.Label(toolbar, text="  AUTOCOMPLETE ON  ·  AUTO-IMPORT ON", fg=GREEN, bg=SURFACE,
                 font=("Segoe UI", 8, "bold")).pack(side="left", padx=(8, 0))
        self._button(toolbar, "Clear", self.clear_script).pack(side="right", padx=(8, 0))
        self._button(toolbar, "Save script", self.save_script, primary=True).pack(side="right", padx=(8, 0))
        self._button(toolbar, "Open script", self.open_script).pack(side="right")

        editor_frame = tk.Frame(editor_container, bg=SURFACE_2, highlightbackground=BORDER,
                                highlightthickness=1)
        editor_frame.pack(fill="both", expand=True)
        self.editor = tk.Text(editor_frame, bg=SURFACE_2, fg=TEXT, insertbackground=ACCENT,
                              font=MONO, relief="flat", bd=0, padx=12, pady=12,
                              undo=True, wrap="none", tabs=("32p",))
        editor_yscroll = tk.Scrollbar(editor_frame, command=self.editor.yview)
        editor_yscroll.pack(side="right", fill="y")
        self.editor.configure(yscrollcommand=editor_yscroll.set)
        editor_xscroll = tk.Scrollbar(editor_frame, orient="horizontal", command=self.editor.xview)
        editor_xscroll.pack(side="bottom", fill="x")
        self.editor.configure(xscrollcommand=editor_xscroll.set)
        self.editor.pack(side="left", fill="both", expand=True)
        self.editor.insert("1.0", "# Your Python automation script\n# Type pyauto, press Tab to complete, or search commands on the left.\n\n")
        self.editor.bind("<<Modified>>", self.editor_modified)
        self.editor.bind("<KeyRelease>", self.editor_key_released)
        self.editor.bind("<ButtonRelease-1>", self.editor_cursor_moved)
        self.editor.bind("<Tab>", self.complete_on_tab)
        self.editor.bind("<Down>", lambda event: self.move_popup_selection(1))
        self.editor.bind("<Up>", lambda event: self.move_popup_selection(-1))
        self.editor.bind("<Escape>", self.escape_autocomplete)
        self.editor.bind("<FocusOut>", lambda _: self.after(130, self.hide_popup_if_unfocused))
        self.editor.edit_modified(False)
        self._label(editor_container, "Type pyauto → Tab to complete  •  ↑/↓ to choose  •  Ctrl+Space for suggestions  •  Missing imports added automatically", 
                    9, MUTED).pack(anchor="w", pady=(7, 0))

        footer = tk.Frame(self, bg=BG, padx=19, pady=7)
        footer.pack(fill="x")
        tk.Label(footer, textvariable=self.status_var, bg=BG, fg=MUTED,
                 font=("Segoe UI", 9), anchor="w").pack(fill="x")

    def _filter_commands(self):
        search = self.search_var.get().lower().strip()
        category = self.category_var.get()
        self.visible_commands = [c for c in COMMANDS if
                                 (category == "All categories" or c.category == category)
                                 and (not search or search in (c.name + " " + c.category + " " +
                                                              c.description + " " + c.snippet + " " +
                                                              " ".join(c.imports)).lower())]
        self.command_list.delete(0, "end")
        for command in self.visible_commands:
            self.command_list.insert("end", "  " + command.name)
        self.count_var.set(f"{len(self.visible_commands)} command{'s' if len(self.visible_commands) != 1 else ''} found")
        if not self.visible_commands:
            self.status_var.set("No matching commands. Try a different search.")

    def _select_command(self, _event=None):
        indices = self.command_list.curselection()
        if not indices:
            return
        command = self.visible_commands[indices[0]]
        self.active_command = command
        self.title_var.set(command.name)
        self.desc_var.set(command.description)
        self.requirements_var.set("\n".join(command.imports) if command.imports else "No imports required (built into Python).")
        self.install_var.set("Install first:  python -m pip install " + " ".join(command.packages)
                             if command.packages else "No extra pip packages required.")
        self.example.configure(state="normal")
        self.example.delete("1.0", "end")
        self.example.insert("1.0", command.snippet)
        self.example.configure(state="disabled")
        self.note_var.set(command.note)
        self.status_var.set(f"Selected: {command.name}  •  {command.category}")

    def _clipboard(self, value, label):
        self.clipboard_clear()
        self.clipboard_append(value)
        self.update_idletasks()
        self.status_var.set(label)

    def copy_snippet(self):
        if self.active_command:
            self._clipboard(self.active_command.snippet, "Example copied to clipboard.")

    def copy_imports(self):
        if self.active_command:
            if self.active_command.imports:
                self._clipboard("\n".join(self.active_command.imports), "Required imports copied.")
            else:
                self.status_var.set("This example doesn't need imports.")

    def focus_search(self, _event=None):
        self.hide_popup()
        self.search_entry.focus_set()
        self.search_entry.selection_range(0, "end")
        return "break"

    def editor_modified(self, _event=None):
        if not self.editor.edit_modified():
            return
        self.editor.edit_modified(False)
        if self._editor_change_job:
            self.after_cancel(self._editor_change_job)
        if self._import_job:
            self.after_cancel(self._import_job)
        self._editor_change_job = self.after(90, self.refresh_autocomplete)
        self._import_job = self.after(270, self.auto_add_imports)

    def editor_key_released(self, event):
        if event.keysym not in ("Up", "Down", "Tab", "Escape", "Return"):
            self.after_idle(self.refresh_autocomplete)
        elif event.keysym == "Return":
            self.hide_popup()

    def editor_cursor_moved(self, _event=None):
        self.after_idle(self.refresh_autocomplete)

    def force_autocomplete(self, _event=None):
        if self.focus_get() != self.editor:
            self.editor.focus_set()
        self.refresh_autocomplete(force=True)
        return "break"

    def refresh_autocomplete(self, force=False):
        self._editor_change_job = None
        if self.focus_get() != self.editor:
            self.hide_popup()
            return
        line = self.editor.get("insert linestart", "insert")
        context = completion_context(line)
        if context is None:
            if force:
                self.status_var.set("Type part of a command, like pyauto, sleep, or pyautogui.cl")
            self.hide_popup()
            return
        _length, module, partial = context
        matches = ranked_suggestions(module, partial)
        if not matches:
            self.hide_popup()
            return
        self._completion_ctx = context
        self._completions = matches
        self.show_popup()

    def show_popup(self):
        bbox = self.editor.bbox("insert")
        if bbox is None:
            self.hide_popup()
            return
        if self._popup is None:
            pop = tk.Toplevel(self)
            pop.overrideredirect(True)
            pop.configure(bg=ACCENT, padx=1, pady=1)
            pop.transient(self)
            panel = tk.Frame(pop, bg=SURFACE_2)
            panel.pack(fill="both", expand=True)
            listing = tk.Listbox(panel, bg=SURFACE_2, fg=TEXT, selectbackground=ACCENT_2,
                                 selectforeground="white", relief="flat", highlightthickness=0,
                                 bd=0, font=("Consolas", 10), activestyle="none", exportselection=False,
                                 width=54, height=7)
            listing.pack(fill="both", expand=True)
            listing.bind("<ButtonRelease-1>", self.popup_clicked)
            listing.bind("<Double-Button-1>", self.popup_clicked)
            description = tk.Label(panel, bg=BG, fg=MUTED, font=("Segoe UI", 9),
                                   anchor="w", justify="left", padx=8, pady=5,
                                   text="Tab: insert  ·  ↑/↓: select  ·  Esc: close")
            description.pack(fill="x")
            self._popup = pop
            self._popup_list = listing
            self._popup_description = description
        selected_label = None
        if self._popup_list.curselection():
            ix = self._popup_list.curselection()[0]
            if ix < self._popup_list.size():
                selected_label = self._popup_list.get(ix)
        self._popup_list.delete(0, "end")
        for item in self._completions:
            self._popup_list.insert("end", "  " + item.label)
        self._popup_list.config(height=min(8, len(self._completions)))
        ix = 0
        if selected_label:
            for idx, item in enumerate(self._completions):
                if "  " + item.label == selected_label:
                    ix = idx
                    break
        self._popup_list.selection_set(ix)
        self._popup_list.activate(ix)
        self._update_popup_description()
        x, y, _width, height = bbox
        x += self.editor.winfo_rootx()
        y += self.editor.winfo_rooty() + height + 3
        pop_width = 465
        pop_height = min(8, len(self._completions)) * 20 + 33
        screen_w, screen_h = self.winfo_screenwidth(), self.winfo_screenheight()
        if x + pop_width > screen_w - 10:
            x = max(6, screen_w - pop_width - 12)
        if y + pop_height > screen_h - 25:
            y = max(6, y - pop_height - height - 7)
        self._popup.geometry(f"{pop_width}x{pop_height}+{x}+{y}")
        self._popup.deiconify()
        self._popup.lift()

    def _update_popup_description(self):
        if not self._popup or not self._completions:
            return
        ix = self._popup_list.curselection()
        selected = self._completions[ix[0] if ix else 0]
        suffix = "  •  " + ", ".join(selected.imports) if selected.imports else ""
        self._popup_description.config(text=selected.description + suffix)

    def move_popup_selection(self, direction):
        if not self._popup or not self._completions or self._popup.state() != "normal":
            return None  # Leave the editor's normal cursor movement intact.
        index = self._popup_list.curselection()
        now = index[0] if index else 0
        new = (now + direction) % len(self._completions)
        self._popup_list.selection_clear(0, "end")
        self._popup_list.selection_set(new)
        self._popup_list.activate(new)
        self._popup_list.see(new)
        self._update_popup_description()
        return "break"

    def complete_on_tab(self, _event=None):
        if self._popup and self._completions and self._popup.state() == "normal":
            self.apply_completion()
            return "break"
        return None  # Default Tab inserts an indentation tab.

    def popup_clicked(self, _event=None):
        if self._popup_list and self._popup_list.curselection():
            self.apply_completion(self._popup_list.curselection()[0])
        return "break"

    def apply_completion(self, selected_index=None):
        if not self._completions:
            return
        if selected_index is None:
            selected_index = self._popup_list.curselection()[0] if self._popup_list.curselection() else 0
        item = self._completions[selected_index]
        # Recompute position in case imports inserted above the caret.
        context = completion_context(self.editor.get("insert linestart", "insert"))
        if not context:
            self.hide_popup()
            return
        length, _module, _prefix = context
        start = self.editor.index(f"insert - {length}c")
        self.hide_popup()
        self.editor.delete(start, "insert")
        self.editor.insert("insert", item.text)
        self._add_missing_imports(item.imports)
        self.auto_add_imports()
        self.editor.focus_set()
        self.status_var.set(f"Completed {item.label} · required imports added automatically")

    def escape_autocomplete(self, _event=None):
        if self._popup and self._popup.state() == "normal":
            self.hide_popup()
            return "break"
        return None

    def hide_popup(self):
        if self._popup:
            self._popup.withdraw()
        self._completions = []
        self._completion_ctx = None

    def hide_popup_if_unfocused(self):
        if self.focus_get() not in (self.editor, self._popup_list):
            self.hide_popup()

    def auto_add_imports(self):
        self._import_job = None
        source = self.editor.get("1.0", "end-1c")
        missing = needed_imports(source)
        if missing:
            self._add_missing_imports(missing)
            self.status_var.set("Automatically added required imports: " + ", ".join(missing))

    def _add_missing_imports(self, imports):
        if not imports:
            return
        source = self.editor.get("1.0", "end-1c")
        # Includes imports declared separately within a multi-import line.
        existing_text = source.splitlines()
        missing = []
        for statement in imports:
            if statement not in missing and statement not in existing_text:
                try:
                    # Generic single-line import check (e.g. import os, pyautogui)
                    import_name = ast.parse(statement).body[0]
                    already = False
                    for line in existing_text:
                        try:
                            parsed = ast.parse(line.lstrip())
                        except SyntaxError:
                            continue
                        if not parsed.body:
                            continue
                        node = parsed.body[0]
                        if isinstance(import_name, ast.Import) and isinstance(node, ast.Import):
                            already = any(a.name == import_name.names[0].name and not a.asname for a in node.names)
                        elif isinstance(import_name, ast.ImportFrom) and isinstance(node, ast.ImportFrom):
                            already = (node.module == import_name.module and any(
                                a.name == import_name.names[0].name and not a.asname for a in node.names))
                        if already:
                            break
                    if not already:
                        missing.append(statement)
                except SyntaxError:
                    missing.append(statement)
        if not missing:
            return
        line_idx = import_insertion_line(source)
        raw_lines = source.splitlines(keepends=True)
        before = "".join(raw_lines[:line_idx])
        at = f"{line_idx + 1}.0"
        # If a last line has no final newline, start a fresh line.
        pre_newline = "" if not before or before.endswith("\n") else "\n"
        block = pre_newline + "\n".join(missing) + "\n\n"
        self.editor.insert(at, block)
        self.editor.edit_separator()

    def close_window(self):
        for job in (self._editor_change_job, self._import_job):
            if job:
                try:
                    self.after_cancel(job)
                except tk.TclError:
                    pass
        if self._popup:
            self._popup.destroy()
        self.destroy()

    def insert_snippet(self):
        if not self.active_command:
            self.status_var.set("Choose a command first.")
            return
        self.hide_popup()
        self._add_missing_imports(self.active_command.imports)
        self.editor.insert("end-1c", "\n" + self.active_command.snippet + "\n")
        self.auto_add_imports()
        self.editor.see("end")
        self.status_var.set(f"Inserted {self.active_command.name} and any missing imports.")

    def open_coordinate_finder(self):
        """Open Coordinate Finder in its own process, leaving the editor untouched."""
        self.hide_popup()
        try:
            subprocess.Popen(
                [sys.executable, "-c", COORDINATE_FINDER_SOURCE],
                stdin=subprocess.DEVNULL,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
            )
        except OSError as exc:
            messagebox.showerror("Coordinate Finder", f"Couldn't open Coordinate Finder:\n{exc}", parent=self)
            return
        self.status_var.set("Coordinate Finder opened.")

    def save_script(self):
        self.hide_popup()
        self.auto_add_imports()
        name = self.file_path.name if self.file_path else self.suggested_filename
        chosen_extension = Path(name).suffix if Path(name).suffix in (".py", ".pyw") else ".py"
        destination = filedialog.asksaveasfilename(
            title="Save Python script", defaultextension=chosen_extension,
            initialfile=name,
            filetypes=[("Python scripts", "*.py *.pyw"),
                       ("GUI scripts (no console)", "*.pyw"),
                       ("Python scripts (.py)", "*.py"),
                       ("All files", "*.*")],
        )
        if not destination:
            return
        try:
            Path(destination).write_text(self.editor.get("1.0", "end-1c"), encoding="utf-8")
            self.file_path = Path(destination)
            self.status_var.set(f"Saved: {destination}")
        except OSError as exc:
            messagebox.showerror("Save failed", str(exc), parent=self)

    def open_script(self):
        self.hide_popup()
        destination = filedialog.askopenfilename(title="Open Python script",
                                                 filetypes=[("Python scripts", "*.py *.pyw"), ("GUI scripts", "*.pyw"),
                                                            ("All files", "*.*")])
        if not destination:
            return
        try:
            content = Path(destination).read_text(encoding="utf-8")
        except (OSError, UnicodeError) as exc:
            messagebox.showerror("Open failed", str(exc), parent=self)
            return
        self.editor.delete("1.0", "end")
        self.editor.insert("1.0", content)
        self.file_path = Path(destination)
        self.suggested_filename = self.file_path.name
        self.auto_add_imports()
        self.status_var.set(f"Opened: {destination}")

    def clear_script(self):
        self.hide_popup()
        if messagebox.askyesno("Clear editor?", "Remove all text from the editor?", parent=self):
            self.editor.delete("1.0", "end")
            self.file_path = None
            self.suggested_filename = "automation_script.py"
            self.status_var.set("Editor cleared.")


if __name__ == "__main__":
    ReferenceApp().mainloop()
