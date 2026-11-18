
@echo off
setlocal
title N0VA - Tool Hub
color 0B

:: CHANGE THIS TO YOUR GITHUB USERNAME
set "N0VALID"

:: N0VA download folder
set "INSTALL_DIR=%USERPROFILE%\Downloads\N0VA"
set "BASE_URL=https://raw.githubusercontent.com/%GH_USER%/N0VA/main"

:menu
cls
echo.
echo  ========================================
echo.
echo               N 0 V A
echo              INSTALLER
echo.
echo  ========================================
echo.
echo   [1] Install Python
echo   [2] Install EXE Studio
echo   [3] Install Windows Tool
echo.
echo   [4] Open Downloads Folder
echo   [0] Exit
echo.
echo  ========================================
echo.
set /p "choice= N0VA > "

if "%choice%"=="1" goto python
if "%choice%"=="2" goto studio
if "%choice%"=="3" goto windows
if "%choice%"=="4" goto folder
if "%choice%"=="0" exit /b

echo Invalid option.
pause
goto menu

:python
cls
echo Installing Python...
where winget >nul 2>&1
if errorlevel 1 (
    echo WinGet is not installed.
) else (
    winget install --id Python.Python.3.14 -e --source winget
)
pause
goto menu

:studio
cls
echo Downloading EXE Studio...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
curl.exe -fL "%BASE_URL%/exe_studio.pyw" -o "%INSTALL_DIR%\exe_studio.pyw"

if errorlevel 1 (
    echo Download failed. Check your GitHub username and files.
) else (
    echo Successfully downloaded EXE Studio!
    echo Saved to: %INSTALL_DIR%
)
pause
goto menu

:windows
cls
echo Downloading Windows Tool...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
curl.exe -fL "%BASE_URL%/windows.pyw" -o "%INSTALL_DIR%\windows.pyw"

if errorlevel 1 (
    echo Download failed. Check your GitHub username and files.
) else (
    echo Successfully downloaded Windows Tool!
    echo Saved to: %INSTALL_DIR%
)
pause
goto menu

:folder
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
start "" "%INSTALL_DIR%"
goto menu
