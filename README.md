# N0VA

A simple tool hub for Windows.

## Menu

| Option | What it does |
| --- | --- |
| 1 | Installs Python with WinGet |
| 2 | Downloads `exe_compiler.pyw` (EXE Studio) |
| 3 | Downloads and opens `cmptr_info.pyw` (Computer Info) |
| 4 | Opens your N0VA downloads |
| 0 | Closes N0VA |

Your apps are saved in `Downloads\N0VA`.

## Run N0VA

Open Command Prompt and paste:

```bat
curl.exe -fL https://raw.githubusercontent.com/N0VALID/N0VA/main/N0VA.cmd -o N0VA.cmd
N0VA.cmd
```

## Adding tools

Upload a new `.pyw` script to this repository. Then add a numbered menu option to `N0VA.cmd` and a matching download command pointing to its GitHub filename.
