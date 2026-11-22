@echo off
setlocal EnableExtensions
title N0VA - Made by N0VALID
rem Windows Terminal owns fullscreen itself; launch it explicitly in fullscreen.
if /i "%~1"=="--nova-fullscreen" goto nova_started
where wt.exe >nul 2>&1
if errorlevel 1 goto nova_started
start "" wt.exe -w new -F new-tab --title "N0VA - Made by N0VALID" "%ComSpec%" /d /c ""%~f0" --nova-fullscreen"
if not errorlevel 1 exit 0
:nova_started
color 0B

rem Run the embedded startup once, before entering the tool menu.
set "N0VA_LAUNCHER=%~f0"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA error: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
if errorlevel 1 (
    echo Startup could not finish. Please copy the error above.
    pause
)
color 0B

set "BASE_URL=https://raw.githubusercontent.com/N0VALID/N0VA/main"
set "DESKTOP_DIR=%USERPROFILE%\Desktop"
for /f "usebackq delims=" %%D in (`powershell.exe -NoProfile -Command "[Environment]::GetFolderPath('Desktop')" 2^>nul`) do if not "%%D"=="" set "DESKTOP_DIR=%%D"
set "INSTALL_DIR=%DESKTOP_DIR%\N0VA"

:menu
cls
set "N0VA_VIEW=menu"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA error: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
if errorlevel 1 (
    echo N0VA - Made by N0VALID - v1.4
    echo [1] Python  [2] EXE Compiler  [3] Computer Info  [4] N0VA Folder
    echo [5] WinUtil [6] Update Apps   [7] Disk Cleanup   [8] Startup Apps
    echo [9] Command Explorer  [10] Install Brave  [11] Install Steam
    echo [12] All Python dependencies  [13] Install everything  [0] Exit
)
set "N0VA_VIEW="
set "choice="
set /p "choice="
if "%choice%"=="1" goto python
if "%choice%"=="2" goto studio
if "%choice%"=="3" goto computer
if "%choice%"=="4" goto folder
if "%choice%"=="5" goto winutil
if "%choice%"=="6" goto updates
if "%choice%"=="7" goto cleanup
if "%choice%"=="8" goto startup
if "%choice%"=="9" goto explorer
if "%choice%"=="10" goto brave
if "%choice%"=="11" goto steam
if "%choice%"=="12" goto dependencies
if "%choice%"=="13" goto install_all
if "%choice%"=="0" goto done
echo.
echo Invalid selection.
pause
goto menu

:python
where winget >nul 2>&1
if errorlevel 1 goto failed
winget install --id Python.Python.3.14 --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity >nul 2>&1
if errorlevel 1 goto failed
goto complete

:studio
call :download "exe_compiler.pyw"
if errorlevel 1 goto failed
goto complete_download

:computer
call :download "cmptr_info.pyw"
if errorlevel 1 goto failed
start "" "%INSTALL_DIR%\cmptr_info.pyw" >nul 2>&1
if errorlevel 1 goto failed
goto complete_download

:explorer
call :download "Python_Command_Explorer_Pro.pyw"
if errorlevel 1 goto failed
start "" "%INSTALL_DIR%\Python_Command_Explorer_Pro.pyw" >nul 2>&1
if errorlevel 1 goto failed
goto complete_download

:winutil
cls
echo Downloading Chris Titus WinUtil. Accept the Windows administrator prompt.
set "N0VA_VIEW=winutil"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA error: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
set "N0VA_VIEW="
rem Keep download errors and UAC cancellation visible before returning.
pause
goto menu

:brave
cls
call :install_app "Brave.Brave"
pause
goto menu

:steam
cls
call :install_app "Valve.Steam"
pause
goto menu

:install_app
where winget.exe >nul 2>&1
if errorlevel 1 (
    echo WinGet is missing. Install or update App Installer from Microsoft Store.
    exit /b 1
)
winget.exe install --id "%~1" --exact --source winget --accept-package-agreements --accept-source-agreements
if errorlevel 1 (
    echo Installation did not finish successfully. See the WinGet message above.
    exit /b 1
)
echo Installation complete.
exit /b 0

