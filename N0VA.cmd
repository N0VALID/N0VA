@echo off
setlocal EnableExtensions
title N0VA
color 0B

set "BASE_URL=https://raw.githubusercontent.com/N0VALID/N0VA/main"
set "INSTALL_DIR=%USERPROFILE%\Downloads\N0VA"

:menu
cls
echo.
echo  ========================================
echo               N 0 V A
echo                TOOL HUB
echo  ========================================
echo.
echo   [1] Install Python
echo   [2] Get EXE Studio
echo   [3] Open Computer Info
echo   [4] Downloads
echo   [0] Exit
echo.
echo  ========================================
echo.
set "choice="
set /p "choice=N0VA ^> "
if "%choice%"=="1" goto python
if "%choice%"=="2" goto studio
if "%choice%"=="3" goto computer
if "%choice%"=="4" goto folder
if "%choice%"=="0" goto done
echo.
echo Invalid selection.
pause
goto menu

:python
cls
where winget >nul 2>&1
if errorlevel 1 (
    echo WinGet not found. Install or update App Installer from Microsoft.
) else (
    echo Installing Python...
    winget install --id Python.Python.3.14 --exact --source winget
    if errorlevel 1 (
        echo.
        echo Python installation failed.
    ) else (
        echo.
        echo Python installation complete.
    )
)
pause
goto menu

:studio
call :download "exe_compiler.pyw" "EXE Studio"
if errorlevel 1 goto menu
echo.
echo EXE Studio downloaded.
pause
goto menu

:computer
call :download "cmptr_info.pyw" "Computer Info"
if errorlevel 1 goto menu
start "" "%INSTALL_DIR%\cmptr_info.pyw"
goto menu

:folder
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
if exist "%INSTALL_DIR%" (
    start "" "%INSTALL_DIR%"
) else (
    echo Could not create the download folder.
    pause
)
goto menu

:download
cls
echo Downloading %~2...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
if not exist "%INSTALL_DIR%" (
    echo Could not create %INSTALL_DIR%.
    pause
    exit /b 1
)
where curl.exe >nul 2>&1
if errorlevel 1 (
    echo curl.exe is not available on this computer.
    pause
    exit /b 1
)
set "TEMP_FILE=%INSTALL_DIR%\%~1.partial"
if exist "%TEMP_FILE%" del /q "%TEMP_FILE%" >nul 2>&1
curl.exe --fail --location --silent --show-error --retry 2 "%BASE_URL%/%~1" --output "%TEMP_FILE%"
if errorlevel 1 (
    if exist "%TEMP_FILE%" del /q "%TEMP_FILE%" >nul 2>&1
    echo Download failed. Check your internet connection and the N0VA repository.
    pause
    exit /b 1
)
for %%F in ("%TEMP_FILE%") do if %%~zF EQU 0 (
    del /q "%TEMP_FILE%" >nul 2>&1
    echo Download failed: empty file.
    pause
    exit /b 1
)
move /y "%TEMP_FILE%" "%INSTALL_DIR%\%~1" >nul
if errorlevel 1 (
    echo Could not save the downloaded file.
    pause
    exit /b 1
)
echo.
echo Saved to: %INSTALL_DIR%\%~1
exit /b 0

:done
endlocal
exit /b 0
