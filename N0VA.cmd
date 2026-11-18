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
where winget >nul 2>&1
if errorlevel 1 goto failed
winget install --id Python.Python.3.14 --exact --source winget --silent --accept-package-agreements --accept-source-agreements --disable-interactivity >nul 2>&1
if errorlevel 1 goto failed
goto complete

:studio
call :download "exe_compiler.pyw"
if errorlevel 1 goto failed
goto complete

:computer
call :download "cmptr_info.pyw"
if errorlevel 1 goto failed
start "" "%INSTALL_DIR%\cmptr_info.pyw" >nul 2>&1
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