:dependencies
cls
echo Installing Python dependencies for all N0VA tools...
set "N0VA_VIEW=dependencies"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA error: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
set "N0VA_VIEW="
pause
goto menu


:install_all
cls
set "N0VA_SETUP_FAILED="
call :install_app "Brave.Brave"
if errorlevel 1 set "N0VA_SETUP_FAILED=1"
call :install_app "Valve.Steam"
if errorlevel 1 set "N0VA_SETUP_FAILED=1"
set "N0VA_VIEW=dependencies"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA error: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
if errorlevel 1 set "N0VA_SETUP_FAILED=1"
set "N0VA_VIEW="
if defined N0VA_SETUP_FAILED (
    echo Some installations failed. Check the messages above before retrying.
) else (
    echo Brave, Steam, and all N0VA Python dependencies are ready.
)
pause
goto menu

:updates
cls
where winget >nul 2>&1
if errorlevel 1 goto failed
echo Updating installed apps...
echo.
winget upgrade --all --accept-package-agreements --accept-source-agreements
if errorlevel 1 goto failed
goto complete

:cleanup
start "" cleanmgr.exe >nul 2>&1
if errorlevel 1 goto failed
goto complete

:startup
start "" "ms-settings:startupapps" >nul 2>&1
if errorlevel 1 goto failed
goto complete

:folder
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%" >nul 2>&1
if not exist "%INSTALL_DIR%" goto failed
start "" "%INSTALL_DIR%" >nul 2>&1
if errorlevel 1 goto failed
goto complete

:download
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%" >nul 2>&1
if not exist "%INSTALL_DIR%" exit /b 1
where curl.exe >nul 2>&1
if errorlevel 1 exit /b 1
set "TEMP_FILE=%INSTALL_DIR%\%~1.partial"
if exist "%TEMP_FILE%" del /q "%TEMP_FILE%" >nul 2>&1
curl.exe --fail --location --silent --retry 2 "%BASE_URL%/%~1" --output "%TEMP_FILE%" >nul 2>&1
if errorlevel 1 goto download_failed
for %%F in ("%TEMP_FILE%") do if %%~zF EQU 0 goto download_failed
move /y "%TEMP_FILE%" "%INSTALL_DIR%\%~1" >nul 2>&1
if errorlevel 1 goto download_failed
exit /b 0

:download_failed
if exist "%TEMP_FILE%" del /q "%TEMP_FILE%" >nul 2>&1
exit /b 1

:complete_download
cls
echo.
echo Done.
echo Saved in Desktop.
echo.
set "continue="
set /p "continue=Press Enter to continue..."
goto menu

:complete
cls
echo.
echo Done.
echo.
set "continue="
set /p "continue=Press Enter to continue..."
goto menu

:failed
cls
echo.
echo Failed.
echo.
set "continue="
set /p "continue=Press Enter to continue..."
goto menu

:done
endlocal
exit 0

