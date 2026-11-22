@echo off
setlocal EnableExtensions
title N0VA - Made by N0VALID
rem Windows Terminal owns fullscreen itself; launch it explicitly in fullscreen.
if /i "%~1"=="--nova-fullscreen" goto nova_started
where wt.exe >nul 2>&1
if errorlevel 1 goto nova_started
start "" wt.exe -w new -F new-tab --title "N0VA - Made by N0VALID" "%ComSpec%" /d /c ""%~f0" --nova-fullscreen"
if not errorlevel 1 exit /b 0
:nova_started
color 0B

rem Run the embedded startup once, before entering the tool menu.
set "N0VA_LAUNCHER=%~f0"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:N0VA_LAUNCHER); $marker = '# N0VA_STARTUP_PS'; try { & ([scriptblock]::Create($text.Substring($text.LastIndexOf($marker) + $marker.Length))) } catch { Write-Host ('N0VA startup error: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
if errorlevel 1 (
    echo Startup could not finish. Please copy the error above.
    pause
)
set "N0VA_LAUNCHER="
color 0B

set "BASE_URL=https://raw.githubusercontent.com/N0VALID/N0VA/main"
set "DESKTOP_DIR=%USERPROFILE%\Desktop"
for /f "usebackq delims=" %%D in (`powershell.exe -NoProfile -Command "[Environment]::GetFolderPath('Desktop')" 2^>nul`) do if not "%%D"=="" set "DESKTOP_DIR=%%D"
set "INSTALL_DIR=%DESKTOP_DIR%\N0VA"

:menu
cls
echo.
echo      NN   NN    000    VV      VV     AAA
echo      NNN  NN   00 00    VV    VV     AA AA
echo      NN N NN   00 00     VV  VV     AA   AA
echo      NN  NNN   00 00      VVVV      AAAAAAA
echo      NN   NN    000        VV       AA   AA
echo.
echo      T O O L   H U B                     Made by N0VALID
echo      ==================================================================
echo.
echo        SETUP AND TOOLS                   WINDOWS
echo.
echo        [1] Install Python                [5] Chris Titus WinUtil
echo        [2] EXE Compiler                  [6] Update Installed Apps
echo        [3] Computer Info                 [7] Disk Cleanup
echo        [4] Open N0VA Folder              [8] Startup Apps
echo        [9] Command Explorer
echo.
echo      ==================================================================
echo        [0] Exit                          MATRIX EDITION / v1.1
echo.
set "choice="
set /p "choice=N0VA ^> "
if "%choice%"=="1" goto python
if "%choice%"=="2" goto studio
if "%choice%"=="3" goto computer
if "%choice%"=="4" goto folder
if "%choice%"=="5" goto winutil
if "%choice%"=="6" goto updates
if "%choice%"=="7" goto cleanup
if "%choice%"=="8" goto startup
if "%choice%"=="9" goto explorer
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
powershell.exe -NoProfile -Command "irm 'https://christitus.com/win' | iex" >nul 2>&1
if errorlevel 1 goto failed
goto complete

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
exit /b 0

# N0VA_STARTUP_PS
# This section is read by PowerShell; cmd exits above before reaching it.
$ErrorActionPreference = 'Stop'
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
                    elseif ($offset -lt 4) { [Console]::ForegroundColor = 'Green' }
                    else { [Console]::ForegroundColor = 'DarkGreen' }
                    [Console]::SetCursorPosition($column.X, $y)
                    [Console]::Write($glyphs[$random.Next($glyphs.Length)])
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
        $left = [Math]::Max(0, [int](([Console]::WindowWidth - $line.Length) / 2))
        [Console]::SetCursorPosition($left, $top)
        [Console]::Write($line)
        $top++
    }
    Start-Sleep -Milliseconds 2200
} finally {
    [Console]::CursorVisible = $previousCursor
    [Console]::ForegroundColor = $previousColor
    [Console]::BackgroundColor = $previousBackground
    [Console]::Clear()
}