# N0VA_STARTUP_PS
# This section is read by PowerShell; cmd exits above before reaching it.
$ErrorActionPreference = 'Stop'
if ($env:N0VA_VIEW -eq 'winutil') {
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    [void][IO.Directory]::CreateDirectory($env:INSTALL_DIR)
    $cttPath = Join-Path $env:INSTALL_DIR 'WinUtil.ps1'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://christitus.com/win' -OutFile $cttPath
    if ((Get-Item -LiteralPath $cttPath).Length -eq 0) { throw 'WinUtil download was empty. Try again.' }
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-NoExit', '-File', ('"{0}"' -f $cttPath))
    $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $arguments -PassThru -ErrorAction Stop
    Write-Host ('WinUtil opened in an administrator PowerShell window (process {0}).' -f $process.Id) -ForegroundColor Green
    Write-Host 'If WinUtil cannot start, its error will stay visible in that window.'
    return
}
if ($env:N0VA_VIEW -eq 'dependencies') {
    function Find-NovaPython {
        foreach ($name in @('py.exe', 'python.exe')) {
            $command = Get-Command $name -ErrorAction SilentlyContinue
            if (-not $command) { continue }
            if ($command.Source -like '*\Microsoft\WindowsApps\python.exe') { continue }
            $arguments = @('-c', 'import sys; print(sys.executable)')
            if ($name -eq 'py.exe') { $arguments = @('-3') + $arguments }
            try { $result = & $command.Source @arguments 2>$null } catch { continue }
            if ($LASTEXITCODE -eq 0 -and $result -and (Test-Path -LiteralPath ([string]($result | Select-Object -Last 1)))) {
                return [string]($result | Select-Object -Last 1)
            }
        }
        # Also find Python installed in this session, before PATH refreshes.
        $roots = @((Join-Path $env:LOCALAPPDATA 'Programs\Python'), $env:ProgramFiles)
        foreach ($root in $roots) {
            if (-not $root -or -not (Test-Path -LiteralPath $root)) { continue }
            foreach ($folder in (Get-ChildItem -LiteralPath $root -Directory -Filter 'Python3*' -ErrorAction SilentlyContinue | Sort-Object Name -Descending)) {
                $candidate = Join-Path $folder.FullName 'python.exe'
                if (Test-Path -LiteralPath $candidate) { return $candidate }
            }
        }
        return $null
    }
    $python = Find-NovaPython
    if (-not $python) {
        Write-Host 'Python was not found. Installing Python first...'
        $winget = Get-Command winget.exe -ErrorAction SilentlyContinue
        if (-not $winget) { throw 'WinGet is missing. Install App Installer from Microsoft Store, then retry.' }
        & $winget.Source install --id Python.Python.3.14 --exact --source winget --accept-package-agreements --accept-source-agreements
        if ($LASTEXITCODE -ne 0) { throw 'Python installation failed. See the WinGet message above.' }
        $python = Find-NovaPython
        if (-not $python) { throw 'Python was installed but could not be found. Reopen N0VA and select option 12 again.' }
    }
    Write-Host ('Using Python: ' + $python) -ForegroundColor Cyan
    & $python -m ensurepip --upgrade
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable pip for this Python installation.' }
    & $python -m pip install --upgrade pip
    if ($LASTEXITCODE -ne 0) { throw 'Updating pip failed. Check the error above and retry.' }
    & $python -m pip install --upgrade pyinstaller pyautogui pillow opencv-python
    if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed. Check the pip error above and retry.' }
    & $python -m pip check
    if ($LASTEXITCODE -ne 0) { throw 'pip found a package conflict. See the details above.' }
    Write-Host 'All N0VA Python dependencies installed successfully.' -ForegroundColor Green
    return
}
# Enable ANSI true color in Windows console hosts as well as Windows Terminal.
$script:NovaTrueColor = ($env:TERM -match 'xterm|screen|vt100')
try {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class NovaVT {
    [DllImport("kernel32.dll")] public static extern IntPtr GetStdHandle(int kind);
    [DllImport("kernel32.dll")] public static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll")] public static extern bool SetConsoleMode(IntPtr handle, uint mode);
}
'@
    $mode = [uint32]0
    $handle = [NovaVT]::GetStdHandle(-11)
    if ([NovaVT]::GetConsoleMode($handle, [ref]$mode)) {
        $script:NovaTrueColor = [NovaVT]::SetConsoleMode($handle, ($mode -bor 4))
    }
} catch { }
$script:NovaEscape = [string][char]27
function Get-NovaInk {
    param([double]$Phase, [double]$Brightness = 1)
    $r = [int]((140 + 115 * [Math]::Sin($Phase)) * $Brightness)
    $g = [int]((140 + 115 * [Math]::Sin($Phase + 2.0944)) * $Brightness)
    $b = [int]((140 + 115 * [Math]::Sin($Phase + 4.1888)) * $Brightness)
    return "$($script:NovaEscape)[38;2;$r;$g;${b}m"
}
function Get-NovaRainbow {
    param([string]$Text, [double]$Phase = 0)
    if (-not $script:NovaTrueColor) { return $Text }
    $builder = New-Object Text.StringBuilder
    for ($i = 0; $i -lt $Text.Length; $i++) {
        [void]$builder.Append((Get-NovaInk -Phase ($Phase + $i * 0.18)))
        [void]$builder.Append($Text[$i])
    }
    [void]$builder.Append("$($script:NovaEscape)[0m")
    return $builder.ToString()
}
function Write-NovaCentered {
    param([string]$Text, [ConsoleColor]$Color = 'Cyan', [switch]$NoNewline, [switch]$Rainbow, [double]$Phase = 0)
    [Console]::ForegroundColor = $Color
    $padding = [Math]::Max(0, [int][Math]::Floor(([Console]::WindowWidth - $Text.Length) / 2))
    $painted = $Text
    if ($Rainbow) { $painted = Get-NovaRainbow -Text $Text -Phase $Phase }
    $output = (' ' * $padding) + $painted
    if ($NoNewline) { [Console]::Write($output) }
    else { [Console]::WriteLine($output) }
}
if ($env:N0VA_VIEW -eq 'menu') {
    [Console]::BackgroundColor = 'Black'
    [Console]::Clear()
    $rows = @(
        @('NN   NN    000    VV      VV     AAA', 'Green'),
        @('NNN  NN   00 00    VV    VV     AA AA', 'Green'),
        @('NN N NN   00 00     VV  VV     AA   AA', 'Cyan'),
        @('NN  NNN   00 00      VVVV      AAAAAAA', 'Cyan'),
        @('NN   NN    000        VV       AA   AA', 'DarkCyan'),
        @('', 'Black'),
        @('T O O L   H U B', 'Green'),
        @('Made by N0VALID', 'Magenta'),
        @('', 'Black'),
        @('==================================================================', 'DarkCyan'),
        @('SETUP AND TOOLS                   WINDOWS                 ', 'Cyan'),
        @('', 'Black'),
        @('[1] Install Python                [5] Chris Titus WinUtil  ', 'Green'),
        @('[2] EXE Compiler                  [6] Update Installed Apps', 'Cyan'),
        @('[3] Computer Info                 [7] Disk Cleanup         ', 'Green'),
        @('[4] Open N0VA Folder              [8] Startup Apps         ', 'Cyan'),
        @('[9] Command Explorer             [10] Install Brave       ', 'Green'),
        @('[11] Install Steam               [12] All Python packages ', 'Cyan'),
        @('[13] Install everything: Brave, Steam + Python packages   ', 'Green'),
        @('', 'Black'),
        @('==================================================================', 'DarkCyan'),
        @('[0] Exit                                             v1.4', 'Magenta'),
        @('', 'Black')
    )
    $top = [Math]::Max(0, [int][Math]::Floor(([Console]::WindowHeight - $rows.Count - 1) / 2))
    for ($blank = 0; $blank -lt $top; $blank++) { [Console]::WriteLine() }
    $rowIndex = 0
    foreach ($row in $rows) {
        Write-NovaCentered -Text $row[0] -Color $row[1] -Rainbow -Phase ($rowIndex * 0.28)
        $rowIndex++
    }
    Write-NovaCentered -Text 'N0VA > ' -Color Green -NoNewline
    return
}
try {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class N0VAWindow {
    [DllImport("kernel32.dll")] public static extern IntPtr GetStdHandle(int kind);
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleDisplayMode(IntPtr output, uint flags, out COORD size);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr window, int command);
    [StructLayout(LayoutKind.Sequential)] public struct COORD { public short X; public short Y; }
}
'@
    $size = New-Object N0VAWindow+COORD
    $fullscreen = [N0VAWindow]::SetConsoleDisplayMode([N0VAWindow]::GetStdHandle(-11), 1, [ref]$size)
    if (-not $fullscreen) {
        # Hosts that cannot enter console fullscreen get a maximized window.
        [void][N0VAWindow]::ShowWindow([N0VAWindow]::GetConsoleWindow(), 3)
    }
} catch { }
$previousColor = [Console]::ForegroundColor
$previousBackground = [Console]::BackgroundColor
$previousCursor = $true
try { $previousCursor = [Console]::CursorVisible } catch { }
try {
    [Console]::BackgroundColor = 'Black'
    [Console]::ForegroundColor = 'Cyan'
    [Console]::CursorVisible = $false
    [Console]::Clear()
    # Independent falling streams, with bright heads and dark green tails.
    $random = New-Object System.Random
    $glyphs = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ:<>/{}[]'
    $width = [Math]::Max(1, [Console]::WindowWidth - 1)
    $height = [Math]::Max(1, [Console]::WindowHeight - 1)
    $columns = @()
    for ($x = 0; $x -lt $width; $x += 2) {
        $columns += [pscustomobject]@{
            X = $x
            Head = $random.Next(-$height, $height)
            Length = $random.Next(5, 16)
            Speed = $random.Next(1, 3)
            Palette = $random.Next(0, 6)
        }
    }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    while ($clock.ElapsedMilliseconds -lt 3000) {
        # Leave the animation cleanly if the user resizes the terminal.
        if ([Console]::WindowWidth -le $width -or [Console]::WindowHeight -le $height) { break }
        foreach ($column in $columns) {
            # Erase the previous stream before advancing it.
            for ($offset = 0; $offset -le $column.Length; $offset++) {
                $y = $column.Head - $offset
                if ($y -ge 0 -and $y -lt $height) {
                    [Console]::SetCursorPosition($column.X, $y)
                    [Console]::Write(' ')
                }
            }
            $column.Head += $column.Speed
            if ($column.Head - $column.Length -ge $height) {
                $column.Head = $random.Next(-12, 0)
            }
            for ($offset = $column.Length; $offset -ge 0; $offset--) {
                $y = $column.Head - $offset
                if ($y -ge 0 -and $y -lt $height) {
                    if ($offset -eq 0) { [Console]::ForegroundColor = 'White' }
                    elseif ($column.Palette -eq 4) {
                        if ($offset -lt 4) { [Console]::ForegroundColor = 'Cyan' }
                        else { [Console]::ForegroundColor = 'DarkCyan' }
                    } elseif ($column.Palette -eq 5) {
                        if ($offset -lt 4) { [Console]::ForegroundColor = 'Magenta' }
                        else { [Console]::ForegroundColor = 'DarkMagenta' }
                    } elseif ($offset -lt 4) { [Console]::ForegroundColor = 'Green' }
                    else { [Console]::ForegroundColor = 'DarkGreen' }
                    [Console]::SetCursorPosition($column.X, $y)
                    $glyph = [string]$glyphs[$random.Next($glyphs.Length)]
                    if ($script:NovaTrueColor -and $offset -gt 0) {
                        $phase = $column.X * 0.07 + $clock.ElapsedMilliseconds / 700.0 + $offset * 0.09
                        $brightness = [Math]::Max(0.12, 1.0 - $offset / [double]($column.Length + 1))
                        [Console]::Write((Get-NovaInk -Phase $phase -Brightness $brightness) + $glyph + "$($script:NovaEscape)[0m")
                    } else { [Console]::Write($glyph) }
                }
            }
        }
        Start-Sleep -Milliseconds 55
    }
    $clock.Stop()
    [Console]::Clear()
    [Console]::ForegroundColor = 'Cyan'
    $logo = @(
        'NN   NN   000   VV     VV    AAA',
        'NNN  NN  00 00   VV   VV    AA AA',
        'NN N NN  00 00   VV   VV   AA   AA',
        'NN  NNN  00 00    VV VV    AAAAAAA',
        'NN   NN   000      VVV     AA   AA',
        '',
        'T O O L   H U B',
        '',
        'Made by N0VALID'
    )
    $top = [Math]::Max(0, [int](([Console]::WindowHeight - $logo.Count) / 2))
    foreach ($line in $logo) {
        [Console]::SetCursorPosition(0, $top)
        Write-NovaCentered -Text $line -Rainbow -Phase ($top * 0.28)
        $top++
    }
    Start-Sleep -Milliseconds 2200
} finally {
    [Console]::CursorVisible = $previousCursor
    [Console]::ForegroundColor = $previousColor
    [Console]::BackgroundColor = $previousBackground
    [Console]::Clear()
}
